#nullable enable
using System.Collections.Generic;
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class TaskQueue
    {
        private readonly List<TaskRequest> queued = new();

        public int Count => queued.Count;
        public bool IsEmpty => queued.Count == 0;

        public void Clear() => queued.Clear();

        public List<TaskRequest> Snapshot()
        {
            return new List<TaskRequest>(queued);
        }

        public void Restore(IEnumerable<TaskRequest> requests)
        {
            queued.Clear();
            if (requests == null)
                return;

            foreach (TaskRequest request in requests)
                queued.Add(request);
        }

        public void Enqueue(TaskRequest request, bool front=false)
        {
            // Insert in descending priority order (stable)
            int index;
            if (front) 
            {
                queued.Insert(0, request);
            }
            else
            {
                index = queued.Count;
                while (index > 0 && queued[index - 1].Priority < request.Priority)
                    index--;

                queued.Insert(index, request);
            }
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
