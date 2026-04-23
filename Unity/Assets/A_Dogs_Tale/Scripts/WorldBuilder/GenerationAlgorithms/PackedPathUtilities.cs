using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

public partial class DungeonGenerator
{
    // ======================= Shared Utility functions =======================
    // shared functions pulled out of above
    Vector2Int RandomCardinal() => DungeonGridUtility.RandomCardinal(rng);

    // This replaces RandomCardinal for starting positions to keep from following borders too much.
    Vector2Int DirAwayFromEdge(Vector2Int pos)
    {
        int border = 10;    // distance to edge that is considered too close
        int W = cfg.mapWidth - 1, H = cfg.mapHeight - 1;
        if ((W - pos.x) < border) return new Vector2Int(-1, 0);
        if ((pos.x) < border) return new Vector2Int(1, 0);
        if ((H - pos.y) < border) return new Vector2Int(0, -1);
        if ((pos.y) < border) return new Vector2Int(0, 1);
        return RandomCardinal();  // not near any edge
    }

    Vector2Int TurnLeft(Vector2Int d, bool left) => DungeonGridUtility.TurnLeft(d, left);

    // checks if the location is inside map bounds (with keepout)
    public bool In(int x, int y) => DungeonGridUtility.InBoundsWithKeepout(x, y, cfg.mapWidth, cfg.mapHeight, cfg.borderKeepout);

    // If you want random edge starts instead of center:
    Vector2Int RandomEdgeStart(int w, int h)
    {
        int ko = cfg.borderKeepout;
        int edge = rng.Next(0, 4);
        return edge switch
        {
            0 => new Vector2Int(rng.Next(ko, w - ko - 1), ko),
            1 => new Vector2Int(rng.Next(ko, w - ko - 1), h - ko - 1),
            2 => new Vector2Int(ko, rng.Next(ko, h - ko - 1)),
            _ => new Vector2Int(w - ko - 1, rng.Next(ko, h - ko - 1)),
        };
    }

    // Used for creating tunnels.
    void CarveDisk(ref Room tmp_room, Vector2Int c, int penWidth)
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;
        int min = -(int)Math.Floor(penWidth / 2f); // makes the negative more to zero
        int max = min + penWidth - 1;

        for (int dy = min; dy <= max; dy++)
            for (int dx = min; dx <= max; dx++)
            {
                int x = c.x + dx, y = c.y + dy;
                if (!In(x, y)) continue;
                if (cfg.useRoundPen && (dx * dx + dy * dy > (penWidth / 2f) * (penWidth / 2f))) continue;

                var tmp_cell = new Cell(x, y);
                tmp_cell.colorFloor = tmp_room.colorFloor;
                tmp_cell.room_number = tmp_room.my_room_number;
                tmp_cell.isCorridor = true;
                tmp_room.cells.Add(tmp_cell);
                corridors.Add((x, y));
                cellGrid[x, y] = tmp_cell;
            }
    }

    Vector2Int MaybeTurn(Vector2Int d, Random r, float wander)
    {
        // with some prob, keep going; else turn 90 degrees left/right
        if (r.NextDouble() < (wander / 1000f)) return d;
        return (r.Next(2) == 0) ? TurnLeft(d, true) : TurnLeft(d, false);
    }

    int Manhattan(Vector2Int a, Vector2Int b) => DungeonGridUtility.Manhattan(a, b);

    // RasterizeLineSafe() looks to be a Bresenham line algorithm for any point to point lines
    IEnumerable<Vector2Int> RasterizeLineSafe(Vector2Int a, Vector2Int b)
    {
        int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        // Hard cap: the line cannot be longer than dx+|dy|+1 steps
        int maxSteps = dx + (-dy) + 1;

        for (int steps = 0; steps < maxSteps; steps++)
        {
            yield return new Vector2Int(x0, y0);
            if (x0 == x1 && y0 == y1) yield break;

            int e2 = err << 1; // 2*err
            bool stepped = false;

            if (e2 >= dy) { err += dy; x0 += sx; stepped = true; }
            if (e2 <= dx) { err += dx; y0 += sy; stepped = true; }

            // Safety: if neither branch moved (shouldn't happen), force a move toward target
            if (!stepped)
            {
                if (x0 != x1) x0 += sx;
                else if (y0 != y1) y0 += sy;
            }
        }
        yield break;
    }
}
