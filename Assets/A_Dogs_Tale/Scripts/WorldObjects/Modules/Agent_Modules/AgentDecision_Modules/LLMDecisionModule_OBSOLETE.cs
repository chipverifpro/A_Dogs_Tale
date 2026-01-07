using DogGame.LLM;
using UnityEngine;

namespace DogGame.Modules
{
    public class LLMDecisionModule_OBSOLETE : AgentDecisionModuleBase
    {
        //    [Header("Input Source")]
        //    [Tooltip("Component that provides PlayerInputState (e.g. NewInputAdapter). Must implement IPlayerInputSource.")]
        //    [SerializeField] private MonoBehaviour inputSourceBehaviour;

        //    private IPlayerInputSource inputSource;
        public override AgentDecisionType DecisionType => AgentDecisionType.LLM;

        private PlayerInputState inputState;   // a pointer, not a local copy
        private GameInputRouter gameInputRouter;

        [Header("Movement")]
        //[SerializeField] private float moveSpeed = 3.5f;  // now in agentMovementModule
        //[SerializeField] private float rotateSpeed = 720f; // now in agentMovementModule

        [Header("Camera Control")]
        [SerializeField] private Camera cameraForMovement;
        [SerializeField] private CameraModeSwitcher cameraModeSwitcher;
    
        //public NavigationSource navigationSource;     // moved to AgentDecisionModuleBase
        //public MotionControlMode motionControlMode;   // moved to MotionModule

        // these store the last given instructions regarding where to go.
        public Vector3? currentDestinationPosition = null;
        public WorldObject currentDestinationObject = null;
        public Vector3? currentManualWorldMoveDir = null;


    private void Start()
    {
        if (gameInputRouter == null)
        {
            gameInputRouter = GameInputRouter.Instance;
            if (gameInputRouter == null)
            {
                Debug.LogError("[LLMDecisionModule] No GameInputRouter in scene.", this);
                enabled = false;
                return;
            }

            if (inputState == null)
                inputState = gameInputRouter.InputState;  // keep a reference, not a copy
            
            if (inputState == null)
            {
                Debug.LogError($"[LLMDecisionModule {worldObject.DisplayName}] inputState is null.", this);
            }

        }

        if (gameInputRouter.InputState == null)
        {
            Debug.LogError($"[LLMInputStateDebugger] gameInputRouter.InputState is null.", this);
        }
    }

        // Initialize called from WorldObject.Awake phase
        public override void Initialize(AgentModule agent)
        {
            base.Initialize(agent);
            //if (worldObject==null)
            //{
                //worldObject = GetComponent<WorldObject>();
                if (worldObject == null)
                    Debug.LogError($"[LLMDecisionModule] could not get worldObject.");
            //}
            //if (inputAdapter == null)
            //    inputAdapter = FindFirstObjectByType<NewInputAdapter>();

            gameInputRouter = GameInputRouter.Instance;
            if (gameInputRouter == null)
            {
                Debug.LogError("[LLMDecisionModule] No GameInputRouter in scene.", this);
                enabled = false;
                return;
            }

            if (inputState == null)
                inputState = gameInputRouter.InputState;  // keep a reference, not a copy
            
            if (inputState == null)
            {
                Debug.LogError($"[LLMDecisionModule {worldObject.DisplayName}] inputState is null.", this);
            }

            if (worldObject.agentMovementModule == null)
            {
                Debug.LogError($"[LLMDecisionModule {worldObject.DisplayName}] No agentMovementModule found.", this);
            }

            if (cameraForMovement == null)
                cameraForMovement = Camera.main;
        }

        private int debugDoubleTick = -1;
        public override void Tick(float deltaTime)
        {
            Debug.LogError("ERROR: For now, LLMDesisionModule is unused, but being 'Tick'ed.");
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            //Debug.Log($"PlayerDecisionModule {worldObject.DisplayName}: Tick {deltaTime}");
            if (gameInputRouter == null)
            {
                Debug.LogWarning("Tick: gameInputRouter == null");
                return;
            }

            var taskControler = worldObject.GetComponent<DogGame.LLM.TaskControler>();
            if (taskControler != null && taskControler.IsDrivingMovement)
            {
                taskControler.Tick(deltaTime);
                //return; // IMPORTANT: don't also write motion inputs this tick
            }

            // Only act if THIS worldObject is the one currently controlled
            if (!gameInputRouter.IsControlled(worldObject))
            {
                Debug.LogWarning($"[{worldObject.DisplayName}] Tick: IsControlled == false");
                return;
            }

            //Debug.Log($"LLMDecisionModule: calling HandleMovement");

            HandleMovement(inputState, deltaTime);

        }

