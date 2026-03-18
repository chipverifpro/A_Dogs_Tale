using DogGame.Modules;

namespace DogGame.Lua
{
    public class EnvState
    {
        public string roomType          = "";
        public bool isIndoor            = false;
        public bool isNight             = false;
        public bool isRaining           = false;

        public bool waterNearby         = false;
        public bool shelterNearby       = false;
        public float terrainDifficulty  = 0.0f;
        public float areaDangerLevel    = 0.0f;

        public WorldObject worldObject;
        public AgentState  state;

        public void InitState(WorldObject worldObject, AgentState state)
        {
            this.worldObject = worldObject;
            this.state = state;
        }

        public void UpdateState(Detail detail)
        {
            // Placeholder for room/weather lookups from world modules.
        }

        public void Tick(float interval)
        {
            terrainDifficulty = UnityEngine.Mathf.Clamp01(terrainDifficulty);
            areaDangerLevel = UnityEngine.Mathf.Clamp01(areaDangerLevel);
        }
    }
}
