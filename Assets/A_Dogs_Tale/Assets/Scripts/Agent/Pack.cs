using System.Collections.Generic;
using UnityEngine;

namespace DogGame.AI
{
    [System.Serializable]
    public class Pack
    {
        public string packName = "Unnamed Pack";

        public AgentController leader;
        public PackTacticsProfile tacticsProfile;

        public List<AgentController> members = new List<AgentController>();

        public Pack(AgentController leader, PackTacticsProfile tacticsProfile)
        {
            this.leader = leader;
            this.tacticsProfile = tacticsProfile;
            if (leader != null && !members.Contains(leader))
            {
                members.Add(leader);
            }
        }

        public void AddMember(AgentController agent, bool setAsLeader = false)
        {
            if (agent == null) return;

            if (!members.Contains(agent))
                members.Add(agent);

            if (setAsLeader)
            {
                leader = agent;
            }
        }

        public void RemoveMember(AgentController agent)
        {
            if (agent == null) return;

            if (members.Contains(agent))
                members.Remove(agent);

            if (leader == agent)
            {
                leader = members.Count > 0 ? members[0] : null;
            }
        }

        public bool Contains(AgentController agent)
        {
            return members.Contains(agent);
        }
    }
}