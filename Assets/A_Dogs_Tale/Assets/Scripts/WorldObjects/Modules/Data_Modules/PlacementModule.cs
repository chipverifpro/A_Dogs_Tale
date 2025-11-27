using UnityEngine;

/// <summary>
/// Flags for which types of rooms this object is appropriate for.
/// Attach to furniture / props to guide auto-placement.
/// </summary>
[System.Flags]
public enum PlacementRoomTypeFlags
{
    None      = 0,
    Bedroom   = 1 << 0,
    Kitchen   = 1 << 1,
    Living    = 1 << 2,
    Bathroom  = 1 << 3,
    Hallway   = 1 << 4,
    Utility   = 1 << 5,
    Generic   = 1 << 6,
    Corridor  = 1 << 7,
    Outdoor   = 1 << 8,
    Any       = ~0
}

/// <summary>
/// Hint for where in a room this object expects to be placed.
/// </summary>
public enum EdgeHint
{
    Free,           // Anywhere
    NearWall,       // Within some distance of a wall
    AgainstWall,    // Back flush to a wall
    InCorner,       // Touching two walls
    CenterOfRoom    // Away from walls
}

/// <summary>
/// Rules for how this object should be oriented when placed.
/// </summary>
public enum RotationRule
{
    Free,               // Any yaw
    Snap90,             // Multiples of 90 degrees
    FaceAwayFromWall,   // Back to the wall (for beds, cabinets, etc.)
    FaceTowardWall,     // Facing the wall (for sinks, wall terminals, etc.)
    AlignWithRoomAxis   // Along dominant room axis (to be filled in by placer)
}

/// <summary>
/// PlacementModule: describes *where* and *how* this object wants to be placed
/// within a room, and exposes helpers to compute size, position, and rotation.
/// Attach this to furniture / scenery prefabs.
/// </summary>
public class PlacementModule : WorldModule
{
    [Header("Room Matching")]
    [Tooltip("Which room types this object is suitable for.")]
    public PlacementRoomTypeFlags allowedRooms = PlacementRoomTypeFlags.Any;

    [Header("Grid Size (X,Y,Z in cells)")]
    [Tooltip("If true, approximate sizeInCells from Renderer bounds using cellSize.")]
    public bool autoSizeFromMesh = true;

    [Tooltip("Exact object size in grid units (X,Y,Z). Auto-filled from mesh bounds.")]
    public Vector3 sizeInCells = Vector3.one;

    [Tooltip("Size of one grid cell in world units. Used when auto-sizing from mesh.")]
    public float cellSize = 1.0f;

    [Header("Placement Hints")]
    [Tooltip("Hint about where in a room this object should be placed.")]
    public EdgeHint edgeHint = EdgeHint.Free;

    [Tooltip("Rule for how this object should be oriented when placed.")]
    public RotationRule rotationRule = RotationRule.Snap90;

    [Header("Clearance Constraints")]
    [Tooltip("Minimum clear cells around this object (for chairs, tables, etc.).")]
    public int minClearCellsAround = 0;

    [Tooltip("If true, object should be touching a wall (NearWall / AgainstWall).")]
    public bool mustTouchWall = false;

    [Header("Wall Clearance")]
    [Tooltip("Extra world-space margin from the wall, on top of half the object depth.")]
    public float wallPadding = 0.05f;

    // Optional cached references
    private Renderer _mainRenderer;
    private LocationModule _location;

    protected override void Awake()
    {
        base.Awake();

        // Soft references – fine if missing on some prefabs.
        _mainRenderer = GetComponentInChildren<Renderer>();
        _location = GetComponent<LocationModule>();

        // Auto-compute size at runtime if requested
        if (autoSizeFromMesh)
        {
            AutoComputeSizeFromMesh();
        }
    }

#if UNITY_EDITOR
    // In editor, Reset() is handy when you first add the component.
    private void Reset()
    {
        _mainRenderer = GetComponentInChildren<Renderer>();
        _location = GetComponent<LocationModule>();

        if (autoSizeFromMesh)
            AutoComputeSizeFromMesh();
    }
#endif

