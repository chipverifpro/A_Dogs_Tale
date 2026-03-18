using DogGame.Modules;

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
        public TaskState Task = new();
        public MemoryState Memory = new();
        public TimeState Time = new();

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
            Task.InitState(worldObject, state);
            Memory.InitState(worldObject, state);
            Time.InitState(worldObject, state);
        }

        public void UpdateState(Detail detail)
        {
            Dog.UpdateState(detail);
            Vision.UpdateState(detail);
            Hearing.UpdateState(detail);
            Scent.UpdateState(detail);
            Pack.UpdateState(detail);
            Env.UpdateState(detail);
            Task.UpdateState(detail);
            Memory.UpdateState(detail);
            Time.UpdateState(detail);
        }

        public void Tick(float interval)
        {
            Dog.Tick(interval);
            Vision.Tick(interval);
            Hearing.Tick(interval);
            Scent.Tick(interval);
            Pack.Tick(interval);
            Env.Tick(interval);
            Task.Tick(interval);
            Memory.Tick(interval);
            Time.Tick(interval);
        }
    }
}
