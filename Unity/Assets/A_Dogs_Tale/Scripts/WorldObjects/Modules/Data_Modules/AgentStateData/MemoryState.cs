using DogGame.Modules;

namespace DogGame.Lua
{
    public class MemoryState
    {
        public string lastDogSeen        = "";
        public string lastFoodFound      = "";
        public string lastThreatSeen     = "";
        public string lastBarkHeard      = "";

        public bool newDogSeen           = false;
        public bool newSoundHeard        = false;
        public bool newScentDetected     = false;

        public WorldObject worldObject;
        public AgentState  state;

        public void InitState(WorldObject worldObject, AgentState state)
        {
            this.worldObject = worldObject;
            this.state = state;
        }

        public void UpdateState(Detail detail)
        {
            // Placeholder for event-history integration.
        }

        public void Tick(float interval)
        {
            newDogSeen = false;
            newSoundHeard = false;
            newScentDetected = false;
        }
    }
}
