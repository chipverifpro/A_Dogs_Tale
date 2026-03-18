using DogGame.Modules;
using UnityEngine;

namespace DogGame.Lua
{
    public class TimeState
    {
        public float time       = 0.0f;
        public float delta      = 0.0f;
        public float timeOfDay  = 12.0f;

        public WorldObject worldObject;
        public AgentState  state;

        public void InitState(WorldObject worldObject, AgentState state)
        {
            this.worldObject = worldObject;
            this.state = state;
        }

        public void UpdateState(Detail detail)
        {
            // Placeholder for game-clock integration.
        }

        public void Tick(float interval)
        {
            delta = interval;
            time = UnityEngine.Time.time;
            timeOfDay = Mathf.Repeat(time / 3600f, 24f);
        }
    }
}
