#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    /// <summary>
    /// Adapter used by tasks. It does NOT integrate movement.
    /// It only writes movement intent into AgentMovementModule.
    /// </summary>
    public sealed class MotionModuleMovementAdapter : IAgentMovementAdapter
    {
        private readonly WorldObject worldObject;

        public MotionModuleMovementAdapter(WorldObject worldObject)
        {
            this.worldObject = worldObject;
        }

        public Vector3 CellToWorld(int cellX, int cellY)
        {
            return new Vector3(cellX, worldObject.locationModule.height, cellY);
        }

        public bool SetMoveTarget(Vector3 worldPosition)
        {
            // Intent only: tell the movement module "go there".
            // You mentioned you have SetDesiredTargetLocation(Vector3 targetLocation_world)
            worldObject.agentMovementModule.SetDesiredTargetLocation(worldPosition);
            return true;
        }

        public void StopMoving()
        {
            // Intent only: clear movement desire
            worldObject.agentMovementModule.ClearDesiredMovement();
        }

        public bool IsAt(Vector3 worldPosition, float stopRadius)
        {
            Vector3 delta = worldPosition - worldObject.locationModule.pos3d_world;
            delta.y = 0f;
            return delta.sqrMagnitude <= stopRadius * stopRadius;
        }
    }
}