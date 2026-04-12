#nullable enable
using DogGame.LLM;
using DogGame.Modules;

namespace DogGame.Lua
{
    public class DogLuaBindings : LuaHelpers
    {
        public DogLuaBindings(
            TaskController taskController,
            WorldObject observer,
            PerceptionEvent perceptionEvent)
            : base(
                taskSink: new TaskControllerLuaTaskSink(taskController),
                observer: observer,
                perceptionEvent: perceptionEvent,
                taskEnvironment: null)
        {
        }
    }
}
