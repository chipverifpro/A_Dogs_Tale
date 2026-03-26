#nullable enable
using UnityEngine;
using DogGame.Modules;
using DogGame.Tasks;
using System.Collections.Generic;
using DogGame.LLM.Debugging;
using NUnit.Framework.Interfaces;
using Newtonsoft.Json.Linq;

namespace DogGame.LLM
{
    [DefaultExecutionOrder(-10)]
    public sealed class TaskController : WorldModule
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
        public bool IsDriving
        {
            get
            {
                EnsureRuntimeState();
                if (taskExecutor == null || taskQueue == null)
                    return false;

                return taskExecutor.HasTask || taskQueue.Count > 0 || taskExecutor.SuspendedCount > 0;
            }
        }
        public bool IsDrivingMovement => IsDriving;

        private MotionAdapter? motionAdapter;

        private DogGame.Tasks.IBlackboard blackboard = null!;

        public static TaskRequest Llm(IAgentTask task, int priority = 60, string? tag = "llm_plan")
            => new(task, priority, TaskSource.LLM, canInterrupt: false, tag: tag);

        public static TaskRequest Reaction(IAgentTask task, int priority = 80, bool resumePrevious = true, string? tag = "reaction")
            => new(task, priority, TaskSource.Reaction, canInterrupt: true, resumePrevious: resumePrevious, tag: tag);

        protected override void Awake()
        {
            if (!EnsureRuntimeState())
                enabled = false;
        }

        private void OnEnable()
        {
            EnsureRuntimeState();
        }

        private bool EnsureRuntimeState()
        {
            if (worldObject == null)
            {
                Debug.LogError("[TaskController] WorldObject not found on this GameObject.");
                return false;
            }

            blackboard ??= new DogGame.Tasks.SimpleBlackboard();

            // Use DisplayName as agent id by default
            agentId = worldObject.DisplayName;

            taskQueue ??= new TaskQueue();
            taskExecutor ??= new TaskExecutor(taskQueue);

            // Movement adapter used by tasks (intent-level; no per-frame Tick here)
            motionAdapter ??= worldObject.motionAdapter;

            taskContext ??= new TaskContext(worldObject, OriginRequestId:null, OriginTag:null);
            motionAdapter = (MotionAdapter)taskContext.Motion;
            return true;
        }

