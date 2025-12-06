using UnityEngine;
using DogGame.AI;  // if your AgentDecisionModuleBase lives here
using DogGame.Input;
// using DogGame.World; // if you need WorldObject, etc.

public class PlayerDecisionModule : AgentDecisionModuleBase
{
//    [Header("Input Source")]
//    [Tooltip("Component that provides PlayerInputState (e.g. NewInputAdapter). Must implement IPlayerInputSource.")]
//    [SerializeField] private MonoBehaviour inputSourceBehaviour;

//    private IPlayerInputSource inputSource;
    public override AgentDecisionType DecisionType => AgentDecisionType.Player;

    [SerializeField] private NewInputAdapter inputAdapter;
    private PlayerInputState inputState;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float rotateSpeed = 720f;
    [SerializeField] private bool useCameraRelativeMovement = true;

    [Header("Camera Control")]
    [SerializeField] private Camera cameraForMovement;
    [SerializeField] private CameraModeSwitcher cameraModeSwitcher; 
    // CameraControllerBase = your own interface / script that handles zoom + view switching
    
    //[SerializeField] private AgentMovementModule agentMovementModule;

    public override void Initialize(AgentModule agent)
    {
        base.Initialize(agent);

        if (inputAdapter == null)
            inputAdapter = FindFirstObjectByType<NewInputAdapter>();

        inputState = inputAdapter.InputState;

        if (worldObject.agentMovementModule == null)
        {
            Debug.LogError($"[PlayerDecisionModule {worldObject.DisplayName}] No AgentagentMovementModule found.", this);
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
        Debug.Log($"PlayerDecisionModule {worldObject.DisplayName}: Tick {deltaTime}");

        PlayerInputState state = inputState;

        HandleCamera(state, deltaTime);
        HandleOneShotActions(state);
        HandleAgentSwitchingAndFormation(state);

        HandleMovement(state, deltaTime);
        HandleInteraction(state, deltaTime);
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

            if (state.cameraViewSelect >= 0)
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

        // 1) WASD / stick input -> camera-relative world direction
        if (state.moveAxis.sqrMagnitude > 0.0001f)
        {
            desiredWorldDir = ConvertInputToWorldDirection(state.moveAxis);
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
            }
            else
            {
                // Reached target; clear the desired move so we can stop
                desiredWorldDir = Vector3.zero;
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
    private Vector3 ConvertInputToWorldDirection(Vector2 moveAxis)
    {
        // If no camera or not camera-relative, just move in world XZ
        Vector3 dir = new Vector3(moveAxis.x, 0f, moveAxis.y);

        if (!useCameraRelativeMovement || cameraForMovement == null)
            return dir;

        // Convert input relative to camera orientation
        Vector3 camForward = cameraForMovement.transform.forward;
        camForward.y = 0f;
        camForward.Normalize();

        Vector3 camRight = cameraForMovement.transform.right;
        camRight.y = 0f;
        camRight.Normalize();

        Vector3 worldDir = camForward * moveAxis.y + camRight * moveAxis.x;
        return worldDir;
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
}