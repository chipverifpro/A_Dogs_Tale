#nullable enable
using System;
using UnityEngine;
using DogGame.Modules;
using System.Collections.Generic;
using DogGame.LLM;
using DogGame.Tasks;
using DogGame.Reactions;
using DogGame.Lua;

public sealed class ReactionModule : WorldModule
{
    public TaskController taskController = null!;

    [SerializeField] private float minInterestToReact = 0.25f;
    [SerializeField] private float globalCooldownSeconds = 0.50f;
    [SerializeField] private float sameEventTypeCooldownSeconds = 0.75f;
    private float globalCooldown;

    [Header("Lua Reactions")]
    [SerializeField] private bool runLuaOnPerceptionEvents = false;
    [TextArea(8, 30)]
    [SerializeField] private string luaReactionScript = "";
    private bool luaDogIsHungry => luaDogHunger01 > 0.8f;
    [Range(0f, 1f)]
    [SerializeField] private float luaDogHunger01 = 0f;

    private ReactionRuleTable ruleTable = null!;
    private LuaRuntime? luaRuntime;
    private bool luaScriptLoaded;
    private string loadedLuaScript = "";
    private readonly Dictionary<string, float> eventTypeCooldownUntil = new();
    private readonly DogState luaDogState = new();
    private readonly VisionState luaVisionState = new();
    private readonly HearingState luaHearingState = new();

    protected override void Awake()
    {
        EnsureRuntimeState();
        ResetRuntimeState();
    }

    private void OnEnable()
    {
        EnsureRuntimeState();
        ResetRuntimeState();
    }

    private void OnDisable()
    {
        ResetRuntimeState();
    }

    public override void Tick(float dt)
    {
        if (!EnsureRuntimeState())
            return;

        if (globalCooldown > 0f)
        {
            globalCooldown = Mathf.Max(0f, globalCooldown - dt);
            if (!runLuaOnPerceptionEvents)
                return;
        }

        var events = CollectPerceptionEvents(dt);
        if (events.Count == 0)
            return;

        // Optional: only process events observed by me (if you added Observer)
        events.RemoveAll(e => e.Observer != worldObject);
        if (events.Count == 0)
            return;

        ApplySameTypeCooldown(events);
        if (events.Count == 0)
            return;

        RunLuaReactions(events);

        // Cooldown gates C# rule-based reactions, not Lua event callbacks.
        if (globalCooldown > 0f)
            return;

        var bestEvent = PickBestEvent(events);
        if (bestEvent.Interest01 < minInterestToReact)
            return;

        if (ruleTable.TrySelectBestRule(worldObject, bestEvent, out var rule, out var score))
        {
            TaskRequest request = rule!.Build(worldObject, bestEvent);

            taskController.Submit(request);

            ruleTable.ArmCooldown(worldObject, bestEvent, rule);
            globalCooldown = globalCooldownSeconds;

            Debug.Log($"[ReactionModule] {worldObject.DisplayName} -> rule='{rule.Name}' score={score:0.00} event={bestEvent.Type}");
        }
    }

    private void EnsureLuaReady()
    {
        if (!runLuaOnPerceptionEvents)
            return;

        if (string.IsNullOrWhiteSpace(luaReactionScript))
        {
            luaScriptLoaded = false;
            loadedLuaScript = "";
            return;
        }

        if (luaRuntime == null)
        {
            luaRuntime = new LuaRuntime();

            var bootstrapEvent = new PerceptionEvent(
                observer: worldObject,
                sense: PerceptionSense.Scent,
                type: PerceptionEventType.SomethingInteresting,
                worldPos: worldObject.transform.position,
                target: null,
                strength01: 0f,
                novelty01: 0f,
                interest01: 0f);

            var bindings = new DogLuaBindings(taskController, worldObject, bootstrapEvent);
            luaRuntime.RegisterBindings(bindings);
        }

        if (luaScriptLoaded &&
            string.Equals(loadedLuaScript, luaReactionScript, StringComparison.Ordinal))
            return;

        //luaDogState.isHungry = luaDogIsHungry;
        luaDogState.hunger = Mathf.Clamp01(luaDogHunger01);
        luaVisionState.foodVisible = false;
        luaHearingState.barkHeard = false;

        luaRuntime.SetState(luaDogState, luaVisionState, luaHearingState);
        luaScriptLoaded = luaRuntime.LoadScript(luaReactionScript);
        if (luaScriptLoaded)
            loadedLuaScript = luaReactionScript;
    }

