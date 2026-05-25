#nullable enable
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_Repeat : IAgentTask
    {
        public string DebugName => $"Repeat(n={repeatCount}, i={completed})";
        public string Description = "Runs a child task repeatedly for a fixed number of completions, or indefinitely when repeatCount is int.MaxValue.";

        private readonly IAgentTask child;
        private readonly int repeatCount;   // use int.MaxValue for "infinite"
        private int completed;

        public Task_Repeat(IAgentTask child, int repeatCount)
        {
            this.child = child;
            this.repeatCount = repeatCount <= 0 ? 0 : repeatCount;
        }

        public void Start(TaskContext context)
        {
            completed = 0;
            if (repeatCount > 0)
                child.Start(context);
        }

        public TaskTickResult Tick(TaskContext context, float dt)
        {
            if (repeatCount == 0) return TaskTickResult.Succeeded();

            // Infinite loop support (repeatCount == int.MaxValue)
            if (repeatCount != int.MaxValue && completed >= repeatCount)
                return TaskTickResult.Succeeded();

            var r = child.Tick(context, dt);
            if (r.Status == TaskStatus.Running) return r;

            // Child finished -> stop and restart
            child.Stop(context);
            completed++;

            if (repeatCount != int.MaxValue && completed >= repeatCount)
                return TaskTickResult.Succeeded();

            child.Start(context);
            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
            child.Stop(context);
        }
    }
}
