#nullable enable
using System;
using UnityEngine;

namespace DogGame.LLM
{
    public enum TaskStatus
    {
        NotStarted,
        Running,
        Succeeded,
        Failed
    }

    public readonly struct TaskTickResult
    {
        public readonly TaskStatus Status;
        public readonly string? FailureReason;

        private TaskTickResult(TaskStatus status, string? failureReason)
        {
            Status = status;
            FailureReason = failureReason;
        }

        public static TaskTickResult Running() => new(TaskStatus.Running, null);
        public static TaskTickResult Succeeded() => new(TaskStatus.Succeeded, null);
        public static TaskTickResult Failed(string reason) => new(TaskStatus.Failed, reason);
    }

    /// <summary>
    /// Runtime task interface. These are "workable actions".
    /// </summary>
    public interface IAgentTask
    {
        string DebugName { get; }

        /// <summary>Called once before Tick begins.</summary>
        void Start(AgentTaskContext context);

        /// <summary>Called every AI tick until succeeded/failed.</summary>
        TaskTickResult Tick(AgentTaskContext context, float deltaTimeSeconds);

        /// <summary>Called once when the task is ended (success or fail).</summary>
        void Stop(AgentTaskContext context);
    }

    /// <summary>
    /// A small context object you can expand later (agent references, world, nav, etc.)
    /// </summary>
    public sealed class AgentTaskContext
    {
        public readonly string AgentId;
        public readonly Transform AgentTransform;

        public readonly IAgentMovementAdapter Movement;

        public AgentTaskContext(string agentId, Transform agentTransform, IAgentMovementAdapter movement)
        {
            AgentId = agentId;
            AgentTransform = agentTransform;
            Movement = movement;
        }
    }

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