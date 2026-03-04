#nullable enable
using DogGame.LLM;

namespace DogGame.Tasks
{
    /// <summary>
    /// Implemented by tasks that contain child tasks (Sequence, Try, Branch, Repeat, etc.)
    /// so we can add children without reflection.
    /// </summary>
    public interface ICompositeAgentTask : IAgentTask
    {
        void AddChild(IAgentTask child);
    }
}