    /// <summary>
    /// Returns true if this object is allowed in a room whose flags include roomFlags.
    /// Typically you'll pass the room's PlacementRoomTypeFlags here.
    /// </summary>
    public bool AllowsRoom(PlacementRoomTypeFlags roomFlags)
    {
        if (allowedRooms == PlacementRoomTypeFlags.Any)
            return true;

        if (allowedRooms == PlacementRoomTypeFlags.None)
            return false;

        return (allowedRooms & roomFlags) != 0;
    }

    /// <summary>
    /// Approximate sizeInCells from the attached renderer's bounds and cellSize.
    /// Safe to call multiple times.
    /// </summary>
    public void AutoComputeSizeFromMesh_OLD_INT()
    {
        if (_mainRenderer == null)
            _mainRenderer = GetComponentInChildren<Renderer>();

        if (_mainRenderer == null)
        {
            Debug.LogWarning($"{name}: PlacementModule.AutoComputeSizeFromMesh found no Renderer.", this);
            return;
        }

        if (cellSize <= 0f)
        {
            Debug.LogWarning($"{name}: PlacementModule.cellSize <= 0, using 1.0 as fallback.", this);
            cellSize = 1.0f;
        }

        Bounds bounds = _mainRenderer.bounds; // world-space bounds (approx is fine)
        float sizeX = bounds.size.x;
        float sizeY = bounds.size.y;
        float sizeZ = bounds.size.z;

        sizeInCells = new Vector3Int(
            Mathf.Max(1, Mathf.CeilToInt(sizeX / cellSize)),
            Mathf.Max(1, Mathf.CeilToInt(sizeY / cellSize)),
            Mathf.Max(1, Mathf.CeilToInt(sizeZ / cellSize))
        );
    }

    public void AutoComputeSizeFromMesh()
    {
        if (_mainRenderer == null)
            _mainRenderer = GetComponentInChildren<Renderer>();

        if (_mainRenderer == null)
        {
            Debug.LogWarning($"{name}: PlacementModule.AutoComputeSizeFromMesh found no Renderer.", this);
            return;
        }

        if (cellSize <= 0f)
            cellSize = 1f;

        Bounds bounds = _mainRenderer.bounds; // world bounds

        // Convert world-space meters → grid-space units
        float sizeX = bounds.size.x / cellSize;
        float sizeY = bounds.size.y / cellSize;
        float sizeZ = bounds.size.z / cellSize;

        sizeInCells = new Vector3(sizeX, sizeY, sizeZ);
    }

    /// <summary>
    /// Choose a rotation for this object based on placement rules and an optional wall direction.
    /// wallDir should be a DirFlags representing the wall the object is placed against,
    /// or DirFlags.None if not placing relative to a wall.
    /// </summary>
    public Quaternion ChooseRotation(DirFlags wallDir)
    {
        switch (rotationRule)
        {
            case RotationRule.Free:
            {
                float yaw = Random.Range(0f, 360f);
                return Quaternion.Euler(0f, yaw, 0f);
            }

            case RotationRule.Snap90:
            {
                int step = Random.Range(0, 4);
                float yaw = step * 90f;
                return Quaternion.Euler(0f, yaw, 0f);
            }

            case RotationRule.FaceAwayFromWall:
            {
                Vector3 forward = GetForwardFromWall(wallDir, awayFromWall: true);
                return Quaternion.LookRotation(forward, Vector3.up);
            }

            case RotationRule.FaceTowardWall:
            {
                Vector3 forward = GetForwardFromWall(wallDir, awayFromWall: false);
                return Quaternion.LookRotation(forward, Vector3.up);
            }

            case RotationRule.AlignWithRoomAxis:
                // Placeholder: the placer can override this and align based on room layout.
                // For now, treat like Snap90.
                {
                    int step = Random.Range(0, 4);
                    float yaw = step * 90f;
                    return Quaternion.Euler(0f, yaw, 0f);
                }

            default:
                return Quaternion.identity;
        }
    }

