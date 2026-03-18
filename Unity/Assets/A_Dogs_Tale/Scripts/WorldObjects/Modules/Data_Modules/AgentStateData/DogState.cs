using DogGame.Modules;
using UnityEngine;

namespace DogGame.Lua
{
    public class DogState
    {
        public float hunger         = 0.0f;
        public float thirst         = 0.0f;
        public float energy         = 1.0f;
        public float pain           = 0.0f;
        public float boredom        = 0.5f;
        public float fear           = 0.0f;
        public float curiosity      = 0.5f;
        public float excitement     = 0.5f;
        public float confidence     = 0.5f;

        public float thresholdHungry = 0.5f;
        public float thresholdThirsty= 0.5f;
        public float thresholdTired  = 0.8f;
        public float thresholdBored  = 0.2f;
        public float thresholdAfraid = 0.5f;

        public bool isHungry => hunger > thresholdHungry;
        public bool isThirsty=> thirst > thresholdThirsty;
        public bool isTired  => energy < thresholdTired;
        public bool isBored  => excitement < thresholdBored;
        public bool isAfraid => fear > thresholdAfraid;
    

        // These allow update to access anything in worldObject or state
        public WorldObject worldObject;
        public AgentState  state;

        public void InitState(WorldObject worldObject, AgentState state)
        {
            this.worldObject = worldObject;
            this.state  = state;
        }

        // before using this structure, update anything that isn't already set.
        public void UpdateState(Detail detail)
        {
            // nothing needed
        }

        // every interval, grow or decay relevant parameters
        public void Tick(float interval)
        {
            hunger      = Mathf.Clamp01(hunger     + interval * .01f);
            thirst      = Mathf.Clamp01(thirst     + interval * .01f);
            energy      = Mathf.Clamp01(energy     - interval * .01f);
            pain        = Mathf.Clamp01(pain       - interval * .01f);
            boredom     = Mathf.Clamp01(hunger     + interval * .01f);
            fear        = Mathf.Clamp01(fear       - interval * .01f);
            curiosity   = Mathf.Clamp01(curiosity  + interval * .01f);
            excitement  = Mathf.Clamp01(excitement - interval * .01f);
            confidence  = Mathf.Clamp01(confidence + interval * .01f);
        }
    }
}
