using System.Collections.Generic;
using DogGame.Modules;
using UnityEngine;

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
        public bool loudNoise               = false;
        public bool barkHeard               = false;
        public bool humanVoiceHeard         = false;
        public bool distressBark            = false;
        public HearingSoundState nearestBark= new();
        public HearingSoundState lastSound  = new();

        public WorldObject worldObject;
        public AgentState  state;

        public void InitState(WorldObject worldObject, AgentState state)
        {
            this.worldObject = worldObject;
            this.state = state;
        }

        public void UpdateState(Detail detail)
        {
            // Placeholder for hearing-module pulls.
        }

        public void Tick(float interval)
        {
            for (int i = 0; i < recentSounds.Count; i++)
                recentSounds[i].age += interval;

            nearestBark.age += interval;
            lastSound.age += interval;

            for (int i = recentSounds.Count - 1; i >= 0; i--)
            {
                if (recentSounds[i].age > 10f)
                    recentSounds.RemoveAt(i);
            }

            if (nearestBark.age > 10f)
                nearestBark = new HearingSoundState();

            if (lastSound.age > 10f)
                lastSound = new HearingSoundState();

            loudNoise = loudNoise && lastSound.age < 1f;
            barkHeard = barkHeard && nearestBark.age < 2f;
            humanVoiceHeard = humanVoiceHeard && lastSound.age < 2f;
            distressBark = distressBark && nearestBark.age < 2f;
        }
    }
}
