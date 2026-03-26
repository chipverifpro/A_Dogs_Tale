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

            Debug.Log($"[TaskController] TryApplyPlanJson invoked controllerAgentId={agentId} rawChars={planResponseJson?.Length ?? 0}");

            // sanitize the LLM Response.
            if (!DogGame.LLM.LLMResponseSanitizer.TryExtractJsonObject(planResponseJson, out string cleanJson, out string err))
            {
                Debug.LogWarning($"PlanResponseV1 invalid: could not extract JSON object. {err}");
                return false;
            }

            ExtractPlanLogContext(cleanJson, out string loggedRequestId, out string? schema, out string? loggedAgentId);
            string loggerAgentId = string.IsNullOrWhiteSpace(loggedAgentId) ? agentId : loggedAgentId!;

            Debug.Log($"[TaskController] Received plan JSON agentId={loggerAgentId} requestId={loggedRequestId} schema={schema ?? "<unknown>"} chars={cleanJson.Length}");
            LLMPacketLogger.LogResponse(
                loggerAgentId,
                loggedRequestId,
                provider: "SanitizedPlanJson",
                responseJson: cleanJson);

            if (string.Equals(schema, "PlanResponseV3", System.StringComparison.Ordinal))
                return TryApplyPlanJsonV3(planResponseJson, cleanJson, loggedRequestId, loggerAgentId);

            var (plan, validation) = PlanResponseV1Parser.ParseAndValidate(cleanJson);

            if (plan == null)
            {
                Debug.LogWarning("PlanResponseV1 invalid:\n" + string.Join("\n", validation.Errors));
                
                LLMPacketLogger.LogResponse(
                    loggerAgentId,
                    loggedRequestId,
                    provider: "ParserError" + string.Join("\n", validation.Errors),
                    responseJson: planResponseJson);
                
                return false;
            }

            if (plan.AgentId != agentId)
            {
                Debug.LogWarning($"Plan agent mismatch: plan.AgentId={plan.AgentId}, controller.AgentId={agentId}");
                return false;
            }

            Debug.Log($"[TaskController] Accepted requestId={plan.RequestId} for controllerAgentId={agentId}");

            if (clearQueueOnNewPlan)
            {
                // Cancel all execution state + queued tasks, not just the queue list.
                taskExecutor.ClearAll(taskContext);
            }

            if (!PlanIntentMapper.TryEnqueueTasksFromPlan(plan, taskQueue, out var error))
            {
                Debug.LogWarning("Plan mapped to zero tasks: " + error);
                LLMPacketLogger.LogResponse(
                    loggerAgentId,
                    plan.RequestId ?? loggedRequestId,
                    provider: "PlanResponseV1_EnqueueRejected",
                    responseJson: cleanJson);
                return false;
            }

            Debug.Log($"[TaskController] Applied PlanResponseV1 requestId={plan.RequestId} agentId={plan.AgentId} intentions={plan.Intentions.Count}");
            LLMPacketLogger.LogResponse(
                loggerAgentId,
                plan.RequestId ?? loggedRequestId,
                provider: "PlanResponseV1_Applied",
                responseJson: cleanJson);

            return true;
        }

        private bool TryApplyPlanJsonV3(string originalPlanResponseJson, string cleanJson, string loggedRequestId, string loggerAgentId)
        {
            var (plan, validation) = PlanResponseV3Parser.ParseAndValidate(cleanJson);

            if (plan == null)
            {
                Debug.LogWarning("PlanResponseV3 invalid:\n" + string.Join("\n", validation.Errors));

                LLMPacketLogger.LogResponse(
                    loggerAgentId,
                    loggedRequestId,
                    provider: "ParserErrorV3 " + string.Join("\n", validation.Errors),
                    responseJson: originalPlanResponseJson);

                return false;
            }

            if (plan.AgentId != agentId)
            {
                Debug.LogWarning($"Plan agent mismatch: plan.AgentId={plan.AgentId}, controller.AgentId={agentId}");
                return false;
            }

            Debug.Log($"[TaskController] Accepted requestId={plan.RequestId} for controllerAgentId={agentId}");

            if (clearQueueOnNewPlan)
                taskExecutor.ClearAll(taskContext);

            if (!PlanIntentMapper.TryEnqueueTasksFromPlan(plan, taskQueue, out var error))
            {
                Debug.LogWarning("PlanResponseV3 mapped to zero tasks: " + error);
                LLMPacketLogger.LogResponse(
                    loggerAgentId,
                    plan.RequestId ?? loggedRequestId,
                    provider: "PlanResponseV3_EnqueueRejected",
                    responseJson: cleanJson);
                return false;
            }

            string actions = string.Join(", ", ExtractActionNames(plan.Intentions));
            Debug.Log($"[TaskController] Applied PlanResponseV3 requestId={plan.RequestId} agentId={plan.AgentId} intentions={plan.Intentions.Count} actions=[{actions}]");
            LLMPacketLogger.LogResponse(
                loggerAgentId,
                plan.RequestId ?? loggedRequestId,
                provider: "PlanResponseV3_Applied",
                responseJson: cleanJson);

            return true;
        }

        private static void ExtractPlanLogContext(string cleanJson, out string requestId, out string? schema, out string? loggedAgentId)
        {
            requestId = "unknown";
            schema = null;
            loggedAgentId = null;

            try
            {
                var root = JObject.Parse(cleanJson);
                schema = root.Value<string>("schema");
                requestId = root.Value<string>("requestId") ?? "unknown";
                loggedAgentId = root.Value<string>("agentId");
            }
            catch
            {
                // Keep fallback values.
            }
        }

        private static List<string> ExtractActionNames(List<JObject> intentions)
        {
            var actions = new List<string>();
            if (intentions == null)
                return actions;

            for (int i = 0; i < intentions.Count; i++)
            {
                string? action = intentions[i]?.Value<string>("action");
                if (!string.IsNullOrWhiteSpace(action))
                    actions.Add(action.Trim());
            }

            return actions;
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
