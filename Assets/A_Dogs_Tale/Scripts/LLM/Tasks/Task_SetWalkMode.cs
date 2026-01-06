#nullable enable
using UnityEngine;
using DogGame.LLM;
using DogGame.Modules;

namespace DogGame.Tasks
{
    public sealed class Task_SetWalkMode : IAgentTask
    {
        public string DebugName => $"SetWalkMode({walkMode})";
        private readonly WalkMode walkMode;

        public Task_SetWalkMode(WalkMode walkMode)
        {
            this.walkMode = walkMode;
        }

        public void Start(AgentTaskContext context) { }

        public TaskTickResult Tick(AgentTaskContext context, float deltaTimeSeconds)
        {
            if (context.Agent == null || context.Agent.agentMovementModule == null)
                return TaskTickResult.Failed("missing_agent_movement_module");

            context.Agent.agentMovementModule.walkMode = walkMode;
            return TaskTickResult.Succeeded();
        }

        public void Stop(AgentTaskContext context) { }
    }
}