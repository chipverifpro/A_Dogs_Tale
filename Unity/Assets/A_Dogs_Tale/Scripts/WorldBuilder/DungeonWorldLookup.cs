using UnityEngine;

public partial class DungeonGenerator
{
    // point bounds checking for rectangular or oval world maps
    public bool IsPointInWorld(Vector2Int point)
    {
        if (point.x < 0 || point.y < 0 ||
            point.x > cfg.mapWidth || point.y > cfg.mapHeight)
            return false; // out of the world
        if (!cfg.roundWorld) return true; // square world, limits are sufficient

        // Round world (axis-aligned ellipse) inscribed in the map.
        Vector2 Cw = new Vector2(cfg.mapWidth * 0.5f, cfg.mapHeight * 0.5f);
        float margin = 0.5f; // hardcoded
        float Rx = cfg.mapWidth * 0.5f - margin;
        float Ry = cfg.mapHeight * 0.5f - margin;
        float Rx2 = Rx * Rx, Ry2 = Ry * Ry;
        float dx = point.x - Cw.x, dy = point.y - Cw.y;
        return ((dx * dx) / Rx2 + (dy * dy) / Ry2) <= 1f; // <= 1 means inside world ellipse
    }

    public Cell GetCellFromHf(int x, int y, int z, int threshold)
    {
        NeighborMatch match;

        if (!buildComplete || hf == null || rooms == null || rooms.Count == 0)
            return null;

        if (hf.TryQueryAt(x, y, z, threshold, out match))
        {
            if (match.roomId < 0 || match.roomId >= rooms.Count)
                return null;

            Room nRoom = rooms[match.roomId];
            if (nRoom == null || nRoom.cells == null)
                return null;

            foreach (Cell cc in nRoom.cells)
            {
                if ((cc.x == x) && (cc.y == y)) return cc;
            }
        }
        return null;
    }
}
