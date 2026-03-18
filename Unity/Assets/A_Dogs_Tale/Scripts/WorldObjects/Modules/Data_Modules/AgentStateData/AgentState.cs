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
    }
}