    private bool EnsureRuntimeState()
    {
        if (worldObject == null)
            return false;

        if (taskController == null)
            taskController = GetComponentInParent<TaskController>();

        if (ruleTable == null)
        {
            ruleTable = new ReactionRuleTable();
            BuildDefaultRules(ruleTable);
        }

        return taskController != null && ruleTable != null;
    }

    private void ResetRuntimeState()
    {
        globalCooldown = 0f;
        eventTypeCooldownUntil.Clear();
        luaRuntime = null;
        luaScriptLoaded = false;
        loadedLuaScript = "";
    }

    private void RunLuaReactions(List<PerceptionEvent> events)
    {
        if (!runLuaOnPerceptionEvents)
            return;

        EnsureLuaReady();
        if (luaRuntime == null || !luaScriptLoaded)
            return;

        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            UpdateLuaStateFromEvent(e);

            luaRuntime.SetState(luaDogState, luaVisionState, luaHearingState);
            luaRuntime.SetPerceptionEvent(e);

            if (!luaRuntime.CallReact())
            {
                Debug.LogError($"[ReactionModule] Lua CallReact failed for event {e.Type} ({e.Sense}).");
                return;
            }
        }
    }

    private void UpdateLuaStateFromEvent(in PerceptionEvent e)
    {
        luaHearingState.barkHeard =
            e.Sense == PerceptionSense.Sound &&
            e.Type == PerceptionEventType.BarkHeard;

        luaVisionState.foodVisible =
            e.Sense == PerceptionSense.Vision &&
            e.Target != null &&
            e.Target.Kind == WorldObjectKind.Item;
    }

    private List<PerceptionEvent> CollectPerceptionEvents(float dt)
    {
        List<PerceptionEvent> events = new();

        // Scent
        var eventsScent = worldObject.scentPerceptionModule.TickScent(dt);
        if (eventsScent != null && eventsScent.Count > 0)
            events.AddRange(eventsScent);

        // Vision (assuming module internally updated earlier; otherwise expose TickVision(dt))
        var eventsVision = worldObject.visionPerceptionModule.GetPerceptionEvents();
        if (eventsVision != null && eventsVision.Count > 0)
            events.AddRange(eventsVision);

        // Hearing
        var eventsHearing = worldObject.hearingModule.GetPerceptionEvents();
        if (eventsHearing != null && eventsHearing.Count > 0)
        {
            events.AddRange(eventsHearing);
            worldObject.hearingModule.ClearPerceptionEvents();
        }

        return events;
    }

    private void ApplySameTypeCooldown(List<PerceptionEvent> events)
    {
        if (events == null || events.Count == 0)
            return;

        if (sameEventTypeCooldownSeconds <= 0f)
            return;

        float now = Time.time;

        for (int i = 0; i < events.Count;)
        {
            PerceptionEvent e = events[i];
            string key = BuildEventTypeCooldownKey(e);

            if (eventTypeCooldownUntil.TryGetValue(key, out float until) && now < until)
            {
                events.RemoveAt(i);
                continue;
            }

            eventTypeCooldownUntil[key] = now + sameEventTypeCooldownSeconds;
            i++;
        }
    }

    private static string BuildEventTypeCooldownKey(in PerceptionEvent e)
    {
        return $"{e.Sense}:{e.Type}";
    }

    private void FilterToSelfObserved(List<PerceptionEvent> events)
    {
        // If you haven't added Observer yet, remove this filter.
        // With Observer, this prevents reacting to other agents' perceptions.
        for (int i = events.Count - 1; i >= 0; i--)
        {
            if (events[i].Observer != worldObject)
                events.RemoveAt(i);
        }
    }

    private static PerceptionEvent PickBestEvent(List<PerceptionEvent> events)
    {
        int bestIndex = 0;
        float bestScore = events[0].Interest01;

        for (int i = 1; i < events.Count; i++)
        {
            float score = events[i].Interest01;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return events[bestIndex];
    }

    private void LogEvent(PerceptionEvent e)
    {
        switch (e.Sense)
        {
            case PerceptionSense.Scent:
                if (e.Scent.HasValue)
                {
                    var s = e.Scent.Value;
                    Debug.Log(
                        $"[ReactionModule] {worldObject.DisplayName} noticed {e.Type} SCENT {s.Category} '{s.ScentName}' " +
                        $"strength={e.Strength01:0.00} novelty={e.Novelty01:0.00} interest={e.Interest01:0.00}"
                    );
                }
                break;

            case PerceptionSense.Vision:
                if (e.Vision.HasValue)
                {
                    var v = e.Vision.Value;
                    Debug.Log(
                        $"[ReactionModule] {worldObject.DisplayName} saw {e.Type} {v.Kind} {v.Relation} " +
                        $"dist={v.DistanceMeters:0.0}m speed={v.SpeedMps:0.0}m/s interest={e.Interest01:0.00}"
                    );
                }
                break;

            default:
                Debug.Log($"[ReactionModule] {worldObject.DisplayName} perceived {e.Type} interest={e.Interest01:0.00}");
                break;
        }
    }

    private bool TryReactToEvent(PerceptionEvent e)
    {
        if (e.Interest01 < minInterestToReact)
            return false;

        // v1 example: Food smell => bark + try sniff
        if (e.Sense == PerceptionSense.Scent &&
            e.Scent.HasValue &&
            e.Scent.Value.Category == ScentCategory.Food)
        {
            var seq = new Task_Sequence(new IAgentTask[]
            {
                // Consider making Bark take 0..1 or 1..10 consistently; you used 10 here.
                new Task_Bark(10),

                new Task_Try(
                    tryTask: new Task_Sniff(null),      //TODO: wants HashSet<string>
                    onFail: new Task_Wait(0.1f))
            });

            taskController.Submit(new TaskRequest(
                task: seq,
                priority: 80,
                source: TaskSource.Reaction,
                canInterrupt: true,
                resumePrevious: true,
                tag: "reaction_food_bark_sniff"
            ));

            return true;
        }

        // v1: no reaction
        return false;
    }

    private void BuildDefaultRules(ReactionRuleTable table)
    {
        // FOOD smell => bark + sniff; resume previous work afterwards.
        table.Add(new ReactionRule(
            name: "FoodSmell_BarkAndSniff",
            match: (observer, e) =>
                e.Sense == PerceptionSense.Scent &&
                e.Scent.HasValue &&
                e.Scent.Value.Category == ScentCategory.Food &&
                e.Interest01 >= 0.25f,

            score: (observer, e) =>
                // Prefer higher interest and novelty; small bias to new smells
                (e.Interest01 * 1.0f) + (e.Novelty01 * 0.25f),

            build: (observer, e) =>
            {
                var seq = new Task_Sequence(new IAgentTask[]
                {
                    new Task_Bark(10),
                    new Task_Try(
                        tryTask: new Task_Sniff(null),  //TODO: wants HashSet<string>
                        onFail: new Task_Wait(0.1f))
                });

                return new TaskRequest(
                    task: seq,
                    priority: 80,
                    source: TaskSource.Reaction,
                    canInterrupt: true,
                    resumePrevious: true,
                    tag: "reaction_food_bark_sniff"
                );
            },

            cooldownSeconds: 1.0f,

            cooldownKey: (observer, e) =>
            {
                // Cooldown per scent key if present, else per rule.
                if (e.Scent.HasValue) return "FoodSmell:" + e.Scent.Value.ScentKey;
                return "FoodSmell_BarkAndSniff";
            }
        ));
    }
}
