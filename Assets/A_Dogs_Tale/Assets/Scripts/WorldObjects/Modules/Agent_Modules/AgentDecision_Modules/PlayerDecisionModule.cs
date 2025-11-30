using UnityEngine;
using DogGame.AI;  // if your AgentDecisionModuleBase lives here
// using DogGame.World; // if you need WorldObject, etc.

public class PlayerDecisionModule : AgentDecisionModuleBase
{
    [Header("Input Source")]
    [Tooltip("Component that provides PlayerInputState (e.g. NewInputAdapter). Must implement IPlayerInputSource.")]
    [SerializeField] private MonoBehaviour inputSourceBehaviour;

    private IPlayerInputSource inputSource;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float rotateSpeed = 720f;
    [SerializeField] private bool useCameraRelativeMovement = true;

    [Header("Camera Control")]
    [SerializeField] private Camera cameraForMovement;
    [SerializeField] private CameraModeSwitcher cameraModeSwitcher; 
    // CameraControllerBase = your own interface / script that handles zoom + view switching
    
    public override void Initialize(AgentModule agent)
    {
        base.Initialize(agent);

        // Resolve input source
        if (inputSourceBehaviour == null)
        {
            inputSourceBehaviour = GetComponentInParent<MonoBehaviour>();
        }

        if (inputSourceBehaviour is IPlayerInputSource src)
        {
            inputSource = src;
        }
        else
        {
            Debug.LogError($"PlayerDecisionModule on {agent.agentName}: inputSourceBehaviour does not implement IPlayerInputSource.", this);
        }

        // Resolve movement module if not wired
    //    if (agentMovementModule == null)
    //        agentMovementModule = worldObject.GetModule<AgentMovementModule>();

    //    if (agentPackMemberModule == null)
    //        agentPackMemberModule = worldObject.GetModule<AgentPackMemberModule>();

    //    if (agentSensesModule == null)
    //        agentSensesModule = worldObject.GetModule<AgentSensesModule>();

        if (cameraForMovement == null)
            cameraForMovement = Camera.main;
    }

    public override void Tick(float deltaTime)
    {
        if (inputSource == null)
        {
            // No input = no movement
            if (agentMovementModule != null)
                agentMovementModule.SetMoveVector(Vector3.zero);
            return;
        }

        PlayerInputState state = inputSource.CurrentState;

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
                dir.cameraModeSwitcher.ApplyZoomDelta(state.zoomDelta);
            }

            if (state.cameraViewSelect >= 0)
            {
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
        if (agentPackMemberModule == null) return;

        if (state.requestedPlayerAgentIndex >= 0)
        {
            agentPackMemberModule.RequestBecomeControlledAgent(state.requestedPlayerAgentIndex);
        }

        if (state.changeFormationPressed)
        {
            agentPackMemberModule.CycleFormation();
        }
    }

    #endregion

    #region Movement

    private void HandleMovement(PlayerInputState state, float deltaTime)
    {
        if (worldObject.motionModule == null)
            return;

        Vector3 desiredMove = Vector3.zero;

        // 1) WASD / stick input
        if (state.moveAxis.sqrMagnitude > 0.0001f)
        {
            desiredMove = ConvertInputToWorldDirection(state.moveAxis);
        }

        // 2) Click-to-move: if we have a click target location and no interact press,
        //    you may want to move toward it. This is optional depending on your design.
        if (state.hasClickTargetLocationWorld && !state.interactPressed)
        {
            worldObject.motionModule.SetMoveTarget(state.clickTargetLocationWorld);
            // If you want click-to-move to override WASD, you can early return here.
        }

        // 3) If using analog WASD movement:
        if (!state.hasClickTargetLocationWorld && desiredMove.sqrMagnitude > 0.0001f)
        {
            worldObject.motionModule.SetMoveVector(desiredMove.normalized * moveSpeed);
        }
        else if (!state.hasClickTargetLocationWorld)
        {
            // No active target and no input: stop moving
            worldObject.motionModule.SetMoveVector(Vector3.zero);
        }

        // Optional: handle facing / rotation here if movementModule doesn't
        // e.g., rotate toward movement direction
        if (desiredMove.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(desiredMove, Vector3.up);
            worldObject.transform.rotation = Quaternion.RotateTowards(worldObject.transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
        }
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
        if (!state.interactPressed)
            return;

        // Priority 1: Interact with clicked object (if any)
        if (state.hasClickTargetWorldObject && state.clickTargetWorldObject != null)
        {
            // TODO: replace with your own interaction system.
            // Could be:
            //   InteractionSystem.Instance.RequestInteract(agentWorldObject, state.clickTargetWorldObject);
            // Or a message to an AgentActionModule.
            Debug.Log($"PlayerDecision: interact with object {state.clickTargetWorldObject.name}");
            return;
        }

        // Priority 2: No object, but clicked location → maybe bark, dig, etc.
        if (state.hasClickTargetLocationWorld)
        {
            // Example: move to that location and then interact
            if (worldObject.motionModule != null)
            {
                worldObject.motionModule.SetMoveTarget(state.clickTargetLocationWorld);
            }

            // Later you can chain a "context action" when you arrive (dig, sniff, etc.)
        }
        else
        {
            // No click info → generic interact (e.g. interact with nearest object in range)
            // TODO: hook to proximity interaction system / sniff, etc.
            Debug.Log("PlayerDecision: generic interact (no target).");
        }
    }

    #endregion
}