#nullable enable
using DogGame.LLM;

namespace DogGame.Tasks
{
    public interface IAgentTaskRunner
    {
        void StartTask(IAgentTask rootTask);
        void AbortAll(string reason);
    }
}