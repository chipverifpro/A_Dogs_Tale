using UnityEngine;

public class PlayerInputState
{
    public Vector2 moveAxis;                    // float vector direction and speed (-1.0 to 1.0)

    // one-shot commands
    public bool barkPressed;
    public bool markTerritoryPressed;

    // camera commands
    public float zoomDelta;                     // float +/- delta zoom, works with keys or mouse wheel, or touchscreen pinch
    public CameraModes cameraViewSelect;        // change view enum. CameraModes.Unchanged = ...

    // player/pack changes
    public int requestedPlayerAgentIndex;       // switch control to pack agent: -1 is no change. for keyboard numbers or menu choices
    public int requestedPlayerAgentDelta;       // switch to next/previous pack member, alternative method to above.
    public bool changeFormationPressed;         // cycles to next formation

    // Skip delay
    public bool anyKeyOrButtonDown;             // skips delay in title screen / interraction / cutscene

    // world and object targeting
    public bool interactPressed;                // "do something" with ClickTargetWorldObject
    public bool selectObjectPressed;            // "selects" ClickTargetWorldObject (currently for debug only)

    // The following are all about what was clicked on.
    public bool hasScreenCoordinateClicked;     // enable
    public Vector3 screenCoordinateClicked;     // Basic Level: possibly useful for special effects/overlays/etc
    
    public bool hasClickTargetLocationWorld;    // enable
    public Vector3 clickTargetLocationWorld;    // world location (floor/wall)
    public Cell clickTargetLocationCell;        // Cell at world location

    public bool hasClickTargetWorldObject;      // enable
    public WorldObject clickTargetWorldObject;  // WorldObject clicked on for interacton, etc

    // --- Input Modifiers ---
    public InputModifiers inputModifiers = InputModifiers.None;
}

[System.Flags]
public enum InputModifiers
{
    None            = 0,

    // Keyboard
    Shift           = 1 << 0,
    Ctrl            = 1 << 1,
    Alt             = 1 << 2,
    Command         = 1 << 3,   // Mac-specific

    // Mouse
    LeftMouse       = 1 << 4,
    RightMouse      = 1 << 5,
    MiddleMouse     = 1 << 6,

    // Touch / Gesture
    OneFinger       = 1 << 7,
    TwoFingers      = 1 << 8,
    ThreeFingers    = 1 << 9,

    // Gamepad
    FaceButtonNorth = 1 << 10,  // Y / Triangle
    FaceButtonEast  = 1 << 11,  // B / Circle
    FaceButtonSouth = 1 << 12,   // A / Cross
    FaceButtonWest  = 1 << 13,  // X / Square
}
