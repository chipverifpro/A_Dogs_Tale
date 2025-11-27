using UnityEngine;

namespace DogGame.AI
{
    public class AgentPackMemberModule : MonoBehaviour
    {
        public Pack currentPack;
        public bool isLeaderOverride; // For debugging / forced leader.

        private AgentController agent;

        public bool IsLeader
        {
            get
            {
                if (isLeaderOverride) return true;
                return currentPack != null && currentPack.leader == agent;
            }
        }

        private void Awake()
        {
            agent = GetComponent<AgentController>();
        }

        public void JoinPack(Pack packToJoin, bool setAsLeader = false)
        {
            if (packToJoin == null) return;

            if (currentPack != null && currentPack != packToJoin)
            {
                LeaveCurrentPack();
            }

            currentPack = packToJoin;
            currentPack.AddMember(agent, setAsLeader);
        }

        public void LeaveCurrentPack()
        {
            if (currentPack == null) return;

            currentPack.RemoveMember(agent);
            currentPack = null;
        }
    }
}