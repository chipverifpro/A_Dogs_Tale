#nullable enable
using UnityEngine;
using DogGame.LLM;
using DogGame.Modules;

namespace DogGame.Tasks
{
    /// <summary>
    /// Adapter used by tasks. It does NOT integrate movement.
    /// It only writes movement intent into AgentMovementModule.
    /// 
    /// </summary>
    public sealed class MotionAdapter : IAgentMovementAdapter
    {
        private readonly WorldObject worldObject;

        public MotionAdapter(WorldObject worldObject)
        {
            this.worldObject = worldObject;
        }

        public Vector3 CellToWorld(int cellX, int cellY)
        {
            return new Vector3(cellX+0.5f, worldObject.locationModule.height, cellY+0.5f);
        }

        public bool SetMoveTarget(Vector3 worldPosition)
        {
            worldObject.agentMovementModule.SetDesiredTargetLocation(
                worldPosition,
                mode: WalkMode.None,
                requestPathfinding: true);
            return true;
        }

        public void StopMoving()
        {
            // Intent only: clear movement desire
            worldObject.agentMovementModule.ClearDesiredMovement();
        }

        public bool IsAt(Vector3 worldPosition)
        {
            Vector3 delta = worldPosition - worldObject.locationModule.pos3d_world;
            delta.y = 0f;
            float stopRadius = worldObject.agentMovementModule != null
                ? worldObject.agentMovementModule.StopDistance
                : 0.20f;
            return delta.sqrMagnitude <= stopRadius * stopRadius;
        }
    }
}
