using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Bridges Unity's new Input System (PlayerInput + InputActions)
/// into a single PlayerInputState struct that the rest of the game can consume.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
[DefaultExecutionOrder(0)]
public class NewInputAdapter : MonoBehaviour
{
    [Header("References")]
    public Dir dir;
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
    [SerializeField] private string strafeActionName          = "Strafe";
    [SerializeField] private string barkActionName            = "Bark";
    [SerializeField] private string markTerritoryActionName   = "MarkTerritory";
    [SerializeField] private string digActionName             = "Dig";
    [SerializeField] private string pauseActionName           = "Pause";
    [SerializeField] private string zoomActionName            = "Zoom";
    [SerializeField] private string changeFormationActionName = "ChangeFormation";
    [SerializeField] private string interactActionName        = "Interact";
    [SerializeField] private string selectObjectActionName    = "SelectObject";
    [SerializeField] private string skipAnyKeyActionName      = "Skip"; // generic "skip / any key"
    [SerializeField] private string popupTab1ActionName       = "PopupTab1";
    [SerializeField] private string popupTab2ActionName       = "PopupTab2";
    [SerializeField] private string popupTab3ActionName       = "PopupTab3";
    [SerializeField] private string popupTab4ActionName       = "PopupTab4";

    // If you have explicit actions for camera view / pack agent switching, you can add them here:
    [SerializeField] private string cameraViewActionName      = "";     // optional: change view
    [SerializeField] private string nextAgentActionName       = "";     // optional: cycle player agent

    [Header("Tap (click-to-move)")]
    [SerializeField] private float tapMaxSeconds = 0.30f;      // must be < holdToOpenSeconds (default in WheelOpener is 0.45) for the wheel
    [SerializeField] private float tapMaxMovePixels = 18f;     // also must match.

    [Header("Mobile Touch Movement")]
    [SerializeField] private bool enableMobileJoystick = true;
    [SerializeField] private bool showMobileJoystickInEditor = false;
    [SerializeField] private Vector2 mobileJoystickMarginPixels = new(36f, 36f);
    [SerializeField] private float mobileJoystickRadiusPixels = 88f;
    [SerializeField, Min(0.1f)] private float mobileJoystickRadiusMultiplier = 3f;
    [SerializeField] private bool mobileJoystickFloatsToPress = true;
    [SerializeField] private float mobileJoystickDragActivationPixels = 18f;
    [SerializeField] private float mobileJoystickActivationWidthPercent = 0.45f;
    [SerializeField] private float mobileJoystickActivationHeightPercent = 0.55f;
    [SerializeField, Range(0f, 0.5f)] private float mobileJoystickDeadZone = 0.12f;
    [SerializeField, Range(0f, 1f)] private float mobileJoystickForwardTurnDeadZone = 0.22f;
    [SerializeField] private bool digitalMobileJoystick = false;
    [SerializeField] private Color mobileJoystickBaseColor = new(1f, 1f, 1f, 0.20f);
    [SerializeField] private Color mobileJoystickKnobColor = new(1f, 1f, 1f, 0.45f);

    [Header("Mobile Pinch Zoom")]
    [SerializeField] private bool enableMobilePinchZoom = true;
    [SerializeField] private float pinchZoomPixelsPerStep = 80f;
    [SerializeField] private float pinchZoomMaxDeltaPerFrame = 3f;
    [SerializeField] private float pinchZoomMinDistancePixels = 24f;

    // Latest snapshot of input. Other systems can read this.
    public PlayerInputState CurrentState { get; private set; }

    // cached actions
    private InputAction moveAction;
    private InputAction strafeAction;
    private InputAction barkAction;
    private InputAction markTerritoryAction;
    private InputAction digAction;
    private InputAction pauseAction;
    private InputAction zoomAction;
    private InputAction changeFormationAction;
    private InputAction interactAction;
    private InputAction selectObjectAction;
    private InputAction skipAnyKeyAction;
    private InputAction popupTab1Action;
    private InputAction popupTab2Action;
    private InputAction popupTab3Action;
    private InputAction popupTab4Action;

    private InputAction cameraViewAction;
    private InputAction nextAgentAction;

    private CameraModes cameraMode = CameraModes.Perspective;

    // track long press versus click
    private bool isPrimaryPressTracking;
    private double primaryPressStartTime;
    private Vector2 primaryPressStartPos;
    private int primaryPressPointerId; // -1 mouse, touchId for touch

    private const int NoMobileJoystickPointer = int.MinValue;
    private static int reservedMobileJoystickPointerId = NoMobileJoystickPointer;
    private static bool mobileJoystickVisibleForOtherInput;
    private static Rect mobileJoystickActivationRectForOtherInput;

    private Canvas mobileJoystickCanvas;
    private RectTransform mobileJoystickBase;
    private RectTransform mobileJoystickKnob;
    private int mobileJoystickPointerId = NoMobileJoystickPointer;
    private int mobileJoystickCandidatePointerId = NoMobileJoystickPointer;
    private Vector2 mobileJoystickCandidateStartScreen;
    private Vector2 mobileJoystickCenterScreen;
    private Vector2 mobileJoystickAxis;

    private bool isPinchZoomActive;
    private float previousPinchDistancePixels;
    
