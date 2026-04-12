#nullable enable
using UnityEngine;
using DogGame.LLM;
using DogGame.Lua;
using DogGame.Modules;

namespace DogGame.Tasks
{
    public sealed class Task_RunLua : IAgentTask
    {
        private const int TaskLuaAutoYieldCounter = 1000;
        private const int TaskLuaMaxResumeCount = 4000;
        private const long TaskLuaMaxCallMilliseconds = 25;

        public string DebugName => $"RunLua({fileNameLua}:{entryFunction})";

        private readonly string fileNameLua;
        private readonly string entryFunction;
        private readonly float maxSeconds;
        private readonly string scentKey;
        private readonly ScentMedium scentMedium;
        private readonly float minThreshold;
        private readonly bool visitRoomCenterBeforeBacktracking;
        private readonly PerceptionEvent? initialEvent;

        private readonly AgentState agentState = new();

        private LuaRuntime? luaRuntime;
        private LuaHelpers? luaHelpers;
        private LuaTaskEnvironment? taskEnvironment;
        private float startedTime;
        private bool initialized;
        private string failureReason = "lua_init_failed";
        private PerceptionEvent activeEvent;

        public Task_RunLua(
            string fileNameLua,
            string entryFunction = "tick",
            float maxSeconds = 120f,
            string scentKey = "",
            ScentMedium scentMedium = ScentMedium.Ground,
            float minThreshold = 0.0002f,
            bool visitRoomCenterBeforeBacktracking = true,
            PerceptionEvent? perceptionEvent = null)
        {
            this.fileNameLua = string.IsNullOrWhiteSpace(fileNameLua) ? string.Empty : fileNameLua.Trim();
            this.entryFunction = string.IsNullOrWhiteSpace(entryFunction) ? "tick" : entryFunction.Trim();
            this.maxSeconds = Mathf.Max(0.25f, maxSeconds);
            this.scentKey = string.IsNullOrWhiteSpace(scentKey) ? string.Empty : scentKey.Trim();
            this.scentMedium = scentMedium;
            this.minThreshold = minThreshold;
            this.visitRoomCenterBeforeBacktracking = visitRoomCenterBeforeBacktracking;
            initialEvent = perceptionEvent;
        }

        public void Start(TaskContext context)
        {
            startedTime = Time.time;
            initialized = false;
            failureReason = "lua_init_failed";

            agentState.InitState(context.Agent, agentState);
            UpdateAgentState();

            activeEvent = initialEvent ?? CreateBootstrapEvent(context.Agent);
            taskEnvironment = new LuaTaskEnvironment(
                scentKey: scentKey,
                scentMedium: scentMedium,
                minThreshold: minThreshold,
                visitRoomCenterBeforeBacktracking: visitRoomCenterBeforeBacktracking);
            taskEnvironment.SetCurrentContext(context);

            luaHelpers = new LuaHelpers(
                taskSink: taskEnvironment.CreateTaskSink(),
                observer: context.Agent,
                perceptionEvent: activeEvent,
                taskEnvironment: taskEnvironment);

            luaRuntime = new LuaRuntime(
                autoYieldCounter: TaskLuaAutoYieldCounter,
                maxResumeCount: TaskLuaMaxResumeCount,
                maxCallMilliseconds: TaskLuaMaxCallMilliseconds);
            luaRuntime.RegisterBindings(luaHelpers);
            luaRuntime.SetState(agentState);
            luaRuntime.SetPerceptionEvent(activeEvent);
            taskEnvironment.UpdateGlobals(luaRuntime.Script, context);

            if (!LuaScriptLoader.TryLoad(fileNameLua, out string source, out string friendlyName, out string? error))
            {
                failureReason = error ?? "lua_script_missing";
                return;
            }

            initialized = luaRuntime.LoadScript(source, friendlyName);
            if (!initialized)
                failureReason = $"lua_load_failed:{friendlyName}";
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            if (!initialized || luaRuntime == null || luaHelpers == null || taskEnvironment == null)
                return TaskTickResult.Failed(failureReason);

            if (Time.time - startedTime > maxSeconds)
                return TaskTickResult.Failed("lua_timeout");

            if (!initialEvent.HasValue)
                activeEvent = CreateBootstrapEvent(context.Agent);

            taskEnvironment.SetCurrentContext(context);
            UpdateAgentState();
            luaRuntime.SetState(agentState);
            luaHelpers.SetPerceptionEvent(activeEvent);
            luaRuntime.SetPerceptionEvent(activeEvent);
            taskEnvironment.UpdateGlobals(luaRuntime.Script, context);

            if (taskEnvironment.TryConsumeCompletion(out TaskTickResult preChildResult))
                return preChildResult;

            if (taskEnvironment.IsDriving)
            {
                taskEnvironment.TickChildTasks(context, deltaTimeSeconds);
                if (taskEnvironment.TryConsumeCompletion(out TaskTickResult childResult))
                    return childResult;

                return TaskTickResult.Running();
            }

            if (!luaRuntime.CallFunction(entryFunction))
                return TaskTickResult.Failed($"lua_runtime_error:{entryFunction}");

            if (taskEnvironment.TryConsumeCompletion(out TaskTickResult postCallResult))
                return postCallResult;

            if (taskEnvironment.IsDriving)
            {
                taskEnvironment.TickChildTasks(context, deltaTimeSeconds);
                if (taskEnvironment.TryConsumeCompletion(out TaskTickResult postChildResult))
                    return postChildResult;
            }

            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
            taskEnvironment?.Stop(context);
        }

        private void UpdateAgentState()
        {
            agentState.UpdateState(Detail.High);
        }

        private static PerceptionEvent CreateBootstrapEvent(WorldObject observer)
        {
            return new PerceptionEvent(
                observer: observer,
                sense: PerceptionSense.Scent,
                type: PerceptionEventType.SomethingInteresting,
                worldPos: observer.transform.position,
                target: null,
                strength01: 0f,
                novelty01: 0f,
                interest01: 0f);
        }
    }
}
