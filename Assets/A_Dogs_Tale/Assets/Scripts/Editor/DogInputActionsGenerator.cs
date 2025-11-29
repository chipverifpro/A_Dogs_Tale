#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using System.IO; // still fine to keep, we'll qualify types explicitly

public static class DogInputActionsGenerator
{
    // We generate a .inputactions JSON file so Unity's Input System importer kicks in.
    private const string AssetPath = "Assets/A_Dogs_Tale/Assets/Input/DogInputActions.inputactions";

    [MenuItem("Tools/DogGame/Rebuild DogInputActions Asset")]
    public static void RebuildDogInputActions()
    {
        // Compute full path to the .inputactions file
        // (Application.dataPath ends with ".../YourProject/Assets")
        string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
        string fullPath    = System.IO.Path.Combine(projectRoot, AssetPath);

        // Ensure directory exists
        string dir = System.IO.Path.GetDirectoryName(fullPath);
        if (!System.IO.Directory.Exists(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
        }

        // Build InputActionAsset in memory
        var asset = ScriptableObject.CreateInstance<InputActionAsset>();

        // --------------------------------------------------
        // 1. Control schemes
        // --------------------------------------------------
        asset.AddControlScheme("Keyboard&Mouse")
             .WithRequiredDevice("<Keyboard>")
             .WithRequiredDevice("<Mouse>");

        asset.AddControlScheme("Gamepad")
             .WithRequiredDevice("<Gamepad>");

        asset.AddControlScheme("Touch")
             .WithRequiredDevice("<Touchscreen>");

        // --------------------------------------------------
        // 2. Gameplay action map
        // --------------------------------------------------
        var gameplay = new InputActionMap("Gameplay");
        asset.AddActionMap(gameplay);

        // Helpers
        InputAction AddButton(string name)
        {
            return gameplay.AddAction(name, InputActionType.Button);
        }

        InputAction AddValue(string name, string expectedControlType)
        {
            var action = gameplay.AddAction(name, InputActionType.Value);
            action.expectedControlType = expectedControlType;
            return action;
        }

        // --------------------------------------------------
        // 3. Actions
        // --------------------------------------------------

        // Movement / pointer / click
        var moveAction   = AddValue("Move", "Vector2");
        var pointAction  = AddValue("Point", "Vector2");
        var clickAction  = AddButton("Click");

        // Dog actions
        var barkAction           = AddButton("Bark");
        var markTerritoryAction  = AddButton("MarkTerritory");
        var interactAction       = AddButton("Interact");

        // Camera
        var zoomAction           = AddValue("Zoom", "Axis");
        var camView1Action       = AddButton("CameraView1");
        var camView2Action       = AddButton("CameraView2");
        var camView3Action       = AddButton("CameraView3");

        // Pack / meta
        var selectAgent1Action   = AddButton("SelectAgent1");
        var selectAgent2Action   = AddButton("SelectAgent2");
        var selectAgent3Action   = AddButton("SelectAgent3");
        var selectAgent4Action   = AddButton("SelectAgent4");
        var selectAgent5Action   = AddButton("SelectAgent5");
        var changeFormationAction= AddButton("ChangeFormation");

        // Optional "press anything" action
        var anyAction            = AddButton("AnyAction");

        // --------------------------------------------------
        // 4. Bindings
        // --------------------------------------------------

        // --- Move: WASD composite ---
        var wasd = moveAction.AddCompositeBinding("2DVector");
        wasd.With("Up",    "<Keyboard>/w");
        wasd.With("Down",  "<Keyboard>/s");
        wasd.With("Left",  "<Keyboard>/a");
        wasd.With("Right", "<Keyboard>/d");

        // Optional arrows composite
        var arrows = moveAction.AddCompositeBinding("2DVector");
        arrows.With("Up",    "<Keyboard>/upArrow");
        arrows.With("Down",  "<Keyboard>/downArrow");
        arrows.With("Left",  "<Keyboard>/leftArrow");
        arrows.With("Right", "<Keyboard>/rightArrow");

        // Gamepad left stick
        moveAction.AddBinding("<Gamepad>/leftStick");

        // --- Point: cursor / primary touch position ---
        pointAction.AddBinding("<Pointer>/position");

        // --- Click: mouse/touch press, gamepad South button ---
        clickAction.AddBinding("<Pointer>/press");
        clickAction.AddBinding("<Gamepad>/buttonSouth");

        // --- Bark ---
        barkAction.AddBinding("<Keyboard>/q");
        barkAction.AddBinding("<Gamepad>/buttonWest"); // X / Square

        // --- Mark territory ---
        markTerritoryAction.AddBinding("<Keyboard>/e");
        markTerritoryAction.AddBinding("<Gamepad>/buttonEast"); // B / Circle

        // --- Interact ---
        interactAction.AddBinding("<Keyboard>/f");
        interactAction.AddBinding("<Gamepad>/buttonSouth"); // A / Cross

        // --- Zoom: mouse scroll + triggers ---
        zoomAction.AddBinding("<Mouse>/scroll/y");         // wheel
        zoomAction.AddBinding("<Gamepad>/rightTrigger");   // zoom in
        zoomAction.AddBinding("<Gamepad>/leftTrigger")
                  .WithProcessor("invert");                // zoom out

        // --- Camera views ---
        camView1Action.AddBinding("<Keyboard>/1");
        camView1Action.AddBinding("<Gamepad>/dpad/up");

        camView2Action.AddBinding("<Keyboard>/2");
        camView2Action.AddBinding("<Gamepad>/dpad/right");

        camView3Action.AddBinding("<Keyboard>/3");
        camView3Action.AddBinding("<Gamepad>/dpad/down");

        // --- Agent selection (keyboard 1–5) ---
        selectAgent1Action.AddBinding("<Keyboard>/1");
        selectAgent2Action.AddBinding("<Keyboard>/2");
        selectAgent3Action.AddBinding("<Keyboard>/3");
        selectAgent4Action.AddBinding("<Keyboard>/4");
        selectAgent5Action.AddBinding("<Keyboard>/5");

        // Optional basic gamepad bindings for agent selection
        selectAgent1Action.AddBinding("<Gamepad>/leftShoulder");
        selectAgent2Action.AddBinding("<Gamepad>/rightShoulder");

        // --- Change formation ---
        changeFormationAction.AddBinding("<Keyboard>/r");
        changeFormationAction.AddBinding("<Gamepad>/buttonNorth"); // Y / Triangle

        // --- AnyAction (optional "press anything") ---
        anyAction.AddBinding("<Keyboard>/anyKey");
        anyAction.AddBinding("<Gamepad>/start");
        anyAction.AddBinding("<Gamepad>/buttonSouth");
        anyAction.AddBinding("<Mouse>/leftButton");
        anyAction.AddBinding("<Touchscreen>/primaryTouch/press");

        // --------------------------------------------------
        // 5. Serialize to JSON and write to .inputactions file
        // --------------------------------------------------
        string json = asset.ToJson();

        // Overwrite existing file if present
        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);

        System.IO.File.WriteAllText(fullPath, json);

        AssetDatabase.ImportAsset(AssetPath);
        AssetDatabase.Refresh();

        var importedAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
        Selection.activeObject = importedAsset;

        Debug.Log("DogInputActions.inputactions rebuilt at: " + AssetPath);
    }
}
#endif