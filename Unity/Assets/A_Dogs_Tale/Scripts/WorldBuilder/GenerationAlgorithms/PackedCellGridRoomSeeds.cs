using System.Collections.Generic;
using UnityEngine;

public static partial class PackedCellGridUtility
{
    public static void ClaimCell(List<Room> rooms, Cell[,] cellGrid, int roomIndex, int x, int y)
    {
        Cell cell = cellGrid[x, y];
        cell.room_number = rooms[roomIndex].my_room_number;
        cell.colorFloor = rooms[roomIndex].colorFloor;
        cell.isCorridor = rooms[roomIndex].isCorridor;
        rooms[roomIndex].cells.Add(cell);
    }

    public static bool CanPlaceSeed(Cell[,] cellGrid, int x, int y, int moatCells, int width, int height, int keepout)
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
                if (!DungeonGridUtility.InBoundsWithKeepout(nx, ny, width, height, keepout)) continue;

                Cell neighbor = cellGrid[nx, ny];
                if (neighbor.isCorridor) return false;
                if (neighbor.room_number >= 0) return false;
            }
        }

        return true;
    }

    public static void CreateRoomSeedAt(List<Room> rooms, Cell[,] cellGrid, int x, int y)
    {
        Room newRoom = new Room { my_room_number = rooms.Count, cells = new List<Cell>() };
        newRoom.setColorFloor(highlight: true);
        newRoom.isCorridor = false;

        Cell newCell = new Cell(x, y);
        cellGrid[x, y] = newCell;
        newCell.room_number = newRoom.my_room_number;
        newCell.height = 0;
        newCell.colorFloor = newRoom.colorFloor;
        newRoom.cells.Add(newCell);

        newRoom.bounds = new RectInt(x, y, 1, 1);
        rooms.Add(newRoom);
    }
}
