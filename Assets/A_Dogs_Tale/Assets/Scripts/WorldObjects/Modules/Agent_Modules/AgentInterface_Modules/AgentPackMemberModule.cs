using UnityEngine;

namespace DogGame.AI
{
    public class AgentPackMemberModule : WorldModule
    {
        public Pack currentPack;
        public bool isLeaderOverride; // For debugging / forced leader.

        private AgentModule agent;

        public bool IsLeader
        {
            get
            {
                if (isLeaderOverride) return true;
                return currentPack != null && currentPack.leader == agent;
            }
        }

        protected override void Awake()
        {
            agent = GetComponent<AgentModule>();
        }

        public override void Tick(float deltaTime)
        {
            Debug.Log($"AgentPackMemberModule {worldObject.DisplayName}: Tick {deltaTime}");
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

        public void RequestBecomeControlledAgent(int agentIndex)
        {
            
        }
        public void CycleFormation()
        {
            
        }
    }
}