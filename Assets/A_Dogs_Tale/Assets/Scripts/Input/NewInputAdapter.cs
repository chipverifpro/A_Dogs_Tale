using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.EventSystems;

/// <summary>
/// Bridges Unity's new Input System (PlayerInput + InputActions)
/// into a single PlayerInputState struct that the rest of the game can consume.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class NewInputAdapter : MonoBehaviour
{
    [Header("Input System")]
    [SerializeField] private PlayerInput playerInput;

    // This is your game-side state container
    [Header("Adapter Output")]
    [SerializeField] private PlayerInputState playerInputState;

    // Public read-only accessor so other scripts (and debugger) can use it
    public PlayerInputState InputState => playerInputState;

    private DogInputActions inputActions;

    [Header("Action Names (must match your InputAction asset)")]
    [SerializeField] private string moveActionName            = "Move";
    [SerializeField] private string barkActionName            = "Bark";
    [SerializeField] private string markTerritoryActionName   = "MarkTerritory";
    [SerializeField] private string zoomActionName            = "Zoom";
    [SerializeField] private string changeFormationActionName = "ChangeFormation";
    [SerializeField] private string interactActionName        = "Interact";
    [SerializeField] private string selectObjectActionName    = "SelectObject";
    [SerializeField] private string skipAnyKeyActionName      = "Skip"; // generic "skip / any key"

    // If you have explicit actions for camera view / pack agent switching, you can add them here:
    [SerializeField] private string cameraViewActionName      = "";     // optional: change view
    [SerializeField] private string nextAgentActionName       = "";     // optional: cycle player agent

    // Latest snapshot of input. Other systems can read this.
    public PlayerInputState CurrentState { get; private set; }

    // cached actions
    private InputAction moveAction;
    private InputAction barkAction;
    private InputAction markTerritoryAction;
    private InputAction zoomAction;
    private InputAction changeFormationAction;
    private InputAction interactAction;
    private InputAction selectObjectAction;
    private InputAction skipAnyKeyAction;

    private InputAction cameraViewAction;
    private InputAction nextAgentAction;


    private void Awake()
    {
        // Ensure we have the PlayerInput component
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        // Create the state instance if not already assigned
        if (playerInputState == null)
        {
            playerInputState = new PlayerInputState();
            Debug.Log("[NewInputAdapter] Created new PlayerInputState instance.", this);
        }

        // Create & enable our generated actions wrapper
        inputActions = new DogInputActions();
        inputActions.Enable();

        CacheActions();

        Debug.Log("[NewInputAdapter] Awake: InputAdapter initialized.", this);
    }

    private void OLD_Awake()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        CacheActions();
    }

    private void OnEnable()
    {
        EnableActions(true);
    }

    private void OnDisable()
    {
        EnableActions(false);
    }

    private void CacheActions()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        var asset = playerInput.actions;
        // Use the currently active map; if none, let PlayerInput manage it.
        var map = playerInput.currentActionMap ?? asset.FindActionMap(playerInput.defaultActionMap, true);

        moveAction            = FindAction(map, moveActionName);
        barkAction            = FindAction(map, barkActionName);
        markTerritoryAction   = FindAction(map, markTerritoryActionName);
        zoomAction            = FindAction(map, zoomActionName);
        changeFormationAction = FindAction(map, changeFormationActionName);
        interactAction        = FindAction(map, interactActionName);
        selectObjectAction    = FindAction(map, selectObjectActionName);
        skipAnyKeyAction      = FindAction(map, skipAnyKeyActionName);

        cameraViewAction      = string.IsNullOrEmpty(cameraViewActionName) ? null : FindAction(map, cameraViewActionName);
        nextAgentAction       = string.IsNullOrEmpty(nextAgentActionName)  ? null : FindAction(map, nextAgentActionName);
    }

    private static InputAction FindAction(InputActionMap map, string name)
    {
        if (map == null || string.IsNullOrEmpty(name))
            return null;

        var action = map.FindAction(name, throwIfNotFound: false);
        if (action == null)
            Debug.LogWarning($"NewInputAdapter: Could not find action '{name}' in map '{map.name}'.");
        return action;
    }

    private void EnableActions(bool enable)
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        if (enable)
            playerInput.actions.Enable();
        else
            playerInput.actions.Disable();
    }

    private void Update_old()
    {
        var state = new PlayerInputState();

        // --- Movement axis ---
        state.moveAxis = moveAction != null
            ? moveAction.ReadValue<Vector2>()
            : Vector2.zero;

        // --- One-shot commands (per-frame triggers) ---
        state.barkPressed = barkAction != null && barkAction.triggered;
        state.markTerritoryPressed = markTerritoryAction != null && markTerritoryAction.triggered;

        // --- Camera commands ---
        state.zoomDelta = zoomAction != null
            ? zoomAction.ReadValue<float>()
            : 0f;

        // cameraViewSelect:
        // By default, leave as "unchanged". If you have a dedicated action,
        // you can interpret its value here.
        state.cameraViewSelect = CameraModes.Unchanged;
        if (cameraViewAction != null && cameraViewAction.triggered)
        {
            // Example: if cameraViewAction is a cycle-next-view button,
            // you might have another system interpret this trigger.
            // For now we just flag that a change was requested:
            if (state.cameraViewSelect == CameraModes.FP)
                state.cameraViewSelect = CameraModes.Overhead;
            else if (state.cameraViewSelect == CameraModes.Overhead)
                state.cameraViewSelect = CameraModes.Perspective;
            else if (state.cameraViewSelect == CameraModes.Perspective)
                state.cameraViewSelect = CameraModes.FP;
        }

        // --- Player/pack changes ---
        // If you have a "next agent" action, you can treat this as "please change":
        state.requestedPlayerAgentIndex = -1; // -1 = no change
        if (nextAgentAction != null && nextAgentAction.triggered)
        {
            // Let your pack/party manager decide what index this means.
            // Here we just signal "some change" with 0 as a placeholder.
            state.requestedPlayerAgentIndex = 0;
        }

        state.changeFormationPressed = changeFormationAction != null && changeFormationAction.triggered;

        // --- World & object targeting ---
        // These are left for another system (raycaster / selection) to fill in.
        state.interactPressed      = interactAction != null && interactAction.triggered;
        state.selectObjectPressed  = selectObjectAction != null && selectObjectAction.triggered;

        state.hasClickTargetLocationWorld = false;
        state.clickTargetLocationWorld    = Vector3.zero;

        state.hasClickTargetWorldObject   = false;
        state.clickTargetWorldObject      = null;

        // --- Skip / any key-or-button ---
        bool anyKeyLogical = false;
        if (skipAnyKeyAction != null && skipAnyKeyAction.triggered)
            anyKeyLogical = true;

        // You can optionally OR in "real" any-key behavior from devices:
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            anyKeyLogical = true;

        if (Gamepad.current != null)
        {
            // Cheap check: if any button was pressed this frame
            foreach (var control in Gamepad.current.allControls)
            {
                if (control is ButtonControl button && button.wasPressedThisFrame)
                {
                    anyKeyLogical = true;
                    break;
                }
            }
        }

        state.anyKeyOrButtonDown = anyKeyLogical;

        // Commit snapshot
        CurrentState = state;

        bool enableDebugLogging=true;
        if (enableDebugLogging && Time.frameCount % 15 == 0)
            {
                Debug.Log(
                    $"[NewInputAdapter] " +
                    $"Move={state.moveAxis} Zoom={state.zoomDelta:F2} " +
                    $"MarkTerritory={state.markTerritoryPressed} BarkDown={state.barkPressed}",
                    this);
            }
    }

    private void Update()
    {
        if (inputActions == null || playerInputState == null)
            return;

        // Use your actual map name: Player or Gameplay
        var map = inputActions.Player;

        Vector2 moveVector = map.Move.ReadValue<Vector2>();
        float zoomAxis     = map.Zoom.ReadValue<float>();
        bool markTerritoryPressed   = map.MarkTerritory.WasPressedThisFrame();
        bool barkPressed   = map.Bark.WasPressedThisFrame();

        if (IsPointerOverUI())
        {
            // Ignore zoom while mouse is over Inspector/Console/etc.
            zoomAxis = 0f;
        }
        // *** CRITICAL: write directly into the shared instance ***
        playerInputState.moveAxis           = moveVector;
        playerInputState.zoomDelta            = zoomAxis;
        playerInputState.markTerritoryPressed = markTerritoryPressed;
        playerInputState.barkPressed = barkPressed;

        bool enableDebugLogging=false;
        if (enableDebugLogging && Time.frameCount % 15 == 0)
        {
            Debug.Log(
                $"[NewInputAdapter] Move={moveVector} Zoom={zoomAxis:F2} " +
                $"MarkTerritory={markTerritoryPressed} Bark={barkPressed}",
                this);
        }
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null 
            && EventSystem.current.IsPointerOverGameObject();
    }
}