using UnityEngine;

namespace DogGame.AI
{
    public class WandererDecisionModule : AgentDecisionModuleBase
    {
        private Vector3 currentWanderTarget;
        private float timeUntilNewTarget;

        public float wanderRadius = 4f;
        public float minTimeBetweenTargets = 1.5f;
        public float maxTimeBetweenTargets = 4f;

        public override void Initialize(AgentModule agentController)
        {
            base.Initialize(agentController);
            PickNewTarget();
        }

        public override void Tick(float deltaTime)
        {
            timeUntilNewTarget -= deltaTime;

            if (timeUntilNewTarget <= 0f ||
                (worldObject.agentModule.transform.position - currentWanderTarget).sqrMagnitude < 0.25f)
            {
                PickNewTarget();
            }

            agentMovementModule.MoveTowards(currentWanderTarget, deltaTime);
        }

        private void PickNewTarget()
        {
            Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
            Vector3 origin = worldObject.transform.position;

            currentWanderTarget = new Vector3(
                origin.x + randomCircle.x,
                origin.y,
                origin.z + randomCircle.y
            );

            timeUntilNewTarget = Random.Range(minTimeBetweenTargets, maxTimeBetweenTargets);

            // TODO: optionally clamp to navigable area / heightfield.
        }
    }
}