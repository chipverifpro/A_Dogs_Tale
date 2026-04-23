using System.Collections.Generic;
using UnityEngine;

public static partial class DungeonLineUtility
{
    public static List<Vector2Int> NoisyBresenhamLine(Vector2Int start, Vector2Int end, float noiseStrength)
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
            float noise = UnityEngine.Random.Range(-1f, 1f) * noiseStrength;

            if (e2 + noise > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 + noise < dx)
            {
                err += dx;
                y0 += sy;
            }

            if (x0 == path[path.Count - 1].x && y0 == path[path.Count - 1].y)
            {
                if (dx > dy)
                    x0 += sx;
                else
                    y0 += sy;
            }
        }

        return path;
    }

    public static List<Vector2Int> OrganicLine(Vector2Int start, Vector2Int end, float jitterChance)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int current = start;

        while (current != end)
        {
            path.Add(current);

            Vector2Int direction = end - current;
            int dx = Mathf.Clamp(direction.x, -1, 1);
            int dy = Mathf.Clamp(direction.y, -1, 1);

            if (UnityEngine.Random.value < jitterChance)
            {
                if (UnityEngine.Random.value < 0.5f)
                    dx = UnityEngine.Random.value < 0.5f ? -1 : 1;
                else
                    dy = UnityEngine.Random.value < 0.5f ? -1 : 1;
            }

            current += new Vector2Int(dx, dy);
        }

        path.Add(end);
        return path;
    }

    public static List<Vector2Int> BezierLine(Vector2Int start, Vector2Int end, float bezierControlOffset, float bezierMaxControl)
    {
        int length = 0;
        Vector2 p0 = start;
        Vector2 p3 = end;
        int controlOffsetLimited;
        Vector2 mid = (p0 + p3) * 0.5f;
        Vector2 dir = (p3 - p0).normalized;
        Vector2 perp = Vector2.Perpendicular(dir);

        length = (int)Vector2.Distance(p0, p3);

        if (bezierControlOffset > (int)(length / bezierMaxControl))
            controlOffsetLimited = (int)(length / bezierMaxControl);
        else
            controlOffsetLimited = (int)bezierControlOffset;

        Vector2 p1 = Vector2.Lerp(p0, mid, 0.5f) + perp * UnityEngine.Random.Range(-controlOffsetLimited, controlOffsetLimited);
        Vector2 p2 = Vector2.Lerp(p3, mid, 0.5f) + perp * UnityEngine.Random.Range(-controlOffsetLimited, controlOffsetLimited);

        length = GetEstimatedBezierLength(p0, p1, p2, p3);
        return SampleBezierCurve(p0, p1, p2, p3, length);
    }

    public static List<Vector2Int> SampleBezierCurve(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int steps)
    {
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
        List<Vector2Int> orderedPoints = new List<Vector2Int>();

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 point = CubicBezier(p0, p1, p2, p3, t);
            Vector2Int tile = Vector2Int.RoundToInt(point);

            if (seen.Add(tile))
                orderedPoints.Add(tile);
        }

        return new List<Vector2Int>(orderedPoints);
    }

    public static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1 - t;
        return u * u * u * p0
             + 3 * u * u * t * p1
             + 3 * u * t * t * p2
             + t * t * t * p3;
    }

    public static int GetEstimatedBezierLength(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        float linearLength = Vector2.Distance(p0, p3);
        float controlLength = Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, p3);
        float estimate = (linearLength + controlLength) / 2f;
        return (int)estimate;
    }
}
