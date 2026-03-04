
using System;
using System.Collections.Generic;
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
    None    = 0,
    Open    = 1 << 0,   // door currently open
    Locked  = 1 << 1,   // requires key/force
    Secret  = 1 << 2,   // hidden (not obvious)
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
    East  = 2,  // +X
    West  = 3   // -X
}

public static class Direction4Util
{
    public static Vector2Int ToDelta(this Direction4 d) => d switch
    {
        Direction4.North => new Vector2Int(0,  1),
        Direction4.South => new Vector2Int(0, -1),
        Direction4.East  => new Vector2Int(1,  0),
        Direction4.West  => new Vector2Int(-1, 0),
        _ => Vector2Int.zero
    };

    public static Direction4 Opposite(this Direction4 d) => d switch
    {
        Direction4.North => Direction4.South,
        Direction4.South => Direction4.North,
        Direction4.East  => Direction4.West,
        Direction4.West  => Direction4.East,
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


public static class DoorFactory
{
    // Returns (aId, bId) for convenience
    public static (int, int) CreateLinkedDoorPair(
        List<Room> rooms,
        int roomAIndex, Vector2Int cellA, Direction4 dirFromA,
        int roomBIndex, Vector2Int cellB /* usually cellA + dirFromA.ToDelta() */,
        int nextDoorId,
        DoorMaterial material,
        DoorFlags initialFlags = DoorFlags.None,
        Color? color = null)
    {
        var doorA = new Door
        {
            id = nextDoorId,
            ownerRoomIndex = roomAIndex,
            cell = cellA,
            openDir = dirFromA,
            neighborRoomIndex = roomBIndex,
            material = material,
            flags = initialFlags,
            color = color ?? Color.clear
        };

        var doorB = new Door
        {
            id = nextDoorId + 1,
            ownerRoomIndex = roomBIndex,
            cell = cellB,
            openDir = dirFromA.Opposite(),
            neighborRoomIndex = roomAIndex,
            material = material,
            flags = initialFlags,
            color = color ?? Color.clear
        };

        // Link partners
        doorA.partnerDoorId = doorB.id;
        doorB.partnerDoorId = doorA.id;

        rooms[roomAIndex].doors.Add(doorA);
        rooms[roomBIndex].doors.Add(doorB);

        return (doorA.id, doorB.id);
    }
}

public static class DoorFactories
{
    /// Create an EDGE-anchored door between two adjacent floor cells (thin walls).
    public static (Door a, Door b) CreateEdgeDoorPair(
        List<Room> rooms,
        int roomAIndex, Vector2Int aEntry,
        int roomBIndex, Vector2Int bEntry,
        int nextDoorId,
        DoorMaterial mat,
        DoorFlags flags = DoorFlags.None)
    {
        var normal = DeltaToDir(bEntry - aEntry);

        var anchor = new DoorAnchor
        {
            type = DoorAnchorType.Edge,
            aEntry = aEntry,
            bEntry = bEntry,
            normal = normal,
            wallStart = default,        // unused
            throughDepthTiles = 0,      // unused for edge type
            spanTiles = 1
        };

        var doorA = new Door
        {
            id = nextDoorId,
            ownerRoomIndex = roomAIndex,
            neighborRoomIndex = roomBIndex,
            partnerDoorId = nextDoorId + 1,
            anchor = anchor,
            material = mat,
            flags = flags
        };

        var doorB = new Door
        {
            id = nextDoorId + 1,
            ownerRoomIndex = roomBIndex,
            neighborRoomIndex = roomAIndex,
            partnerDoorId = nextDoorId,
            anchor = new DoorAnchor
            {
                type = DoorAnchorType.Edge,
                aEntry = bEntry,
                bEntry = aEntry,
                normal = normal.Opposite(),
                wallStart = default,
                throughDepthTiles = 0,
                spanTiles = 1
            },
            material = mat,
            flags = flags
        };

        rooms[roomAIndex].doors.Add(doorA);
        rooms[roomBIndex].doors.Add(doorB);
        return (doorA, doorB);
    }

    /// Create a TILE-anchored door that lives inside wall tiles (thick walls).
    /// wallStart is the first wall tile at the doorway’s centerline; it will carve throughDepthTiles along 'normal'.
    public static (Door a, Door b) CreateTileDoorPair(
        List<Room> rooms,
        int roomAIndex, Vector2Int aEntry,
        int roomBIndex, Vector2Int bEntry,
        Vector2Int wallStart,
        Direction4 normal,
        int spanTiles,            // opening width across the wall face
        int throughDepthTiles,    // how deep to tunnel through wall
        int nextDoorId,
        DoorMaterial mat,
        DoorFlags flags = DoorFlags.None)
    {
        var anchor = new DoorAnchor
        {
            type = DoorAnchorType.Tile,
            aEntry = aEntry,
            bEntry = bEntry,
            normal = normal,
            wallStart = wallStart,
            spanTiles = Mathf.Max(1, spanTiles),
            throughDepthTiles = Mathf.Max(1, throughDepthTiles)
        };

        var doorA = new Door
        {
            id = nextDoorId,
            ownerRoomIndex = roomAIndex,
            neighborRoomIndex = roomBIndex,
            partnerDoorId = nextDoorId + 1,
            anchor = anchor,
            material = mat,
            flags = flags
        };
        var doorB = new Door
        {
            id = nextDoorId + 1,
            ownerRoomIndex = roomBIndex,
            neighborRoomIndex = roomAIndex,
            partnerDoorId = nextDoorId,
            anchor = new DoorAnchor
            {
                type = DoorAnchorType.Tile,
                aEntry = bEntry,
                bEntry = aEntry,
                normal = normal.Opposite(),
                wallStart = wallStart, // same physical doorway centerline
                spanTiles = anchor.spanTiles,
                throughDepthTiles = anchor.throughDepthTiles
            },
            material = mat,
            flags = flags
        };

        rooms[roomAIndex].doors.Add(doorA);
        rooms[roomBIndex].doors.Add(doorB);
        return (doorA, doorB);
    }

    private static Direction4 DeltaToDir(Vector2Int d)
    {
        if (d == new Vector2Int(0, 1)) return Direction4.North;
        if (d == new Vector2Int(0, -1)) return Direction4.South;
        if (d == new Vector2Int(1, 0)) return Direction4.East;
        if (d == new Vector2Int(-1, 0)) return Direction4.West;
        throw new ArgumentException($"Delta {d} is not 4-connected.");
    }
}

public static class DoorPlacement
{
    /// Convert a grid cell to world. If your grid origin/scale differs, adjust here.
    public static Vector3 GridToWorld(Vector2Int cell, float cellSize = 1f, float floorY = 0f)
        => new Vector3(cell.x * cellSize, floorY, cell.y * cellSize);

    /// Compute a world pose (position/rotation) to place the door prefab.
    /// For edge: midpoint between aEntry and bEntry.
    /// For tile: center of wallStart.
    public static void GetWorldPose(Door door, out Vector3 pos, out Quaternion rot, float cellSize = 1f, float floorY = 0f)
    {
        var anc = door.anchor;
        Vector3 forward = DirToForward(anc.normal);

        if (anc.type == DoorAnchorType.Edge)
        {
            var a = GridToWorld(anc.aEntry, cellSize, floorY);
            var b = GridToWorld(anc.bEntry, cellSize, floorY);
            pos = (a + b) * 0.5f;
        }
        else
        {
            // Tile anchored at the center of the first wall tile; your mesh may offset for depth
            var c = GridToWorld(anc.wallStart, cellSize, floorY);
            pos = c;
        }

        rot = Quaternion.LookRotation(forward, Vector3.up);
    }

    public static Vector3 GetWorldSize(Door door, float cellSize = 1f, float defaultThickness = 0.15f, float height = 2.2f)
    {
        var anc = door.anchor;
        float width = Mathf.Max(1, anc.spanTiles) * cellSize;
        float depth = anc.IsTileAnchored ? Mathf.Max(1, anc.throughDepthTiles) * cellSize : defaultThickness;
        return new Vector3(width, height, depth);
    }

    private static Vector3 DirToForward(Direction4 d) => d switch
    {
        Direction4.North => new Vector3(0, 0, 1),
        Direction4.South => new Vector3(0, 0, -1),
        Direction4.East => new Vector3(1, 0, 0),
        Direction4.West => new Vector3(-1, 0, 0),
        _ => Vector3.forward
    };
}

public static class DoorCarver
{
    public const byte FLOOR = 0;
    public const byte WALL = 1;

    /// Carve a rectangular tunnel for a TILE-anchored door.
    /// Returns the list of carved cells (useful for later decoration).
    public static List<Vector2Int> CarveTileDoorway(byte[,] map, Door door)
    {
        var carved = new List<Vector2Int>();
        var anc = door.anchor;
        if (anc.type != DoorAnchorType.Tile) return carved;

        Vector2Int n = anc.normal.ToDelta();
        Vector2Int t = new Vector2Int(-n.y, n.x); // tangent along span

        // Center line at wallStart, span extends half to each side (integer clamped)
        int half = (anc.spanTiles - 1) / 2;

        for (int s = -half; s <= half; s++)
        {
            var start = anc.wallStart + t * s;
            for (int d = 0; d < anc.throughDepthTiles; d++)
            {
                var c = start + n * d;
                if (!InBounds(map, c)) continue;
                if (map[c.x, c.y] != FLOOR) // if it’s wall, carve
                {
                    map[c.x, c.y] = FLOOR;
                    carved.Add(c);
                }
            }
        }

        // Optionally ensure entry cells are floor (robustness):
        ForceFloor(map, anc.aEntry);
        ForceFloor(map, anc.bEntry);

        return carved;
    }

    private static bool InBounds(byte[,] map, Vector2Int p)
        => p.x >= 0 && p.y >= 0 && p.x < map.GetLength(0) && p.y < map.GetLength(1);

    private static void ForceFloor(byte[,] map, Vector2Int p)
    {
        if (InBounds(map, p)) map[p.x, p.y] = FLOOR;
    }
}

public static class DoorSync
{
    // Use a global registry for quick lookup; populate as you create doors.
    public static readonly Dictionary<int, Door> ById = new Dictionary<int, Door>();

    public static void Register(params Door[] doors)
    {
        foreach (var d in doors) ById[d.id] = d;
    }

    public static void SetOpen(int doorId, bool open)
    {
        if (!ById.TryGetValue(doorId, out var d)) return;
        d.SetOpen(open);

        if (ById.TryGetValue(d.partnerDoorId, out var p))
            p.SetOpen(open);
    }

    public static void SetLocked(int doorId, bool locked)
    {
        if (!ById.TryGetValue(doorId, out var d)) return;
        if (locked) d.flags |= DoorFlags.Locked; else d.flags &= ~DoorFlags.Locked;

        if (ById.TryGetValue(d.partnerDoorId, out var p))
        {
            if (locked) p.flags |= DoorFlags.Locked; else p.flags &= ~DoorFlags.Locked;
        }
    }
}