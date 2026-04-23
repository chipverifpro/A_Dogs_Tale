using System.Collections.Generic;
using UnityEngine;

public static partial class PackedCellGridUtility
{
    public static Cell[,] CreateEmptyCellGrid(int width, int height, Color defaultColor)
    {
        Cell[,] cellGrid = new Cell[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                cellGrid[x, y] = CreateEmptyCell(x, y, defaultColor);
            }
        }

        return cellGrid;
    }

    public static Cell[,] CreateCellGridFromRooms(List<Room> rooms, int width, int height, Color defaultColor)
    {
        Cell[,] cellGrid = new Cell[width, height];

        foreach (Room room in rooms)
        {
            foreach (Cell roomCell in room.cells)
            {
                cellGrid[roomCell.pos.x, roomCell.pos.y] = roomCell;
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (cellGrid[x, y] == null)
                    cellGrid[x, y] = CreateEmptyCell(x, y, defaultColor);
            }
        }

        return cellGrid;
    }

    private static Cell CreateEmptyCell(int x, int y, Color defaultColor)
    {
        Cell cell = new Cell(x, y);
        cell.room_number = -1;
        cell.isCorridor = false;
        cell.colorFloor = defaultColor;
        return cell;
    }
}
