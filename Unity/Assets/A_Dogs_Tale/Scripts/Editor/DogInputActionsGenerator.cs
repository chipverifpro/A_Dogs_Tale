#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static class DogInputActionsGenerator
{
    private const string AssetPath = "Assets/A_Dogs_Tale/Input/DogInputActions.inputactions";

    [MenuItem("Tools/DogGame/Rebuild DogInputActions Asset")]
    public static void RebuildDogInputActions()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string fullPath = Path.Combine(projectRoot, AssetPath);

        string dir = Path.GetDirectoryName(fullPath);
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        var asset = ScriptableObject.CreateInstance<InputActionAsset>();
        var player = new InputActionMap("Player");
        var ui = new InputActionMap("UI");
        asset.AddActionMap(player);
        asset.AddActionMap(ui);

        InputAction AddAction(InputActionMap map, string name, InputActionType type, string expectedControlType = "")
        {
            var action = map.AddAction(name, type);
            if (!string.IsNullOrEmpty(expectedControlType))
                action.expectedControlType = expectedControlType;
            return action;
        }

        InputAction AddButton(InputActionMap map, string name)
        {
            return AddAction(map, name, InputActionType.Button);
        }

        InputAction AddValue(InputActionMap map, string name, string expectedControlType)
        {
            return AddAction(map, name, InputActionType.Value, expectedControlType);
        }

        InputAction AddPassThrough(InputActionMap map, string name, string expectedControlType)
        {
            return AddAction(map, name, InputActionType.PassThrough, expectedControlType);
        }

        var moveAction = AddValue(player, "Move", "Vector2");
        var strafeAction = AddValue(player, "Strafe", "Axis");
        var lookAction = AddValue(player, "Look", "Vector2");
        var jumpAction = AddButton(player, "Jump");
        var interactAction = AddButton(player, "Interact");
        var barkAction = AddButton(player, "Bark");
        var sprintAction = AddButton(player, "Sprint");
        var pauseAction = AddButton(player, "Pause");
        var cameraViewAction = AddButton(player, "CameraView");
        var markTerritoryAction = AddButton(player, "MarkTerritory");
        var digAction = AddButton(player, "Dig");
        var zoomAction = AddValue(player, "Zoom", "Axis");
        var changeFormationAction = AddButton(player, "ChangeFormation");
        var selectObjectAction = AddButton(player, "SelectObject");
        var skipAnyKeyAction = AddButton(player, "SkipAnyKey");
        var nextAgentAction = AddValue(player, "NextAgent", "Integer");
        var popupTab1Action = AddButton(player, "PopupTab1");
        var popupTab2Action = AddButton(player, "PopupTab2");
        var popupTab3Action = AddButton(player, "PopupTab3");
        var popupTab4Action = AddButton(player, "PopupTab4");

        var navigateAction = AddValue(ui, "Navigate", "Vector2");
        var submitAction = AddButton(ui, "Submit");
        var cancelAction = AddButton(ui, "Cancel");
        var pointAction = AddPassThrough(ui, "Point", "Vector2");
        var clickAction = AddPassThrough(ui, "Click", "Button");
        var rightClickAction = AddPassThrough(ui, "RightClick", "Button");
        var middleClickAction = AddPassThrough(ui, "MiddleClick", "Button");
        var scrollWheelAction = AddPassThrough(ui, "ScrollWheel", "Vector2");
        var trackedDevicePositionAction = AddPassThrough(ui, "TrackedDevicePosition", "Vector3");
        var trackedDeviceOrientationAction = AddPassThrough(ui, "TrackedDeviceOrientation", "Quaternion");

        var wasd = moveAction.AddCompositeBinding("2DVector");
        wasd.With("Up",    "<Keyboard>/w");
        wasd.With("Down",  "<Keyboard>/s");
        wasd.With("Left",  "<Keyboard>/a");
        wasd.With("Right", "<Keyboard>/d");

        var arrows = moveAction.AddCompositeBinding("2DVector");
        arrows.With("Up",    "<Keyboard>/upArrow");
        arrows.With("Down",  "<Keyboard>/downArrow");
        arrows.With("Left",  "<Keyboard>/leftArrow");
        arrows.With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");
        interactAction.AddBinding("<Keyboard>/e");
        interactAction.AddBinding("<Gamepad>/buttonWest");
        barkAction.AddBinding("<Keyboard>/q");
        barkAction.AddBinding("<Gamepad>/rightShoulder");
        sprintAction.AddBinding("<Keyboard>/leftShift");
        sprintAction.AddBinding("<Gamepad>/leftStickPress");
        pauseAction.AddBinding("<Keyboard>/escape");
        pauseAction.AddBinding("<Gamepad>/start");
        cameraViewAction.AddBinding("<Keyboard>/tab");
        cameraViewAction.AddBinding("<Gamepad>/buttonNorth");
        markTerritoryAction.AddBinding("<Keyboard>/r");
        markTerritoryAction.AddBinding("<Gamepad>/rightTrigger");
        digAction.AddBinding("<Keyboard>/v");
        zoomAction.AddBinding("<Mouse>/scroll/y");

        var zoomKeyboardAxis = zoomAction.AddCompositeBinding("1DAxis");
        zoomKeyboardAxis.With("Negative", "<Keyboard>/z");
        zoomKeyboardAxis.With("Positive", "<Keyboard>/x");

        var zoomGamepadAxis = zoomAction.AddCompositeBinding("1DAxis");
        zoomGamepadAxis.With("Negative", "<Gamepad>/leftTrigger");
        zoomGamepadAxis.With("Positive", "<Gamepad>/rightTrigger");

        changeFormationAction.AddBinding("<Keyboard>/f");
        changeFormationAction.AddBinding("<Gamepad>/dpad/right");
        selectObjectAction.AddBinding("<Mouse>/leftButton");
        selectObjectAction.AddBinding("<Keyboard>/a");
        skipAnyKeyAction.AddBinding("<Keyboard>/anyKey");
        skipAnyKeyAction.AddBinding("<Gamepad>/button*");

        var nextAgentAxis = nextAgentAction.AddCompositeBinding("1DAxis");
        nextAgentAxis.With("Negative", "<Keyboard>/minus");
        nextAgentAxis.With("Positive", "<Keyboard>/equals");

        var strafeAxis = strafeAction.AddCompositeBinding("1DAxis");
        strafeAxis.With("Negative", "<Keyboard>/q");
        strafeAxis.With("Positive", "<Keyboard>/e");

        popupTab1Action.AddBinding("<Keyboard>/1");
        popupTab2Action.AddBinding("<Keyboard>/2");
        popupTab3Action.AddBinding("<Keyboard>/3");
        popupTab4Action.AddBinding("<Keyboard>/4");

        var navigateWasd = navigateAction.AddCompositeBinding("2DVector");
        navigateWasd.With("Up", "<Keyboard>/w");
        navigateWasd.With("Down", "<Keyboard>/s");
        navigateWasd.With("Left", "<Keyboard>/a");
        navigateWasd.With("Right", "<Keyboard>/d");

        var navigateArrows = navigateAction.AddCompositeBinding("2DVector");
        navigateArrows.With("Up", "<Keyboard>/upArrow");
        navigateArrows.With("Down", "<Keyboard>/downArrow");
        navigateArrows.With("Left", "<Keyboard>/leftArrow");
        navigateArrows.With("Right", "<Keyboard>/rightArrow");
        navigateAction.AddBinding("<Gamepad>/dpad");

        submitAction.AddBinding("<Keyboard>/enter");
        submitAction.AddBinding("<Gamepad>/buttonSouth");
        cancelAction.AddBinding("<Keyboard>/escape");
        cancelAction.AddBinding("<Gamepad>/buttonEast");
        pointAction.AddBinding("<Mouse>/position");
        clickAction.AddBinding("<Mouse>/leftButton");
        clickAction.AddBinding("<Gamepad>/buttonSouth");
        rightClickAction.AddBinding("<Mouse>/rightButton");
        middleClickAction.AddBinding("<Mouse>/middleButton");
        scrollWheelAction.AddBinding("<Mouse>/scroll");
        trackedDevicePositionAction.AddBinding("<OculusTrackingReference>/devicePosition");
        trackedDeviceOrientationAction.AddBinding("<OculusTrackingReference>/deviceRotation");

        string json = asset.ToJson();

        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);

        System.IO.File.WriteAllText(fullPath, json);

        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        var importedAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
        Selection.activeObject = importedAsset;

        Debug.Log($"DogInputActions.inputactions rebuilt at: {AssetPath}");
    }
}
#endif
