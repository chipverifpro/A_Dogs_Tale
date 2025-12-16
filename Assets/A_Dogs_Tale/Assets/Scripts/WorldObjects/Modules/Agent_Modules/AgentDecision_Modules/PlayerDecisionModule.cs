using UnityEngine;
using DogGame.AI;  // if your AgentDecisionModuleBase lives here
// using DogGame.World; // if you need WorldObject, etc.

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

        [Header("Movement")]
        //[SerializeField] private float moveSpeed = 3.5f;  // now in agentMovementModule
        //[SerializeField] private float rotateSpeed = 720f; // now in agentMovementModule

        [Header("Camera Control")]
        [SerializeField] private Camera cameraForMovement;
        [SerializeField] private CameraModeSwitcher cameraModeSwitcher;
    
        //public NavigationSource navigationSource;     // moved to AgentDecisionModuleBase
        //public MotionControlMode motionControlMode;   // moved to MotionModule

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

            //    if (agentPackMemberModule == null)
            //        agentPackMemberModule = worldObject.GetModule<AgentPackMemberModule>();

            //    if (agentSensesModule == null)
            //        agentSensesModule = worldObject.GetModule<AgentSensesModule>();

            if (cameraForMovement == null)
                cameraForMovement = Camera.main;
        }

        public override void Tick(float deltaTime)
        {
            //Debug.Log($"PlayerDecisionModule {worldObject.DisplayName}: Tick {deltaTime}");
            if (gameInputRouter == null)
            {
                Debug.LogWarning("Tick: gameInputRouter == null");
                return;
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

            HandleCamera(inputState, deltaTime);
            HandleOneShotActions(inputState);
            HandleAgentSwitchingAndFormation(inputState);

            HandleMovement(inputState, deltaTime);
            HandleInteraction(inputState, deltaTime);
        }

        #region Camera

        private void HandleCamera(PlayerInputState state, float deltaTime)
        {
            if (dir.cameraModeSwitcher != null)
            {
                if (Mathf.Abs(state.zoomDelta) > 0.0001f)
                {
                    Debug.Log($"ApplyZoomDelta: {state.zoomDelta}");
                    dir.cameraModeSwitcher.ApplyZoomDelta(state.zoomDelta);
                }

                if (state.cameraViewSelect != CameraModes.Unchanged)
                {
                    Debug.Log($"SelectView: {state.cameraViewSelect}");
                    dir.cameraModeSwitcher.SelectView(state.cameraViewSelect);
                }
            }
        }

        #endregion

        #region One-shot actions

        private void HandleOneShotActions(PlayerInputState state)
        {
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

        #region Pack / player agent selection

        private void HandleAgentSwitchingAndFormation(PlayerInputState state)
        {
            if (worldObject.agentPackMemberModule == null) return;

            if (state.requestedPlayerAgentIndex >= 0)
            {
                worldObject.agentPackMemberModule.RequestBecomeControlledAgent(state.requestedPlayerAgentIndex);
            }

            if (state.changeFormationPressed)
            {
                worldObject.agentPackMemberModule.CycleFormation();
            }
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
            if (state.hasClickTargetLocationWorld && !state.interactPressed)
            {
                Vector3 toTarget = state.clickTargetLocationWorld - worldObject.transform.position;
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
                    // Optional: you could clear hasClickTargetLocationWorld here in your state
                }
            }

            // 3) Feed intent into AgentagentMovementModule
            if (desiredWorldDir.sqrMagnitude > 0.0001f)
            {

                // If you have a sprint flag in PlayerInputState, use it here.
                bool run = false; // state.sprintHeld; // <-- adjust to your actual field name
                worldObject.agentMovementModule.SetDesiredMove(desiredWorldDir, 1.0f, run);
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
            return new Vector3(moveAxis.x, 0f, moveAxis.y);  // not what we want, so don't let above code fail!
        }
        #endregion

        #region Interaction

        private void HandleInteraction(PlayerInputState state, float deltaTime)
        {
            // Only act on frames where the interact button was pressed
            if (!state.interactPressed)
                return;

            // Priority 1: Interact with clicked object (if any)
            if (state.hasClickTargetWorldObject && state.clickTargetWorldObject != null)
            {
                // TODO: replace with your own interaction system.
                // e.g.:
                // InteractionSystem.Instance.RequestInteract(worldObject, state.clickTargetWorldObject);
                // or forward to an AgentActionModule on this agent.

                Debug.Log(
                    $"[PlayerDecision {worldObject.DisplayName}] " +
                    $"Interact with object {state.clickTargetWorldObject.name}"
                );
                return;
            }

            // Priority 2: No object, but clicked location → contextual interact “at that spot”
            if (state.hasClickTargetLocationWorld)
            {
                // At this point, HandleMovement is already responsible for click-to-move
                // toward state.clickTargetLocationWorld (using AgentMovementModule).
                // Here we just decide that the player wants a context action AT that location.

                Debug.Log(
                    $"[PlayerDecision {worldObject.DisplayName}] " +
                    $"Context interact at location {state.clickTargetLocationWorld}"
                );

                // Later you might:
                // - Queue a "when I arrive there, perform dig/sniff/use" action
                //   via an AgentActionModule or InteractionSystem.
                // - Set a small state flag like pendingContextActionTarget = state.clickTargetLocationWorld;
                // and have another module watch for arrival and fire the action.
            }
            else
            {
                // Priority 3: No click info → generic interact
                // e.g. "interact with nearest object in range", "sniff", etc.
                Debug.Log(
                    $"[PlayerDecision {worldObject.DisplayName}] " +
                    "Generic interact (no specific target)."
                );

                // TODO: hook to a proximity-based interaction system.
            }
        }
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

            Vector3 toTarget = (packLoyalty.targetPosition - selfPosition);
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
                worldObject.agentMovementModule.SetDesiredTargetWorldObject(worldObject.agentPackMemberModule.currentPack.packLeader); // whatever your API is
                worldObject.motionModule.facingMode = FacingMode.FaceMovementDirection;
                return;
            }
        }
        #endregion
    }
}