    /// <summary>
    /// Compute a world-space position for this object centered in the given cell,
    /// using the cell's pos3d_world plus an optional Y offset.
    /// EdgeHint-specific adjustments (e.g., pushing against wall) are handled by the placer.
    /// </summary>
    public Vector3 ComputeBaseWorldPosition(Cell cell, float yOffset = 0f)
    {
        // IMPORTANT: use world-space coordinate, not grid-space.
        Vector3 pos = cell.pos3d_world;
        pos.y += yOffset;
        return pos;
    }

    /// <summary>
    /// Optional helper: applies both position and rotation to this object's transform
    /// based on a chosen cell and wall direction. Caller is responsible for choosing
    /// an appropriate cell/wall for the edgeHint.
    /// </summary>
    public void ApplyPlacement_old(Cell cell, DirFlags wallDir, float yOffset = 0f)
    {
        Vector3 worldPos = ComputeBaseWorldPosition(cell, yOffset);
        Quaternion rot = ChooseRotation(wallDir);

        transform.position = worldPos;
        transform.rotation = rot;

        // If we have a LocationModule, keep it in sync with grid position.
        if (_location != null)
        {
            _location.cell = cell;
            _location.pos3d_f = cell.pos3d_f; // grid-space master
            _location.yawDeg = transform.eulerAngles.y;
        }
    }

    public void ApplyPlacement(Cell cell, DirFlags wallDir, float yOffset = 0f)
    {
        Vector3 worldPos = ComputeBaseWorldPosition(cell, yOffset);

        // If this object is meant to be near/against a wall, nudge it inward
        if (edgeHint == EdgeHint.NearWall || edgeHint == EdgeHint.AgainstWall || mustTouchWall)
        {
            worldPos += ComputeWallClearanceOffset(wallDir);
        }

        Quaternion rot = ChooseRotation(wallDir);

        transform.position = worldPos;
        transform.rotation = rot;

        if (_location != null)
        {
            _location.cell    = cell;
            _location.pos3d_f = cell.pos3d_f;
            _location.yawDeg  = transform.eulerAngles.y;
        }
    }

    /// <summary>
    /// Tiny helper to convert a wall DirFlags into a forward vector.
    /// If awayFromWall is true, we face into the room.
    /// </summary>
    private Vector3 GetForwardFromWall(DirFlags wallDir, bool awayFromWall)
    {
        if (wallDir == DirFlags.None)
        {
            // Fallback if no wall information available.
            return Vector3.forward;
        }

        // Your DirFlagsEx helper converts a DirFlags (N,E,S,W or combos) to a Vector2Int.
        Vector2Int dir2 = wallDir.ToVector2Int();

        // Grid coords: x = east/west, y = north/south
        Vector3 normal = new Vector3(dir2.x, 0f, dir2.y);

        if (normal.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        normal.Normalize();

        // If the wall normal points INTO the room, then "away from wall" is -normal.
        // If it's the other way around in your layout, swap the sign.
        return awayFromWall ? -normal : normal;
    }

    /// <summary>
    /// Compute an inward offset to keep this object from intersecting a wall,
    /// based on its sizeInCells and cellSize. wallDir is the wall this object
    /// is placed against; inward is assumed to be away-from-wall (into the room).
    /// </summary>
    public Vector3 ComputeWallClearanceOffset(DirFlags wallDir)
    {
        if (wallDir == DirFlags.None)
            return Vector3.zero;

        Vector3 inward = GetForwardFromWall(wallDir, awayFromWall: true);
        if (inward.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        inward.Normalize();

        // Determine “depth” of the object in the direction we are pushing
        // Based on whether X or Z component dominates
        float depth;

        if (Mathf.Abs(inward.x) > Mathf.Abs(inward.z))
            depth = Mathf.Abs(sizeInCells.x) * cellSize;
        else
            depth = Mathf.Abs(sizeInCells.z) * cellSize;

        float halfDepth = 0.5f * depth;

        return inward * (halfDepth + wallPadding);
    }

}