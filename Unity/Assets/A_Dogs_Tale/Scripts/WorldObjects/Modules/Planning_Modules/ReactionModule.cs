#nullable enable
using UnityEngine;
using DogGame.Modules;
using System.Collections.Generic;
using DogGame.LLM;
using DogGame.Tasks;
using DogGame.Reactions;

public sealed class ReactionModule : WorldModule
{
    public TaskController taskController = null!;

    [SerializeField] private float minInterestToReact = 0.25f;
    [SerializeField] private float globalCooldownSeconds = 0.50f;
    private float globalCooldown;

    private ReactionRuleTable ruleTable = null!;

    protected override void Awake()
    {
        if (taskController == null)
            taskController = GetComponentInParent<TaskController>();

        ruleTable = new ReactionRuleTable();
        BuildDefaultRules(ruleTable);
    }

    public override void Tick(float dt)
    {
        if (taskController == null || worldObject == null)
            return;

        if (globalCooldown > 0f)
        {
            globalCooldown = Mathf.Max(0f, globalCooldown - dt);
            return;
        }

        var events = CollectPerceptionEvents(dt);
        if (events.Count == 0)
            return;

        // Optional: only process events observed by me (if you added Observer)
        events.RemoveAll(e => e.Observer != worldObject);

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

        return events;
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