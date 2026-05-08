using UnityEngine;
using InspectorTools;

namespace DogGame.Modules
{
    [InspectorNote("AgentDecision_Modules/Follower Decision Module", "Follows another Agent.")]
    [DisallowMultipleComponent]
    public class FollowerDecisionModule : AgentDecisionModuleBase
    {
        public override AgentDecisionType DecisionType => AgentDecisionType.Follower;

        [Header("Dependencies")]
        //[SerializeField] private AgentMovementModule agentMovementModule;

        [Header("Follow Target")]
        [Tooltip("Current target to follow. Can be pack leader, laser point, etc.")]
        [SerializeField] private WorldObject followTarget;

        [Tooltip("Desired following distance in meters.")]
        [SerializeField] private float followDistanceMeters = 0.5f;

        [Tooltip("How close a follower must be to a breadcrumb before moving to the next one.")]
        [SerializeField] private float breadcrumbArrivalDistanceMeters = 0.35f;

        [Tooltip("Speed multiplier used while following pack breadcrumbs with a formation offset.")]
        [SerializeField, Min(0f)] private float formationFollowSpeedMultiplier = 1.2f;

        [Tooltip("If true, will automatically follow pack leader at startup when in a pack.")]
        [SerializeField] private bool autoFollowPackLeaderOnStart = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = false;

        // Initialize called from WorldObject.Awake phase
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
        public void SetFollowTarget(WorldObject newTarget, float desiredDistanceMeters)
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
                worldObject.packMemberModule == null)
            {
                return;
            }

            var packMember = worldObject.packMemberModule;
            var currentPack = packMember.currentPack;

            if (currentPack == null || currentPack.packLeader == null)
                return;

            followTarget = currentPack.packLeader;