        #region Movement

        private void HandleMovement(PlayerInputState state, float deltaTime)
        {
            if (worldObject.agentMovementModule == null)
            {
                Debug.LogWarning($"[PlayerDecisionModule {worldObject.DisplayName}] No AgentagentMovementModule found.", this);
                return;
            }

            Vector3 desiredWorldDir = Vector3.zero;
            
            // --- Combine moveAxis and strafeAxis into combinedMoveAxis
            //     WASD / stick input -> moveAxis         (x and y)
            //     QE keboard / stick input -> strafeAxis (x only)

            Vector2 combinedMoveAxis = state.moveAxis;

            // combine moveAxis and strafeAxis into one
            if (state.moveAxis.sqrMagnitude > 0.0001f || Mathf.Abs(state.strafeAxis) > 0.0001f)
            {
                if (state.moveAxis.x <= 0f && state.strafeAxis <= 0f)
                {
                    // both movements are negative, take the Min
                    combinedMoveAxis.x = Mathf.Min(state.moveAxis.x, state.strafeAxis);
                }
                else if (state.moveAxis.x >= 0f && state.strafeAxis >= 0f)
                {
                    // both movements are positive, take the Max
                    combinedMoveAxis.x = Mathf.Max(state.moveAxis.x, state.strafeAxis);
                }
                else // strafe is opposite direction to move, add both, let them cancel each other out
                {
                    combinedMoveAxis.x = state.moveAxis.x + state.strafeAxis;
                }
            }

            // 2) Click-to-move: if we have a click target location and no interact press,
            //    steer toward that point. (Very simple version: straight-line steering.)
            
            // 2A) New target was a location: new orders
            if (state.hasClickTargetLocationWorld && !state.interactPressed)
            {
                currentDestinationPosition = state.clickTargetLocationWorld; // head to location, new orders arrived
                currentDestinationObject = null;  // stop heading to object if we had been
            }
            // 2B) New target is an object: new orders
            if (state.hasClickTargetWorldObject && !state.interactPressed)
            {
                currentDestinationObject = state.clickTargetWorldObject; // head to object, new orders arrived
            }

            // current target is an object, let's figure out where it is now.
            if (currentDestinationObject!=null)
            {
                if (currentDestinationObject.locationModule != null)
                    currentDestinationPosition = currentDestinationObject.locationModule.pos3d_world;
            }

            // fianlly, head to destination location (or object's current location)
            if (currentDestinationPosition != null)
            {
                Vector3 toTarget = (Vector3)currentDestinationPosition - worldObject.transform.position;
                toTarget.y = 0f;

                const float stopDistance = 0.25f; // tweak as needed

                if (toTarget.sqrMagnitude > stopDistance * stopDistance)
                {
                    desiredWorldDir = toTarget.normalized;
                    navigationSource = NavigationSource.ClickToMove;
                    worldObject.motionModule.motionControlMode = MotionControlMode.GoalDirected;
                    worldObject.motionModule.facingMode = FacingMode.FaceMovementDirection;
                }
                else
                {
                    // Reached target; clear the desired move so we can stop
                    desiredWorldDir = Vector3.zero;
                    navigationSource = NavigationSource.None;
                    worldObject.motionModule.motionControlMode = MotionControlMode.GoalDirected;
                    worldObject.motionModule.facingMode = FacingMode.FaceMovementDirection;

                    // Send notification of arrival...
                    if (currentDestinationObject!=null)
                    {
                        // Send event Arrived at currentDestinationObject
                        // use-case: interact with object
                    }
                    else if (currentDestinationPosition!=null)
                    {
                        // Send event Arrived at currentDestinationPosition
                        // use-case: patrol between points allows changing to next point.
                    }
                    
                    // clear current destination
                    currentDestinationPosition = null;
                    currentDestinationObject = null;
                }
            }
            currentManualWorldMoveDir = desiredWorldDir;
            MovementHeadToDestination();
        }

