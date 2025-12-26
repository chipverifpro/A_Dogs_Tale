#nullable enable
using UnityEngine;

namespace DogGame.LLM
{
    /// <summary>
    /// Movement adapter that drives motionModule.Move(desiredVelocity, deltaTime, 999f)
    /// by converting an active target position into a desired velocity each frame.
    /// </summary>
    public sealed class MotionModuleMovementAdapter : IAgentMovementAdapter
    {
        private readonly Transform agentTransform;
        private readonly IMotionModuleBridge motionBridge;

        private readonly float maxMoveSpeed;
        private readonly float arriveSlowRadius;

        private Vector3? currentTargetWorld;

        public MotionModuleMovementAdapter(
            Transform agentTransform,
            IMotionModuleBridge motionBridge,
            float maxMoveSpeed = 3.0f,
            float arriveSlowRadius = 1.25f)
        {
            this.agentTransform = agentTransform;
            this.motionBridge = motionBridge;
            this.maxMoveSpeed = Mathf.Max(0.1f, maxMoveSpeed);
            this.arriveSlowRadius = Mathf.Max(0.1f, arriveSlowRadius);
        }

        public Vector3 CellToWorld(int cellX, int cellY)
        {
            return new Vector3(cellX, agentTransform.position.y, cellY);
        }

        public bool SetMoveTarget(Vector3 worldPosition)
        {
            currentTargetWorld = worldPosition;
            return true;
        }

        public void StopMoving()
        {
            currentTargetWorld = null;
            motionBridge.Move(Vector3.zero);
        }

        public bool IsAt(Vector3 worldPosition, float stopRadius)
        {
            Vector3 delta = worldPosition - agentTransform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= stopRadius * stopRadius;
        }

        /// <summary>
        /// Call every frame after tasks have potentially updated the target.
        /// Converts target -> desired velocity and forwards to motionBridge.Move().
        /// </summary>
        public void Tick(float deltaTimeSeconds)
        {
            motionBridge.SetDeltaTime(deltaTimeSeconds);

            if (currentTargetWorld == null)
            {
                motionBridge.Move(Vector3.zero);
                return;
            }

            Vector3 target = currentTargetWorld.Value;
            Vector3 toTarget = target - agentTransform.position;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;
            if (distance < 0.0001f)
            {
                motionBridge.Move(Vector3.zero);
                return;
            }

            Vector3 direction = toTarget / distance;

            float speedScale = 1f;
            if (distance < arriveSlowRadius)
                speedScale = Mathf.Clamp01(distance / arriveSlowRadius);

            Vector3 desiredVelocity = direction * (maxMoveSpeed * speedScale);

            motionBridge.Move(desiredVelocity);
        }
    }
}