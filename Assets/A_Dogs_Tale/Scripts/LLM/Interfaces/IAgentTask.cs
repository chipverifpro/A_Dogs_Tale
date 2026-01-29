#nullable enable
using DogGame.Tasks;
using UnityEngine;

namespace DogGame.LLM
{
//1) IAgentTask — decouples “what to do” from “how it’s executed”
//
//Who uses it
//	•	Implemented by: Task_MoveToCell, Task_Wait, future tasks like
//      Task_Bark, Task_Sniff, Task_Branch, etc.
//	•	Consumed by: TaskExecutor (it runs Start/Tick/Stop)
//	•	Stored by: TaskQueue
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
        void Start(TaskContext context);

        /// <summary>Called every AI tick until succeeded/failed.</summary>
        TaskTickResult Tick(TaskContext context, float deltaTimeSeconds);

        /// <summary>Called once when the task is ended (success or fail).</summary>
        void Stop(TaskContext context);
    }

    /// <summary>
    /// A small context object you can expand later (agent references, world, nav, etc.)
    /// TODO: look into how this is used.  Can everything be found inside Agent?
    /// AgentID = Agent.AgentId,
    /// AgentTransform: Agent., Movement: maybe, Blackboard: yes, Current
    /// </summary>
    public sealed class TaskContext
    {
        public readonly WorldObject Agent;
        public string? OriginRequestId;   // set when task came from LLM plan
        public string? OriginTag;

        // everything else comes directly from Agent (which is a WorldObject)
        public string AgentId => Agent.DisplayName;
        public Transform AgentTransform => Agent.transform;
        public MotionAdapter Motion => Agent.motionAdapter;
        public IBlackboard Blackboard => Agent.blackboardModule.Blackboard;
        public Vector2Int CurrentCellPos => Agent.locationModule.cell.pos;
        
        public TaskContext(WorldObject worldObject, string? OriginRequestId, string? OriginTag)
        {
            Agent = worldObject;
            this.OriginRequestId = OriginRequestId;
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