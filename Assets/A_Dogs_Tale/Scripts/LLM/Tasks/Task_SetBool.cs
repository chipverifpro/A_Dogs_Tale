#nullable enable
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_SetBool : IAgentTask
    {
        public string DebugName => $"SetBool({key}={value})";
        private readonly string key;
        private readonly bool value;

        public Task_SetBool(string key, bool value)
        {
            this.key = key;
            this.value = value;
        }

        public void Start(AgentTaskContext context) { }
        public TaskTickResult Tick(AgentTaskContext context, float dt)
        {
            context.Blackboard.SetBool(key, value);
            return TaskTickResult.Succeeded();
        }
        public void Stop(AgentTaskContext context) { }
    }
}