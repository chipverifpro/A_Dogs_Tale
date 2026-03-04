#nullable enable
using System;
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_WaitUntil : IAgentTask
    {
        public string DebugName => $"WaitUntil(timeout={timeoutSeconds:0.0}s)";

        private readonly Func<TaskContext, bool> predicate;
        private readonly float timeoutSeconds;

        private float elapsed;

        public Task_WaitUntil(Func<TaskContext, bool> predicate, float timeoutSeconds = 2.0f)
        {
            this.predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            this.timeoutSeconds = Mathf.Max(0.01f, timeoutSeconds);
        }

        public void Start(TaskContext context)
        {
            elapsed = 0f;
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            elapsed += Mathf.Max(0f, deltaTimeSeconds);

            bool ok = false;
            try
            {
                ok = predicate(context);
            }
            catch (Exception ex)
            {
                return TaskTickResult.Failed($"predicate_exception:{ex.Message}");
            }

            if (ok)
                return TaskTickResult.Succeeded();

            if (elapsed >= timeoutSeconds)
                return TaskTickResult.Failed("timeout");

            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context) { }
    }
}