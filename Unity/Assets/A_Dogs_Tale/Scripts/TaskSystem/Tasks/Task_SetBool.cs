#nullable enable
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_SetBool : IAgentTask
    {
        public string DebugName => $"SetBool({key}={value})";
        public string Description = "Sets a blackboard bool key to the specified value and succeeds immediately.";
        private readonly string key;
        private readonly bool value;

        public Task_SetBool(string key, bool value)
        {
            this.key = key;
            this.value = value;
        }

        public void Start(TaskContext context) { }
        public TaskTickResult Tick(TaskContext context, float dt)
        {
            context.Blackboard.SetBool(key, value);
            return TaskTickResult.Succeeded();
        }
        public void Stop(TaskContext context) { }
    }
}
