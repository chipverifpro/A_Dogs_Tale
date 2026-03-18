using System.Collections.Generic;

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
        public int size;
        public bool isLeader;
        public PackMemberState leader = new();
        public List<PackMemberState> members = new();

        public float leaderDistance;
        public bool formationBroken;
        public bool memberInTrouble;
        public bool memberBarking;

        public bool isSeparated;
        public bool memberMissing;
        public bool regroupRequested;
    }
}
