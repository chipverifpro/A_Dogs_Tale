#nullable enable
using UnityEngine;

namespace DogGame.LLM
{
    public sealed class SimpleMovementAdapter : IAgentMovementAdapter
    {
        private readonly Transform agentTransform;
        private readonly float moveSpeed;
        private Vector3? currentTarget;

        // Grid mapping (replace with your real mapping later)
        private readonly float cellSize;
        private readonly Vector3 gridOrigin;

        public SimpleMovementAdapter(Transform agentTransform, float moveSpeed = 2.0f, float cellSize = 1.0f, Vector3? gridOrigin = null)
        {
            this.agentTransform = agentTransform;
            this.moveSpeed = moveSpeed;
            this.cellSize = cellSize;
            this.gridOrigin = gridOrigin ?? Vector3.zero;
        }

        public Vector3 CellToWorld(int cellX, int cellY)
        {
            return gridOrigin + new Vector3(cellX * cellSize, 0f, cellY * cellSize);
        }

        public bool SetMoveTarget(Vector3 worldPosition)
        {
            currentTarget = worldPosition;

            // Simple “direct move” each call (you’ll likely call this each tick).
            agentTransform.position = Vector3.MoveTowards(
                agentTransform.position,
                worldPosition,
                moveSpeed * Time.deltaTime);

            return true;
        }

        public void StopMoving()
        {
            currentTarget = null;
        }

        public bool IsAt(Vector3 worldPosition, float stopRadius)
        {
            return Vector3.Distance(agentTransform.position, worldPosition) <= stopRadius;
        }
    }
}