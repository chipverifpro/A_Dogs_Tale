using UnityEngine;
using DogGame.AI;
using DogGame.LLM;  // if your AgentDecisionModuleBase lives here
// using DogGame.World; // if you need WorldObject, etc.
using DogGame.Tasks;

namespace DogGame.Modules
{
    public class PlayerDecisionModule : AgentDecisionModuleBase
    {
        //    [Header("Input Source")]
        //    [Tooltip("Component that provides PlayerInputState (e.g. NewInputAdapter). Must implement IPlayerInputSource.")]
        //    [SerializeField] private MonoBehaviour inputSourceBehaviour;

        //    private IPlayerInputSource inputSource;
        public override AgentDecisionType DecisionType => AgentDecisionType.Player;

        private PlayerInputState inputState;   // a pointer, not a local copy
        private GameInputRouter gameInputRouter;

        //[Header("Movement")]
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

        TaskControler taskControler = null;

    private void Start()
    {
        if (gameInputRouter == null)
        {
            gameInputRouter = GameInputRouter.Instance;
            if (gameInputRouter == null)
            {
                Debug.LogError("[PlayerDecisionModule] No GameInputRouter in scene.", this);
                enabled = false;
                return;
            }

            if (inputState == null)
                inputState = gameInputRouter.InputState;  // keep a reference, not a copy
            
            if (inputState == null)
            {
                Debug.LogError($"[PlayerDecisionModule {worldObject.DisplayName}] inputState is null.", this);
            }  
        }

        if (gameInputRouter.InputState == null)
        {
            Debug.LogError($"[PlayerInputStateDebugger] gameInputRouter.InputState is null.", this);
        }

        taskControler = worldObject.GetComponent<TaskControler>();

    }

        // Initialize called from WorldObject.Awake phase
        public override void Initialize(AgentModule agent)
        {
            base.Initialize(agent);
            //if (worldObject==null)
            //{
                //worldObject = GetComponent<WorldObject>();
                if (worldObject == null)
                    Debug.LogError($"[PlayerDecisionModule] could not get worldObject.");
            //}
            //if (inputAdapter == null)
            //    inputAdapter = FindFirstObjectByType<NewInputAdapter>();

            gameInputRouter = GameInputRouter.Instance;
            if (gameInputRouter == null)
            {
                Debug.LogError("[PlayerDecisionModule] No GameInputRouter in scene.", this);
                enabled = false;
                return;
            }

            if (inputState == null)
                inputState = gameInputRouter.InputState;  // keep a reference, not a copy
            
            if (inputState == null)
            {
                Debug.LogError($"[PlayerDecisionModule {worldObject.DisplayName}] inputState is null.", this);
            }

            if (worldObject.agentMovementModule == null)
            {
                Debug.LogError($"[PlayerDecisionModule {worldObject.DisplayName}] No agentMovementModule found.", this);
            }

            if (cameraForMovement == null)
                cameraForMovement = Camera.main;
        }

        private int debugDoubleTick = -1;
        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            if (gameInputRouter == null)
            {
                Debug.LogWarning("Tick: gameInputRouter == null");
                return;
            }

            if (taskControler != null && taskControler.IsDrivingMovement)
            {
                //Debug.Log("taskControler is driving movement.");
                taskControler.Tick(deltaTime);
                //return; // IMPORTANT: don't also write motion inputs this tick
            }

            // Only act if THIS worldObject is the one currently controlled
            if (!gameInputRouter.IsControlled(worldObject))
            {
                Debug.LogWarning($"[{worldObject.DisplayName}] Tick: IsControlled == false");
                return;
            }

            if (inputState == null)
            {
                Debug.LogWarning("Tick: inputState == null");
                return;
            }

            //Debug.Log($"PlayerDecisionModule: ready to process inputState");

            HandleOneShotActions(inputState);

            // Only do player controlled movement if LLM isn't driving movement
            if (!(taskControler != null && taskControler.IsDrivingMovement))
            {
                //Debug.Log("PlayerDecisionModule is driving movement.");
                HandleMovement(inputState, deltaTime);
            }
            

            // if player has requested activating an object, it will be sent from a
            // central location, not here in playerInputModule.  Currently that location
            // is GameInputRouter.

            //if (worldObject.activatorModule!=null)
            //    worldObject.activatorModule.HandleActivate(inputState, deltaTime);
        }

        #region One-shot actions

        private void HandleOneShotActions(PlayerInputState state)
        {
            //Debug.Log("PlayerDecisionModule HandleOneShotActions");
            if (state.barkPressed && worldObject.noiseMakerModule != null)
            {
                worldObject.noiseMakerModule.Bark();
            }

            if (state.markTerritoryPressed && worldObject.scentEmitterModule != null)
            {
                worldObject.scentEmitterModule.EmitOnDemandScent(1.0f); // spread over 1 second
            }

            // You can also use state.anyKeyOrButtonPressed to skip cutscenes,
            // advance dialogue, etc. Hook that into your game state manager.
        }

        #endregion

        #region Movement

