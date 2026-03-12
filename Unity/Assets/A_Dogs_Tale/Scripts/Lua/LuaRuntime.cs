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
        private DogLuaBindings bindings;

        public LuaRuntime()
        {
            RegisterUserdataTypesOnce();

            script = new Script();

            script.Options.DebugPrint = message =>
            {
                Debug.Log("[Lua] " + message);
            };
        }

        private static void RegisterUserdataTypesOnce()
        {
            if (userdataTypesRegistered)
                return;

            UserData.RegisterType<DogState>();
            UserData.RegisterType<VisionState>();
            UserData.RegisterType<HearingState>();
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
        }

        public void SetState(DogState dogState, VisionState visionState, HearingState hearingState)
        {
            script.Globals["Dog"] = dogState;
            script.Globals["Vision"] = visionState;
            script.Globals["Hearing"] = hearingState;
            script.Globals["Event"] = DynValue.Nil;
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
    }
}
