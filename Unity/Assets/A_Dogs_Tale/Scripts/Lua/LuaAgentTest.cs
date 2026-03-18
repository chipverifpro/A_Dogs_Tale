using System.Collections.Generic;
using DogGame.LLM;
using DogGame.Modules;
using UnityEngine;

namespace DogGame.Lua
{
    public class LuaAgentTest : MonoBehaviour
    {
        public TaskController taskController;
        public WorldObject observer;
        public PerceptionEvent perceptionEvent;
        [SerializeField] private bool reactToPerceptionEvents = true;
        [SerializeField] private float sameEventTypeCooldownSeconds = 0.75f;

        private LuaRuntime luaRuntime = null!;

        private readonly List<PerceptionEvent> scratchEvents = new();
        private readonly Dictionary<string, float> eventTypeCooldownUntil = new();
        private DogState dogState = new();
        private VisionState visionState = new();
        private HearingState hearingState = new();
        private bool scriptLoaded;

        [TextArea(10, 30)]
        public string luaScript = @"
function react(event)
    if event ~= nil and event.hasScent then
        print('Scent event: ' .. event.scentCategory .. ' ' .. event.scentName)
        Bark(1)
        Sniff(1.0)
        FollowEventScent()
        return
    end

    if event ~= nil and event.hasVision then
        print('Vision event: ' .. event.visionKind .. ' dist=' .. event.visionDistanceMeters)
        FaceEventTarget(8.0, 1.0)
        MoveToTarget(0.8)
        return
    end

    if event ~= nil and event.hasSound then
        print('Sound event: ' .. event.soundCategory .. '/' .. event.soundSubtype .. ' loud=' .. event.soundLoudness01)
        MoveToEventSound(0.7)
        if event.type == 'BarkHeard' then
            Bark(1)
        end
        return
    end

    if Dog.isHungry and Vision.foodVisible then
        print('Hungry and food visible')
        Bark(1)
        MoveToEvent(0.3)
        return
    end

    if Hearing.barkHeard then
        print('Heard a bark')
        Bark(1)
        return
    end

    print('Nothing interesting')
end
";

        private void Start()
        {
            Debug.Log("[LuaAgentTest] Start");

            if (observer == null)
            {
                Debug.LogError("[LuaAgentTest] observer is null.");
                return;
            }

            luaRuntime = new LuaRuntime();

            // Bootstrap event so bindings can build event-context tasks before first live event arrives.
            perceptionEvent = new PerceptionEvent(
                observer: observer,
                sense: PerceptionSense.Scent,
                type: PerceptionEventType.SomethingInteresting,
                worldPos: observer.transform.position,
                target: null,
                strength01: 0f,
                novelty01: 0f,
                interest01: 0f);

            var bindings = new DogLuaBindings(taskController, observer, perceptionEvent);
            luaRuntime.RegisterBindings(bindings);

            dogState = new DogState
            {
                //isHungry = true,
                hunger = 0.8f
            };

            visionState = new VisionState
            {
                foodVisible = true
            };

            hearingState = new HearingState
            {
                barkHeard = false
            };

            luaRuntime.SetState(dogState, visionState, hearingState);
            scriptLoaded = luaRuntime.LoadScript(luaScript);
            if (!scriptLoaded)
            {
                Debug.LogError("[LuaAgentTest] LoadScript failed; skipping react.");
                return;
            }

            Debug.Log("[LuaAgentTest] Ready (script loaded; waiting for perception events)");
        }

        private void Update()
        {
            if (!reactToPerceptionEvents || !scriptLoaded || observer == null)
                return;

            CollectPerceptionEvents(Time.deltaTime, scratchEvents);
            if (scratchEvents.Count == 0)
                return;

            for (int i = 0; i < scratchEvents.Count; i++)
            {
                perceptionEvent = scratchEvents[i];
                if (IsEventTypeOnCooldown(perceptionEvent))
                    continue;

                UpdateStateFromEvent(perceptionEvent);

                luaRuntime.SetState(dogState, visionState, hearingState);
                luaRuntime.SetPerceptionEvent(perceptionEvent);

                if (!luaRuntime.CallReact())
                {
                    Debug.LogError("[LuaAgentTest] CallReact failed for perception event.");
                    return;
                }
            }
        }

        private void CollectPerceptionEvents(float dt, List<PerceptionEvent> events)
        {
            events.Clear();

            if (observer.scentPerceptionModule != null)
            {
                var scentEvents = observer.scentPerceptionModule.TickScent(dt);
                if (scentEvents != null && scentEvents.Count > 0)
                    events.AddRange(scentEvents);
            }

            if (observer.visionPerceptionModule != null)
            {
                var visionEvents = observer.visionPerceptionModule.GetPerceptionEvents();
                if (visionEvents != null && visionEvents.Count > 0)
                    events.AddRange(visionEvents);
            }

            if (observer.hearingModule != null)
            {
                var hearingEvents = observer.hearingModule.GetPerceptionEvents();
                if (hearingEvents != null && hearingEvents.Count > 0)
                {
                    events.AddRange(hearingEvents);
                    observer.hearingModule.ClearPerceptionEvents();
                }
            }

            events.RemoveAll(e => e.Observer != observer);
        }

        private void UpdateStateFromEvent(PerceptionEvent e)
        {
            hearingState.barkHeard = e.Sense == PerceptionSense.Sound && e.Type == PerceptionEventType.BarkHeard;

            bool visionFood =
                e.Sense == PerceptionSense.Vision &&
                e.Target != null &&
                e.Target.Kind == WorldObjectKind.Item;

            visionState.foodVisible = visionFood;
        }

        private bool IsEventTypeOnCooldown(in PerceptionEvent e)
        {
            if (sameEventTypeCooldownSeconds <= 0f)
                return false;

            string key = $"{e.Sense}:{e.Type}";
            float now = Time.time;

            if (eventTypeCooldownUntil.TryGetValue(key, out float until) && now < until)
                return true;

            eventTypeCooldownUntil[key] = now + sameEventTypeCooldownSeconds;
            return false;
        }
    }
}
