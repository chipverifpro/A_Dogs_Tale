using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    // ROOM READY
    // Returns a count of the directions that have corridors of greater than corridorWidth long
    // Works with wide corridors.
    int CountCorridorNeighbors(int x, int y)
    {
        int minStraight = cfg.corridor.corridorWidth;
        int scanLen = minStraight + 1;
        int c = 0;
        int L = CountRun(x, y, -1, 0, scanLen);
        int R = CountRun(x, y, 1, 0, scanLen);
        int D = CountRun(x, y, 0, -1, scanLen);
        int U = CountRun(x, y, 0, 1, scanLen);
        if (L > minStraight) c++;
        if (R > minStraight) c++;
        if (D > minStraight) c++;
        if (U > minStraight) c++;
        return c;
    }

    // ROOM READY
    // Pick a tangent direction at (x,y) by counting how many contiguous corridor
    // cells exist to the Left/Right/Down/Up (L/R/D/U). Wider corridors are handled
    // because we look past a single neighbor. Returns (1,0) for horizontal,
    // (0,1) for vertical, or Vector2Int.zero if nothing usable is found.
    Vector2Int PickTangentDir(int x, int y, int scanLen = 12, int minStraight = 2)
    {
        int W = cfg.mapWidth - 1, H = cfg.mapHeight - 1;

        int L = CountRun(x, y, -1, 0, scanLen);
        int R = CountRun(x, y, 1, 0, scanLen);
        int D = CountRun(x, y, 0, -1, scanLen);
        int U = CountRun(x, y, 0, 1, scanLen);

        int horizontal = L + R;
        int vertical = D + U;

        // Strong signal: both sides present along an axis
        bool hasHoriz = (L >= minStraight && R >= minStraight);
        bool hasVert = (D >= minStraight && U >= minStraight);

        if (hasHoriz && !hasVert) return new Vector2Int(1, 0);
        if (!hasHoriz && hasVert) return new Vector2Int(0, 1);

        // If both (junction) or neither (corner/dead-end), pick the axis with more total run.
        if (horizontal > vertical) return new Vector2Int(1, 0);
        if (vertical > horizontal) return new Vector2Int(0, 1);

        // Tie-breakers:
        // 1) prefer the side with the single longest run
        int longest = Mathf.Max(Mathf.Max(L, R), Mathf.Max(D, U));
        if (longest == 0) return Vector2Int.zero; // isolated or no corridors around

        if (longest == L || longest == R) return new Vector2Int(1, 0);
        if (longest == D || longest == U) return new Vector2Int(0, 1);

        return Vector2Int.zero; // very rare fallback
    }

    // ROOMS ready
    // Returns the number of cells that continue to be a corridor in a given
    //   direction (d) from starting point (s).
    // Only looks as far as maxSteps.
    // Used in Seed_AlongCorridors via the PickTangentDir and CountCorridorNeighbors functions
    //   to determine which directions the corridor goes from a given location
    //   (since corridors can be wide we need to look farther).
    int CountRun(int sx, int sy, int dx, int dy, int maxSteps)
    {
        int c = 0;
        for (int i = 1; i <= maxSteps; i++)
        {
            int nx = sx + dx * i, ny = sy + dy * i;
            if (!In(nx, ny)) break;

            // Fast path using your grid flag:
            if (!cellGrid[nx, ny].isCorridor) break;

            c++;
        }
        return c;
    }

    // ROOM ready
    // Used by Seed_AlongCorridors algorithm
    // Return a direction 90 degrees left or right.
    Vector2Int Perp(Vector2Int t, bool left) => DungeonGridUtility.Perpendicular(t, left);

    // In-place shuffle of a list of Vector2Int
    void Shuffle(List<Vector2Int> list) => DungeonGridUtility.Shuffle(list, rng);

    // CONVERTED to ROOM
    // Used by Seed_AlongCorridors algorithm and Scraps_SeedAndGrowUntilPacked algorithm
    // Checks if location is unoccupied and moat cells around it are clear also.
    bool CanPlaceSeed(int x, int y, int moatCells)
    {
        return PackedCellGridUtility.CanPlaceSeed(cellGrid, x, y, moatCells, cfg.mapWidth, cfg.mapHeight, cfg.borderKeepout);
    }

    // CONVERTED to ROOM
    // Used by Seed_AlongCorridors algorithm
    // assumes location is a valid seed location = passed CanPlaceSeed()
    void CreateRoomSeedAt(int x, int y)
    {
        PackedCellGridUtility.CreateRoomSeedAt(rooms, cellGrid, x, y);
    }

    public Room ExtractRoomFromVectors(List<Vector2Int> vect)
    {
        Color color;
        Debug.Log($"Extracting {vect.Count} vectors..");

        var result = new Room();
        //HashSet<(int, int)> corridorHash = new();
        // Convert corridors
        foreach (var pr in vect)
        {
            List<Cell> cells = new();

            //if (pr.cells.Count == 0) continue;
            // Finalize room bounds
            int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;

            // Create this Room's cell list (x,y) only
            foreach (var c in vect)
            {
                cells.Add(new Cell(c.x, c.y));
                { if (c.x < minx) minx = c.x; if (c.x > maxx) maxx = c.x; if (c.y < miny) miny = c.y; if (c.y > maxy) maxy = c.y; }
            }

            // Create Room object
            var r = new Room
            {
                my_room_number = 1,
                area = cells.Count,
                bounds = new RectInt(minx, miny, Mathf.Max(1, maxx - minx + 1), Mathf.Max(1, maxy - miny + 1)),
                cells = cells,
                isCorridor = true,
            };
            r.setColorFloor(highlight: false);
            color = r.colorFloor;
            foreach (Cell cell in r.cells) cell.colorFloor = color;

            return r;
        }

        return result;
    }
}
