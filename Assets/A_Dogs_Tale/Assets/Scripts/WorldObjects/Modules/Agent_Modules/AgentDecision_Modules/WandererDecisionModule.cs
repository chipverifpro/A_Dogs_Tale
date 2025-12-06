using System.ComponentModel;
using UnityEngine;

namespace DogGame.AI
{
    public class WandererDecisionModule : AgentDecisionModuleBase
    {
        public override AgentDecisionType DecisionType => AgentDecisionType.Wanderer;

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
        Debug.Log($"WanderDecisionModule {worldObject.DisplayName}: Tick {deltaTime}");

        if (worldObject.agentMovementModule == null)
        {
            Debug.LogWarning(
                $"[WanderDecisionModule {worldObject.DisplayName}] " +
                $"No AgentMovementModule found; cannot wander.",
                this);
            return;
        }

        // Countdown to pick a new wander target
        timeUntilNewTarget -= deltaTime;

        Vector3 currentPos = worldObject.agentModule.transform.position;

        // How close is "close enough" to the wander target?
        const float arriveDistance = 0.5f;
        float sqrDistToTarget = (currentWanderTarget - currentPos).sqrMagnitude;

        bool needNewTarget =
            timeUntilNewTarget <= 0f ||
            sqrDistToTarget < arriveDistance * arriveDistance;

        if (needNewTarget)
        {
            PickNewTarget();
            currentWanderTarget.y = currentPos.y; // keep on same height if needed

            // Reset timer, e.g. random interval; assuming PickNewTarget sets it or:
            // timeUntilNewTarget = Random.Range(minWanderInterval, maxWanderInterval);

            sqrDistToTarget = (currentWanderTarget - currentPos).sqrMagnitude;
        }

        // Now steer toward the current wander target
        Vector3 toTarget = currentWanderTarget - currentPos;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > arriveDistance * arriveDistance)
        {
            Vector3 worldDir = toTarget.normalized;

            // Wander is usually a walk, not a run
            bool run = false;
            float speedFactor = 1.0f; // full walkSpeed from AgentMovementModule

            worldObject.agentMovementModule.SetDesiredMove(worldDir, speedFactor, run);
        }
        else
        {
            // We are close enough: slow to a stop
            worldObject.agentMovementModule.ClearDesiredMove();
        }
    }

 //   [SerializeField] private LocationModule locationModule;
 //   [SerializeField] private float wanderRadius = 5f;
 //   [SerializeField] private float minTimeBetweenTargets = 1.5f;
 //   [SerializeField] private float maxTimeBetweenTargets = 4.0f;

    private void PickNewTarget()
    {
        // 1. Determine origin of wandering
        // Prefer LocationModule if present, otherwise use the agent's transform.
        Vector3 origin;

        if (worldObject.locationModule != null)
        {
            // For now assume LocationModule exposes current world position:
            // origin = locationModule.CurrentWorldPosition;
            origin = worldObject.locationModule.transform.position;  // adjust when LocationModule is fleshed out
        }
        else if (worldObject != null && worldObject.agentModule != null)
        {
            origin = worldObject.agentModule.transform.position;
        }
        else
        {
            origin = transform.position;
        }

        // 2. Choose a random point in a circle around origin (XZ plane)
        Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;

        Vector3 candidate = new Vector3(
            origin.x + randomCircle.x,
            origin.y,
            origin.z + randomCircle.y
        );

        // 3. Optionally clamp to navigable / walkable area via LocationModule
        if (worldObject.locationModule != null)
        {
            // When you implement LocationModule, you might have:
            // candidate = locationModule.ClampToWalkable(candidate);
            // candidate.y = locationModule.GetGroundHeightAt(candidate);
        }

        currentWanderTarget = candidate;

        // 4. Pick a new time window before we wander somewhere else
        timeUntilNewTarget = Random.Range(minTimeBetweenTargets, maxTimeBetweenTargets);
    }
    }
}