    public static bool IsPointerReservedForMobileJoystick(int pointerId)
    {
        return pointerId == reservedMobileJoystickPointerId;
    }

    public static bool IsScreenPositionInMobileJoystickZone(Vector2 screenPosition)
    {
        return mobileJoystickVisibleForOtherInput &&
               mobileJoystickActivationRectForOtherInput.Contains(screenPosition);
    }

    public bool MobileJoystickVisiblePreference => enableMobileJoystick;
    public bool DigitalMobileJoystickPreference => digitalMobileJoystick;

    public void SetMobileJoystickVisiblePreference(bool visible)
    {
        enableMobileJoystick = visible;
        if (visible)
            return;

        ClearMobileJoystick();
        SetMobileJoystickUiVisible(false);
        UpdateMobileJoystickSharedState(false);
    }

    public void SetDigitalMobileJoystickPreference(bool enabled)
    {
        digitalMobileJoystick = enabled;
    }

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

        TryBindRuntimeState(logFailure: true);

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

        TryBindRuntimeState(logFailure: true);

        ConfigureUiInputModule();
        PersistentGameSettings.ApplySavedToInputAdapter(this);

        var existingWheelUi = DogGame.UI.InteractionWheel.MenuWheelUIFactory.TryGetExisting();
        if (existingWheelUi != null)
        {
            existingWheelUi.CloseMenuWheel();
            Debug.Log("[NewInputAdapter] Closed existing MenuWheelUI during startup.", existingWheelUi);
        }
    }

    private void OnEnable()
    {
        TryBindRuntimeState(logFailure: false);
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
        strafeAction          = FindAction(map, strafeActionName);
        barkAction            = FindAction(map, barkActionName);
        markTerritoryAction   = FindAction(map, markTerritoryActionName);
        digAction             = FindAction(map, digActionName);
        pauseAction           = FindAction(map, pauseActionName);
        zoomAction            = FindAction(map, zoomActionName);
        changeFormationAction = FindAction(map, changeFormationActionName);
        interactAction        = FindAction(map, interactActionName);
        selectObjectAction    = FindAction(map, selectObjectActionName);
        skipAnyKeyAction      = FindAction(map, skipAnyKeyActionName);
        popupTab1Action       = FindAction(map, popupTab1ActionName);
        popupTab2Action       = FindAction(map, popupTab2ActionName);
        popupTab3Action       = FindAction(map, popupTab3ActionName);
        popupTab4Action       = FindAction(map, popupTab4ActionName);

        cameraViewAction      = string.IsNullOrEmpty(cameraViewActionName) ? null : FindAction(map, cameraViewActionName);
        nextAgentAction       = string.IsNullOrEmpty(nextAgentActionName)  ? null : FindAction(map, nextAgentActionName);
    }

    private bool TryBindRuntimeState(bool logFailure)
    {
        if (dir == null)
            dir = Dir.Instance;

        if (gameInputRouter == null)
            gameInputRouter = GameInputRouter.Instance;

        if (gameInputRouter == null)
        {
            if (logFailure)
                Debug.LogWarning("[NewInputAdapter] Waiting for GameInputRouter after reload.", this);
            return false;
        }

        if (gameInputRouter.InputState == null)
        {
            if (logFailure)
                Debug.LogWarning("[NewInputAdapter] GameInputRouter.InputState is null.", this);
            return false;
        }

        playerInputState = gameInputRouter.InputState;
        CacheActions();
        return dir != null;
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

    private void ConfigureUiInputModule()
    {
        if (playerInput == null || playerInput.actions == null)
        {
            Debug.LogWarning("[NewInputAdapter] Cannot configure UI input module because PlayerInput actions are unavailable.", this);
            return;
        }

        if (EventSystem.current == null)
        {
            Debug.LogWarning("[NewInputAdapter] No EventSystem present; UI will not receive pointer clicks.", this);
            return;
        }

        var uiModule = EventSystem.current.currentInputModule as InputSystemUIInputModule;
        if (uiModule == null)
        {
            uiModule = EventSystem.current.GetComponent<InputSystemUIInputModule>();
        }

        if (uiModule == null)
        {
            Debug.LogWarning("[NewInputAdapter] EventSystem has no InputSystemUIInputModule; UI clicks may be ignored.", EventSystem.current);
            return;
        }

        var actions = playerInput.actions;
        uiModule.actionsAsset = actions;
        uiModule.move = CreateActionReference(actions, "UI/Navigate");
        uiModule.submit = CreateActionReference(actions, "UI/Submit");
        uiModule.cancel = CreateActionReference(actions, "UI/Cancel");
        uiModule.point = CreateActionReference(actions, "UI/Point");
        uiModule.leftClick = CreateActionReference(actions, "UI/Click");
        uiModule.middleClick = CreateActionReference(actions, "UI/MiddleClick");
        uiModule.rightClick = CreateActionReference(actions, "UI/RightClick");
        uiModule.scrollWheel = CreateActionReference(actions, "UI/ScrollWheel");
        uiModule.trackedDevicePosition = CreateActionReference(actions, "UI/TrackedDevicePosition");
        uiModule.trackedDeviceOrientation = CreateActionReference(actions, "UI/TrackedDeviceOrientation");

        Debug.Log(
            $"[NewInputAdapter] Configured InputSystemUIInputModule on '{EventSystem.current.name}' using actions asset '{actions.name}'. " +
            $"point={(uiModule.point != null ? uiModule.point.action?.name : "null")} " +
            $"leftClick={(uiModule.leftClick != null ? uiModule.leftClick.action?.name : "null")}",
            uiModule);
    }

    private InputActionReference CreateActionReference(InputActionAsset asset, string actionPath)
    {
        var action = asset.FindAction(actionPath, throwIfNotFound: false);
        if (action == null)
        {
            Debug.LogWarning($"[NewInputAdapter] UI action '{actionPath}' was not found in actions asset '{asset.name}'.", this);
            return null;
        }

        return InputActionReference.Create(action);
    }

    private void LogUiRaycastHits(Vector2 screenPos, int maxHits = 8)
    {
        if (EventSystem.current == null)
            return;

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPos
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        if (results.Count == 0)
        {
            Debug.Log($"[NewInputAdapter] UI raycast found no hits at {screenPos}.");
            return;
        }

        int count = Mathf.Min(results.Count, maxHits);
        for (int i = 0; i < count; i++)
        {
            var result = results[i];
            string path = result.gameObject != null ? GetTransformPath(result.gameObject.transform) : "null";
//            Debug.Log(
//                $"[NewInputAdapter] UI raycast hit[{i}] go='{result.gameObject?.name}' path='{path}' " +
//                $"module='{result.module?.GetType().Name}' sortOrder={result.sortingOrder} depth={result.depth} distance={result.distance}");
        }
    }

    private static string GetTransformPath(Transform current)
    {
        if (current == null)
            return "null";

        var parts = new List<string>();
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private void Update()
    {
        if (!TryBindRuntimeState(logFailure: false))
            return;

        var state = gameInputRouter.InputState;
        Vector2 joystickAxis = UpdateMobileJoystick();
        state.overheadWorldMoveFromMobileJoystick = false;


        // --- Movement axis ---
        state.moveAxis = moveAction != null
            ? moveAction.ReadValue<Vector2>()
            : Vector2.zero;

        bool hasMobileJoystickInput = joystickAxis.sqrMagnitude > 0.0001f;
        if (hasMobileJoystickInput)
        {
            if (ShouldUseOverheadWorldMobileJoystickMode())
            {
                state.moveAxis = joystickAxis;
                state.overheadWorldMoveFromMobileJoystick = true;
            }
            else
            {
                state.moveAxis = ApplyMobileJoystickMode(joystickAxis);
            }
        }

        if (state.moveAxis != Vector2.zero)
        {
            // moveAxis will interrupt travel to destination
            state.hasClickTargetWorldObject   = false;
            state.hasClickTargetLocationWorld = false;
            state.hasPendingClickTargetLocationWorld = false;
        }
        // -- Strafe Movement ---
        state.strafeAxis = strafeAction != null
            ? strafeAction.ReadValue<float>()
            : 0f;
        if (state.overheadWorldMoveFromMobileJoystick)
            state.strafeAxis = 0f;

        if (state.strafeAxis != 0f)
        {
            // like moveAxis, strafeAcis will also interrupt travel to destination
            state.hasClickTargetWorldObject   = false;
            state.hasClickTargetLocationWorld = false;
            state.hasPendingClickTargetLocationWorld = false;
        }

        // --- One-shot commands (per-frame triggers) ---
        state.barkPressed = barkAction != null && barkAction.triggered;
        state.markTerritoryPressed = markTerritoryAction != null && markTerritoryAction.triggered;
        state.digPressed = (digAction != null && digAction.triggered) ||
                           (digAction == null && Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame);
        state.pausePressed = pauseAction != null && pauseAction.triggered;
        state.requestedPopupTabIndex = 0;
        if (popupTab1Action != null && popupTab1Action.triggered) state.requestedPopupTabIndex = 1;
        else if (popupTab2Action != null && popupTab2Action.triggered) state.requestedPopupTabIndex = 2;
        else if (popupTab3Action != null && popupTab3Action.triggered) state.requestedPopupTabIndex = 3;
        else if (popupTab4Action != null && popupTab4Action.triggered) state.requestedPopupTabIndex = 4;


        // --- Camera commands (zoom / change view) ---
        if (IsMouseOverGameView())
        {
            state.zoomDelta = zoomAction != null
                ? zoomAction.ReadValue<float>()
                : 0f;
            state.zoomDelta += GetMobilePinchZoomDelta();
            if (IsMousePointerOverInteractionDialogScrollableList())
                state.zoomDelta = 0f;
        }
        else
        {
            // Ignore zoom while mouse is over Inspector/Console/etc.
            state.zoomDelta = 0f;
            ClearPinchZoomTracking();
        }


        // --- Camera View Select ---
        // By default, leave as "unchanged". If you have a dedicated action,
        // you can interpret its value here.
        //state.cameraViewSelect = CameraModes.Unchanged;
        if (cameraViewAction != null && cameraViewAction.triggered)
        {
            cameraMode = CameraModeSwitcher.GetNextViewMode(cameraMode);
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

        // If the wheel is open, don't generate click-to-move targets this frame.
        var wheelUI = DogGame.UI.InteractionWheel.MenuWheelUIFactory.TryGetExisting();
        if (wheelUI != null && wheelUI.IsOpen)
        {
            state.hasClickTargetLocationWorld = false;
            state.hasPendingClickTargetLocationWorld = false;
            state.hasClickTargetWorldObject = false;
            CurrentState = state;
            return;
        }

        // --- World & object targeting ---
        if (TryGetTapScreenPosition(out Vector2 screenPosition))
        {
            Vector3 screenPosition3 = new(screenPosition.x, screenPosition.y, 0f);
            //Debug.Log($"TryGetMouseClickScreenPosition returned {screenPosition:0}");
            // These are left for another system (raycaster / selection) to fill in.
            state.interactPressed      = interactAction != null && interactAction.triggered;
            state.selectObjectPressed  = selectObjectAction != null && selectObjectAction.triggered;

            state.screenCoordinateClicked = screenPosition;

            if (dir == null || dir.convertScreenToWorld == null)
            {
                state.hasClickTargetLocationWorld = false;
                state.hasPendingClickTargetLocationWorld = false;
                state.hasClickTargetWorldObject = false;
                CurrentState = state;
                return;
            }

            // (1) convert screen to world location and cell
            Vector3 ?worldLocation = dir.convertScreenToWorld.getWorldPointFromRaycast(screenPosition3);
            if (worldLocation != null)
            {
                state.hasClickTargetLocationWorld = true;
                state.hasPendingClickTargetLocationWorld = true;
                state.clickTargetLocationCell     = dir.convertScreenToWorld.ConvertWorldLocationToCell((Vector3)worldLocation);
                state.clickTargetLocationWorld    = state.clickTargetLocationCell != null
                    ? state.clickTargetLocationCell.pos3d_world
                    : (Vector3)worldLocation;

                if (state.clickTargetLocationCell!=null)
                    Debug.Log($"Clicked on worldLocation {worldLocation} using cell center at {state.clickTargetLocationCell.pos3d_world}");
                else
                    Debug.Log($"Clicked on worldLocation {state.clickTargetLocationWorld} but cell is null");
            }
            else
            {
                state.hasClickTargetLocationWorld = false;
                state.hasPendingClickTargetLocationWorld = false;
                state.clickTargetLocationWorld    = Vector3.zero;
                state.clickTargetLocationCell     = null;
            }

            // (2) convert screen to targeted object
            //Debug.Log($"dir = {dir}, convertScreenToWorld = {dir.convertScreenToWorld}, screenPosition3 = {screenPosition3}");

            WorldObject targetWorldObject = dir.convertScreenToWorld.GetWorldObjectFromRaycast(screenPosition3);
            if (targetWorldObject!=null)
            {
                // If we want to limit the selection distance...
                // USE: bool CheckSelectionDistance(WorldObject currentSelection, WorldObject player)
                // then, hasClick = returned value
                state.hasClickTargetWorldObject   = true;
                state.clickTargetWorldObject      = targetWorldObject;
                Debug.Log($"Clicked on worldObject {targetWorldObject.name}");
            }
            else
            {
                state.hasClickTargetWorldObject   = false;
                state.clickTargetWorldObject      = null;
                //Debug.Log($"Clicked but no worldObject found");
            }
        }
        else
        {
            state.hasClickTargetWorldObject   = false;
        }

        // --- Commit snapshot ---
        CurrentState = state;
    }

    public bool TryGetTapScreenPosition(out Vector2 screenPos)
    {
        screenPos = default;

        if (isPinchZoomActive)
            return false;

        // 1) Touch has priority
        if (Touchscreen.current != null)
        {
            // Find a touch that is either starting, in progress, or just ended.
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch == null) continue;

                int touchId = touch.touchId.ReadValue();
                if (IsPointerReservedForMobileJoystick(touchId))
                    continue;

                bool pressedThisFrame = touch.press.wasPressedThisFrame;
                bool isPressed = touch.press.isPressed;
                bool releasedThisFrame = touch.press.wasReleasedThisFrame;

                if (pressedThisFrame)
                {
                    Vector2 pressPos = touch.position.ReadValue();
                    if (IsPointerOverUI(touchId, pressPos))
                    {
                        Debug.Log($"[NewInputAdapter] Ignoring touch press over UI. touchId={touchId} pos={pressPos}");
                        ClearTapTracking();
                        return false;
                    }

                    // Start tracking
                    isPrimaryPressTracking = true;
                    primaryPressStartTime = Time.unscaledTimeAsDouble;
                    primaryPressStartPos = pressPos;
                    primaryPressPointerId = touchId;
                    return false;
                }

                // Only consider ending the same touch we started with
                if (isPrimaryPressTracking && primaryPressPointerId == touchId)
                {
                    if (isPressed)
                    {
                        // Still down; if it moves too far, cancel the tap (becomes drag/hold)
                        Vector2 currentPos = touch.position.ReadValue();
                        float moved = Vector2.Distance(currentPos, primaryPressStartPos);
                        if (moved > tapMaxMovePixels)
                        {
                            ClearTapTracking();
                        }
                        return false;
                    }

                    if (releasedThisFrame)
                    {
                        Vector2 releasePos = touch.position.ReadValue();
                        double held = Time.unscaledTimeAsDouble - primaryPressStartTime;
                        float moved = Vector2.Distance(releasePos, primaryPressStartPos);

                        bool isTap = held <= tapMaxSeconds && moved <= tapMaxMovePixels;

                        ClearTapTracking();

                        if (isTap && !IsPointerOverUI(touchId, releasePos))
                        {
                            Debug.Log($"[NewInputAdapter] Touch tap accepted for world input. touchId={touchId} pos={releasePos}");
                            screenPos = releasePos;
                            return true;
                        }
                        if (isTap)
                        {
                            Debug.Log($"[NewInputAdapter] Touch tap released over UI, suppressing world input. touchId={touchId} pos={releasePos}");
                        }
                    }
                }
            }

            // If tracking but we didn't find that touch anymore, clear (edge case)
            // (e.g., touch canceled)
            // Note: conservative; avoids stuck tracking.
        }

        // 2) Mouse
        if (Mouse.current == null)
            return false;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (IsShiftClickModifierActive())
            {
                ClearTapTracking();
                return false;
            }

            Vector2 mousePos = Mouse.current.position.ReadValue();
            if (IsPointerOverUI(-1, mousePos))
            {
                //Debug.Log($"[NewInputAdapter] Ignoring mouse press over UI. pos={mousePos}");
                LogUiRaycastHits(mousePos);
                ClearTapTracking();
                return false;
            }

            isPrimaryPressTracking = true;
            primaryPressStartTime = Time.unscaledTimeAsDouble;
            primaryPressStartPos = mousePos;
            primaryPressPointerId = -1;
            return false;
        }

        if (isPrimaryPressTracking && primaryPressPointerId == -1)
        {
            Vector2 currentPos = Mouse.current.position.ReadValue();

            // Cancel tap if user drags too far while holding
            if (Mouse.current.leftButton.isPressed)
            {
                float moved = Vector2.Distance(currentPos, primaryPressStartPos);
                if (moved > tapMaxMovePixels)
                    ClearTapTracking();

                return false;
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                double held = Time.unscaledTimeAsDouble - primaryPressStartTime;
                float moved = Vector2.Distance(currentPos, primaryPressStartPos);

                bool isTap = held <= tapMaxSeconds && moved <= tapMaxMovePixels;
                bool suppressTap = IsShiftClickModifierActive();

                ClearTapTracking();

                if (isTap && suppressTap)
                    return false;

                if (isTap && !IsPointerOverUI(-1, currentPos))
                {
                    Debug.Log($"[NewInputAdapter] Mouse tap accepted for world input. pos={currentPos}");
                    screenPos = currentPos;
                    return true;
                }
                if (isTap)
                {
                    Debug.Log($"[NewInputAdapter] Mouse tap released over UI, suppressing world input. pos={currentPos}");
                }
            }
        }

        return false;
    }

    private float GetMobilePinchZoomDelta()
    {
        if (!enableMobilePinchZoom)
        {
            ClearPinchZoomTracking();
            return 0f;
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null)
        {
            ClearPinchZoomTracking();
            return 0f;
        }

        if (!TryGetTwoPinchTouches(touchscreen, out Vector2 first, out Vector2 second))
        {
            ClearPinchZoomTracking();
            return 0f;
        }

        float distance = Vector2.Distance(first, second);
        if (distance < pinchZoomMinDistancePixels)
        {
            ClearPinchZoomTracking();
            return 0f;
        }

        if (!isPinchZoomActive)
        {
            isPinchZoomActive = true;
            previousPinchDistancePixels = distance;
            ClearTapTracking();
            return 0f;
        }

        float pixelDelta = distance - previousPinchDistancePixels;
        previousPinchDistancePixels = distance;

        float pixelsPerStep = Mathf.Max(1f, pinchZoomPixelsPerStep);
        float zoomDelta = pixelDelta / pixelsPerStep;
        return Mathf.Clamp(zoomDelta, -pinchZoomMaxDeltaPerFrame, pinchZoomMaxDeltaPerFrame);
    }

    private bool TryGetTwoPinchTouches(Touchscreen touchscreen, out Vector2 first, out Vector2 second)
    {
        first = default;
        second = default;
        int found = 0;

        for (int i = 0; i < touchscreen.touches.Count; i++)
        {
            var touch = touchscreen.touches[i];
            if (touch == null || !touch.press.isPressed)
                continue;

            int touchId = touch.touchId.ReadValue();
            if (IsPointerReservedForMobileJoystick(touchId))
                continue;

            Vector2 position = touch.position.ReadValue();
            if (IsPointerOverUI(touchId, position))
                continue;

            if (found == 0)
                first = position;
            else
                second = position;

            found++;
            if (found >= 2)
                return true;
        }

        return false;
    }

    private void ClearPinchZoomTracking()
    {
        isPinchZoomActive = false;
        previousPinchDistancePixels = 0f;
    }

    private Vector2 UpdateMobileJoystick()
    {
        bool visible = ShouldShowMobileJoystick();
        EnsureMobileJoystickUi();
        SetMobileJoystickUiVisible(visible);
        UpdateMobileJoystickSharedState(visible);

        if (!visible)
        {
            ClearMobileJoystick();
            return Vector2.zero;
        }

        if (!mobileJoystickFloatsToPress)
        {
            mobileJoystickCenterScreen = GetMobileJoystickCenterScreen();
            SetMobileJoystickBasePosition(mobileJoystickCenterScreen);
        }

        if (TryUpdateMobileJoystickFromTouch(out Vector2 touchAxis))
        {
            UpdateMobileJoystickSharedState(visible);
            return touchAxis;
        }

        if (TryUpdateMobileJoystickFromMouse(out Vector2 mouseAxis))
        {
            UpdateMobileJoystickSharedState(visible);
            return mouseAxis;
        }

        ClearMobileJoystick();
        UpdateMobileJoystickSharedState(visible);
        return Vector2.zero;
    }

    private bool TryUpdateMobileJoystickFromTouch(out Vector2 axis)
    {
        axis = Vector2.zero;

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null)
            return false;

        for (int i = 0; i < touchscreen.touches.Count; i++)
        {
            var touch = touchscreen.touches[i];
            if (touch == null)
                continue;

            int touchId = touch.touchId.ReadValue();
            Vector2 position = touch.position.ReadValue();
            bool pressedThisFrame = touch.press.wasPressedThisFrame;
            bool isPressed = touch.press.isPressed;
            bool releasedThisFrame = touch.press.wasReleasedThisFrame;

            if (mobileJoystickFloatsToPress && mobileJoystickPointerId == NoMobileJoystickPointer)
            {
                if (mobileJoystickCandidatePointerId == NoMobileJoystickPointer)
                {
                    if (!pressedThisFrame || !IsInMobileJoystickActivationZone(position) || IsPointerOverUI(touchId, position))
                        continue;

                    mobileJoystickCandidatePointerId = touchId;
                    mobileJoystickCandidateStartScreen = position;
                    mobileJoystickCenterScreen = position;
                    SetMobileJoystickBasePosition(mobileJoystickCenterScreen);
                    SetMobileJoystickKnobOffset(Vector2.zero);
                    SetMobileJoystickControlsVisible(true);
                }

                if (mobileJoystickCandidatePointerId != touchId)
                    continue;

                if (!isPressed || releasedThisFrame)
                {
                    ClearMobileJoystick();
                    return false;
                }

                float activationPixels = Mathf.Max(0f, mobileJoystickDragActivationPixels);
                if (Vector2.Distance(position, mobileJoystickCandidateStartScreen) < activationPixels)
                {
                    axis = Vector2.zero;
                    return true;
                }

                mobileJoystickPointerId = touchId;
                reservedMobileJoystickPointerId = touchId;
                mobileJoystickCenterScreen = mobileJoystickCandidateStartScreen;
                SetMobileJoystickBasePosition(mobileJoystickCenterScreen);
                ClearTapTracking();
            }
            else if (mobileJoystickPointerId == NoMobileJoystickPointer)
            {
                if (!pressedThisFrame || !IsInMobileJoystickActivationZone(position))
                    continue;

                mobileJoystickPointerId = touchId;
                reservedMobileJoystickPointerId = touchId;
                ClearTapTracking();
            }

            if (mobileJoystickPointerId != touchId)
                continue;

            if (!isPressed || releasedThisFrame)
            {
                ClearMobileJoystick();
                return false;
            }

            axis = CalculateMobileJoystickAxis(position);
            return true;
        }

        return false;
    }

    private bool TryUpdateMobileJoystickFromMouse(out Vector2 axis)
    {
        axis = Vector2.zero;

#if UNITY_EDITOR
        if (!showMobileJoystickInEditor)
            return false;

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return false;

        Vector2 position = mouse.position.ReadValue();
        if (mobileJoystickFloatsToPress && mobileJoystickPointerId == NoMobileJoystickPointer)
        {
            if (mobileJoystickCandidatePointerId == NoMobileJoystickPointer)
            {
                if (!mouse.leftButton.wasPressedThisFrame || !IsInMobileJoystickActivationZone(position) || IsPointerOverUI(-1, position))
                    return false;

                mobileJoystickCandidatePointerId = -1;
                mobileJoystickCandidateStartScreen = position;
                mobileJoystickCenterScreen = position;
                SetMobileJoystickBasePosition(mobileJoystickCenterScreen);
                SetMobileJoystickKnobOffset(Vector2.zero);
                SetMobileJoystickControlsVisible(true);
            }

            if (mobileJoystickCandidatePointerId != -1)
                return false;

            if (!mouse.leftButton.isPressed)
            {
                ClearMobileJoystick();
                return false;
            }

            float activationPixels = Mathf.Max(0f, mobileJoystickDragActivationPixels);
            if (Vector2.Distance(position, mobileJoystickCandidateStartScreen) < activationPixels)
            {
                axis = Vector2.zero;
                return true;
            }

            mobileJoystickPointerId = -1;
            reservedMobileJoystickPointerId = -1;
            mobileJoystickCenterScreen = mobileJoystickCandidateStartScreen;
            SetMobileJoystickBasePosition(mobileJoystickCenterScreen);
            ClearTapTracking();
        }
        else if (mobileJoystickPointerId == NoMobileJoystickPointer)
        {
            if (!mouse.leftButton.wasPressedThisFrame || !IsInMobileJoystickActivationZone(position))
                return false;

            mobileJoystickPointerId = -1;
            reservedMobileJoystickPointerId = -1;
            ClearTapTracking();
        }

        if (mobileJoystickPointerId != -1)
            return false;

        if (!mouse.leftButton.isPressed)
        {
            ClearMobileJoystick();
            return false;
        }

        axis = CalculateMobileJoystickAxis(position);
        return true;
#else
        return false;
#endif
    }

    private Vector2 CalculateMobileJoystickAxis(Vector2 pointerScreenPosition)
    {
        Vector2 delta = pointerScreenPosition - mobileJoystickCenterScreen;
        float radius = GetMobileJoystickRadiusPixels();
        Vector2 clamped = Vector2.ClampMagnitude(delta, radius);
        Vector2 rawAxis = clamped / radius;

        float magnitude = rawAxis.magnitude;
        if (magnitude <= mobileJoystickDeadZone)
        {
            mobileJoystickAxis = Vector2.zero;
        }
        else
        {
            float scaledMagnitude = Mathf.InverseLerp(mobileJoystickDeadZone, 1f, magnitude);
            mobileJoystickAxis = rawAxis.normalized * scaledMagnitude;
        }

        SetMobileJoystickKnobOffset(clamped);
        return mobileJoystickAxis;
    }

    private Vector2 ApplyMobileJoystickMode(Vector2 axis)
    {
        return digitalMobileJoystick
            ? QuantizeMobileJoystickAxis(axis)
            : ApplyMobileJoystickForwardTurnDeadZone(axis);
    }

    private bool ShouldUseOverheadWorldMobileJoystickMode()
    {
        return dir != null &&
               dir.cameraModeSwitcher != null &&
               dir.cameraModeSwitcher.cameraMode == CameraModes.Overhead;
    }

    private Vector2 ApplyMobileJoystickForwardTurnDeadZone(Vector2 axis)
    {
        if (axis.y > mobileJoystickDeadZone && Mathf.Abs(axis.x) <= mobileJoystickForwardTurnDeadZone)
            axis.x = 0f;

        return axis;
    }

    private static Vector2 QuantizeMobileJoystickAxis(Vector2 axis)
    {
        float magnitude = axis.magnitude;
        if (magnitude <= 0f)
            return Vector2.zero;

        float angle = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
        int sector = Mathf.RoundToInt(angle / 45f);
        float quantizedAngle = sector * 45f * Mathf.Deg2Rad;
        Vector2 direction = new(Mathf.Cos(quantizedAngle), Mathf.Sin(quantizedAngle));
        return direction.normalized * magnitude;
    }

    private void ClearMobileJoystick()
    {
        mobileJoystickPointerId = NoMobileJoystickPointer;
        mobileJoystickCandidatePointerId = NoMobileJoystickPointer;
        reservedMobileJoystickPointerId = NoMobileJoystickPointer;
        mobileJoystickAxis = Vector2.zero;
        SetMobileJoystickKnobOffset(Vector2.zero);
        if (mobileJoystickFloatsToPress)
            SetMobileJoystickControlsVisible(false);
    }

    private bool ShouldShowMobileJoystick()
    {
        if (!ShouldUseMobileJoystick())
            return false;

        if (dir != null && dir.gen != null && !dir.gen.buildComplete)
            return false;

        SceneFader fader = dir != null ? dir.sceneFader : null;
        if (fader != null)
        {
            if (fader.menuCanvasGroup != null && fader.menuCanvasGroup.alpha > 0.01f)
                return false;
            if (fader.splashCanvasGroup != null && fader.splashCanvasGroup.alpha > 0.01f)
                return false;
        }

        return true;
    }

    private bool ShouldUseMobileJoystick()
    {
        if (!enableMobileJoystick)
            return false;

#if UNITY_EDITOR
        return showMobileJoystickInEditor;
#else
        return Application.isMobilePlatform;
#endif
    }

    private Vector2 GetMobileJoystickCenterScreen()
    {
        float radius = GetMobileJoystickRadiusPixels();
        return new Vector2(
            mobileJoystickMarginPixels.x + radius,
            mobileJoystickMarginPixels.y + radius);
    }

    private float GetMobileJoystickRadiusPixels()
    {
        return Mathf.Max(1f, mobileJoystickRadiusPixels * Mathf.Max(0.1f, mobileJoystickRadiusMultiplier));
    }

    private bool IsInMobileJoystickActivationZone(Vector2 screenPosition)
    {
        if (mobileJoystickFloatsToPress)
            return screenPosition.x >= 0f &&
                   screenPosition.y >= 0f &&
                   screenPosition.x <= Screen.width &&
                   screenPosition.y <= Screen.height;

        return GetMobileJoystickActivationRect().Contains(screenPosition);
    }

    private Rect GetMobileJoystickActivationRect()
    {
        float width = Screen.width * Mathf.Clamp01(mobileJoystickActivationWidthPercent);
        float height = Screen.height * Mathf.Clamp01(mobileJoystickActivationHeightPercent);
        return new Rect(0f, 0f, width, height);
    }

    private void UpdateMobileJoystickSharedState(bool visible)
    {
        if (mobileJoystickFloatsToPress)
        {
            bool hasFloatingJoystickTouch =
                mobileJoystickPointerId != NoMobileJoystickPointer ||
                mobileJoystickCandidatePointerId != NoMobileJoystickPointer;
            mobileJoystickVisibleForOtherInput = visible && hasFloatingJoystickTouch;
            mobileJoystickActivationRectForOtherInput = mobileJoystickVisibleForOtherInput
                ? new Rect(0f, 0f, Screen.width, Screen.height)
                : default;
            return;
        }

        mobileJoystickVisibleForOtherInput = visible;
        mobileJoystickActivationRectForOtherInput = visible ? GetMobileJoystickActivationRect() : default;
    }

    private void EnsureMobileJoystickUi()
    {
        if (mobileJoystickCanvas != null)
            return;

        GameObject canvasObject = new("MobileTouchControlsCanvas");
        mobileJoystickCanvas = canvasObject.AddComponent<Canvas>();
        mobileJoystickCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mobileJoystickCanvas.sortingOrder = 500;
        canvasObject.AddComponent<CanvasScaler>();

        float radius = GetMobileJoystickRadiusPixels();
        mobileJoystickBase = CreateMobileJoystickImage("JoystickBase", canvasObject.transform, radius * 2f, mobileJoystickBaseColor);
        mobileJoystickKnob = CreateMobileJoystickImage("JoystickKnob", mobileJoystickBase, radius * 0.88f, mobileJoystickKnobColor);
        mobileJoystickKnob.anchorMin = new Vector2(0.5f, 0.5f);
        mobileJoystickKnob.anchorMax = new Vector2(0.5f, 0.5f);
        SetMobileJoystickKnobOffset(Vector2.zero);
        SetMobileJoystickControlsVisible(!mobileJoystickFloatsToPress);
    }

    private RectTransform CreateMobileJoystickImage(string objectName, Transform parent, float size, Color color)
    {
        GameObject imageObject = new(objectName);
        imageObject.transform.SetParent(parent, worldPositionStays: false);

        Image image = imageObject.AddComponent<Image>();
        image.sprite = CreateCircleSprite(objectName);
        image.color = color;
        image.raycastTarget = false;

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static Sprite CreateCircleSprite(string spriteName)
    {
        const int textureSize = 64;
        Texture2D texture = new(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color clear = new(1f, 1f, 1f, 0f);
        Color white = Color.white;
        float radius = (textureSize - 2) * 0.5f;
        Vector2 center = new((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius - distance + 1f);
                texture.SetPixel(x, y, alpha > 0f ? new Color(white.r, white.g, white.b, alpha) : clear);
            }
        }

        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        return Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
    }

    private void SetMobileJoystickUiVisible(bool visible)
    {
        if (mobileJoystickCanvas != null && mobileJoystickCanvas.gameObject.activeSelf != visible)
            mobileJoystickCanvas.gameObject.SetActive(visible);

        if (visible && !mobileJoystickFloatsToPress)
            SetMobileJoystickControlsVisible(true);
    }

    private void SetMobileJoystickControlsVisible(bool visible)
    {
        if (mobileJoystickBase != null && mobileJoystickBase.gameObject.activeSelf != visible)
            mobileJoystickBase.gameObject.SetActive(visible);
    }

    private void SetMobileJoystickBasePosition(Vector2 screenPosition)
    {
        if (mobileJoystickBase != null)
            mobileJoystickBase.anchoredPosition = screenPosition;
    }

    private void SetMobileJoystickKnobOffset(Vector2 offset)
    {
        if (mobileJoystickKnob != null)
            mobileJoystickKnob.anchoredPosition = offset;
    }

    private static bool IsShiftClickModifierActive()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.shiftKey.isPressed;
    }

    private void ClearTapTracking()
    {
        isPrimaryPressTracking = false;
        primaryPressStartTime = 0;
        primaryPressStartPos = default;
        primaryPressPointerId = 0;
    }
    private bool IsPointerOverUI(int pointerId = -1, Vector2? screenPosition = null)
    {
        if (EventSystem.current == null)
            return false;

        if (pointerId >= 0)
        {
            if (EventSystem.current.IsPointerOverGameObject(pointerId))
                return true;

            return screenPosition.HasValue && IsScreenPositionOverBlockingUI(screenPosition.Value);
        }

        if (EventSystem.current.IsPointerOverGameObject())
            return true;

        return screenPosition.HasValue && IsScreenPositionOverBlockingUI(screenPosition.Value);
    }

    private static bool IsScreenPositionOverBlockingUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        for (int i = 0; i < results.Count; i++)
        {
            GameObject hitObject = results[i].gameObject;
            if (hitObject == null)
                continue;

            if (hitObject.GetComponentInParent<Selectable>(includeInactive: false) != null)
                return true;

            Graphic graphic = hitObject.GetComponent<Graphic>();
            if (graphic != null && graphic.raycastTarget)
                return true;
        }

        return false;
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

    private static bool IsMousePointerOverInteractionDialogScrollableList()
    {
        Mouse mouse = Mouse.current;
        return mouse != null &&
            InteractionDialogUI.IsPointerOverScrollableList(mouse.position.ReadValue());
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
