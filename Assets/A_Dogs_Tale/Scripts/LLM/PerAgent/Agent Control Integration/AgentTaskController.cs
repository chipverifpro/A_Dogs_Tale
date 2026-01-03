#nullable enable
using UnityEngine;
using DogGame.Modules;
using DogGame.Tasks;

namespace DogGame.LLM
{
    [DefaultExecutionOrder(-10)]  // create TaskQueue before AgentTaskExecutor tries to use it in Awake   
    public sealed class AgentTaskController : WorldModule
    {
        [SerializeField] private string agentId = "player";
        public string AgentId => agentId;
        
        [Header("Queue behavior")]
        [SerializeField] private bool clearQueueOnNewPlan = true;

        public AgentTaskQueue TaskQueue { get; private set; } = null!;
            // Task queue: accessed as controller.TaskQueue
            // owner:    AgentTaskController.cs
            // consumer: AgentTaskExecutor.cs    consumer (advances tasks by dequeuing)
            // producer: Decision Modules        producer
            //           Reaction Module         producer
            //           LLM plan appier         producer
            //           Task_Branch             producer
        public AgentTaskExecutor Executor { get; private set; } = null!;
        public AgentTaskContext Context { get; private set; } = null!;

        /// <summary>True when LLM tasks should be considered "in control".</summary>
        public bool IsDriving => TaskQueue.Count > 0;
        // IsDrivingMovement: For now it’s the same, but later you can drive dialogue/tasks without movement.
        public bool IsDrivingMovement => IsDriving;
        private WorldObjectMotionBridge? motionBridge;
        private MotionModuleMovementAdapter? motionAdapter;

        protected override void Awake()
        {
            agentId = worldObject.DisplayName;
            TaskQueue = new AgentTaskQueue();
            Executor  = new AgentTaskExecutor(TaskQueue);

            if (worldObject == null)
            {
                Debug.LogError("[AgentTaskController] WorldObject not found on this GameObject.");
                enabled = false;
                return;
            }

            // 2) Build bridge + adapter
            motionBridge = new WorldObjectMotionBridge(worldObject);

            motionAdapter = new MotionModuleMovementAdapter(
                worldObject: worldObject);

            // 3) Use this adapter in the task context
            Context = new AgentTaskContext(agentId, worldObject, transform, motionAdapter);
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
                TaskQueue.Clear();
            
            if (!PlanIntentMapper.TryEnqueueTasksFromPlan(plan, TaskQueue, out var error))
            {
                Debug.LogWarning("Plan mapped to zero tasks: " + error);
                return false;
            }

            return true;
        }

        private bool wasDrivingMovementLastTick;

        public void StopMovementWhenControlGained(float deltaTimeSeconds)
        {
            bool isDrivingMovementNow = IsDrivingMovement;

            // If we just took control, kill any leftover player velocity immediately.
            if (isDrivingMovementNow && !wasDrivingMovementLastTick)
            {
                Debug.Log($"AgentTaskController: Stop movement when LLM gains control.");
                Context.Movement.StopMoving(); // this calls motionModule.Move(Vector3.zero, ...)
            }

            wasDrivingMovementLastTick = isDrivingMovementNow;
        }

        private int debugDoubleTick = -1;
        /// <summary>
        /// Call from your AI update path (DecisionModule / AgentModule). This will run tasks and
        /// drive worldObject.motionModule.Move() via the adapter.
        /// </summary>
        public override void Tick(float deltaTimeSeconds)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            if (dir && worldObject)
            {
                if (dir.gameInputRouter.InputState.anyKeyOrButtonDown)
                {
                    var llm = worldObject.GetComponent<DogGame.LLM.AgentTaskController>();
                    //llm?.CancelAllTasks();
                    Debug.Log($"AgentTaskController: Cancelling tasks due to anyKeyOrButtonDown");
                    CancelAllTasks();
                }
            }
            else
            {
                Debug.LogWarning($"AgentTaskController.Tick: dir or worldObject is null.  Cannot call CancelAllTasks.");
            }
            StopMovementWhenControlGained(deltaTimeSeconds);

            // Tasks update the adapter's target (SetMoveTarget)
            Executor.Tick(Context, deltaTimeSeconds);
        }

        public void CancelAllTasks()
        {
            TaskQueue.Clear();
            // Ensure current task ends and movement stops
            Context.Movement.StopMoving();
        }
    }
}