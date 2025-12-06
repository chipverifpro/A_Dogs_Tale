using UnityEngine;

namespace DogGame.AI
{
    public class FollowerDecisionModule : AgentDecisionModuleBase
    {
        public override AgentDecisionType DecisionType => AgentDecisionType.Follower;

        [Header("Dependencies")]
        //[SerializeField] private AgentMovementModule agentMovementModule;

        [Header("Follow Target")]
        [Tooltip("Current target to follow. Can be pack leader, laser point, etc.")]
        [SerializeField] private Transform followTarget;

        [Tooltip("Desired following distance in meters.")]
        [SerializeField] private float followDistanceMeters = 1.5f;

        [Tooltip("If true, will automatically follow pack leader at startup when in a pack.")]
        [SerializeField] private bool autoFollowPackLeaderOnStart = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = false;

        public override void Initialize(AgentModule owner)
        {
            base.Initialize(owner);

//            if (worldObject.agentMovementModule == null)
//            {
//                worldObject.agentMovementModule = GetComponent<AgentMovementModule>();
//                if (worldObject.agentMovementModule == null)
//                {
//                    Debug.LogError(
//                        $"[FollowerDecisionModule {worldObject.DisplayName}] No AgentMovementModule found.",
//                        this);
//                    enabled = false;
//                    return;
//                }
//            }

            if (autoFollowPackLeaderOnStart && followTarget == null)
            {
                TrySetDefaultPackLeaderFollowTarget();
            }
        }

        /// <summary>
        /// Public API: set an explicit follow target and desired distance.
        /// This can be a pack leader, laser dot, waypoint, etc.
        /// </summary>
        public void SetFollowTarget(Transform newTarget, float desiredDistanceMeters)
        {
            followTarget = newTarget;
            if (desiredDistanceMeters > 0f)
            {
                followDistanceMeters = desiredDistanceMeters;
            }

            if (enableDebugLogging)
            {
                string targetName = followTarget != null ? followTarget.name : "null";
                Debug.Log(
                    $"[FollowerDecisionModule {worldObject.DisplayName}] " +
                    $"SetFollowTarget: {targetName}, distance={followDistanceMeters}",
                    this);
            }
        }

        /// <summary>
        /// Public API: clear the current follow target.
        /// The agent will decelerate to a stop.
        /// </summary>
        public void ClearFollowTarget()
        {
            followTarget = null;
            worldObject.agentMovementModule?.ClearDesiredMove();

            if (enableDebugLogging)
            {
                Debug.Log(
                    $"[FollowerDecisionModule {worldObject.DisplayName}] ClearFollowTarget.",
                    this);
            }
        }

        /// <summary>
        /// Public API: re-evaluate the pack and follow the pack leader if present.
        /// </summary>
        public void ResetToPackLeaderIfAvailable()
        {
            TrySetDefaultPackLeaderFollowTarget();
        }

        /// <summary>
        /// Internal helper: try to get the pack leader from the pack system and use that as follow target.
        /// </summary>
        private void TrySetDefaultPackLeaderFollowTarget()
        {
            if (worldObject == null ||
                worldObject.agentModule == null ||
                worldObject.agentModule.agentPackMemberModule == null)
            {
                return;
            }

            var packMember = worldObject.agentModule.agentPackMemberModule;
            var currentPack = packMember.currentPack;

            if (currentPack == null || currentPack.leader == null)
                return;

            followTarget = currentPack.leader.transform;

            if (enableDebugLogging)
            {
                Debug.Log(
                    $"[FollowerDecisionModule {worldObject.DisplayName}] " +
                    $"Default follow target set to pack leader {currentPack.leader.name}.",
                    this);
            }
        }

        public override void Tick(float deltaTime)
        {
            if (enableDebugLogging)
            {
                Debug.Log(
                    $"[FollowerDecisionModule {worldObject.DisplayName}] Tick {deltaTime}",
                    this);
            }

            // If we lost our follow target (destroyed, disabled, etc.), try to fall back to pack leader
            if (followTarget == null && autoFollowPackLeaderOnStart)
            {
                TrySetDefaultPackLeaderFollowTarget();
            }

            if (followTarget == null)
            {
                // No target to follow; decelerate to a stop
                worldObject.agentMovementModule.ClearDesiredMove();
                return;
            }

            // Compute direction to follow target
            Vector3 currentPos = worldObject.agentModule.transform.position;
            Vector3 targetPos = followTarget.position;

            Vector3 toTarget = targetPos - currentPos;
            toTarget.y = 0f;

            float sqrDistanceToTarget = toTarget.sqrMagnitude;
            float desiredDistance = followDistanceMeters;
            float sqrDesiredDistance = desiredDistance * desiredDistance;

            if (sqrDistanceToTarget > sqrDesiredDistance)
            {
                // Too far: move toward the follow target
                Vector3 worldDirection = toTarget.normalized;

                bool run = false;           // Followers walk by default; tweak if needed
                float speedFactor = 1.0f;   // Use full walk speed from AgentMovementModule

                worldObject.agentMovementModule.SetDesiredMove(worldDirection, speedFactor, run);

                if (enableDebugLogging && Time.frameCount % 30 == 0)
                {
                    Debug.Log(
                        $"[FollowerDecisionModule {worldObject.DisplayName}] " +
                        $"Following {followTarget.name}, dist={Mathf.Sqrt(sqrDistanceToTarget):F2}",
                        this);
                }
            }
            else
            {
                // Close enough: slow to a stop
                worldObject.agentMovementModule.ClearDesiredMove();
            }
        }
    }
}