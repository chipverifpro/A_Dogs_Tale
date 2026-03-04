#nullable enable
using System;
using UnityEngine;

namespace DogGame.LLM
{
    /// <summary>
    /// Pluggable movement interface. Implement this using your existing motion/nav system.
    /// </summary>
    public interface IAgentMovementAdapter
    {
        /// <summary>
        /// Convert a grid cell into a world-space destination.
        /// </summary>
        Vector3 CellToWorld(int cellX, int cellY);

        /// <summary>
        /// Begin or update movement toward a destination. Called every tick by tasks.
        /// Return true if movement is possible this tick.
        /// </summary>
        bool SetMoveTarget(Vector3 worldPosition);

        /// <summary>
        /// Stop agent movement (optional; can be no-op).
        /// </summary>
        void StopMoving();

        /// <summary>
        /// Returns whether the agent is considered "at" the destination.
        /// </summary>
        bool IsAt(Vector3 worldPosition, float stopRadius);
    }
}
