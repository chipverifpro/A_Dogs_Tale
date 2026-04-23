using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour
{
    // --------- Corridor line algorithms ----------
    // These wrappers preserve the existing DungeonGenerator API while the
    // implementations live in DungeonLineUtility.

    public List<Vector2Int> OrthogonalLine(Vector2Int from, Vector2Int to)
    {
        return DungeonLineUtility.OrthogonalLine(from, to);
    }

    public List<Vector2Int> BresenhamLine(Vector2Int start, Vector2Int end)
    {
        return DungeonLineUtility.BresenhamLine(start, end);
    }

    public List<Vector2Int> NoisyBresenhamLine(Vector2Int start, Vector2Int end)
    {
        return DungeonLineUtility.NoisyBresenhamLine(start, end, cfg.organicJitterChance);
    }

    public List<Vector2Int> OrganicLine(Vector2Int start, Vector2Int end)
    {
        return DungeonLineUtility.OrganicLine(start, end, cfg.organicJitterChance);
    }

    public List<Vector2Int> BezierLine(Vector2Int start, Vector2Int end)
    {
        return DungeonLineUtility.BezierLine(start, end, cfg.bezierControlOffset, cfg.bezierMaxControl);
    }

    List<Vector2Int> SampleBezierCurve(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int steps)
    {
        return DungeonLineUtility.SampleBezierCurve(p0, p1, p2, p3, steps);
    }

    Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        return DungeonLineUtility.CubicBezier(p0, p1, p2, p3, t);
    }

    int GetEstimatedBezierLength(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return DungeonLineUtility.GetEstimatedBezierLength(p0, p1, p2, p3);
    }
}