        private void HandleMovement(PlayerInputState state, float deltaTime)
        {
            if (worldObject.agentMovementModule == null)
            {
                Debug.LogWarning($"[PlayerDecisionModule {worldObject.DisplayName}] No AgentagentMovementModule found.", this);
                return;
            }

            //Debug.Log($"PlayerDecisionModule HandleMovement(anyKey={state.anyKeyOrButtonDown}, {deltaTime})");

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


            // 1) WASD / stick input -> camera-relative world direction
            if (combinedMoveAxis.sqrMagnitude > 0.0001f)
            {
                // First, manual controls disable current click-to-move status
                currentDestinationPosition = null;  // stop heading to location
                currentDestinationObject = null;    // stop heading to object

                desiredWorldDir = ConvertInputToWorldDirection(combinedMoveAxis);
                navigationSource = NavigationSource.PlayerDirection;
                worldObject.motionModule.motionControlMode = MotionControlMode.DirectInput;
                if (Mathf.Abs(state.strafeAxis) > 0.0001f)  // any strafe element, set Strafe
                    worldObject.motionModule.facingMode = FacingMode.Strafe; 
                else
                    UpdateFacingModeForDirectInput(desiredWorldDir);    // handles strafe/backpedalling.
           }

            // 2) Click-to-move: if we have a click target location and no interact press,
            //    steer toward that point. (Very simple version: straight-line steering.)
            
            // 2B) New click target is an object: new orders
            if (state.hasClickTargetWorldObject && !state.interactPressed)
            {
                currentDestinationObject = state.clickTargetWorldObject; // head to object, new orders arrived
                SubmitMoveToTargetObjectTask(currentDestinationObject);
                return;
            }
            // 2A) New click target was a location: new orders
            if (state.hasClickTargetLocationWorld && !state.interactPressed)
            {
                currentDestinationPosition = state.clickTargetLocationWorld; // head to location, new orders arrived
                currentDestinationObject = null;  // stop heading to object if we had been
                SubmitMoveToTargetPositionTask((Vector3)currentDestinationPosition);
                return;
            }

/*
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
            */
        }

        public void MovementHeadToDestination()
        {
            // bring in manual input desiredWorldDir
            Vector3 desiredWorldDir = currentManualWorldMoveDir==null ? Vector3.zero : (Vector3)currentManualWorldMoveDir;

            // 3) Feed intent into agentMovementModule
            if (currentDestinationObject != null)
            {
                // Move to target object, and keep tracking it.
                worldObject.agentMovementModule.SetDesiredTargetWorldObject(currentDestinationObject);

            }
            else if (desiredWorldDir.sqrMagnitude > 0.0001f)
            {
                // Move to target location by 1 step.
                // If you have a sprint flag in PlayerInputState, use it here.
                //bool run = false; // state.sprintHeld; // <-- adjust to your actual field name
                worldObject.agentMovementModule.SetDesiredMove(desiredWorldDir, maxDistance:1.0f, speedFactor:1.0f, changeWalkMode:WalkMode.Walk);
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

        public void SubmitMoveToTargetObjectTask(WorldObject targetWorldObject)
        {
            if (targetWorldObject != null)
            {
                taskControler.Submit(new TaskRequest(
                    task: new Task_MoveToObject(targetWorldObject, stopRadius: 0.6f),
                    priority: 100,
                    source: TaskSource.Player,
                    canInterrupt: true,
                    resumePrevious: false,
                    clearStackOnStart: true,
                    tag: "player_move_to_object"
                ));
            } 
        }
        public void SubmitMoveToTargetPositionTask(Vector3 targetLocation)
        {
            {
                taskControler.Submit(new TaskRequest(
                    task: new Task_MoveToLocation(targetLocation.x, targetLocation.z, stopRadius: 0.6f),
                    priority: 100,
                    source: TaskSource.Player,
                    canInterrupt: true,
                    resumePrevious: false,
                    clearStackOnStart: true,
                    tag: "player_move_to_location"
                ));
            }
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


        private Vector3 ConvertInputToWorldDirection(Vector2 moveAxis)
        {
            // If no input, early out
            if (moveAxis.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            // 1. If overhead mode → use player-relative movement
            if (this.transform != null)
            {
                Vector3 forward = this.transform.forward;
                forward.y = 0f;
                forward.Normalize();

                Vector3 right = this.transform.right;
                right.y = 0f;
                right.Normalize();

                return forward * moveAxis.y + right * moveAxis.x;
            }

            // 3. Fallback: world-relative XZ
            Debug.LogWarning("this.transform == null, maybe not a good thing? ",this);
            return new Vector3(moveAxis.x, 0f, moveAxis.y);     // probably not what we want, so don't fail above!
        }
        #endregion

        #region Interaction
        // Not handled here...  GameInputRouter sends target the HandleActivate event.
        #endregion
        
        #region PackLoyalty
        // Mode A: Soft influence (blend / bias movement)
        // If the player is controlling movement, just bias desired velocity slightly back toward pack.
        public static Vector3 ApplyPackLoyaltyBias(
            Vector3 desiredWorldVelocity,
            Vector3 selfPosition,
            PackLoyaltyResult packLoyalty,
            float maxBiasStrength = 0.65f)
        {
            if (!packLoyalty.isActive || packLoyalty.directive == PackLoyaltyDirective.None)
                return desiredWorldVelocity;

            Vector3 toTarget = (packLoyalty.targetLocation - selfPosition);
            if (toTarget.sqrMagnitude < 0.001f)
                return desiredWorldVelocity;

            Vector3 loyaltyDirection = toTarget.normalized;

            // Blend: higher urge means more pull.
            float bias = Mathf.Clamp01(packLoyalty.urge01) * maxBiasStrength;

            Vector3 biased = Vector3.Lerp(desiredWorldVelocity, loyaltyDirection * desiredWorldVelocity.magnitude, bias);
            return biased;
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
            if (!(taskControler != null && taskControler.IsDrivingMovement))
            {
                Debug.Log("LLM was still driving movement when Player took over.");
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