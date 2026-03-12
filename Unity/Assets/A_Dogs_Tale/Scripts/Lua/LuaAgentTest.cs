using System;
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

        private LuaRuntime luaRuntime;

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

            luaRuntime = new LuaRuntime();

            perceptionEvent = PerceptionEvent.MakeScent(
                observer: observer,
                type: PerceptionEventType.NewSmell,
                worldPos: new Vector3(5f, 5f, 0f),
                scentKey: "agent:3",
                category: ScentCategory.Food,
                scentName: "Hot Dog",
                strength01: 0.8f,
                novelty01: 0.9f,
                interest01: 0.85f);

            DogLuaBindings bindings = new DogLuaBindings(taskController, observer, perceptionEvent);
            luaRuntime.RegisterBindings(bindings);

            DogState dogState = new DogState
            {
                isHungry = true,
                hunger = 0.8f
            };

            VisionState visionState = new VisionState
            {
                foodVisible = true
            };

            HearingState hearingState = new HearingState
            {
                barkHeard = false
            };

            luaRuntime.SetState(dogState, visionState, hearingState);
            luaRuntime.SetPerceptionEvent(perceptionEvent);
            bool loaded = luaRuntime.LoadScript(luaScript);
            if (!loaded)
            {
                Debug.LogError("[LuaAgentTest] LoadScript failed; skipping react.");
                return;
            }

            bool reacted = luaRuntime.CallReact();
            if (!reacted)
            {
                Debug.LogError("[LuaAgentTest] CallReact failed.");
                return;
            }

            Debug.Log("[LuaAgentTest] Done (script loaded and react executed)");
        }
    }
}
