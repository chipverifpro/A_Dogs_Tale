using DogGame.Modules;

namespace DogGame.Lua
{
    public class TaskState
    {
        public string current            = "";
        public string target             = "";
        public string destination        = "";

        public bool targetVisible        = false;
        public float targetDistance      = 0.0f;
        public bool destinationReached   = false;
        public bool pathBlocked          = false;

        public WorldObject worldObject;
        public AgentState  state;

        public void InitState(WorldObject worldObject, AgentState state)
        {
            this.worldObject = worldObject;
            this.state = state;
        }

        public void UpdateState(Detail detail)
        {
            // Placeholder for task-controller pulls.
        }

        public void Tick(float interval)
        {
            targetDistance = UnityEngine.Mathf.Max(0f, targetDistance);
        }
    }
}
