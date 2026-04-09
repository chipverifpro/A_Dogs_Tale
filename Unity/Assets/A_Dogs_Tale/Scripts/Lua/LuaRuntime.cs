#nullable enable
using System;
using MoonSharp.Interpreter;
using UnityEngine;
using DogGame.Modules;

namespace DogGame.Lua
{
    public class LuaRuntime
    {
        private static bool userdataTypesRegistered = false;
        private readonly Script script;
        private readonly AgentState defaultState = new();
        private readonly ScentState defaultScentState = new();
        private readonly PackState defaultPackState = new();
        private readonly EnvState defaultEnvState = new();
        private readonly RoomState defaultRoomState = new();
        private readonly TaskState defaultTaskState = new();
        private readonly MemoryState defaultMemoryState = new();
        private readonly TimeState defaultTimeState = new();
        private DogLuaBindings? bindings;

        public LuaRuntime()
        {
            RegisterUserdataTypesOnce();

            script = new Script();

            script.Options.DebugPrint = message =>
            {
                Debug.Log("[Lua] " + message);
                BottomBanner.LogRichMessage(BannerSense.None,BannerLevel.None,"<i>[Lua]</i> " + message, includeGameTime:true);
            };
        }

        private static void RegisterUserdataTypesOnce()
        {
            if (userdataTypesRegistered)
                return;

            UserData.RegisterType<AgentState>();
            UserData.RegisterType<DogState>();
            UserData.RegisterType<VisionState>();
            UserData.RegisterType<VisionAgentState>();
            UserData.RegisterType<VisionObjectState>();
            UserData.RegisterType<HearingState>();
            UserData.RegisterType<HearingSoundState>();
            UserData.RegisterType<ScentState>();
            UserData.RegisterType<PackState>();
            UserData.RegisterType<PackMemberState>();
            UserData.RegisterType<EnvState>();
            UserData.RegisterType<RoomState>();
            UserData.RegisterType<TaskState>();
            UserData.RegisterType<MemoryState>();
            UserData.RegisterType<TimeState>();
            UserData.RegisterType<PerceptionEventState>();

            userdataTypesRegistered = true;
        }

        public void RegisterBindings(DogLuaBindings bindings)
        {
            this.bindings = bindings;
            script.Globals["Bark"] = (Action<int>)bindings.Bark;
            script.Globals["MoveToEvent"] = (Action<float>)bindings.MoveToEvent;
            script.Globals["MoveToTarget"] = (Action<float>)bindings.MoveToTarget;
            script.Globals["FaceEventTarget"] = (Action<float, float>)bindings.FaceEventTarget;
            script.Globals["MoveUntilEventSeen"] = (Action<float, float>)bindings.MoveUntilEventSeen;
            script.Globals["MoveToEventSound"] = (Action<float>)bindings.MoveToEventSound;
            script.Globals["Sniff"] = (Action<float>)bindings.Sniff;
            script.Globals["FollowScent"] = (Action<string, string>)bindings.FollowScent;
            script.Globals["FollowEventScent"] = (Action)bindings.FollowEventScent;
            script.Globals["FollowEventScentAir"] = (Action)bindings.FollowEventScentAir;
            script.Globals["GoThroughDoor"] = (Action<int>)bindings.GoThroughDoor;
            script.Globals["GoToRoomCenter"] = (Action)bindings.GoToRoomCenter;
        }

        public void SetState(DogState dogState, VisionState visionState, HearingState hearingState)
        {
            SetState(
                dogState,
                visionState,
                hearingState,
                defaultScentState,
                defaultPackState,
                defaultEnvState,
                defaultRoomState,
                defaultTaskState,
                defaultMemoryState,
                defaultTimeState);
        }

        public void SetState(
            DogState dogState,
            VisionState visionState,
            HearingState hearingState,
            ScentState scentState,
            PackState packState,
            EnvState envState,
            RoomState roomState,
            TaskState taskState,
            MemoryState memoryState,
            TimeState timeState)
        {
            defaultState.Dog = dogState;
            defaultState.Vision = visionState;
            defaultState.Hearing = hearingState;
            defaultState.Scent = scentState;
            defaultState.Pack = packState;
            defaultState.Env = envState;
            defaultState.Room = roomState;
            defaultState.Task = taskState;
            defaultState.Memory = memoryState;
            defaultState.Time = timeState;

            script.Globals["State"] = defaultState;
            script.Globals["Dog"] = dogState;
            script.Globals["Vision"] = visionState;
            script.Globals["Hearing"] = hearingState;
            script.Globals["Scent"] = scentState;
            script.Globals["Pack"] = packState;
            script.Globals["Env"] = envState;
            script.Globals["Room"] = roomState;
            script.Globals["Task"] = taskState;
            script.Globals["Memory"] = memoryState;
            script.Globals["Time"] = timeState;
            script.Globals["Event"] = DynValue.Nil;
        }

        public void SetGlobal(string name, object? value)
        {
            script.Globals[name] = value == null
                ? DynValue.Nil
                : DynValue.FromObject(script, value);
        }

        public void SetPerceptionEvent(PerceptionEvent perceptionEvent)
        {
            if (bindings != null)
                bindings.SetPerceptionEvent(perceptionEvent);
            script.Globals["Event"] = PerceptionEventState.FromPerceptionEvent(perceptionEvent);
        }

        public bool LoadScript(string luaCode)
        {
            try
            {
                script.DoString(luaCode);
                return true;
            }
            catch (InterpreterException exception)
            {
                Debug.LogError("[LuaRuntime] Lua load error: " + exception.DecoratedMessage);
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogError("[LuaRuntime] General load error: " + exception);
                return false;
            }
        }

        public bool CallReact()
        {
            try
            {
                DynValue reactFunction = script.Globals.Get("react");

                if (reactFunction.IsNil())
                {
                    Debug.LogError("[LuaRuntime] Lua function 'react' was not found.");
                    return false;
                }

                DynValue perceptionEvent = script.Globals.Get("Event");
                script.Call(reactFunction, perceptionEvent);
                return true;
            }
            catch (InterpreterException exception)
            {
                Debug.LogError("[LuaRuntime] Lua runtime error: " + exception.DecoratedMessage);
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogError("[LuaRuntime] General runtime error: " + exception);
                return false;
            }
        }

        public bool CallTick()
        {
            try
            {
                DynValue tickFunction = script.Globals.Get("tick");

                if (tickFunction.IsNil())
                {
                    Debug.LogError("[LuaRuntime] Lua function 'tick' was not found.");
                    return false;
                }

                script.Call(tickFunction);
                return true;
            }
            catch (InterpreterException exception)
            {
                Debug.LogError("[LuaRuntime] Lua runtime error: " + exception.DecoratedMessage);
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogError("[LuaRuntime] General runtime error: " + exception);
                return false;
            }
        }
    }
}
