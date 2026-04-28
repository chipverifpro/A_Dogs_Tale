using DogGame.Modules;

// This file is a state class that holds all the subState classes.
// It passes initialize, update, and tick calls to the subState classes.

namespace DogGame.Lua
{
    public class AgentState
    {
        public DogState Dog = new();
        public VisionState Vision = new();
        public HearingState Hearing = new();
        public ScentState Scent = new();
        public PackState Pack = new();
        public EnvState Env = new();
        public RoomState Room = new();
        public TaskState Task = new();
        public TimeState Time = new();
        public MemoryState Memory = new();

        public WorldObject worldObject;
        public AgentState state;

        public void InitState(WorldObject worldObject, AgentState state)
        {
            this.worldObject = worldObject;
            this.state = state;

            Dog.InitState(worldObject, state);
            Vision.InitState(worldObject, state);
            Hearing.InitState(worldObject, state);
            Scent.InitState(worldObject, state);
            Pack.InitState(worldObject, state);
            Env.InitState(worldObject, state);
            Room.InitState(worldObject, state);
            Task.InitState(worldObject, state);
            Time.InitState(worldObject, state);
            Memory.InitState(worldObject, state);
        }

        public void UpdateState(Detail detail)
        {
            Dog.UpdateState(detail);
            Vision.UpdateState(detail);
            Hearing.UpdateState(detail);
            Scent.UpdateState(detail);
            Pack.UpdateState(detail);
            Env.UpdateState(detail);
            Room.UpdateState(detail);
            Task.UpdateState(detail);
            Time.UpdateState(detail);
            Memory.UpdateState(detail);
        }

        public void Tick(float interval)
        {
            Dog.Tick(interval);
            Vision.Tick(interval);
            Hearing.Tick(interval);
            Scent.Tick(interval);
            //PerceptionEvent.Tick(interval);
            Pack.Tick(interval);
            Env.Tick(interval);
            Room.Tick(interval);
            Task.Tick(interval);
            Time.Tick(interval);
            Memory.Tick(interval);
        }
    }
}