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
function react()
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

            perceptionEvent = new PerceptionEvent(observer: observer,
                    sense: PerceptionSense.Vision,
                    type: PerceptionEventType.TargetSeen,
                    worldPos: new Vector3(5f,5f,0),
                    target: null,
                    strength01: 0.5f,
                    novelty01: 0.5f,
                    interest01: 0.5f,
                    scent: null,
                    vision: null,
                    sound: null);

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
