#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_Abort : IAgentTask
    {
        public string DebugName => succeed ? "AbortSuccess" : $"AbortFail({reason})";

        private readonly bool succeed;
        private readonly string? reason;

        public Task_Abort(bool succeed, string? reason = null)
        {
            this.succeed = succeed;
            this.reason = reason;
        }

        public void Start(TaskContext context) { }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            return succeed ? TaskTickResult.Succeeded()
                           : TaskTickResult.Failed(reason ?? "aborted");
        }

        public void Stop(TaskContext context) { }
    }
}