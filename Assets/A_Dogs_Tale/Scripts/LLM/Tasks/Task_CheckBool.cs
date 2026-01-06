#nullable enable
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_CheckBool : IAgentTask
    {
        public string DebugName => $"CheckBool({key}=={expected})";
        private readonly string key;
        private readonly bool expected;

        public Task_CheckBool(string key, bool expected)
        {
            this.key = key;
            this.expected = expected;
        }

        public void Start(AgentTaskContext context) { }

        public TaskTickResult Tick(AgentTaskContext context, float dt)
        {
            if (!context.Blackboard.TryGetBool(key, out var v))
                return TaskTickResult.Failed("missing_key");

            return v == expected ? TaskTickResult.Succeeded() : TaskTickResult.Failed("bool_mismatch");
        }

        public void Stop(AgentTaskContext context) { }
    }
}