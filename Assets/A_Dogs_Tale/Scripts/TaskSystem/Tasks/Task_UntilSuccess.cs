#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_UntilSuccess : IAgentTask
    {
        public string DebugName => $"UntilSuccess(elapsed={elapsed:0.0}s)";

        private readonly IAgentTask attempt;
        private readonly float timeoutSeconds;   // <=0 means infinite
        private float elapsed;

        public Task_UntilSuccess(IAgentTask attempt, float timeoutSeconds = 0f)
        {
            this.attempt = attempt;
            this.timeoutSeconds = timeoutSeconds;
        }

        public void Start(TaskContext context)
        {
            elapsed = 0f;
            attempt.Start(context);
        }

        public TaskTickResult Tick(TaskContext context, float dt)
        {
            elapsed += Mathf.Max(0f, dt);

            if (timeoutSeconds > 0f && elapsed >= timeoutSeconds)
                return TaskTickResult.Failed("until_success_timeout");

            var r = attempt.Tick(context, dt);
            if (r.Status == TaskStatus.Running) return r;

            // If succeeded, we're done.
            if (r.Status == TaskStatus.Succeeded)
            {
                attempt.Stop(context);
                return TaskTickResult.Succeeded();
            }

            // Failed -> restart attempt and keep going
            attempt.Stop(context);
            attempt.Start(context);
            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context) => attempt.Stop(context);
    }
}