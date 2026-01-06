#nullable enable
using UnityEngine;
using DogGame.Modules;
using DogGame.Tasks;

namespace DogGame.LLM
{
    [DefaultExecutionOrder(-10)]
    public sealed class AgentTaskController : WorldModule
    {
        [SerializeField] private string agentId = "player";
        public string AgentId => agentId;

        [Header("Queue behavior")]
        [SerializeField] private bool clearQueueOnNewPlan = true;

        public AgentTaskQueue TaskQueue { get; private set; } = null!;
        public AgentTaskExecutor Executor { get; private set; } = null!;
        public AgentTaskContext Context { get; private set; } = null!;

        /// <summary>
        /// True when task control is active (either running a task or tasks are queued/suspended).
        /// </summary>
        public bool IsDriving => Executor.HasTask || TaskQueue.Count > 0 || Executor.SuspendedCount > 0;
        public bool IsDrivingMovement => IsDriving;

        private MotionModuleMovementAdapter? motionAdapter;

        private DogGame.Tasks.IBlackboard blackboard = null!;

        public static TaskRequest Llm(IAgentTask task, int priority = 60, string? tag = "llm_plan")
            => new(task, priority, TaskSource.LLM, canInterrupt: false, tag: tag);

        public static TaskRequest Reaction(IAgentTask task, int priority = 80, bool resumePrevious = true, string? tag = "reaction")
            => new(task, priority, TaskSource.Reaction, canInterrupt: true, resumePrevious: resumePrevious, tag: tag);

        protected override void Awake()
        {
            if (worldObject == null)
            {
                Debug.LogError("[AgentTaskController] WorldObject not found on this GameObject.");
                enabled = false;
                return;
            }

            blackboard = new DogGame.Tasks.SimpleBlackboard();

            // Use DisplayName as agent id by default
            agentId = worldObject.DisplayName;

            TaskQueue = new AgentTaskQueue();
            Executor  = new AgentTaskExecutor(TaskQueue);

        
            // Movement adapter used by tasks (intent-level; no per-frame Tick here)
            motionAdapter = new MotionModuleMovementAdapter(worldObject: worldObject);

            Context = new AgentTaskContext(agentId, worldObject, transform, motionAdapter, blackboard);
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
                Executor.ClearAll(Context);
            }

            if (!PlanIntentMapper.TryEnqueueTasksFromPlan(plan, TaskQueue, out var error))
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
                Debug.Log("AgentTaskController: Stop movement when task control gains control.");
                Context.Movement.StopMoving();
            }

            wasDrivingMovementLastTick = isDrivingMovementNow;
        }

        private int debugDoubleTick = -1;

        public override void Tick(float deltaTimeSeconds)
        {
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: AgentTaskController.Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            // Temporary dev behavior: any input cancels task control (also blocks Tab, etc.).
            if (dir && worldObject && dir.gameInputRouter.InputState.anyKeyOrButtonDown)
            {
                Debug.Log("AgentTaskController: Cancelling tasks due to anyKeyOrButtonDown");
                CancelAllTasks();
                return;
            }

            StopMovementWhenControlGained();

            Executor.Tick(Context, deltaTimeSeconds);
        }

        public void CancelAllTasks()
        {
            Executor.ClearAll(Context);
        }

        public void Submit(TaskRequest request)
        {
            if (!Executor.TryInterruptWith(Context, request))
                TaskQueue.Enqueue(request);
        }
    }
}