#nullable enable
using System.Collections.Generic;

namespace DogGame.LLM.Translation
{
    public interface ITaskFactory
    {
        object CreateTask(string taskTypeName, Dictionary<string, object?> parameters);
        void AddChild(object parentTask, object childTask);
    }
}