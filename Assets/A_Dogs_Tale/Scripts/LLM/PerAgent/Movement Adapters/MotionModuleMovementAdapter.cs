#nullable enable
using System.IO;
using DogGame.Modules;
using Mono.Cecil;
using UnityEngine;

namespace DogGame.LLM
{
    /// <summary>
    /// Movement adapter that drives motionModule.Move(desiredVelocity, deltaTime, 999f)
    /// by converting an active target position into a desired velocity each frame.
    /// </summary>
    public sealed class MotionModuleMovementAdapter : IAgentMovementAdapter
    {
        //private readonly Transform agentTransform;
        private readonly WorldObject worldObject;
        private readonly IMotionModuleBridge motionBridge;

        private readonly float maxMoveSpeed;
        private readonly float arriveSlowRadius;

        private Vector3? currentTargetWorld;

        public MotionModuleMovementAdapter(
            //Transform agentTransform,
            WorldObject worldObject,
            IMotionModuleBridge motionBridge,
            float maxMoveSpeed = 3.0f,
            float arriveSlowRadius = 1.25f)
        {
            //this.agentTransform = agentTransform;
            this.worldObject = worldObject;
            this.motionBridge = motionBridge;
            this.maxMoveSpeed = Mathf.Max(0.1f, maxMoveSpeed);
            this.arriveSlowRadius = Mathf.Max(0.1f, arriveSlowRadius);
        }

        public Vector3 CellToWorld(int cellX, int cellY)
        {
            return new Vector3(cellX, worldObject.locationModule.height, cellY);
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
            Vector3 delta = worldPosition - worldObject.locationModule.pos3d_world;
            delta.y = 0f;
            return delta.sqrMagnitude <= stopRadius * stopRadius;
        }

        private int debugDoubleTick = -1;   // detects if Tick is run more than once per frame

        /// <summary>
        /// Call every frame after tasks have potentially updated the target.
        /// Converts target -> desired velocity and forwards to motionBridge.Move().
        /// </summary>
        public void Tick(float deltaTimeSeconds)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            motionBridge.SetDeltaTime(deltaTimeSeconds);

            if (currentTargetWorld == null)
            {
                worldObject.agentMovementModule.SetDesiredVelocity(Vector3.zero);
                return;
            }

            Vector3 target = currentTargetWorld.Value;
            Vector3 toTarget = target - worldObject.locationModule.pos3d_world;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;
            if (distance < 0.0001f)
            {
                worldObject.agentMovementModule.SetDesiredVelocity(Vector3.zero);
                return;
            }

            Vector3 direction = toTarget / distance;

            float speedScale = 1f;
            if (distance < arriveSlowRadius)
                speedScale = Mathf.Clamp01(distance / arriveSlowRadius);

            Vector3 desiredVelocity = direction * (maxMoveSpeed * speedScale);

            worldObject.agentMovementModule.SetDesiredVelocity(desiredVelocity);
        }
    }
}