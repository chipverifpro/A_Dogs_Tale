#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_Wait : IAgentTask
    {
        public string DebugName => $"Wait({durationSeconds:0.00}s)";

        private readonly float durationSeconds;
        private float remainingSeconds;

        public Task_Wait(float durationSeconds)
        {
            this.durationSeconds = Mathf.Max(0f, durationSeconds);
            remainingSeconds = this.durationSeconds;
            Debug.Log(DebugName);
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