namespace DogGame.Lua
{
    public class MemoryState
    {
        public string lastDogSeen = "";
        public string lastFoodFound = "";
        public string lastThreatSeen = "";
        public string lastBarkHeard = "";

        public bool newDogSeen;
        public bool newSoundHeard;
        public bool newScentDetected;
    }
}
