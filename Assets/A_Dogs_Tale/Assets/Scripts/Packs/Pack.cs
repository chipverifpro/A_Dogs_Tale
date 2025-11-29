using System.Collections.Generic;
using UnityEngine;

namespace DogGame.AI
{
    [System.Serializable]
    public class Pack
    {
        public string packName = "Unnamed Pack";

        public AgentModule leader;
        public PackTacticsProfile tacticsProfile;

        public List<AgentModule> members = new List<AgentModule>();

        public Pack(AgentModule leader, PackTacticsProfile tacticsProfile)
        {
            this.leader = leader;
            this.tacticsProfile = tacticsProfile;
            if (leader != null && !members.Contains(leader))
            {
                members.Add(leader);
            }
        }

        public void AddMember(AgentModule agent, bool setAsLeader = false)
        {
            if (agent == null) return;

            if (!members.Contains(agent))
                members.Add(agent);

            if (setAsLeader)
            {
                leader = agent;
            }
        }

        public void RemoveMember(AgentModule agent)
        {
            if (agent == null) return;

            if (members.Contains(agent))
                members.Remove(agent);

            if (leader == agent)
            {
                leader = members.Count > 0 ? members[0] : null;
            }
        }

        public bool Contains(AgentModule agent)
        {
            return members.Contains(agent);
        }
    }
}