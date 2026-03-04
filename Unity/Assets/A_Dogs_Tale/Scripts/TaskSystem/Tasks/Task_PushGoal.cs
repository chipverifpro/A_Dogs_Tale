#nullable enable
using DogGame.LLM;

namespace DogGame.Tasks
{
    /// <summary>
    /// Phase-1 "goal push": writes a string into the blackboard.
    /// </summary>
    public sealed class Task_PushGoal : IAgentTask
    {
        public string DebugName => $"PushGoal('{goalId}')";

        private readonly string goalId;
        private readonly bool overwrite;

        public Task_PushGoal(string goalId, bool overwrite = true)
        {
            this.goalId = goalId;
            this.overwrite = overwrite;
        }

        public void Start(TaskContext context) { }

        public TaskTickResult Tick(TaskContext context, float dt)
        {
            const string key = "goal.current";

            if (!overwrite && context.Blackboard.TryGetString(key, out var existing) && !string.IsNullOrEmpty(existing))
                return TaskTickResult.Succeeded();

            context.Blackboard.SetString(key, goalId);
            return TaskTickResult.Succeeded();
        }

        public void Stop(TaskContext context) { }
    }
}