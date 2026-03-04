#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_Timeout : IAgentTask
    {
        public string DebugName => $"Timeout({timeoutSeconds:0.0}s): {child.DebugName}";

        private readonly IAgentTask child;
        private readonly float timeoutSeconds;
        private float elapsed;

        public Task_Timeout(IAgentTask child, float timeoutSeconds)
        {
            this.child = child;
            this.timeoutSeconds = Mathf.Max(0.01f, timeoutSeconds);
        }

        public void Start(TaskContext context)
        {
            elapsed = 0f;
            child.Start(context);
        }

        public TaskTickResult Tick(TaskContext context, float dt)
        {
            elapsed += Mathf.Max(0f, dt);
            if (elapsed >= timeoutSeconds)
                return TaskTickResult.Failed("timeout");

            return child.Tick(context, dt);
        }

        public void Stop(TaskContext context) => child.Stop(context);
    }
}