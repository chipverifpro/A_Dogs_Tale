using UnityEngine;

namespace DogGame.AI
{
    public class FollowerDecisionModule : AgentDecisionModuleBase
    {
        public float followDistance = 2.5f;
        public float rejoinDistance = 7f;

        public override void Tick(float deltaTime)
        {
            if (packMember == null || packMember.currentPack == null || packMember.currentPack.leader == null)
            {
                // No pack or no leader: fallback to wandering for now.
                // TODO: later, we might idle instead of wander.
                movement.Stop();
                return;
            }

            Transform leaderTransform = packMember.currentPack.leader.transform;
            Vector3 toLeader = leaderTransform.position - agent.transform.position;
            toLeader.y = 0f;

            float distanceToLeader = toLeader.magnitude;

            if (distanceToLeader > rejoinDistance)
            {
                // We are far behind: hurry up.
                Vector3 targetPosition = leaderTransform.position;
                movement.MoveTowards(targetPosition, deltaTime);
            }
            else if (distanceToLeader > followDistance)
            {
                // Adjust gap a bit.
                Vector3 targetPosition = leaderTransform.position - toLeader.normalized * followDistance;
                movement.MoveTowards(targetPosition, deltaTime);
            }
            else
            {
                // Close enough: maybe look at what leader is looking at.
                movement.Stop();
                // TODO: orientation / idle animation / sniff behaviors.
            }

            // TODO: use PackTacticsProfile to decide flanking positions / roles.
        }
    }
}