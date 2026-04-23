using System.Collections.Generic;
using UnityEngine;

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
        Vector2Int t = new Vector2Int(-n.y, n.x);
        int half = (anc.spanTiles - 1) / 2;

        for (int s = -half; s <= half; s++)
        {
            var start = anc.wallStart + t * s;
            for (int d = 0; d < anc.throughDepthTiles; d++)
            {
                var c = start + n * d;
                if (!InBounds(map, c)) continue;
                if (map[c.x, c.y] != FLOOR)
                {
                    map[c.x, c.y] = FLOOR;
                    carved.Add(c);
                }
            }
        }

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
