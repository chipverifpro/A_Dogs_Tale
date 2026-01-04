#nullable enable

namespace DogGame.LLM
{
    public readonly struct TaskRequest
    {
        public readonly IAgentTask Task;
        public readonly int Priority;           // 0–100
        public readonly bool CanInterrupt;
        public readonly bool ResumePrevious;
        public readonly bool ClearStackOnStart;
        public readonly TaskSource Source;
        public readonly string? Tag;

        public TaskRequest(
            IAgentTask task,
            int priority,
            TaskSource source,
            bool canInterrupt = true,
            bool resumePrevious = false,
            bool clearStackOnStart = false,
            string? tag = null)
        {
            Task = task;
            Priority = priority;
            Source = source;
            CanInterrupt = canInterrupt;
            ResumePrevious = resumePrevious;
            ClearStackOnStart = clearStackOnStart;
            Tag = tag;
        }
    }
}