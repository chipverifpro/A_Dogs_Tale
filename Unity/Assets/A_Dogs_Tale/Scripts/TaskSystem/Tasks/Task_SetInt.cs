#nullable enable
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_SetInt : IAgentTask
    {
        public string DebugName => $"SetInt({key}={value})";

        private readonly string key;
        private readonly int value;

        public Task_SetInt(string key, int value)
        {
            this.key = key;
            this.value = value;
        }

        public void Start(TaskContext context) { }

        public TaskTickResult Tick(TaskContext context, float dt)
        {
            context.Blackboard.SetInt(key, value);
            return TaskTickResult.Succeeded();
        }

        public void Stop(TaskContext context) { }
    }
}
