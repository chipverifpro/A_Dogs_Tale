using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class DungeonGenerator
{
    // UNUSED
    public void ClearTileAndNeighborWalls(Tilemap tilemap, Vector3Int cellPos)
    {
        var squareR2 = NeighborCache.Get(2, NeighborCache.Shape.Square, borderOnly: false, includeDiagonals: true);

        foreach (var offset in squareR2)
        {
            var neighbor = cellPos + offset;
            if (tilemap.GetTile(neighbor) == wallTile)
                tilemap.SetTile(neighbor, null);
        }

        tilemap.SetTile(cellPos, null); // Clear the main tile
    }

    // Get the closest floor tile location in this room to a given target location
    // TODO: for overlapping rooms where corridor will be zero length, do sommething different
    public Vector2Int GetClosestPointInTilesList(List<Vector2Int> tile_list, Vector2Int target, int minimum_corridor_length)
    {
        int min_distance = int.MaxValue;
        int cur_distance = int.MaxValue;
        Vector2Int closest_point = Vector2Int.zero;

        if (tile_list.Count == 0) return Vector2Int.zero;

        foreach (var t in tile_list)
        {
            cur_distance = (t - target).sqrMagnitude;
            if ((cur_distance < min_distance) && (cur_distance > minimum_corridor_length))
            {
                min_distance = cur_distance;
                closest_point = t;
            }
        }

        return closest_point;
    }
}
