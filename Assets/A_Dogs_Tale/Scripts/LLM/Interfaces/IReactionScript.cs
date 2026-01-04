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

}