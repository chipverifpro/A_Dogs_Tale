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

        private float steeringCooldownSeconds;
        private HerdSteeringResult cachedSteering;
        private bool hasCachedSteering;

        public override void Tick(float deltaTime)
        {
            if (worldObject.agentMovementModule == null)
            {
                Debug.LogWarning($"[HerdDecisionModule {worldObject.DisplayName}] No AgentMovementModule found.", this);
                return;
            }

            Herd herd = worldObject.packMemberModule != null
                ? worldObject.packMemberModule.currentPack as Herd
                : null;

            if (herd == null)
            {
                worldObject.agentMovementModule.ClearDesiredMovement();
                return;
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
                return;
            }

            worldObject.agentMovementModule.SetDesiredMove(
                cachedSteering.directionMap,
                maxDistance: maxMoveDistancePerTick,
                speedFactor: cachedSteering.speedFactor,
                changeWalkMode: cachedSteering.walkMode);

            if (enableDebugLogging && Time.frameCount % 30 == 0)
            {
                Debug.Log(
                    $"[HerdDecisionModule {worldObject.DisplayName}] " +
                    $"dir={cachedSteering.directionMap} speed={cachedSteering.speedFactor:0.00} danger={cachedSteering.dangerNearby}",
                    this);
            }
        }

        public override void BeginDecisionModule(bool resume = false)
        {
            steeringCooldownSeconds = 0f;
            hasCachedSteering = false;
            UseAutonomousFaceMovement();
        }

        public override void EndDecisionModule()
        {
            hasCachedSteering = false;
            StopMovementIntent();
        }
    }
}
