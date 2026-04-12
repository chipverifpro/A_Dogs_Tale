#nullable enable
using DogGame.LLM;
using DogGame.Lua;
using DogGame.Modules;

namespace DogGame.Tasks
{
    public sealed class Task_ScentFollowLua : IAgentTask
    {
        private readonly Task_RunLua innerTask;

        public Task_ScentFollowLua(
            string scentKey,
            ScentMedium medium,
            float minThreshold = 0.0002f,
            float maxSeconds = 120f,
            string luaFileName = "Task_ScentFollow.lua")
        {
            innerTask = new Task_RunLua(
                fileNameLua: luaFileName,
                entryFunction: "tick",
                maxSeconds: maxSeconds,
                scentKey: scentKey,
                scentMedium: medium,
                minThreshold: minThreshold,
                visitRoomCenterBeforeBacktracking: true);
        }

        public string DebugName => innerTask.DebugName;

        public void Start(TaskContext context)
        {
            innerTask.Start(context);
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            return innerTask.Tick(context, deltaTimeSeconds);
        }

        public void Stop(TaskContext context)
        {
            innerTask.Stop(context);
        }
    }
}
