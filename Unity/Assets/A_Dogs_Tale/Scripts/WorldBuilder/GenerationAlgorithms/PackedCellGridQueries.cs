using System.Collections.Generic;

public static partial class PackedCellGridUtility
{
    public static void UpdateRoomsFromCellGrid(List<Room> rooms, Cell[,] cellGrid)
    {
        for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            Room room = rooms[roomIndex];
            for (int cellIndex = 0; cellIndex < room.cells.Count; cellIndex++)
            {
                Cell cell = room.cells[cellIndex];
                rooms[roomIndex].cells[cellIndex] = cellGrid[cell.pos.x, cell.pos.y];
            }
        }
    }

    public static IEnumerable<(int x, int y)> FourNeighbors(int x, int y, int width, int height)
    {
        int maxX = width - 1;
        int maxY = height - 1;
        if (x > 0) yield return (x - 1, y);
        if (x < maxX - 1) yield return (x + 1, y);
        if (y > 0) yield return (x, y - 1);
        if (y < maxY - 1) yield return (x, y + 1);
    }

    public static bool IsNearCorridor(Cell[,] cellGrid, int x, int y, int moatCells, int width, int height, int keepout)
    {
        for (int dy = -moatCells; dy <= moatCells; dy++)
        {
            for (int dx = -moatCells; dx <= moatCells; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (!DungeonGridUtility.InBoundsWithKeepout(nx, ny, width, height, keepout)) continue;
                if (cellGrid[nx, ny].isCorridor) return true;
            }
        }

        return false;
    }

    public static bool ClearOfForeign(Cell[,] cellGrid, int x, int y, int moatCells, int width, int height, int keepout)
    {
        for (int dy = -moatCells; dy <= moatCells; dy++)
        {
            for (int dx = -moatCells; dx <= moatCells; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (!DungeonGridUtility.InBoundsWithKeepout(nx, ny, width, height, keepout)) continue;

                Cell neighbor = cellGrid[nx, ny];
                if (neighbor.isCorridor) return false;
                if (neighbor.room_number >= 0) return false;
            }
        }

        return true;
    }

    public static bool TouchesDifferentOrCorridor(Cell[,] cellGrid, int[,] label, int x, int y, int my, int moatCells, int width, int height, int keepout)
    {
        for (int dy = -moatCells; dy <= moatCells; dy++)
        {
            for (int dx = -moatCells; dx <= moatCells; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (!DungeonGridUtility.InBoundsWithKeepout(nx, ny, width, height, keepout)) continue;

                if (cellGrid[nx, ny].isCorridor) return true;
                int neighborLabel = label[nx, ny];
                if (neighborLabel >= 0 && neighborLabel != my) return true;
            }
        }

        return false;
    }

    public static bool CanClaim(Cell[,] cellGrid, int x, int y, int moatCells, int width, int height, int keepout)
    {
        if (!DungeonGridUtility.InBoundsWithKeepout(x, y, width, height, keepout)) return false;

        Cell cell = cellGrid[x, y];
        if (cell.isCorridor) return false;
        if (cell.room_number >= 0) return false;

        for (int dy = -moatCells; dy <= moatCells; dy++)
        {
            for (int dx = -moatCells; dx <= moatCells; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (!DungeonGridUtility.InBoundsWithKeepout(nx, ny, width, height, keepout)) return false;

                Cell neighbor = cellGrid[nx, ny];
                if (neighbor.isCorridor) return false;
                if (neighbor.room_number >= 0) return false;
            }
        }

        return true;
    }
}
