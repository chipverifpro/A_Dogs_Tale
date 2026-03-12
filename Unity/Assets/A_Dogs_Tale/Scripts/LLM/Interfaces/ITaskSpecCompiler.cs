using DogGame.LLM;
using DogGame.Reactions;

public interface ITaskSpecCompiler
{
    IAgentTask Compile(TaskSpec taskSpec);
}