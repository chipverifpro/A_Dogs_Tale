using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.EventSystems;
using UnityEditor;

/// <summary>
/// Bridges Unity's new Input System (PlayerInput + InputActions)
/// into a single PlayerInputState struct that the rest of the game can consume.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
[DefaultExecutionOrder(0)]
public class NewInputAdapter : MonoBehaviour
{
    [Header("References")]
    public Directory dir;
    //public ConvertScreenToWorld convertScreenToWorld;  // move to directory

        
    [Header("Input System")]
    [SerializeField] private PlayerInput playerInput;
    public GameInputRouter gameInputRouter;

    // This is your game-side state container (a C# class, so will not show in Unity Editor)
    [Header("Adapter Output")]
    private PlayerInputState playerInputState;

    // Public read-only accessor so other scripts (and debugger) can use it
    public PlayerInputState InputState => playerInputState;

    //private DogInputActions inputActions;

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

    private CameraModes cameraMode = CameraModes.Perspective;

    
    private void Awake()
    {
        // Ensure we have the PlayerInput component
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();
        
        if (playerInput == null)
        {
            Debug.LogError("[NewInputAdapter] Missing PlayerInput component.", this);
            enabled = false;
        }

        if (GameInputRouter.Instance == null)
        {
            Debug.LogError("[NewInputAdapter] No GameInputRouter in scene.", this);
            //enabled = false;
            return;
        }
        
        playerInputState = GameInputRouter.Instance.InputState;
        
        CacheActions();

        Debug.Log("[NewInputAdapter] Awake: InputAdapter initialized.", this);
    }

