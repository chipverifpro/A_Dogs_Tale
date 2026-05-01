#nullable enable
using UnityEngine;
using DogGame.LLM;
using DogGame.Modules;

namespace DogGame.Tasks
{
    public sealed class Task_SetWalkMode : IAgentTask
    {
        public string DebugName => $"SetWalkMode({walkMode})";
        public string Description = "Sets the agent movement module's walk mode and succeeds immediately.";
        private readonly WalkMode walkMode;

        public Task_SetWalkMode(WalkMode walkMode)
        {
            this.walkMode = walkMode;
        }

        public void Start(TaskContext context) { }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            if (context.Agent == null || context.Agent.agentMovementModule == null)
                return TaskTickResult.Failed("missing_agent_movement_module");

            global::Pack? pack = context.Agent.packMemberModule != null
                ? context.Agent.packMemberModule.currentPack
                : null;

            if (pack != null)
                pack.SetWalkMode(walkMode);
            else
                context.Agent.agentMovementModule.SetWalkMode(walkMode);

            return TaskTickResult.Succeeded();
        }

        public void Stop(TaskContext context) { }
    }
}
