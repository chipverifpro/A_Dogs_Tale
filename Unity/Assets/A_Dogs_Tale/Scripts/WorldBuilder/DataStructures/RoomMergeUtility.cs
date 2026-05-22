using System.Collections.Generic;
using UnityEngine;

public static class RoomMergeUtility
{
    /// <summary>
    /// Merge rooms that overlap. If considerAdjacency is true, rooms that touch by edge/corner are merged too.
    /// </summary>
    public static List<Room> MergeOverlappingRooms(List<Room> rooms, bool considerAdjacency = false, bool eightWay = true)
    {
        if (rooms == null || rooms.Count == 0) return new List<Room>();

        var dsu = new DSU(rooms.Count);
        var owner = new Dictionary<Vector2Int, int>(1024);

        Vector2Int[] n4 = new[]
        {
            new Vector2Int( 1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int( 0, 1),
            new Vector2Int( 0,-1)
        };
        Vector2Int[] n8 = new[]
        {
            new Vector2Int( 1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int( 0, 1),
            new Vector2Int( 0,-1),
            new Vector2Int( 1, 1),
            new Vector2Int( 1,-1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1,-1)
        };
        var neighbors = eightWay ? n8 : n4;

        Debug.LogWarning($"MergeOverlappingRooms begins rooms={rooms.Count}");
        for (int i = 0; i < rooms.Count; i++)
        {
            var cells = rooms[i].cells;
            for (int k = 0; k < cells.Count; k++)
            {
                var tile = cells[k].pos;

                if (!owner.TryGetValue(tile, out int existingOwner))
                    owner[tile] = i;
                else
                    dsu.Union(i, existingOwner);

                if (considerAdjacency)
                {
                    foreach (var d in neighbors)
                    {
                        var neighbor = tile + d;
                        if (owner.TryGetValue(neighbor, out int neighborOwner))
                            dsu.Union(i, neighborOwner);
                    }
                }
            }
        }

        var groupedTiles = new Dictionary<int, List<Vector2Int>>();
        var groupedHeights = new Dictionary<int, List<int>>();
        var groupedSeen = new Dictionary<int, HashSet<Vector2Int>>();

        for (int i = 0; i < rooms.Count; i++)
        {
            int root = dsu.Find(i);
            if (!groupedTiles.ContainsKey(root))
            {
                groupedTiles[root] = new List<Vector2Int>(rooms[i].cells.Count);
                groupedHeights[root] = new List<int>(rooms[i].cells.Count);
                groupedSeen[root] = new HashSet<Vector2Int>();
            }

            var roomCells = rooms[i].cells;
            for (int k = 0; k < rooms[i].cells.Count; k++)
            {
                var tile = roomCells[k].pos;
                var height = roomCells[k].height;
                if (!groupedSeen[root].Add(tile))
                    continue;

                groupedTiles[root].Add(tile);
                groupedHeights[root].Add(height);
            }
        }

        var merged = new List<Room>(groupedTiles.Count);
        foreach (var root in groupedTiles.Keys)
        {
            var newRoom = new Room(groupedTiles[root], groupedHeights[root]);
            newRoom.setColorFloor(highlight: true);
            merged.Add(newRoom);
        }

        merged.Sort((a, b) => b.Size.CompareTo(a.Size));
        Debug.LogWarning($"MergeOverlappingRooms ends rooms={rooms.Count}");
        return merged;
    }

    public static List<Room> SimpleMergeOverlappingRooms(List<Room> rooms, bool considerAdjacency = false, bool eightWay = true)
    {
        int index_A;
        int index_B;
        Room room_A;
        Room room_B;
        int cell_A;
        int cell_B;
        if (rooms.Count<=1) return rooms; // No overlap if less than two rooms
        for (index_A = rooms.Count-2; index_A >= 0; index_A--)
        {
            room_A = rooms[index_A];
            for (index_B = rooms.Count-1; index_B > index_A; index_B--)
            {
                room_B = rooms[index_B];
                bool removedFromRoomB = false;
                // compare individual Cells in both rooms
                for (cell_A = room_A.cells.Count-1; cell_A >= 0; cell_A--)
                {
                    for (cell_B = room_B.cells.Count-1; cell_B >= 0; cell_B--)
                    {
                        if (room_A.cells[cell_A].pos == room_B.cells[cell_B].pos)
                        {
                            room_B.cells.RemoveAt(cell_B);
                            removedFromRoomB = true;
                        }
                    }
                }
                if (room_B.cells.Count == 0)
                {
                    // if room_B is empty, delete it.
                    rooms.RemoveAt(index_B);
                }
                else if (removedFromRoomB)
                {
                    room_B.ResetCellDictionary();
                }
            }
        }
        RemoveOverlappingCellsFromLaterRooms(rooms, removeEmptyRooms: true);
        return rooms;
    }

    public static int RemoveOverlappingCellsFromLaterRooms(List<Room> rooms, bool removeEmptyRooms = true)
    {
        if (rooms == null || rooms.Count == 0) return 0;

        int removedCount = 0;
        var seen = new HashSet<Vector2Int>();

        for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            Room room = rooms[roomIndex];
            if (room == null || room.cells == null)
                continue;

            bool changed = false;
            var uniqueCells = new List<Cell>(room.cells.Count);
            for (int cellIndex = 0; cellIndex < room.cells.Count; cellIndex++)
            {
                Cell cell = room.cells[cellIndex];
                if (cell == null)
                {
                    changed = true;
                    removedCount++;
                    continue;
                }

                if (!seen.Add(cell.pos))
                {
                    changed = true;
                    removedCount++;
                    continue;
                }

                uniqueCells.Add(cell);
            }

            if (changed)
            {
                room.cells.Clear();
                room.cells.AddRange(uniqueCells);
                room.ResetCellDictionary();
            }

            if (removeEmptyRooms && room.cells.Count == 0)
            {
                rooms.RemoveAt(roomIndex);
                roomIndex--;
            }
        }

        RefreshRoomCellOwnership(rooms);

        if (removedCount > 0)
            Debug.LogWarning($"RemoveOverlappingCellsFromLaterRooms removed {removedCount} overlapping cells.");

        return removedCount;
    }

    private static void RefreshRoomCellOwnership(List<Room> rooms)
    {
        for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            Room room = rooms[roomIndex];
            if (room == null || room.cells == null)
                continue;

            room.my_room_number = roomIndex;
            for (int cellIndex = 0; cellIndex < room.cells.Count; cellIndex++)
                room.cells[cellIndex].room_number = roomIndex;
            room.GetBounds();
        }
    }
}
