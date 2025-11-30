using UnityEngine;

namespace DogGame.AI
{
    public class FollowerDecisionModule : AgentDecisionModuleBase
    {
        public float followDistance = 2.5f;
        public float rejoinDistance = 7f;

        public override void Tick(float deltaTime)
        {
            if (worldObject.agentModule.agentPackMemberModule == null || worldObject.agentModule.agentPackMemberModule.currentPack == null || worldObject.agentModule.agentPackMemberModule.currentPack.leader == null)
            {
                // No pack or no leader: fallback to wandering for now.
                // TODO: later, we might idle instead of wander.
                worldObject.agentModule.agentMovementModule.Stop();
                return;
            }

            Transform leaderTransform = worldObject.agentModule.agentPackMemberModule.currentPack.leader.transform;
            Vector3 toLeader = leaderTransform.position - worldObject.transform.position;
            toLeader.y = 0f;

            float distanceToLeader = toLeader.magnitude;

            if (distanceToLeader > rejoinDistance)
            {
                // We are far behind: hurry up.
                Vector3 targetPosition = leaderTransform.position;
                worldObject.agentModule.agentMovementModule.MoveTowards(targetPosition, deltaTime);
            }
            else if (distanceToLeader > followDistance)
            {
                // Adjust gap a bit.
                Vector3 targetPosition = leaderTransform.position - toLeader.normalized * followDistance;
                worldObject.agentModule.agentMovementModule.MoveTowards(targetPosition, deltaTime);
            }
            else
            {
                // Close enough: maybe look at what leader is looking at.
                worldObject.agentModule.agentMovementModule.Stop();
                // TODO: orientation / idle animation / sniff behaviors.
            }

            // TODO: use PackTacticsProfile to decide flanking positions / roles.
        }
    }
}