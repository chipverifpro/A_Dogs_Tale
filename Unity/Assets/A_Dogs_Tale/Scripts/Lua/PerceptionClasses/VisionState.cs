using System.Collections.Generic;

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

        public bool smallAnimalVisible;
        public bool foodVisible;
    }
}
