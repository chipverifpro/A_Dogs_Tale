public partial class DungeonGenerator
{
    // ROOM READY
    // Allocate the memory for cellGrid.  Fill it with cells containing location only.
    //   Do it once at the beginning.
    void InitializeCellGrid()
    {
        cellGrid = PackedCellGridUtility.CreateEmptyCellGrid(cfg.mapWidth, cfg.mapHeight, colorDefault);
    }

    // take every cell in Rooms, and make the references in cellGrid point to the same cell for automatic cross updating.
    void UpdateCellGridFromRooms(System.Collections.Generic.List<Room> rooms)
    {
        cellGrid = PackedCellGridUtility.CreateCellGridFromRooms(rooms, cfg.mapWidth, cfg.mapHeight, colorDefault);
    }

    void UpdateRoomsFromCellGrid()
    {
        PackedCellGridUtility.UpdateRoomsFromCellGrid(rooms, cellGrid);
    }

    /*
    // unused. Obsolete. Use CanPlaceSeed() instead.
    bool CanPlaceReSeed(int x, int y, int moatCells)
    {
        if (!In(x, y)) return false;
        var c = cellGrid[x, y];
        if (c.isCorridor) return false;
        if (c.room_number >= 0) return false;

        // keep distance from corridors & existing rooms (moat)
        for (int dy = -moatCells; dy <= moatCells; dy++)
            for (int dx = -moatCells; dx <= moatCells; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (!In(nx, ny)) continue;
                var n = cellGrid[nx, ny];
                if (n.isCorridor) return false;
                if (n.room_number >= 0) return false;
            }
        return true;
    }
    */

    // ROOM READY
    // used in Scraps_SeedAndGrowUntilPacked() to verify seeds are far enough apart.
    // simple Manhattan distance (delta x + delta y) between two points.
    int Manhattan((int x, int y) a, (int x, int y) b) => DungeonGridUtility.Manhattan(a, b);

    // NOTE: these next three functions are doing similar things: (maybe combine in future)
    //       IsNearCorridor, ClearOfForeign, TouchesDifferentOrCorridor

    // ROOM READY
    // used in Scraps_VoronoiFill.
    // returns true if (x,y is at least moatCells away from a corridor)
    bool IsNearCorridor(int x, int y, int moatCells)
    {
        return PackedCellGridUtility.IsNearCorridor(cellGrid, x, y, moatCells, cfg.mapWidth, cfg.mapHeight, cfg.borderKeepout);
    }

    // ROOM READY
    // used in Scraps_VoronoiFill.
    // returns true if (x,y) is at least moatCells away from non-empty cells (other rooms and any corridor).
    bool ClearOfForeign(int x, int y, int moatCells)
    {
        return PackedCellGridUtility.ClearOfForeign(cellGrid, x, y, moatCells, cfg.mapWidth, cfg.mapHeight, cfg.borderKeepout);
    }

    // ROOM READY
    // used in Scraps_VoroniFill during peel.
    // determines if we are adjacent to a room or corridor (with moat between).
    // variables are named weird: label=?  my=label[x,y] ? what do these do?
    bool TouchesDifferentOrCorridor(int[,] label, int x, int y, int my, int moatCells)
    {
        return PackedCellGridUtility.TouchesDifferentOrCorridor(cellGrid, label, x, y, my, moatCells, cfg.mapWidth, cfg.mapHeight, cfg.borderKeepout);
    }
}
