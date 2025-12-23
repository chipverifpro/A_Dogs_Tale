#nullable enable
using System.Collections.Generic;

namespace DogGame.LLM
{
    public sealed class AgentTaskQueue
    {
        private readonly Queue<IAgentTask> queuedTasks = new();

        public int Count => queuedTasks.Count;

        public void Enqueue(IAgentTask task) => queuedTasks.Enqueue(task);

        public bool TryDequeue(out IAgentTask? task)
        {
            if (queuedTasks.Count == 0)
            {
                task = null;
                return false;
            }

            task = queuedTasks.Dequeue();
            return true;
        }

        public void Clear() => queuedTasks.Clear();
    }
}