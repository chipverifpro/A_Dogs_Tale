using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour
{    
    // --------- Corridor line algorithms ----------
    // These return a list of points, which the DrawCorrior function will follow
    // while handling the width and slope to generate a corridor Room:
    //
    //   ORTHOGONAL (straight in cardinal directions only, one 90 degree bend)
    //   BRESENHAM (straight direct between end points)
    //   NOISY BRESENHAM (slightly wiggly version of a straight line)
    //   ORGANIC (kinda jiggles while going 45 degrees and then vertical or horizontal)
    //   BEZIER (curved quadratic form with two random control points)
    //
    //   future: STAIRS in versions that are straight, square, octagon, or switchback
    //           primarily for going up and down one or more a vertical floors
    //   future: LADDER (vertical only), or PIT or SHAFT or ELEVATOR or...

    // ---------------- Orthogonal Line ----------------

    // Orthogonal Line algorithm: creates an orthogonal line between two points.
    // Randomly starts with either x or y direction and makes just one turn.
    public List<Vector2Int> OrthogonalLine(Vector2Int from, Vector2Int to)
    {
        List<Vector2Int> line = new List<Vector2Int>();
        Vector2Int current = from;
        bool xFirst = UnityEngine.Random.Range(0, 2) == 0;

        if (xFirst)
        {
            // Move in x direction first
            while (current.x != to.x)
            {
                //tilemap.SetTile(new Vector3Int(current.x, current.y, 0), floorTile);
                line.Add(new Vector2Int(current.x, current.y));
                current.x += current.x < to.x ? 1 : -1;
            }
        }
        // Move in y direction
        while (current.y != to.y)
        {
            //tilemap.SetTile(new Vector3Int(current.x, current.y, 0), floorTile);
            line.Add(new Vector2Int(current.x, current.y));
            current.y += current.y < to.y ? 1 : -1;
        }
        // Move in x direction
        while (current.x != to.x)
        {
            //tilemap.SetTile(new Vector3Int(current.x, current.y, 0), floorTile);
            line.Add(new Vector2Int(current.x, current.y));
            current.x += current.x < to.x ? 1 : -1;
        }
        return line;
    }

    // ---------------- Bresenham's Line ----------------

    // Bresenham's Line algorithm: creates a straight line between two points.
    public List<Vector2Int> BresenhamLine(Vector2Int start, Vector2Int end)
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

    // ---------------- Noisy Bresenham's Line ----------------

    // Noisy Bresenham's Line algorithm: creates a straight line between two points with added jiggle noise.
    public List<Vector2Int> NoisyBresenhamLine(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        float noiseStrength = cfg.organicJitterChance;
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

            // Add UnityEngine.Random noise to the decision
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

            // Always force at least one step to avoid infinite loop
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

    // ---------------- Organic Line ----------------

    // Organic Line algorithm: creates a wonky, wiggly line between two points.
    // Tends to first do a 45 degree diagonal and then switches to horizontal or vertical.
    // Not too bad for short lines as the jagginess can be good.
    public List<Vector2Int> OrganicLine(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int current = start;

        while (current != end)
        {
            path.Add(current);

            Vector2Int direction = end - current;
            int dx = Mathf.Clamp(direction.x, -1, 1);
            int dy = Mathf.Clamp(direction.y, -1, 1);

            // Introduce a slight chance to “wiggle”
            if (UnityEngine.Random.value < cfg.organicJitterChance)
            {
                if (UnityEngine.Random.value < 0.5f)
                    //dx = 0; // favor y
                    dx = UnityEngine.Random.value < 0.5f ? -1 : 1; // favor y
                else
                    //dy = 0; // favor x
                    dy = UnityEngine.Random.value < 0.5f ? -1 : 1; // favor y
            }

            current += new Vector2Int(dx, dy);
        }

        path.Add(end);
        return path;
    }

    // ---------------- Bezier Line ----------------

    // Generates the points for a bezier line between two map locations.
    // Creates 2 random control points giving the line a gentle curve.
    public List<Vector2Int> BezierLine(Vector2Int start, Vector2Int end)
    {
        int length = 0;
        Vector2 p0 = start;
        Vector2 p3 = end;
        int controlOffsetLimited;
        Vector2 mid = (p0 + p3) * 0.5f;
        Vector2 dir = (p3 - p0).normalized;
        Vector2 perp = Vector2.Perpendicular(dir);

        length = (int)Vector2.Distance(p0, p3);

        // Short corridors need less control offset or they go crazy
        if (cfg.bezierControlOffset > (int)(length / cfg.bezierMaxControl))
            controlOffsetLimited = (int)(length / cfg.bezierMaxControl);
        else
            controlOffsetLimited = (int)cfg.bezierControlOffset;

        // Improved control points: pull toward midpoint + random bend
        Vector2 p1 = Vector2.Lerp(p0, mid, 0.5f) + perp * Random.Range(-controlOffsetLimited, controlOffsetLimited);
        Vector2 p2 = Vector2.Lerp(p3, mid, 0.5f) + perp * Random.Range(-controlOffsetLimited, controlOffsetLimited);

        length = GetEstimatedBezierLength(p0, p1, p2, p3); // overestimate, determines number of sample points
        List<Vector2Int> path = SampleBezierCurve(p0, p1, p2, p3, length);

        return path;
    }

    // Samples points and eliminates duplicates, returning list in sampled order.
    List<Vector2Int> SampleBezierCurve(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int steps)
    {
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>(); // prevents useless duplicate points
        List<Vector2Int> orderedPoints = new List<Vector2Int>();

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 point = CubicBezier(p0, p1, p2, p3, t);
            Vector2Int tile = Vector2Int.RoundToInt(point);

            if (seen.Add(tile)) // Add and returns true if point was not already in the set
            {
                orderedPoints.Add(tile);
            }
        }

        return new List<Vector2Int>(orderedPoints);
    }

    // Calculates samples of a Bezier line
    Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1 - t;
        return u * u * u * p0
             + 3 * u * u * t * p1
             + 3 * u * t * t * p2
             + t * t * t * p3;
    }

    // A fast, rough upper bound length estimate of a Bezier line.
    // Used to determine sampling interval when converting line to cells,
    // results in slight oversampling which is removed by duplication checks.
    // Never produces undersampling which would leave gaps in corriders.
    int GetEstimatedBezierLength(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        float linearLength = Vector2.Distance(p0, p3);
        float controlLength = Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, p3);
        float estimate = (linearLength + controlLength) / 2f;
        return (int)estimate;
    }

}