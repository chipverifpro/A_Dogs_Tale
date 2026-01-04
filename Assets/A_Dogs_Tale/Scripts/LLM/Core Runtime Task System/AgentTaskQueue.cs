#nullable enable
using System.Collections.Generic;

namespace DogGame.LLM
{
    public sealed class AgentTaskQueue
    {
        private readonly List<TaskRequest> queued = new();

        public int Count => queued.Count;
        public bool IsEmpty => queued.Count == 0;

        public void Clear() => queued.Clear();

        public void Enqueue(TaskRequest request)
        {
            // Insert in descending priority order (stable)
            int index = queued.Count;
            while (index > 0 && queued[index - 1].Priority < request.Priority)
                index--;

            queued.Insert(index, request);
        }

        public bool TryDequeue(out TaskRequest request)
        {
            if (queued.Count == 0)
            {
                request = default;
                return false;
            }

            request = queued[0];
            queued.RemoveAt(0);
            return true;
        }

        public bool Peek(out TaskRequest request)
        {
            if (queued.Count == 0)
            {
                request = default;
                return false;
            }

            request = queued[0];
            return true;
        }
    }
}