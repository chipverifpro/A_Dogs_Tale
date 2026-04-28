using DogGame.Modules;

namespace DogGame.Lua
{
    public class ScentState
    {
        public string strongestScent    = "";
        public WorldObject scentSource  = null;
        public bool foodTrail           = false;
        public bool dogTrail            = false;
        public bool humanTrail          = false;
        public bool predatorTrail       = false;

        public float trailAge           = 0.0f;
        public float trailDirectionX    = 0.0f;
        public float trailDirectionY    = 0.0f;
        public float trailDirectionZ    = 0.0f;
        public float trailStrength      = 0.0f;

        public bool foodNearby          = false;
        public bool freshTrail          = false;
        public bool strangerDogScent    = false;
        public bool packMemberScent     = false;

        public WorldObject worldObject;
        public AgentState  state;

        public void InitState(WorldObject worldObject, AgentState state)
        {
            this.worldObject = worldObject;
            this.state = state;
        }

        public void UpdateState(Detail detail)
        {
            // Placeholder for scent-perception pulls.
        }

        public void Tick(float interval)
        {
            if (trailStrength > 0f)
                trailAge += interval;

            trailStrength = UnityEngine.Mathf.Clamp01(trailStrength - interval * 0.05f);
            freshTrail = trailStrength > 0f && trailAge < 30f;
        }
    }
}