    private void Start()
    {
        if (playerInput == null)
            return;

        if (playerInput.actions == null)
        {
            Debug.LogError("[NewInputAdapter] PlayerInput has no Actions asset assigned.", this);
            return;
        }

        var maps = playerInput.actions.actionMaps;
        foreach (var m in maps)
            m.Disable();

        var gameplayMap = playerInput.actions.FindActionMap("Player", false);
        if (gameplayMap == null)
        {
            Debug.LogError("[NewInputAdapter] Could not find 'Player' action map in actions asset.", this);
            return;
        }

        gameplayMap.Enable();

        if (gameInputRouter == null)
        {
            gameInputRouter = GameInputRouter.Instance;
            if (gameInputRouter == null)
            {
                Debug.LogError("[PlayerDecisionModule] No GameInputRouter in scene.", this);
                enabled = false;
                return;
            }
        }

        if (gameInputRouter.InputState == null)
        {
            Debug.LogError($"[PlayerInputStateDebugger] gameInputRouter.InputState is null.", this);
        }
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

        if (map==null) Debug.LogError($"NewInputAdapter.CacheActions: map == null");

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

    private void Update()
    {
        var state = gameInputRouter.InputState;


        // --- Movement axis ---
        state.moveAxis = moveAction != null
            ? moveAction.ReadValue<Vector2>()
            : Vector2.zero;

        if (state.moveAxis != Vector2.zero)
        {
            // moveAxis will interrupt travel to destination
            state.hasClickTargetWorldObject   = false;
            state.hasClickTargetLocationWorld = false;
        }

        // --- One-shot commands (per-frame triggers) ---
        state.barkPressed = barkAction != null && barkAction.triggered;
        state.markTerritoryPressed = markTerritoryAction != null && markTerritoryAction.triggered;


        // --- Camera commands (zoom / change view) ---
        if (IsMouseOverGameView())
        {
            state.zoomDelta = zoomAction != null
                ? zoomAction.ReadValue<float>()
                : 0f;    
        }
        else
        {
            // Ignore zoom while mouse is over Inspector/Console/etc.
            state.zoomDelta = 0f;        
        }


        // --- Camera View Select ---
        // By default, leave as "unchanged". If you have a dedicated action,
        // you can interpret its value here.
        //state.cameraViewSelect = CameraModes.Unchanged;
        if (cameraViewAction != null && cameraViewAction.triggered)
        {
            // Example: if cameraViewAction is a cycle-next-view button,
            // you might have another system interpret this trigger.
            // Instead, we cycle it here, probably shouldn't do it here.
            if (cameraMode == CameraModes.FP)
                cameraMode = CameraModes.Overhead;
            else if (cameraMode == CameraModes.Overhead)
                cameraMode = CameraModes.Perspective;
            else if (cameraMode == CameraModes.Perspective)
                cameraMode = CameraModes.FP;
            else cameraMode = CameraModes.FP;
            
            state.cameraViewSelect = cameraMode;
        } else
        {
            state.cameraViewSelect = CameraModes.Unchanged;
        }


        // --- Player change (next/previous) ---
        state.requestedPlayerAgentDelta = 0;
        // If you have a "next agent" action, you can treat this as "please change":
        //state.requestedPlayerAgentDelta = +1 / -1
        if (nextAgentAction != null && nextAgentAction.triggered)
        {
            state.requestedPlayerAgentDelta = (int)nextAgentAction.ReadValue<float>();
            // Let your pack/party manager decide what index this means.
            // Note that I specified it as a float in the GUI, but actually need an integer, thus the cast.
            // Here we just signal "+/- change" with 0 as no change.

            // example usage:
            //prevPlayerAgentIndex += state.requestedPlayerAgentDelta;
            //state.requestedPlayerAgentIndex = prevPlayerAgentIndex;
        }


        // --- Pack Formation change (next) ---
        state.changeFormationPressed = changeFormationAction != null && changeFormationAction.triggered;


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


        // --- State Modifiers (ie. Shift / RightMouse / TwoFingers / FaceButtonNorth) ---
        UpdateModifiers(state);


        // --- World & object targeting ---
        if (TryGetMouseClickScreenPosition(out Vector2 screenPosition))
        {
            Vector3 screenPosition3 = new(screenPosition.x, screenPosition.y, 0f);
            Debug.Log($"TryGetMouseClickScreenPosition returned {screenPosition:0}");
            // These are left for another system (raycaster / selection) to fill in.
            state.interactPressed      = interactAction != null && interactAction.triggered;
            state.selectObjectPressed  = selectObjectAction != null && selectObjectAction.triggered;

            state.screenCoordinateClicked = screenPosition;

            // (1) convert screen to world location and cell
            Vector3 ?worldLocation = dir.convertScreenToWorld.getWorldPointFromRaycast(screenPosition3);
            if (worldLocation != null)
            {
                state.hasClickTargetLocationWorld = true;
                state.clickTargetLocationWorld    = (Vector3)worldLocation;
                state.clickTargetLocationCell     = dir.convertScreenToWorld.ConvertWorldLocationToCell((Vector3)worldLocation);
                if (state.clickTargetLocationCell!=null)
                    Debug.Log($"Clicked on worldLocation {state.clickTargetLocationWorld} in cell at {state.clickTargetLocationCell.pos3d_world}");
                else
                    Debug.Log($"Clicked on worldLocation {state.clickTargetLocationWorld} but cell is null");
            }
            else
            {
                state.hasClickTargetLocationWorld = false;
                state.clickTargetLocationWorld    = Vector3.zero;
                state.clickTargetLocationCell     = null;
            }

            // (2) convert screen to targeted object
            WorldObject worldObject = dir.convertScreenToWorld.GetWorldObjectFromRaycast(screenPosition);
            if (worldObject!=null)
            {
                // If we want to limit the selection distance...
                // USE: bool CheckSelectionDistance(WorldObject currentSelection, WorldObject player)
                // then, hasClick = returned value
                state.hasClickTargetWorldObject   = true;
                state.clickTargetWorldObject      = worldObject;
                Debug.Log($"Clicked on worldObject {worldObject.name}");
            }
            else
            {
                state.hasClickTargetWorldObject   = false;
                state.clickTargetWorldObject      = null;
            }
        }
        else
        {

        }

        // --- Commit snapshot ---
        CurrentState = state;
    }

    public static bool TryGetMouseClickScreenPosition(out Vector2 screenPos)
    {
        screenPos = default;

        if (Mouse.current == null)
            return false;

        if (Mouse.current.leftButton.wasPressedThisFrame ||
            Mouse.current.rightButton.wasPressedThisFrame)
        {
            screenPos = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        return false;
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null 
            && EventSystem.current.IsPointerOverGameObject();
    }

    private bool IsMouseOverGameView()
    {
    #if UNITY_EDITOR
        var w = UnityEditor.EditorWindow.mouseOverWindow;

        // Not over any Unity window at all?
        if (w == null)
            return false;

        // Game view type name is "GameView"
        return w.GetType().Name == "GameView";
    #else
        // In a build, the only window is the game itself
        return (Application.isFocused && !IsPointerOverUI());
    #endif
    }


    private void UpdateModifiers(PlayerInputState state)
    {
        InputModifiers mods = InputModifiers.None;

        // --- Keyboard checks ---
        if (Keyboard.current != null)
        {
            if (Keyboard.current.shiftKey.isPressed)
                mods |= InputModifiers.Shift;

            if (Keyboard.current.ctrlKey.isPressed)
                mods |= InputModifiers.Ctrl;

            if (Keyboard.current.altKey.isPressed)
                mods |= InputModifiers.Alt;

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (Keyboard.current.leftCommandKey.isPressed || Keyboard.current.rightCommandKey.isPressed)
                mods |= InputModifiers.Command;
#endif
        }

        // --- Mouse button checks ---
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.isPressed)
                mods |= InputModifiers.LeftMouse;

            if (Mouse.current.rightButton.isPressed)
                mods |= InputModifiers.RightMouse;

            if (Mouse.current.middleButton.isPressed)
                mods |= InputModifiers.MiddleMouse;
        }

        // --- Touch checks ---
        int activeTouches = 0;
        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.isInProgress)
                    activeTouches++;
            }
        }

        if (activeTouches == 1) mods |= InputModifiers.OneFinger;
        if (activeTouches == 2) mods |= InputModifiers.TwoFingers;
        if (activeTouches == 3) mods |= InputModifiers.ThreeFingers;

        // Assign
        state.inputModifiers = mods;
    }
}