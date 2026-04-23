using System;
using UnityEngine;

/* Usage:
Thin wall (two rooms share an edge)

var (a, b) = DoorFactories.CreateEdgeDoorPair(
    rooms, idxCommon, aEntryTile, idxKitchen, bEntryTile,
    nextDoorId, DoorMaterial.Wood, DoorFlags.None);

DoorSync.Register(a, b);
nextDoorId += 2;


Thick wall (there’s a wall tile between floors)

// normal points from A→B; wallStart is the wall tile between them
var (a, b) = DoorFactories.CreateTileDoorPair(
    rooms, idxCommon, aEntry, idxKitchen, bEntry,
    wallStart, normal: Direction4.North,
    spanTiles: 1, throughDepthTiles: 1,
    nextDoorId, DoorMaterial.ReinforcedWood, DoorFlags.None);

DoorCarver.CarveTileDoorway(map, a); // updates byte map to open a tunnel
DoorSync.Register(a, b);
nextDoorId += 2;


Build 3D visuals

DoorPlacement.GetWorldPose(a, out var pos, out var rot, cellSize: 1f, floorY: 0f);
// pick prefab by a.material + a.IsOpen
var go = Instantiate(prefab, pos, rot, parent);
go.name = $"Door_{a.id}_{rooms[a.ownerRoomIndex].name}";

*/

[Serializable]
public class Door
{
    // Unique id so we can link the two sides safely across saves.
    // Tip: assign incrementally during generation, not a GUID (lighter).
    public int id;

    // Local owner room index (into your global rooms list) for reference/debug.
    // Not strictly required if the door lives inside the Room.doors list,
    // but helpful for cross-checks and when you cache doors globally.
    public int ownerRoomIndex;

    // Geometry/Placement
    public DoorAnchor anchor;

    // Cell position inside the owner's room (tile coordinate where the door sits).
    public Vector2Int cell;

    // Direction the door swings/opens *from the owner's room perspective*.
    // This is also the direction toward the neighboring cell/room the door connects to.
    public Direction4 openDir;

    // Partner linkage: the door on the other side.
    // Use an id so we avoid Unity serialization cycles.
    public int partnerDoorId = -1;      // -1 = none/unknown yet
    public int neighborRoomIndex = -1;  // optional: which room is on the other side

    // State & properties
    public DoorFlags flags = DoorFlags.None;
    public DoorMaterial material = DoorMaterial.Wood;

    // Style/interaction (optional but handy)
    public enum DoorStyle : byte { SingleSwing, DoubleSwing, Slide, Portcullis, Archway }
    public DoorStyle style = DoorStyle.SingleSwing;

    public enum HingeSide : byte { Left, Right, Center /* slide/portcullis */ }
    public HingeSide hinge = HingeSide.Left;

    public float openAngleDeg = 100f;     // for swing doors
    public float openSpeed = 1f;          // anim speed scalar

    // Cosmetic/gameplay extras (optional, extend as needed)
    public Color color = Color.clear;      // e.g., paint, glow; clear = auto/none
    public string keyTag = "";             // non-empty means this door uses a key tag
    public int lockDifficulty = 0;         // 0=trivial, scale as you like
    public int trapDifficulty = 0;         // disarm/check DC if Trapped
    public string note = "";               // debug/authoring notes

    // ---- Convenience ----
    public bool IsOpen => (flags & DoorFlags.Open) != 0;
    public bool IsLocked => (flags & DoorFlags.Locked) != 0;
    public bool IsSecret => (flags & DoorFlags.Secret) != 0;
    public bool IsTrapped => (flags & DoorFlags.Trapped) != 0;

    public void SetOpen(bool open)
    {
        if (open) flags |= DoorFlags.Open;
        else flags &= ~DoorFlags.Open;
    }

    public void ToggleOpen()
    {
        flags ^= DoorFlags.Open;
    }

    public override string ToString()
        => $"Door(id:{id}, owner:{ownerRoomIndex}, cell:{cell}, dir:{openDir}, partner:{partnerDoorId}, flags:{flags}, mat:{material})";
}

[Flags]
public enum DoorFlags : int
{
    None = 0,
    Open = 1 << 0,   // door currently open
    Locked = 1 << 1,   // requires key/force
    Secret = 1 << 2,   // hidden (not obvious)
    Trapped = 1 << 3,   // trap armed
}

public enum DoorMaterial : byte
{
    Wood,
    ReinforcedWood,
    Iron,
    Steel,
    Portcullis,
    Stone,
    Magic
}

public enum Direction4 : sbyte
{
    North = 0,  // +Y
    South = 1,  // -Y
    East = 2,  // +X
    West = 3   // -X
}

public static class Direction4Util
{
    public static Vector2Int ToDelta(this Direction4 d) => d switch
    {
        Direction4.North => new Vector2Int(0, 1),
        Direction4.South => new Vector2Int(0, -1),
        Direction4.East => new Vector2Int(1, 0),
        Direction4.West => new Vector2Int(-1, 0),
        _ => Vector2Int.zero
    };

    public static Direction4 Opposite(this Direction4 d) => d switch
    {
        Direction4.North => Direction4.South,
        Direction4.South => Direction4.North,
        Direction4.East => Direction4.West,
        Direction4.West => Direction4.East,
        _ => d
    };
}

public enum DoorAnchorType : byte { Edge, Tile }

[Serializable]
public struct DoorAnchor
{
    public DoorAnchorType type;

    // For both types:
    public Vector2Int aEntry;   // floor cell on side A (owner side)
    public Vector2Int bEntry;   // floor cell on side B (neighbor side)
    public Direction4 normal;   // from A toward B (door “forward”)

    // Edge-anchored (thin walls): the grid edge is implicit by (aEntry,bEntry).
    // Tile-anchored (thick walls): the wall cell(s) the door occupies:
    public Vector2Int wallStart;     // first wall tile (centered placement)
    public int throughDepthTiles;    // how many wall tiles deep (>=1)
    public int spanTiles;            // width of opening (1 for single, 2+ for double)

    // Helper: is this thick-wall?
    public bool IsTileAnchored => type == DoorAnchorType.Tile;
}
