#nullable enable
using UnityEngine;

namespace DogGame.LLM
{
    public interface IReactionScript
    {
        string DebugName { get; }
        bool CanTrigger(AgentTaskContext context, in PerceptionSnapshot snapshot);
        void Enqueue(AgentTaskContext context, AgentTaskQueue queue, in PerceptionSnapshot snapshot);
    }

/*
    public enum TaskSource { Player, LLM, SimpleAI, Script }


    public readonly struct TaskRequest
    {
        public readonly TaskSource Source;
        public readonly int Priority;      // 0..100
        public readonly bool CanInterrupt; // can preempt current?
        public readonly string? Tag;       // "movement", "dialogue", "interaction"
        public readonly IAgentTask Task;

        public TaskRequest(
            TaskSource source,
            int priority,
            bool canInterrupt,
            IAgentTask task,
            string? tag = null)
        {
            Source = source;
            Priority = priority;
            CanInterrupt = canInterrupt;
            Task = task;
            Tag = tag;
        }
    }
*/
}