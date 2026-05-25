#nullable enable
using DogGame.Tasks;

namespace DogGame.LLM
{
    public interface IReactionScript
    {
        string DebugName { get; }
        bool CanTrigger(TaskContext context, in PerceptionSnapshot snapshot);
        void Enqueue(TaskContext context, TaskQueue queue, in PerceptionSnapshot snapshot);
    }

}