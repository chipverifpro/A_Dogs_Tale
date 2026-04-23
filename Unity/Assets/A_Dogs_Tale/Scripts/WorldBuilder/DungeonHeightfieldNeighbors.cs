using UnityEngine;

public partial class DungeonGenerator
{
    public Cell[,] GetEightNeighborCells(Cell centerCell, int threshold = 50)
    {
        Cell[,] neighbors = new Cell[3, 3];
        neighbors[1, 1] = centerCell;

        int x = centerCell.x;
        int y = centerCell.y;
        int z = centerCell.z;

        // Relative direction vectors in grid coords (not array coords)
        Vector2Int relN = DirFlagsEx.ToVector2Int(DirFlags.N);
        Vector2Int relE = DirFlagsEx.ToVector2Int(DirFlags.E);
        Vector2Int relS = DirFlagsEx.ToVector2Int(DirFlags.S);
        Vector2Int relW = DirFlagsEx.ToVector2Int(DirFlags.W);

        // Helper to place a neighbor at relative offset
        bool TryPlaceAt(Vector2Int rel, out Cell placedCell)
        {
            placedCell = null;

            if (!hf.TryQueryAt(x + rel.x, y + rel.y, z, threshold, out NeighborMatch match))
                return false;

            placedCell = rooms[match.roomId].cells[match.cellId];
            neighbors[1 + rel.x, 1 + rel.y] = placedCell;
            return true;
        }

        // STEP 1: cardinals if no wall on center cell in that direction
        if (!centerCell.walls.HasFlag(DirFlags.N)) TryPlaceAt(relN, out _);
        if (!centerCell.walls.HasFlag(DirFlags.E)) TryPlaceAt(relE, out _);
        if (!centerCell.walls.HasFlag(DirFlags.S)) TryPlaceAt(relS, out _);
        if (!centerCell.walls.HasFlag(DirFlags.W)) TryPlaceAt(relW, out _);

        // Fetch cardinal cells (may be null). center of array is at 1,1
        Cell nCell = neighbors[1+relN.x, 1+relN.y];
        Cell eCell = neighbors[1+relE.x, 1+relE.y];
        Cell sCell = neighbors[1+relS.x, 1+relS.y];
        Cell wCell = neighbors[1+relW.x, 1+relW.y];

        // STEP 2: corner accessibility (either route open)
        bool canNW =
            (nCell != null && !nCell.walls.HasFlag(DirFlags.W)) ||
            (wCell != null && !wCell.walls.HasFlag(DirFlags.N));

        bool canNE =
            (nCell != null && !nCell.walls.HasFlag(DirFlags.E)) ||
            (eCell != null && !eCell.walls.HasFlag(DirFlags.N));

        bool canSW =
            (sCell != null && !sCell.walls.HasFlag(DirFlags.W)) ||
            (wCell != null && !wCell.walls.HasFlag(DirFlags.S));

        bool canSE =
            (sCell != null && !sCell.walls.HasFlag(DirFlags.E)) ||
            (eCell != null && !eCell.walls.HasFlag(DirFlags.S));

        // STEP 3: place corners using RELATIVE offsets
        if (canNW) TryPlaceAt(relN + relW, out _); // (-1, +1)
        if (canNE) TryPlaceAt(relN + relE, out _); // (+1, +1)
        if (canSW) TryPlaceAt(relS + relW, out _); // (-1, -1)
        if (canSE) TryPlaceAt(relS + relE, out _); // (+1, -1)

        return neighbors;
    }
}
