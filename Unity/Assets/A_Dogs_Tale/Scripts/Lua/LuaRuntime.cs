using System;
using MoonSharp.Interpreter;
using UnityEngine;

namespace DogGame.Lua
{
    public class LuaRuntime
    {
        private static bool userdataTypesRegistered = false;
        private readonly Script script;

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

            userdataTypesRegistered = true;
        }

        public void RegisterBindings(DogLuaBindings bindings)
        {
            script.Globals["Bark"] = (Action<int>)bindings.Bark;
            script.Globals["MoveToEvent"] = (Action<float>)bindings.MoveToEvent;
            script.Globals["MoveToTarget"] = (Action<float>)bindings.MoveToTarget;
        }

        public void SetState(DogState dogState, VisionState visionState, HearingState hearingState)
        {
            script.Globals["Dog"] = dogState;
            script.Globals["Vision"] = visionState;
            script.Globals["Hearing"] = hearingState;
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

                script.Call(reactFunction);
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