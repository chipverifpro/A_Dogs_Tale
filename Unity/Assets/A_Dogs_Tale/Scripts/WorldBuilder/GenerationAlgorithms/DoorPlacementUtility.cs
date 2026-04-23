using System.Collections.Generic;
using UnityEngine;

public static class DoorPlacementUtility
{
    public static bool TryPlaceDoor(DoorCandidate door, Cell[,] cellGrid, HashSet<(int, int)> corridors)
    {
        int width = cellGrid.GetLength(0);
        int height = cellGrid.GetLength(1);
        Vector2Int dirVec = door.dir.ToVector2Int();

        Cell source = cellGrid[door.x, door.y];
        if (source == null || source.room_number != door.roomId || source.isCorridor) return false;

        int cx = door.x + dirVec.x;
        int cy = door.y + dirVec.y;
        for (int i = 0; i < door.span; i++)
        {
            if (!DungeonGridUtility.InBounds(cx, cy, width, height)) return false;

            Cell wallCell = cellGrid[cx, cy];
            if (wallCell.isCorridor)
            {
                // Already open.
            }
            else if (wallCell.room_number >= 0)
            {
                return false;
            }
            else
            {
                wallCell.isCorridor = true;
                wallCell.room_number = -1;
                corridors.Add((cx, cy));
                wallCell.walls &= ~door.dir.Opposite();
            }

            cx += dirVec.x;
            cy += dirVec.y;
        }

        if (!DungeonGridUtility.InBounds(cx, cy, width, height)) return false;

        Cell target = cellGrid[cx, cy];
        if (door.toCorridor)
        {
            if (!target.isCorridor) return false;
        }
        else
        {
            if (target.room_number != door.targetRoomId || target.isCorridor) return false;
        }

        source.doors |= door.dir;

        int nearX = door.span > 0 ? (door.x + dirVec.x) : cx;
        int nearY = door.span > 0 ? (door.y + dirVec.y) : cy;
        Cell near = cellGrid[nearX, nearY];
        DirFlags opposite = door.dir.Opposite();

        near.doors |= opposite;

        if (!door.toCorridor && door.span == 0)
            target.doors |= opposite;

        return true;
    }
}
