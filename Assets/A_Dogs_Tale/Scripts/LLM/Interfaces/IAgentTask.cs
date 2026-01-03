#nullable enable
using UnityEngine;

namespace DogGame.LLM
{
//1) IAgentTask — decouples “what to do” from “how it’s executed”
//
//Who uses it
//	•	Implemented by: Task_MoveToCell, Task_Wait, future tasks like
//      Task_Bark, Task_Sniff, Task_Branch, etc.
//	•	Consumed by: AgentTaskExecutor (it runs Start/Tick/Stop)
//	•	Stored by: AgentTaskQueue
//
//  Why it’s necessary
//      It gives you a single contract so any producer (Player, Wander AI,
//      Reaction scripts, LLM plan mapper) can enqueue work without caring how it runs.

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
        public readonly WorldObject Agent;

        public readonly IAgentMovementAdapter Movement;

        public Vector2Int CurrentCellPos => new Vector2Int((int)AgentTransform.position.x,(int)AgentTransform.position.z);
        
        public AgentTaskContext(string agentId, WorldObject worldObject, Transform agentTransform, IAgentMovementAdapter movement)
        {
            AgentId = agentId;
            Agent = worldObject;
            AgentTransform = agentTransform;
            Movement = movement;
        }
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

    public enum TaskStatus
    {
        NotStarted,
        Running,
        Succeeded,
        Failed
    }

}