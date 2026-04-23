using System;
using System.Collections.Generic;
using UnityEngine;

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
            wallStart = default,
            throughDepthTiles = 0,
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
        int spanTiles,
        int throughDepthTiles,
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
                wallStart = wallStart,
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
