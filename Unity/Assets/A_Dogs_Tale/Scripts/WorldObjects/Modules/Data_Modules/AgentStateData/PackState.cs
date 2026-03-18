using System.Collections.Generic;
using DogGame.Modules;

namespace DogGame.Lua
{
    public class PackMemberState
    {
        public int id = -1;
        public string name = "";
        public float distance;
        public bool inTrouble;
        public bool barking;
    }

    public class PackState
    {
        public int size                       = 0;
        public bool isLeader                 = false;
        public PackMemberState leader        = new();
        public List<PackMemberState> members = new();

        public float leaderDistance          = 0.0f;
        public bool formationBroken          = false;
        public bool memberInTrouble          = false;
        public bool memberBarking            = false;

        public bool isSeparated              = false;
        public bool memberMissing            = false;
        public bool regroupRequested         = false;

        public WorldObject worldObject;
        public AgentState  state;

        public void InitState(WorldObject worldObject, AgentState state)
        {
            this.worldObject = worldObject;
            this.state = state;
        }

        public void UpdateState(Detail detail)
        {
            // Placeholder for pack-member module integration.
        }

        public void Tick(float interval)
        {
            size = members.Count;
            leaderDistance = UnityEngine.Mathf.Max(0f, leaderDistance);
        }
    }
}
