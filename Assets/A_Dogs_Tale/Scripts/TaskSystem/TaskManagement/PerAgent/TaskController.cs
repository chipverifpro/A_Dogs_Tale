#nullable enable
using UnityEngine;
using DogGame.Modules;
using DogGame.Tasks;
using System.Collections.Generic;

namespace DogGame.LLM
{
    [DefaultExecutionOrder(-10)]
    public sealed class TaskControler : WorldModule
    {
        [SerializeField] private string agentId = "player";
        public string AgentId => agentId;

        [Header("Queue behavior")]
        [SerializeField] private bool clearQueueOnNewPlan = true;

        public TaskQueue taskQueue { get; private set; } = null!;
        public TaskExecutor taskExecutor { get; private set; } = null!;
        public TaskContext taskContext { get; private set; } = null!;

        /// <summary>
        /// True when task control is active (either running a task or tasks are queued/suspended).
        /// </summary>
        public bool IsDriving => taskExecutor.HasTask || taskQueue.Count > 0 || taskExecutor.SuspendedCount > 0;
        public bool IsDrivingMovement => IsDriving;

        private MotionAdapter? motionAdapter;

        private DogGame.Tasks.IBlackboard blackboard = null!;

        public static TaskRequest Llm(IAgentTask task, int priority = 60, string? tag = "llm_plan")
            => new(task, priority, TaskSource.LLM, canInterrupt: false, tag: tag);

        public static TaskRequest Reaction(IAgentTask task, int priority = 80, bool resumePrevious = true, string? tag = "reaction")
            => new(task, priority, TaskSource.Reaction, canInterrupt: true, resumePrevious: resumePrevious, tag: tag);

        protected override void Awake()
        {
            if (worldObject == null)
            {
                Debug.LogError("[TaskControler] WorldObject not found on this GameObject.");
                enabled = false;
                return;
            }

            blackboard = new DogGame.Tasks.SimpleBlackboard();

            // Use DisplayName as agent id by default
            agentId = worldObject.DisplayName;

            taskQueue = new TaskQueue();
            taskExecutor  = new TaskExecutor(taskQueue);

        
            // Movement adapter used by tasks (intent-level; no per-frame Tick here)
            motionAdapter = new MotionAdapter(worldObject: worldObject);

            taskContext = new TaskContext(agentId, worldObject, transform, motionAdapter, blackboard);
        }

        public bool TryApplyPlanJson(string planResponseJson)
        {
            var (plan, validation) = PlanResponseV1Parser.ParseAndValidate(planResponseJson);
            if (plan == null)
            {
                Debug.LogWarning("PlanResponseV1 invalid:\n" + string.Join("\n", validation.Errors));
                return false;
            }

            if (plan.AgentId != agentId)
            {
                Debug.LogWarning($"Plan agent mismatch: plan.AgentId={plan.AgentId}, controller.AgentId={agentId}");
                return false;
            }

            if (clearQueueOnNewPlan)
            {
                // Cancel all execution state + queued tasks, not just the queue list.
                taskExecutor.ClearAll(taskContext);
            }

            if (!PlanIntentMapper.TryEnqueueTasksFromPlan(plan, taskQueue, out var error))
            {
                Debug.LogWarning("Plan mapped to zero tasks: " + error);
                return false;
            }

            return true;
        }

        private bool wasDrivingMovementLastTick;

        public void StopMovementWhenControlGained()
        {
            bool isDrivingMovementNow = IsDrivingMovement;

            // If we just took control, kill any leftover player intent immediately.
            if (isDrivingMovementNow && !wasDrivingMovementLastTick)
            {
                Debug.Log("TaskControler: Stop movement when task control gains control.");
                taskContext.Movement.StopMoving();
            }

            wasDrivingMovementLastTick = isDrivingMovementNow;
        }

        private int debugDoubleTick = -1;

        public override void Tick(float deltaTimeSeconds)
        {
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: TaskControler.Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            // Temporary dev behavior: any input cancels task control (also blocks Tab, etc.).
            if (dir && worldObject && dir.gameInputRouter.InputState.anyKeyOrButtonDown)
            {
                Debug.Log("TaskControler: Cancelling tasks due to anyKeyOrButtonDown");
                CancelAllTasks();
                return;
            }

            StopMovementWhenControlGained();

            taskExecutor.Tick(taskContext, deltaTimeSeconds);
        }

        public void CancelAllTasks()
        {
            taskExecutor.ClearAll(taskContext);
        }

        public void Submit(TaskRequest request)
        {
            if (!taskExecutor.TryInterruptWith(taskContext, request))
                taskQueue.Enqueue(request);
        }

        public TaskRequest SubmitSequence(
            int priority,
            TaskSource source,
            IEnumerable<IAgentTask> tasks,
            bool canInterrupt = true,
            bool resumePrevious = false,
            bool clearStackOnStart = false,
            string? tag = null)
        {
            var seq = new Task_Sequence(tasks is IAgentTask[] arr ? arr : new List<IAgentTask>(tasks).ToArray());

            return new TaskRequest(
                task: seq,
                priority: priority,
                source: source,
                canInterrupt: canInterrupt,
                resumePrevious: resumePrevious,
                clearStackOnStart: clearStackOnStart,
                tag: tag
            );
        }
    }
}