        public void MovementHeadToDestination()
        {
            // 3) Feed intent into agentMovementModule
            if (currentDestinationObject != null || currentDestinationPosition != null)
            {
                // Move to target object, and keep tracking it.
                worldObject.agentMovementModule.PointTowardTargetObjectLocation();
            }
            else
            {
                // No active target and no input: decelerate to stop
                worldObject.agentMovementModule.ClearDesiredMove();
            }

            // NOTE:
            // We do NOT rotate the worldObject here anymore.
            // MotionModule (called by AgentagentMovementModule) handles facing the move direction.
        }

        private void UpdateFacingModeForDirectInput(Vector3 worldMoveDir)
        {
            var motion = worldObject.motionModule;

            // No movement? Don’t change facing.
            Vector3 flatMove = new Vector3(worldMoveDir.x, 0f, worldMoveDir.z);
            if (flatMove.sqrMagnitude < 0.0001f)
                return;

            flatMove.Normalize();

            // Current facing on the XZ plane
            Transform bodyRoot = motion.bodyRoot; // or however you access it
            Vector3 flatForward = bodyRoot.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = Vector3.forward;
            else
                flatForward.Normalize();

            float alignment = Vector3.Dot(flatForward, flatMove);
            // alignment:
            //  +1  = moving straight forward
            //   0  = pure strafe
            //  -1  = straight backward

            //const float forwardThreshold   =  0.4f;  // > this = "mostly forward"
            const float backpedalThreshold = -0.4f;  // < this = "mostly backward"

            //if (alignment > forwardThreshold)
            if (alignment > backpedalThreshold)
            {
                // Moving mostly forward: let MotionModule rotate with movement
                motion.facingMode = FacingMode.FaceMovementDirection;
            }
            else
            {
                // Strafing or backpedalling: keep facing where we already are
                motion.facingMode = FacingMode.Manual;
            }

            // If you want to treat strafe vs backpedal differently:
            // if (alignment < backpedalThreshold) { ... }
        }

        // Mode B: Hard interrupt (autopilot overrides)
        // If separation is extreme, ignore player input and set a “return-to-pack” goal.
        
        // In your DecisionModule tick:
        public void ApplyPackLoyaltyOverride()
        {
            var pack = worldObject.motivationModule.latestPackLoyalty;

            if (pack.isActive && pack.urge01 > 0.85f) // tune later
            {
                worldObject.motionModule.motionControlMode = MotionControlMode.Autopilot;
                worldObject.agentMovementModule.SetDesiredTargetWorldObject(worldObject.packMemberModule.currentPack.packLeader); // whatever your API is
                worldObject.motionModule.facingMode = FacingMode.FaceMovementDirection;
                return;
            }
        }
        #endregion

        #region BeginEndDecisionModule
        // Run this when THIS decision module becomes active
        public override void BeginDecisionModule(bool resume=false)
        {
            // TODO: This doesn't stop LLM until user PressAnyKey.  Needs to be done in LLMDriver.
            var taskControler = GetComponent<TaskControler>();
            if (!(taskControler != null && taskControler.IsDrivingMovement))
            {
                Debug.Log("LLM was still driving movement when Player took over.");
                taskControler.StopMovementWhenControlGained();
            }

            if (resume)
            {
                currentManualWorldMoveDir = null;    // no need to resume manual input control.
                // resume actions/state: currentDestination
                if (currentDestinationObject!=null || currentDestinationPosition!=null || currentManualWorldMoveDir!=null)
                {
                    MovementHeadToDestination();    // resume prior movements from last time we were active
                }
            }
            else
            {
                // clear state left over from last time we were active
                currentDestinationPosition = null;
                currentDestinationObject = null;
                currentManualWorldMoveDir = null;
                // stop any in-progress movement
                worldObject.agentMovementModule.ClearDesiredMove();
            }            
        }

        // Run this when THIS decision module becomes inactive
        public override void EndDecisionModule()
        {
            // retain state (in case requested to resume): currentDestination*
            
            // stop actions in progress: Move
            worldObject.agentMovementModule.ClearDesiredMove();
        }
        #endregion
    }
}