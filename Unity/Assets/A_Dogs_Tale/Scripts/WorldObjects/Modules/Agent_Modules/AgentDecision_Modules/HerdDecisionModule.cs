using UnityEngine;
using InspectorTools;

namespace DogGame.Modules
{
    [InspectorNote("AgentDecision_Modules/Herd Decision Module", "Boids-based flocking for sheep herds.")]
    [DisallowMultipleComponent]
    public class HerdDecisionModule : AgentDecisionModuleBase
    {
        public override AgentDecisionType DecisionType => AgentDecisionType.Herd;

        [Header("Herd Steering")]
        [SerializeField, Min(0.02f)] private float steeringRecomputeIntervalSeconds = 0.10f;
        [SerializeField, Min(0.05f)] private float maxMoveDistancePerTick = 1.0f;
        [SerializeField] private bool enableDebugLogging = false;
        [SerializeField, Min(1)] private int debugLogEveryFrames = 60;

        private float steeringCooldownSeconds;
        private HerdSteeringResult cachedSteering;
        private bool hasCachedSteering;
        private bool hasRequestedHerdActivation;

        public override void Tick(float deltaTime)
        {
            if (worldObject.agentMovementModule == null)
            {
                LogDebug(null, "Tick failed: no AgentMovementModule.", force: true);
                return;
            }

            Herd herd = worldObject.packMemberModule != null
                ? worldObject.packMemberModule.currentPack as Herd
                : null;

            if (herd == null)
            {
                worldObject.agentMovementModule.ClearDesiredMovement();
                LogDebug(null, "Tick failed: currentPack is not a Herd.", force: false);
                return;
            }

            if (!hasRequestedHerdActivation)
            {
                herd.ReassertHerdDecisionModules($"Tick activation requested by {worldObject.DisplayName}", forceLog: enableDebugLogging);
                hasRequestedHerdActivation = true;
            }

            steeringCooldownSeconds -= deltaTime;
            if (steeringCooldownSeconds <= 0f)
            {
                hasCachedSteering = herd.TryComputeSteering(worldObject, out cachedSteering);
                steeringCooldownSeconds = steeringRecomputeIntervalSeconds;
            }

            if (!hasCachedSteering)
            {
                worldObject.agentMovementModule.ClearDesiredMove();
                LogDebug(herd, "No steering result; clearing desired move.", force: false);
                return;
            }

            if (cachedSteering.usePathTarget)
            {
                worldObject.agentMovementModule.SetDesiredTargetLocationMap(
                    cachedSteering.targetMap,
                    cachedSteering.walkMode,
                    requestPathfinding: true,
                    speedFactor: cachedSteering.speedFactor);
                if (ShouldEmitDebugLog(herd))
                {
                    LogDebug(
                        herd,
                        $"Applied gather path target={cachedSteering.targetMap} speed={cachedSteering.speedFactor:0.00} " +
                        $"moveInProgress={worldObject.agentMovementModule.MoveToDestinationInProgress}",
                        force: false);
                }
            }
            else
            {
                worldObject.agentMovementModule.ClearDesiredTarget();
                worldObject.agentMovementModule.SetDesiredMove(
                    cachedSteering.directionMap,
                    maxDistance: maxMoveDistancePerTick,
                    speedFactor: cachedSteering.speedFactor,
                    changeWalkMode: cachedSteering.walkMode);
                if (ShouldEmitDebugLog(herd))
                {
                    LogDebug(
                        herd,
                        $"Applied boids move dir={cachedSteering.directionMap} speed={cachedSteering.speedFactor:0.00} " +
                        $"danger={cachedSteering.dangerNearby}",
                        force: false);
                }
            }

        }

        public override void BeginDecisionModule(bool resume = false)
        {
            steeringCooldownSeconds = 0f;
            hasCachedSteering = false;
            hasRequestedHerdActivation = false;
            UseAutonomousFaceMovement();
            Herd herd = GetCurrentHerd();
            LogDebug(herd, $"BeginDecisionModule resume={resume}", force: true);
        }

        public override void EndDecisionModule()
        {
            hasCachedSteering = false;
            StopMovementIntent();
            LogDebug(GetCurrentHerd(), "EndDecisionModule", force: true);
        }

        private Herd GetCurrentHerd()
        {
            return worldObject != null && worldObject.packMemberModule != null
                ? worldObject.packMemberModule.currentPack as Herd
                : null;
        }

        private void LogDebug(Herd herd, string message, bool force)
        {
            bool shouldLog = force
                ? enableDebugLogging || (herd != null && herd.DebugLoggingEnabled)
                : ShouldEmitDebugLog(herd);

            if (!shouldLog)
                return;

            Debug.Log($"[HerdDecisionModule {worldObject.DisplayName}] {message}", this);
        }

        private bool ShouldEmitDebugLog(Herd herd)
        {
            if (enableDebugLogging && Time.frameCount % Mathf.Max(1, debugLogEveryFrames) == 0)
                return true;

            return herd != null && herd.ShouldEmitDebugLogThisFrame();
        }
    }
}
