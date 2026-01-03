#nullable enable
using UnityEngine;

namespace DogGame.LLM
{
    public sealed class WorldObjectMotionBridge : IMotionModuleBridge
    {
        private readonly WorldObject worldObject; // <-- adjust type/namespace if needed
        private float lastDeltaTime;

        public WorldObjectMotionBridge(WorldObject worldObject)
        {
            this.worldObject = worldObject;
        }

        public void SetDeltaTime(float deltaTime)
        {
            lastDeltaTime = deltaTime;
        }

        public void Move(Vector3 desiredVelocity)
        {
            if (true)
                Debug.Log($"[{worldObject.DisplayName}] WorldObjectMotionBridge.Move calls SetDesiredVelocity({desiredVelocity})");
            
            worldObject.agentMovementModule.SetDesiredVelocity(desiredVelocity);
        }
    }
}