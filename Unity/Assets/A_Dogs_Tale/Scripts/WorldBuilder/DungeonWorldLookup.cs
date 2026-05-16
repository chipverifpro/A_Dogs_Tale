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

    public bool TrySampleFloorAtMapPosition(
        Vector3 mapPosition,
        int threshold,
        out float floorMapY,
        out Vector3 floorMapNormal,
        out Cell floorCell)
    {
        floorMapY = 0f;
        floorMapNormal = Vector3.up;
        floorCell = null;

        if (!buildComplete || hf == null)
            return false;

        float unitHeight = cfg != null ? Mathf.Max(0.0001f, cfg.unitHeight) : 1f;
        int x = Mathf.FloorToInt(mapPosition.x);
        int y = Mathf.FloorToInt(mapPosition.z);
        int heightSteps = Mathf.RoundToInt(mapPosition.y / unitHeight);

        floorCell = GetCellFromHf(x, y, heightSteps, threshold);
        if (floorCell == null)
            return false;

        return TrySampleFloorAtMapPosition(mapPosition, floorCell, out floorMapY, out floorMapNormal);
    }

    public bool TrySampleFloorAtMapPosition(
        Vector3 mapPosition,
        Cell floorCell,
        out float floorMapY,
        out Vector3 floorMapNormal)
    {
        floorMapY = 0f;
        floorMapNormal = Vector3.up;

        if (floorCell == null)
            return false;

        float unitHeight = cfg != null ? Mathf.Max(0.0001f, cfg.unitHeight) : 1f;
        floorMapNormal = (floorCell.tiltFloor * Vector3.up).normalized;
        if (floorMapNormal == Vector3.zero || float.IsNaN(floorMapNormal.x) || float.IsNaN(floorMapNormal.y) || float.IsNaN(floorMapNormal.z))
            floorMapNormal = Vector3.up;

        Vector3 planePoint = new Vector3(
            floorCell.x + 0.5f,
            floorCell.height * unitHeight,
            floorCell.y + 0.5f);

        float normalY = floorMapNormal.y;
        if (Mathf.Abs(normalY) < 1e-5f)
        {
            floorMapY = planePoint.y;
            return true;
        }

        floorMapY = planePoint.y - (
            floorMapNormal.x * (mapPosition.x - planePoint.x) +
            floorMapNormal.z * (mapPosition.z - planePoint.z)) / normalY;

        return !float.IsNaN(floorMapY) && !float.IsInfinity(floorMapY);
    }
}
