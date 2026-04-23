using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    // FindTwoClosestRooms does just that.  This way we connect rooms with short corridors first that are unlikely to
    // cross another room.
    // TODO: not very efficient, should use hashes
    public Vector2Int FindTwoClosestRooms(List<int> unconnected_rooms)
    {
        if (unconnected_rooms.Count < 2) return Vector2Int.zero; // not enough rooms

        Vector2Int closestPair = Vector2Int.zero;
        float minDistance = float.MaxValue;

        for (int i = 0; i < rooms.Count; i++)
        {
            if (!unconnected_rooms.Contains(i)) continue;  // i is not a unique room

            List<Vector2Int> room_cells_i = get_union_of_connected_room_cells(i);
            Vector2Int center_i = GetCenterOfTiles(room_cells_i);

            for (int j = i + 1; j < rooms.Count; j++)
            {
                if (!unconnected_rooms.Contains(j)) continue;  // j is not a unique room

                List<Vector2Int> room_cells_j = get_union_of_connected_room_cells(j);
                Vector2Int center_j = GetCenterOfTiles(room_cells_j);

                float distance = Vector2Int.Distance(center_i, center_j);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPair = new Vector2Int(i, j);
                }
            }
        }

        return closestPair;
    }

    // UNCHANGED
    // GetCeterOfTiles is used in finding a short corridor between unconnected rooms
    public Vector2Int GetCenterOfTiles(List<Vector2Int> tiles)
    {
        if (tiles.Count == 0) return new Vector2Int(0, 0);

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var t in tiles)
        {
            if (t.x < minX) minX = t.x;
            if (t.y < minY) minY = t.y;
            if (t.x > maxX) maxX = t.x;
            if (t.y > maxY) maxY = t.y;
        }

        return new Vector2Int(((minX + maxX) / 2), (minY + maxY) / 2);
    }
}
