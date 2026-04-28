using DogGame.Lua;
using NUnit.Framework.Constraints;
using UnityEngine;
using InspectorTools;

// at Update() it pulls in latest information
namespace DogGame.Modules
{
    public enum Detail {None=0, Low=1, Medium=2, High=3};

    [DisallowMultipleComponent]
    [InspectorNote("Data_Modules/Agent State Module", "Manages all state data modules, providing Agent, Dog, Env, Hearing, Memory, Pack, Perception, Room, Scent, Task, Time, and Vision state.")]
    public class AgentStateModule : WorldModule
    {
        public AgentState state = new();
            
        public void Start()
        {
            // knowing world object makes pulling data from the WorldModules easier.
            // knowing state helps pull info from other fields.
            //   Make sure you know the other state is already updated or you will get old data.
            state.InitState(worldObject, state);
        }

        // call this to get latest detailed information filled in.
        public void UpdateStateAll(Detail detail)
        {
            state.UpdateState(detail);
        }

        // This updates things based on time elapsed, such as hunger.
        public override void Tick(float dt)
        {
            state.Tick(dt);
        }
    }
}
