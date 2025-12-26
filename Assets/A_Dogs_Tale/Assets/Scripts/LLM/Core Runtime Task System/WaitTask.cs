#nullable enable
using UnityEngine;

namespace DogGame.LLM
{
    public sealed class WaitTask : IAgentTask
    {
        public string DebugName => $"Wait({durationSeconds:0.00}s)";

        private readonly float durationSeconds;
        private float remainingSeconds;

        public WaitTask(float durationSeconds)
        {
            this.durationSeconds = Mathf.Max(0f, durationSeconds);
            remainingSeconds = this.durationSeconds;
        }

        public void Start(AgentTaskContext context)
        {
            remainingSeconds = durationSeconds;
            context.Movement.StopMoving();
        }

        public TaskTickResult Tick(AgentTaskContext context, float deltaTimeSeconds)
        {
            remainingSeconds -= Mathf.Max(0f, deltaTimeSeconds);
            return remainingSeconds <= 0f ? TaskTickResult.Succeeded() : TaskTickResult.Running();
        }

        public void Stop(AgentTaskContext context)
        {
            // no-op
        }
    }
}