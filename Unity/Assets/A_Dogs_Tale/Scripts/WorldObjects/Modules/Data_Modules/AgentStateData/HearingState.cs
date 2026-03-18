using System.Collections.Generic;

namespace DogGame.Lua
{
    public class HearingSoundState
    {
        public string type = "";
        public int sourceAgent = -1;
        public float distance;
        public float directionX;
        public float directionY;
        public float directionZ;
        public float loudness;
        public float age;
    }

    public class HearingState
    {
        public List<HearingSoundState> recentSounds = new();
        public bool loudNoise;
        public bool barkHeard;
        public bool humanVoiceHeard;
        public bool distressBark;
        public HearingSoundState nearestBark = new();
        public HearingSoundState lastSound = new();
    }
}
