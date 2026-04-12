#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using MoonSharp.Interpreter;
using UnityEngine;
using DogGame.Modules;

namespace DogGame.Lua
{
    public class LuaRuntime
    {
        private const int DefaultAutoYieldCounter = 2000;
        private const int DefaultMaxResumeCount = 500;
        private const long DefaultMaxCallMilliseconds = 8;

        private static bool userdataTypesRegistered = false;
        private readonly Script script;
        private readonly int autoYieldCounter;
        private readonly int maxResumeCount;
        private readonly long maxCallMilliseconds;
        private readonly AgentState defaultState = new();
        private readonly ScentState defaultScentState = new();
        private readonly PackState defaultPackState = new();
        private readonly EnvState defaultEnvState = new();
        private readonly RoomState defaultRoomState = new();
        private readonly TaskState defaultTaskState = new();
        private readonly MemoryState defaultMemoryState = new();
        private readonly TimeState defaultTimeState = new();
        private LuaHelpers? bindings;
        private string debugAgentName = "<unknown>";
        private string debugScriptName = "<unloaded>";

        public LuaRuntime()
            : this(
                autoYieldCounter: DefaultAutoYieldCounter,
                maxResumeCount: DefaultMaxResumeCount,
                maxCallMilliseconds: DefaultMaxCallMilliseconds)
        {
        }

        public LuaRuntime(int autoYieldCounter, int maxResumeCount, long maxCallMilliseconds)
        {
            RegisterUserdataTypesOnce();

            this.autoYieldCounter = Math.Max(1, autoYieldCounter);
            this.maxResumeCount = Math.Max(1, maxResumeCount);
            this.maxCallMilliseconds = Math.Max(1L, maxCallMilliseconds);

            script = new Script();

            script.Options.DebugPrint = message =>
            {
                string prefix = BuildDebugPrefix();
                UnityEngine.Debug.Log(prefix + " " + message);
                BottomBanner.LogRichMessage(BannerSense.None,BannerLevel.None,"<i>" + prefix + "</i> " + message, includeGameTime:true);
            };
        }

        public Script Script => script;

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

        public void RegisterBindings(LuaHelpers bindings)
        {
            this.bindings = bindings;
            debugAgentName = bindings.ObserverDisplayName;
            bindings.Register(script);
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

        public void SetState(AgentState agentState)
        {
            SetState(
                agentState.Dog,
                agentState.Vision,
                agentState.Hearing,
                agentState.Scent,
                agentState.Pack,
                agentState.Env,
                agentState.Room,
                agentState.Task,
                agentState.Memory,
                agentState.Time);
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
            return LoadScript(luaCode, "lua_script");
        }

        public bool LoadScript(string luaCode, string friendlyName)
        {
            debugScriptName = string.IsNullOrWhiteSpace(friendlyName)
                ? "lua_script"
                : Path.GetFileName(friendlyName);

            try
            {
                script.DoString(luaCode, null, friendlyName);
                return true;
            }
            catch (InterpreterException exception)
            {
                UnityEngine.Debug.LogError("[LuaRuntime] Lua load error: " + exception.DecoratedMessage);
                return false;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError("[LuaRuntime] General load error: " + exception);
                return false;
            }
        }

        public bool CallReact()
        {
            DynValue perceptionEvent = script.Globals.Get("Event");
            return CallFunction("react", perceptionEvent);
        }

        public bool CallTick()
        {
            return CallFunction("tick");
        }

        public bool CallFunction(string functionName, params object?[] args)
        {
            try
            {
                DynValue function = script.Globals.Get(functionName);
                if (function.IsNil())
                {
                    UnityEngine.Debug.LogError($"[LuaRuntime] Lua function '{functionName}' was not found.");
                    return false;
                }

                DynValue coroutineValue = script.CreateCoroutine(function);
                MoonSharp.Interpreter.Coroutine coroutine = coroutineValue.Coroutine;
                coroutine.AutoYieldCounter = autoYieldCounter;

                Stopwatch stopwatch = Stopwatch.StartNew();
                DynValue[] dynArgs = BuildDynArgs(args);
                DynValue callResult = dynArgs.Length == 0
                    ? coroutine.Resume()
                    : coroutine.Resume(dynArgs);
                int resumeCount = 0;

                while (coroutine.State == CoroutineState.ForceSuspended)
                {
                    resumeCount++;
                    if (resumeCount > maxResumeCount || stopwatch.ElapsedMilliseconds > maxCallMilliseconds+1000)
                    {
                        UnityEngine.Debug.LogError($"[LuaRuntime] Lua function '{functionName}' exceeded the execution budget.  stopwatch={stopwatch.ElapsedMilliseconds} > max={maxCallMilliseconds}");
                        return false;
                    }

                    callResult = coroutine.Resume();
                }

                if (coroutine.State != CoroutineState.Dead)
                {
                    UnityEngine.Debug.LogError($"[LuaRuntime] Lua function '{functionName}' yielded unexpectedly (state={coroutine.State}, type={callResult.Type}).");
                    return false;
                }

                return true;
            }
            catch (InterpreterException exception)
            {
                UnityEngine.Debug.LogError("[LuaRuntime] Lua runtime error: " + exception.DecoratedMessage);
                return false;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError("[LuaRuntime] General runtime error: " + exception);
                return false;
            }
        }

        private DynValue[] BuildDynArgs(object?[] args)
        {
            if (args == null || args.Length == 0)
                return Array.Empty<DynValue>();

            DynValue[] dynArgs = new DynValue[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                object? arg = args[i];
                dynArgs[i] = arg switch
                {
                    null => DynValue.Nil,
                    DynValue dynValue => dynValue,
                    _ => DynValue.FromObject(script, arg)
                };
            }

            return dynArgs;
        }

        private string BuildDebugPrefix()
        {
            return $"[Lua agent={debugAgentName} script={debugScriptName}]";
        }
    }
}
