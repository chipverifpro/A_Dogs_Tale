using DogGame.Lua;
using NUnit.Framework.Constraints;
using UnityEngine;

// at Update() it pulls in latest information
namespace DogGame.Modules
{
    public enum Detail {None=0, Low=1, Medium=2, High=3};

    [DisallowMultipleComponent]
    public class AgentStateModule : WorldModule
    {
        public AgentState state = new();

        private float prevIntervalTime = 0f;
            
        public void Start()
        {
            // knowing world object makes pulling data from the WorldModules easier.
            // knowing state helps pull info from other fields.
            //   Make sure you know the other state is already updated or you will get old data.
            state.Dog.InitState(worldObject, state);
            state.Env.InitState(worldObject, state);
            state.Hearing.InitState(worldObject, state);
            state.Memory.InitState(worldObject, state);
            state.Pack.InitState(worldObject, state);
            state.Scent.InitState(worldObject, state);
            state.Task.InitState(worldObject, state);
            state.Time.InitState(worldObject, state);
            state.Vision.InitState(worldObject, state);
            state.Room.InitState(worldObject, state);
            // todo: the rest
        }

        // call this to get latest detailed information filled in.
        public void UpdateStateAll(Detail detail)
        {
            state.Dog.UpdateState(detail);
            state.Env.UpdateState(detail);
            state.Hearing.UpdateState(detail);
            state.Memory.UpdateState(detail);
            state.Pack.UpdateState(detail);
            state.Scent.UpdateState(detail);
            state.Task.UpdateState(detail);
            state.Time.UpdateState(detail);
            state.Vision.UpdateState(detail);
            state.Room.UpdateState(detail);
        }

        // This updates things based on time elapsed, such as hunger.
        public override void Tick(float dt)
        {
            // determine how long since last tick.
            // could use dt instead.
            float interval = Time.time - prevIntervalTime;
            prevIntervalTime = Time.time;

            state.Dog.Tick(interval);
            state.Env.Tick(interval);
            state.Hearing.Tick(interval);
            state.Memory.Tick(interval);
            state.Pack.Tick(interval);
            state.Scent.Tick(interval);
            state.Task.Tick(interval);
            state.Time.Tick(interval);
            state.Vision.Tick(interval);
            state.Room.Tick(interval);
        }
    }
}