        public bool TryApplyPlanJson(string planResponseJson)
        {
            if (!EnsureRuntimeState())
                return false;

            // sanitize the LLM Response.
            if (!DogGame.LLM.LLMResponseSanitizer.TryExtractJsonObject(planResponseJson, out string cleanJson, out string err))
            {
                Debug.LogWarning($"PlanResponseV1 invalid: could not extract JSON object. {err}");
                return false;
            }

            string? schema = null;
            try
            {
                schema = JObject.Parse(cleanJson).Value<string>("schema");
            }
            catch
            {
                // Fall through into the normal parser error path below.
            }

            if (string.Equals(schema, "PlanResponseV3", System.StringComparison.Ordinal))
                return TryApplyPlanJsonV3(planResponseJson, cleanJson);

            var (plan, validation) = PlanResponseV1Parser.ParseAndValidate(cleanJson);

            if (plan == null)
            {
                Debug.LogWarning("PlanResponseV1 invalid:\n" + string.Join("\n", validation.Errors));
                
                LLMPacketLogger.LogResponse(
                    agentId,
                    "requestID",
                    provider: "ParserError" + string.Join("\n", validation.Errors),
                    responseJson: planResponseJson);
                
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

        private bool TryApplyPlanJsonV3(string originalPlanResponseJson, string cleanJson)
        {
            var (plan, validation) = PlanResponseV3Parser.ParseAndValidate(cleanJson);

            if (plan == null)
            {
                Debug.LogWarning("PlanResponseV3 invalid:\n" + string.Join("\n", validation.Errors));

                LLMPacketLogger.LogResponse(
                    agentId,
                    "requestID",
                    provider: "ParserErrorV3 " + string.Join("\n", validation.Errors),
                    responseJson: originalPlanResponseJson);

                return false;
            }

            if (plan.AgentId != agentId)
            {
                Debug.LogWarning($"Plan agent mismatch: plan.AgentId={plan.AgentId}, controller.AgentId={agentId}");
                return false;
            }

            if (clearQueueOnNewPlan)
                taskExecutor.ClearAll(taskContext);

            if (!PlanIntentMapper.TryEnqueueTasksFromPlan(plan, taskQueue, out var error))
            {
                Debug.LogWarning("PlanResponseV3 mapped to zero tasks: " + error);
                return false;
            }

            return true;
        }

        private bool wasDrivingMovementLastTick;

        public void StopMovementWhenControlGained()
        {
            if (!EnsureRuntimeState())
                return;

            bool isDrivingMovementNow = IsDrivingMovement;

            // If we just took control, kill any leftover player intent immediately.
            if (isDrivingMovementNow && !wasDrivingMovementLastTick)
            {
                Debug.Log("TaskController: Stop movement when task control gains control.");
                taskContext.Motion.StopMoving();
            }

            wasDrivingMovementLastTick = isDrivingMovementNow;
        }

        private int debugDoubleTick = -1;

        public override void Tick(float deltaTimeSeconds)
        {
            if (!EnsureRuntimeState())
                return;

            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: TaskController.Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            // Temporary dev behavior: any input cancels task control (also blocks Tab, etc.).
            var router = dir != null ? (dir.gameInputRouter != null ? dir.gameInputRouter : GameInputRouter.Instance) : GameInputRouter.Instance;
            if (dir && worldObject && router != null && router.InputState != null && router.InputState.anyKeyOrButtonDown)
            {
                Debug.Log("DISABLED: TaskController: Cancelling tasks due to anyKeyOrButtonDown");
                //CancelAllTasks();
                //return;
            }

            StopMovementWhenControlGained();

            taskExecutor.Tick(taskContext, deltaTimeSeconds);
        }

        public void CancelAllTasks()
        {
            if (!EnsureRuntimeState())
                return;

            taskExecutor.ClearAll(taskContext);
        }

        public void Submit(TaskRequest request)
        {
            if (!EnsureRuntimeState())
                return;

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

        // Assuming you already have one of these:
        // [SerializeField] private TaskQueue taskQueue;
        // public TaskQueue Queue => taskQueue;  // if you expose it

        public void EnqueueTask(
            IAgentTask task,
            int priority,
            TaskSource source,
            bool canInterrupt = true,
            bool resumePrevious = false,
            bool clearStackOnStart = false,
            string? tag = null,
            bool front = false)
        {
            if (taskQueue == null)
            {
                Debug.LogWarning("[TaskController] taskQueue is null; cannot enqueue task.");
                return;
            }

            var request = new TaskRequest(
                task: task,
                priority: Mathf.Clamp(priority, 0, 100),
                source: source,
                canInterrupt: canInterrupt,
                resumePrevious: resumePrevious,
                clearStackOnStart: clearStackOnStart,
                tag: tag);

            taskQueue.Enqueue(request, front: front);
        }

        // Convenience helper matching your old LLMApplyMode intent
        public void EnqueueTask(
            IAgentTask task,
            int priority,
            TaskSource source,
            LLMApplyMode applyMode,
            string? tag = null,
            bool front = false)
        {
            bool canInterrupt = applyMode != LLMApplyMode.Append;
            bool resumePrevious = applyMode == LLMApplyMode.SuspendThenInterrupt;
            bool clearStack = false; // keep separate for emergencies

            EnqueueTask(
                task: task,
                priority: priority,
                source: source,
                canInterrupt: canInterrupt,
                resumePrevious: resumePrevious,
                clearStackOnStart: clearStack,
                tag: tag,
                front: front);
        }

        public void EnqueueEmergency(
            IAgentTask task,
            int priority,
            TaskSource source,
            string? tag = null)
        {
            EnqueueTask(
                task: task,
                priority: Mathf.Clamp(priority, 0, 100),
                source: source,
                canInterrupt: true,
                resumePrevious: false,
                clearStackOnStart: true,
                tag: tag,
                front: true);
        }
    }
}
