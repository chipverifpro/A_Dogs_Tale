using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static partial class PackedRoomDoorConnectivityUtility
{
    public static void StripDoorsForOpenAirTheme(List<Room> rooms, bool isOpenAirTheme)
    {
        if (!isOpenAirTheme || rooms == null)
            return;

        foreach (Room room in rooms)
        {
            if (room == null)
                continue;

            room.doors.Clear();

            if (room.cells == null)
                continue;

            foreach (Cell cell in room.cells)
            {
                if (cell == null)
                    continue;

                cell.doors = DirFlags.None;
            }
        }
    }

    public static void RefreshRoomsConnectedToCorridor(List<Room> rooms, Cell[,] cellGrid, List<DoorCandidate> candidates, int width, int height, int borderKeepout)
    {
        if (rooms == null || rooms.Count == 0)
            return;

        for (int roomId = 0; roomId < rooms.Count; roomId++)
            rooms[roomId].connectedToCorridor = false;

        rooms[0].connectedToCorridor = true;

        var queue = new Queue<int>();
        queue.Enqueue(0);

        while (queue.Count > 0)
        {
            int connectedRoomId = queue.Dequeue();

            for (int i = 0; i < candidates.Count; i++)
            {
                DoorCandidate candidate = candidates[i];
                if (!IsDoorCandidatePlacedByGrid(cellGrid, candidate, width, height, borderKeepout))
                    continue;

                if (candidate.toCorridor)
                {
                    if (!rooms[candidate.roomId].connectedToCorridor)
                    {
                        rooms[candidate.roomId].connectedToCorridor = true;
                        queue.Enqueue(candidate.roomId);
                    }

                    continue;
                }

                if (candidate.roomId == connectedRoomId && !rooms[candidate.targetRoomId].connectedToCorridor)
                {
                    rooms[candidate.targetRoomId].connectedToCorridor = true;
                    queue.Enqueue(candidate.targetRoomId);
                }
                else if (candidate.targetRoomId == connectedRoomId && !rooms[candidate.roomId].connectedToCorridor)
                {
                    rooms[candidate.roomId].connectedToCorridor = true;
                    queue.Enqueue(candidate.roomId);
                }
            }
        }
    }

    public static int CountRoomsConnectedToCorridor(List<Room> rooms)
    {
        if (rooms == null)
            return 0;

        int count = 0;
        for (int roomId = 0; roomId < rooms.Count; roomId++)
        {
            if (rooms[roomId] != null && rooms[roomId].connectedToCorridor)
                count++;
        }

        return count;
    }

    public static bool IsDoorCandidatePlacedByGrid(Cell[,] cellGrid, DoorCandidate candidate, int width, int height, int borderKeepout)
    {
        if (cellGrid == null || !DungeonGridUtility.InBoundsWithKeepout(candidate.x, candidate.y, width, height, borderKeepout))
            return false;

        Cell source = cellGrid[candidate.x, candidate.y];
        if (source == null || !source.doors.HasFlag(candidate.dir))
            return false;

        UnityEngine.Vector2Int dirVec = candidate.dir.ToVector2Int();
        int nearX = candidate.x + dirVec.x;
        int nearY = candidate.y + dirVec.y;
        if (!DungeonGridUtility.InBoundsWithKeepout(nearX, nearY, width, height, borderKeepout))
            return false;

        Cell near = cellGrid[nearX, nearY];
        return near != null && near.doors.HasFlag(candidate.dir.Opposite());
    }
}
