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
    public int requestedPlayerAgentIndex;       // switch control to pack agent: -1 is no change
    public int requestedPlayerAgentDelta;       // switch to next/previous pack member, alternative to above.
    public bool changeFormationPressed;         // cycles to next formation

    // world and object targeting
    public bool interactPressed;                // "do something" with ClickTargetWorldObject
    public bool selectObjectPressed;            // "selects" ClickTargetWorldObject (currently for debug only)

    public bool hasClickTargetLocationWorld;
    public Vector3 clickTargetLocationWorld;    // world location (floor/wall)

    public bool hasClickTargetWorldObject;
    public WorldObject clickTargetWorldObject;  // object ID clicked on for interacton, etc

    // Skip delay
    public bool anyKeyOrButtonDown;             // skips delay in title screen / interraction / cutscene
}