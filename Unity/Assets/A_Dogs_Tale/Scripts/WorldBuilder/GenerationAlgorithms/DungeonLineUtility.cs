using System.Collections.Generic;
using UnityEngine;

public static partial class DungeonLineUtility
{
    public static List<Vector2Int> OrthogonalLine(Vector2Int from, Vector2Int to)
    {
        List<Vector2Int> line = new List<Vector2Int>();
        Vector2Int current = from;
        bool xFirst = UnityEngine.Random.Range(0, 2) == 0;

        if (xFirst)
        {
            while (current.x != to.x)
            {
                line.Add(new Vector2Int(current.x, current.y));
                current.x += current.x < to.x ? 1 : -1;
            }
        }

        while (current.y != to.y)
        {
            line.Add(new Vector2Int(current.x, current.y));
            current.y += current.y < to.y ? 1 : -1;
        }

        while (current.x != to.x)
        {
            line.Add(new Vector2Int(current.x, current.y));
            current.x += current.x < to.x ? 1 : -1;
        }

        return line;
    }

    public static List<Vector2Int> BresenhamLine(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        int x0 = start.x;
        int y0 = start.y;
        int x1 = end.x;
        int y1 = end.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            path.Add(new Vector2Int(x0, y0));
            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }

        return path;
    }
}
