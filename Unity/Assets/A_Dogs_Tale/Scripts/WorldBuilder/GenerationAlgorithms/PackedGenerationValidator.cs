using System.Collections.Generic;
using UnityEngine;

public static class PackedGenerationValidator
{
    public static bool CheckRoomsToGridConsistency(List<Room> rooms, Cell[,] cellGrid, int mapWidth, int mapHeight, int borderKeepout)
    {
        int mismatches = 0;
        int gridWidth = cellGrid.GetLength(0);
        int gridHeight = cellGrid.GetLength(1);

        for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            Room room = rooms[roomIndex];
            if (room.my_room_number != roomIndex)
            {
                Debug.LogWarning($"Room index mismatch: rooms[{roomIndex}].my_room_number == {room.my_room_number}");
                mismatches++;
            }

            foreach (Cell cell in room.cells)
            {
                int x = cell.pos.x;
                int y = cell.pos.y;

                if ((uint)x >= (uint)gridWidth || (uint)y >= (uint)gridHeight)
                {
                    Debug.LogWarning($"Room->Grid: cell {x},{y} out of bounds for grid {gridWidth}x{gridHeight} (room {roomIndex}).");
                    mismatches++;
                    continue;
                }

                Cell gridCell = cellGrid[x, y];
                if (gridCell == null)
                {
                    Debug.LogWarning($"Room->Grid: grid cell null at {x},{y} (room {roomIndex}).");
                    mismatches++;
                    continue;
                }

                mismatches += ReportCellDifferences(cell, gridCell, roomIndex, cellGrid, mapWidth, mapHeight, borderKeepout);
            }
        }

        if (mismatches > 0)
            Debug.LogWarning($"CheckRoomsToGridConsistancy: found {mismatches} mismatches.");

        return mismatches == 0;
    }

    private static int ReportCellDifferences(Cell a, Cell b, int expectedRoom, Cell[,] cellGrid, int mapWidth, int mapHeight, int borderKeepout)
    {
        int diffs = 0;
        const float EPS = 1e-4f;

        if (a.room_number != expectedRoom)
        {
            Debug.Log($"Cell {a.pos.x},{a.pos.y}: room_number {a.room_number} != expected {expectedRoom}");
            diffs++;
        }

        if (b.room_number != expectedRoom)
        {
            Debug.Log($"Grid {b.pos.x},{b.pos.y}: room_number {b.room_number} != expected {expectedRoom}");
            diffs++;
        }

        if (a.pos != b.pos)
        {
            Debug.Log($"Cell {a.pos} vs Grid {b.pos}: pos mismatch");
            diffs++;
        }

        if (a.height != b.height)
        {
            Debug.Log($"Cell {a.pos}: height {a.height} vs Grid {b.height}");
            diffs++;
        }

        if (a.type != b.type)
        {
            Debug.Log($"Cell {a.pos}: type {a.type} vs Grid {b.type}");
            diffs++;
        }

        if (a.walls != b.walls)
        {
            Debug.Log($"Cell {a.pos}: walls {a.walls} vs Grid {b.walls}");
            diffs++;
        }

        if (a.doors != b.doors)
        {
            Debug.Log($"Cell {a.pos}: doors {a.doors} vs Grid {b.doors}");
            diffs++;
        }

        foreach (DirFlags dir in DirFlagsEx.AllCardinals)
        {
            DirFlags oppositeDir = DirFlagsEx.Opposite(dir);
            Vector2Int dirVec = DirFlagsEx.ToVector2Int(dir);
            if (dirVec == Vector2Int.zero) continue;
            if (!DungeonGridUtility.InBoundsWithKeepout(a.pos.x + dirVec.x, a.pos.y + dirVec.y, mapWidth, mapHeight, borderKeepout)) continue;

            if (a.doors.HasFlag(dir) != cellGrid[a.pos.x + dirVec.x, a.pos.y + dirVec.y].doors.HasFlag(oppositeDir))
            {
                Debug.LogError($"Grid {a.pos}: door {dir} has no match in Grid {a.pos.x + dirVec.x},{a.pos.y + dirVec.y} door {oppositeDir}");
                diffs++;
            }

            if (a.walls.HasFlag(dir) != cellGrid[a.pos.x + dirVec.x, a.pos.y + dirVec.y].walls.HasFlag(oppositeDir))
            {
                Debug.LogError($"Grid {a.pos}: wall {dir} has no match in Grid {a.pos.x + dirVec.x},{a.pos.y + dirVec.y}: wall {oppositeDir}");
                diffs++;
            }
        }

        if (!ColorApprox(a.colorFloor, b.colorFloor))
        {
            Debug.Log($"Cell {a.pos}: colorFloor {a.colorFloor} vs Grid {b.colorFloor}");
            diffs++;
        }

        if (!QuatApprox(a.tiltFloor, b.tiltFloor, 0.1f))
        {
            Debug.Log($"Cell {a.pos}: tiltFloor {a.tiltFloor.eulerAngles} vs Grid {b.tiltFloor.eulerAngles}");
            diffs++;
        }

        if (Mathf.Abs(a.travel_cost - b.travel_cost) > EPS)
        {
            Debug.Log($"Cell {a.pos}: travel_cost {a.travel_cost} vs Grid {b.travel_cost}");
            diffs++;
        }

        if (a.isCorridor != b.isCorridor)
        {
            Debug.Log($"Cell {a.pos}: isCorridor {a.isCorridor} vs Grid {b.isCorridor}");
            diffs++;
        }

        return diffs;
    }

    private static bool ColorApprox(Color a, Color b, float eps = 1e-3f)
    {
        return Mathf.Abs(a.r - b.r) <= eps
            && Mathf.Abs(a.g - b.g) <= eps
            && Mathf.Abs(a.b - b.b) <= eps
            && Mathf.Abs(a.a - b.a) <= eps;
    }

    private static bool QuatApprox(Quaternion a, Quaternion b, float maxAngleDeg)
    {
        return Quaternion.Angle(a, b) <= maxAngleDeg;
    }
}
