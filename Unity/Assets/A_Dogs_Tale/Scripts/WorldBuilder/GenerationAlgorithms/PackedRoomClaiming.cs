using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    // Unused.
    // Temporary hack for getting rid of lost cells.
    int DeleteAllCellsAtPos(Vector2Int pos)
    {
        int num_deleted = 0;
        for (int try_room_id = 0; try_room_id < rooms.Count; try_room_id++)
        {
            int cell_index = rooms[try_room_id].cells.FindIndex(cell => cell.pos.x == pos.x && cell.pos.y == pos.y);
            if (cell_index != -1)
            {
                Cell cell = rooms[try_room_id].cells[cell_index];
                rooms[try_room_id].cells.RemoveAt(cell_index);
                num_deleted++;
            }
        }
        return num_deleted;
    }

    // ROOMS READY
    // Builds one HashSet frontier for room (ri) containing all the cells that the room
    //   could grow to.
    void RebuildFrontierFor(int ri, int moatCells, HashSet<(int, int)> dst)
    {
        dst.Clear();
        foreach (var c in rooms[ri].cells)
            foreach (var nb in FourNeighbors(c.x, c.y))
                if (CanClaim(ri, nb.x, nb.y, moatCells))
                    dst.Add((nb.x, nb.y));
    }

    // ComputeAabb() gets the bounding rectangle of all cell locations in a Room
    // same as Room.GetBounds()?????????????????????
    RectInt ComputeAabb(Room r)
    {
        int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;
        foreach (var c in r.cells)
        {
            if (c.x < minx) minx = c.x; if (c.x > maxx) maxx = c.x;
            if (c.y < miny) miny = c.y; if (c.y > maxy) maxy = c.y;
        }
        if (minx == int.MaxValue) return new RectInt(0, 0, 0, 0);
        return new RectInt(minx, miny, maxx - minx + 1, maxy - miny + 1);
    }

    // Unused.
    // Wavefront helpers (compactness-biased pick).
    // Selects best cell in frontier, based on score:
    //    SCORE: -3: not close to corridor
    //           +2: adjacent to more cells in the same room
    (int x, int y) PickFrontier_CompactBias(HashSet<(int x, int y)> frontier, int ri)
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;    // Debug was -1
        int bestScore = int.MinValue; (int x, int y) best = (-1, -1);
        foreach (var p in frontier)
        {
            int s = 0;
            foreach (var nb in FourNeighbors(p.x, p.y))
            {
                if (!In(nb.x, nb.y)) continue;                      // off-map
                if (cellGrid[nb.x, nb.y].room_number == ri) s += 2; // prefer filling along our boundary
                if (cellGrid[nb.x, nb.y].isCorridor) s -= 3;        // keep distance to corridors
            }
            if (s > bestScore) { bestScore = s; best = p; }
        }
        if (best.x < 0 && frontier.Count > 0) foreach (var p in frontier) { best = p; break; }
        return best;
    }

    // ROOM READY
    // Easy little routine to return location of the cells in 4 directions, skipping those that point off-map.
    IEnumerable<(int x, int y)> FourNeighbors(int x, int y)
    {
        return PackedCellGridUtility.FourNeighbors(x, y, cfg.mapWidth, cfg.mapHeight);
    }

    // ROOM READY
    // Nearly the same as Can Place Seed.  Can easily merge functionality.  TODO.
    // Only difference is that ri is passed in and may return true if the
    //   room already owns the cell or neighbors which is useful here.
    bool CanClaim(int ri, int x, int y, int moatCells)
    {
        return PackedCellGridUtility.CanClaim(cellGrid, x, y, moatCells, cfg.mapWidth, cfg.mapHeight, cfg.borderKeepout);
    }

    // ROOM READY
    // Grabs the cell from cellGrid, and adds it to a given room (ri).
    // Does not check previous owner except to determine if it already owns it.
    void ClaimCell(int ri, int x, int y)
    {
        PackedCellGridUtility.ClaimCell(rooms, cellGrid, ri, x, y);
    }
}
