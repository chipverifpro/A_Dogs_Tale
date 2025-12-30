#nullable enable
using UnityEngine;

namespace DogGame.LLM
{
    public sealed class AgentTaskController : MonoBehaviour
    {
        Directory? dir;
        WorldObject? worldObject;
        [SerializeField] private string agentId = "player";
        public string AgentId => agentId;

        public AgentTaskQueue TaskQueue { get; private set; } = null!;
        public AgentTaskExecutor Executor { get; private set; } = null!;
        public AgentTaskContext Context { get; private set; } = null!;

        /// <summary>True when LLM tasks should be considered "in control".</summary>
        public bool IsDriving => Executor.HasTask || TaskQueue.Count > 0;
        // IsDrivingMovement: For now it’s the same, but later you can drive dialogue/tasks without movement.
        public bool IsDrivingMovement => IsDriving;
        private WorldObjectMotionBridge? motionBridge;
        private MotionModuleMovementAdapter? motionAdapter;

        private void Awake()
        {
            dir = FindFirstObjectByType<Directory>();

            TaskQueue = new AgentTaskQueue();
            Executor  = new AgentTaskExecutor(TaskQueue);

            // 1) Get your WorldObject so we can call worldObject.motionModule.Move(...)
            // If WorldObject is not on this same GO, switch to GetComponentInParent<WorldObject>().
            worldObject = GetComponent<WorldObject>();
            if (worldObject == null)
            {
                Debug.LogError("[AgentTaskController] WorldObject not found on this GameObject. " +
                               "Attach AgentTaskController to the same object that has WorldObject, " +
                               "or change GetComponent<WorldObject>() to GetComponentInParent<WorldObject>().");
                enabled = false;
                return;
            }

            // 2) Build bridge + adapter
            motionBridge = new WorldObjectMotionBridge(worldObject);

            motionAdapter = new MotionModuleMovementAdapter(
                agentTransform: transform,
                motionBridge: motionBridge,
                maxMoveSpeed: 3.0f,
                arriveSlowRadius: 1.25f);

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

            // Run tasks (may set a target)
            Executor.Tick(Context, deltaTimeSeconds);

            // Drive motion each tick (writes velocity every frame, including zero when idle)
            motionAdapter?.Tick(deltaTimeSeconds);

            wasDrivingMovementLastTick = isDrivingMovementNow;
        }

        /// <summary>
        /// Call from your AI update path (DecisionModule / AgentModule). This will run tasks and
        /// drive worldObject.motionModule.Move() via the adapter.
        /// </summary>
        public void Tick(float deltaTimeSeconds)
        {
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

            // Adapter converts target -> desired velocity and calls motionModule.Move(...)
            motionAdapter?.Tick(deltaTimeSeconds);
        }

        public void CancelAllTasks()
        {
            TaskQueue.Clear();
            // Ensure current task ends and movement stops
            Context.Movement.StopMoving();
        }
    }
}