            if (enableDebugLogging)
            {
                Debug.Log(
                    $"[FollowerDecisionModule {worldObject.DisplayName}] " +
                    $"Default follow target set to pack leader {currentPack.packLeader.name}.",
                    this);
            }
        }

        #region Tick

        private int debugDoubleTick = -1;
        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            if (enableDebugLogging)
            {
                //Debug.Log(
                //   $"[FollowerDecisionModule {worldObject.DisplayName}] Tick {deltaTime}",
                //   this);
            }

            // If we lost our follow target (destroyed, disabled, etc.), try to fall back to pack leader
            if (followTarget == null && autoFollowPackLeaderOnStart)
            {
                TrySetDefaultPackLeaderFollowTarget();
            }

            if (worldObject.agentMovementModule == null)
            {
                Debug.LogWarning($"[FollowerDecisionModule {worldObject.DisplayName}] No AgentMovementModule found.", this);
                return;
            }

            if (followTarget == null)
            {
                // No target to follow; decelerate to a stop
                worldObject.agentMovementModule.ClearDesiredMove();
                return;
            }

            Vector3 targetPos = default;
            bool formationFollowActive = false;
            bool followingPackLeader = TryGetCurrentPackLeaderFollow(out Pack currentPack);
            if (followingPackLeader && !TryGetNextBreadcrumbMapPosition(currentPack, out targetPos, out formationFollowActive))
            {
                worldObject.agentMovementModule.ClearDesiredMove();
                return;
            }

            if (!followingPackLeader)
            {
                targetPos = followTarget.pos3d_map;
            }

            // Compute direction to current breadcrumb or non-pack follow target.
            Vector3 currentPos = worldObject.pos3d_map;

            Vector3 toTarget = targetPos - currentPos;
            toTarget.y = 0f;

            float sqrDistanceToTarget = toTarget.sqrMagnitude;
            
            float desiredDistance = followingPackLeader ? breadcrumbArrivalDistanceMeters : followDistanceMeters;
            float sqrDesiredDistance = desiredDistance * desiredDistance;

            if (enableDebugLogging && Time.frameCount % 30 == 0)
            {
                string targetLabel = followingPackLeader ? "breadcrumb" : followTarget.name;
                Debug.Log(
                    $"[FollowerDecisionModule {worldObject.DisplayName}] " +
                    $"Following {targetLabel}, dist={Mathf.Sqrt(sqrDistanceToTarget):F2},desired={desiredDistance}",
                    this);
            }
            if (sqrDistanceToTarget > sqrDesiredDistance)
            {
                // Too far: move toward the follow target
                Vector3 worldDirection = toTarget.normalized;

                float speedFactor = formationFollowActive ? formationFollowSpeedMultiplier : 1.0f;

                // only need to do the square root if we are close (<1)
                float magDistanceToTarget = (sqrDistanceToTarget>1f) ? 1f : Mathf.Sqrt(sqrDistanceToTarget);

                worldObject.agentMovementModule.SetDesiredMove(worldDirection01: worldDirection, 
                                                               maxDistance: magDistanceToTarget,
                                                               speedFactor: speedFactor,
                                                               changeWalkMode: WalkMode.None);
                
                if (enableDebugLogging && Time.frameCount % 30 == 0)
                {
                    string targetLabel = followingPackLeader ? "breadcrumb" : followTarget.name;
                    Debug.Log(
                        $"[FollowerDecisionModule {worldObject.DisplayName}] " +
                        $"Following {targetLabel}, dist={Mathf.Sqrt(sqrDistanceToTarget):F2}",
                        this);
                }
            }
            else
            {
                // Close enough: slow to a stop
                worldObject.agentMovementModule.ClearDesiredMove();
            }
        }

        #endregion

        private bool TryGetCurrentPackLeaderFollow(out Pack currentPack)
        {
            currentPack = worldObject.packMemberModule != null
                ? worldObject.packMemberModule.currentPack
                : null;

            return currentPack != null &&
                   currentPack.packLeader != null &&
                   followTarget == currentPack.packLeader;
        }

        private bool TryGetNextBreadcrumbMapPosition(Pack currentPack, out Vector3 targetMapPosition, out bool usedFormation)
        {
            targetMapPosition = default;
            usedFormation = false;

            BreadcrumbTrail trail = currentPack != null ? currentPack.trail : null;
            if (trail == null)
                return false;

            bool useFormation = TryGetFormationContext(
                currentPack,
                out PackFormations formations,
                out int positionInPack,
                out int numberInPack);

            trail.RecordIfNeeded();

            if (useFormation)
                MarkCurrentFormationCrumbArrivedIfNeeded(trail);

            Crumb crumb = trail.GetNextCrumb(
                worldObject,
                breadcrumbArrivalDistanceMeters,
                markArrivals: !useFormation);

            if (crumb == null || !crumb.valid)
            {
                if (worldObject.agentMovementModule != null && worldObject.agentMovementModule.next_formationCrumb != null)
                    worldObject.agentMovementModule.next_formationCrumb.valid = false;
                return false;
            }

            if (useFormation)
            {
                targetMapPosition = BuildFormationTargetMapPosition(
                    crumb,
                    formations,
                    currentPack.formation,
                    positionInPack,
                    numberInPack,
                    currentPack.formationSpacing);
                usedFormation = true;
                return true;
            }

            targetMapPosition = new Vector3(crumb.pos2.x, crumb.height, crumb.pos2.y);
            return true;
        }

        private bool TryGetFormationContext(
            Pack currentPack,
            out PackFormations formations,
            out int positionInPack,
            out int numberInPack)
        {
            formations = null;
            positionInPack = -1;
            numberInPack = 0;

            if (currentPack == null || currentPack.packAgentList == null)
                return false;

            positionInPack = currentPack.GetPositionInPack(worldObject);
            if (positionInPack <= 0)
                return false;

            numberInPack = currentPack.packAgentList.Count;
            if (positionInPack >= numberInPack)
                return false;

            Dir dir = Dir.Instance;
            formations = dir != null ? dir.packFormations : null;
            return formations != null;
        }

        private void MarkCurrentFormationCrumbArrivedIfNeeded(BreadcrumbTrail trail)
        {
            if (trail == null || worldObject.agentMovementModule == null)
                return;

            Crumb formationCrumb = worldObject.agentMovementModule.next_formationCrumb;
            if (formationCrumb == null || !formationCrumb.valid)
                return;

            Vector2 currentPos = worldObject.locationModule != null
                ? worldObject.locationModule.pos2_f
                : new Vector2(worldObject.pos3d_map.x, worldObject.pos3d_map.z);

            if ((currentPos - formationCrumb.pos2).sqrMagnitude <= breadcrumbArrivalDistanceMeters * breadcrumbArrivalDistanceMeters)
                trail.MarkCurrentCrumbArrived(worldObject);
        }

        private Vector3 BuildFormationTargetMapPosition(
            Crumb crumb,
            PackFormations formations,
            FormationsEnum formation,
            int positionInPack,
            int numberInPack,
            float formationSpacing)
        {
            Vector2 offset = formations.GetOffsetForFormation(formation, positionInPack, numberInPack);
            Vector2 rotatedOffset = formations.RotateAndScaleOffset(offset, crumb.yawDeg, formationSpacing);
            Vector2 targetPos2 = crumb.pos2 + rotatedOffset;

            Crumb formationCrumb = worldObject.agentMovementModule.next_formationCrumb;
            if (formationCrumb == null)
            {
                formationCrumb = new Crumb();
                worldObject.agentMovementModule.next_formationCrumb = formationCrumb;
            }

            formationCrumb.pos2 = targetPos2;
            formationCrumb.height = crumb.height;
            formationCrumb.yawDeg = crumb.yawDeg;
            formationCrumb.valid = true;

            return new Vector3(targetPos2.x, crumb.height, targetPos2.y);
        }
        
        public override void BeginDecisionModule(bool resume=false)
        {
        }
        public override void EndDecisionModule()
        {
            StopMovementIntent();
        }
    }
}
