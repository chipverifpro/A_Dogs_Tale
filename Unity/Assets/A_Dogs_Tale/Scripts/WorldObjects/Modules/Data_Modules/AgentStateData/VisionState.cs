using System.Collections.Generic;
using DogGame.Modules;

namespace DogGame.Lua
{
    public class VisionAgentState
    {
        public int id = -1;
        public string type = "";
        public float distance;
        public bool isPackMember;
        public bool isThreat;
        public bool isMoving;
        public float directionX;
        public float directionY;
        public float directionZ;
    }

    public class VisionObjectState
    {
        public int id = -1;
        public WorldObject visionSource = null;
        public string type = "";
        public string name = "";
        public float distance;
        public float directionX;
        public float directionY;
        public float directionZ;
    }

    public class VisionState
    {
        public List<VisionAgentState> visibleAgents = new();
        public List<VisionObjectState> visibleObjects = new();
        public List<VisionObjectState> visibleFood = new();
        public List<VisionAgentState> visibleThreats = new();

        public VisionAgentState nearestDog = new();
        public VisionAgentState nearestHuman = new();
        public VisionObjectState nearestFood = new();
        public VisionAgentState nearestThreat = new();

        public bool smallAnimalVisible = false;
        public bool foodVisible = false;

        public WorldObject worldObject;
        public AgentState  state;

        public void InitState(WorldObject worldObject, AgentState state)
        {
            this.worldObject = worldObject;
            this.state = state;
        }

        public void UpdateState(Detail detail)
        {
            // Placeholder for vision-perception pulls.
        }

        public void Tick(float interval)
        {
            // Vision is refreshed from perception; no passive drift needed yet.
        }
    }
}
