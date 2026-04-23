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
            new Vector2Int( 1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0,-1),
            new Vector2Int( 1, 1), new Vector2Int( 1,-1), new Vector2Int(-1, 1), new Vector2Int(-1,-1)
        };
        var neighbors = eightWay ? n8 : n4;

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
        return merged;
    }
}
