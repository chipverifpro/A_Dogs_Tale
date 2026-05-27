using UnityEngine;
using System.Collections.Generic;

namespace DogGame.Tasks
{
    public readonly struct DoorLookupInfo
    {
        public readonly int doorId;
        public readonly int roomId;
        public readonly Vector2Int cell;
        public readonly DirFlags direction;
        public readonly Vector2Int throughCell;
        public readonly int targetRoomId;

        public DoorLookupInfo(int doorId, int roomId, Vector2Int cell, DirFlags direction, Vector2Int throughCell, int targetRoomId)
        {
            this.doorId = doorId;
            this.roomId = roomId;
            this.cell = cell;
            this.direction = direction;
            this.throughCell = throughCell;
            this.targetRoomId = targetRoomId;
        }
    }

    public static class DoorIdUtility
    {
        public static int Build(Vector2Int fromCell, DirFlags direction)
        {
            Vector2Int toCell = fromCell + direction.ToVector2Int();
            Canonicalize(ref fromCell, ref toCell);

            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + fromCell.x;
                hash = (hash * 31) + fromCell.y;
                hash = (hash * 31) + toCell.x;
                hash = (hash * 31) + toCell.y;
                return hash;
            }
        }

        public static bool TryGetDoorId(IReadOnlyList<Room> rooms, int roomId, Vector2Int cell, DirFlags direction, out int doorId)
        {
            doorId = -1;

            if (rooms == null || roomId < 0 || roomId >= rooms.Count)
                return false;

            if (!direction.IsCardinal())
                return false;

            Room room = rooms[roomId];
            if (room == null || room.cells == null)
                return false;

            for (int i = 0; i < room.cells.Count; i++)
            {
                Cell roomCell = room.cells[i];
                if (roomCell == null || roomCell.pos != cell)
                    continue;

                if ((roomCell.doors & direction) == 0)
                    return false;

                doorId = Build(cell, direction);
                return true;
            }

            return false;
        }

        public static bool TryGetDoorInfo(IReadOnlyList<Room> rooms, int doorId, out DoorLookupInfo info)
        {
            info = default;

            if (rooms == null)
                return false;

            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                Room room = rooms[roomIndex];
                if (room == null || room.cells == null)
                    continue;

                if (TryGetDoorInfoInRoom(rooms, room, doorId, out info))
                    return true;
            }

            return false;
        }

        public static bool TryGetDoorInfoInRoom(IReadOnlyList<Room> rooms, Room room, int doorId, out DoorLookupInfo info)
        {
            info = default;

            if (room == null || room.cells == null)
                return false;

            for (int i = 0; i < room.cells.Count; i++)
            {
                Cell cell = room.cells[i];
                if (cell == null || cell.doors == DirFlags.None)
                    continue;

                foreach (DirFlags direction in DirFlagsEx.AllCardinals)
                {
                    if ((cell.doors & direction) == 0)
                        continue;

                    int resolvedDoorId = Build(cell.pos, direction);
                    if (resolvedDoorId != doorId)
                        continue;

                    Vector2Int throughCell = cell.pos + direction.ToVector2Int();
                    int roomId = ResolveRoomId(room);
                    int targetRoomId = ResolveRoomIdAtCell(rooms, throughCell);
                    info = new DoorLookupInfo(resolvedDoorId, roomId, cell.pos, direction, throughCell, targetRoomId);
                    return true;
                }
            }

            return false;
        }

        public static void GetDoorInfos(IReadOnlyList<Room> rooms, int doorId, List<DoorLookupInfo> results)
        {
            if (results == null)
                return;

            results.Clear();

            if (rooms == null)
                return;

            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                Room room = rooms[roomIndex];
                if (room == null || room.cells == null)
                    continue;

                for (int cellIndex = 0; cellIndex < room.cells.Count; cellIndex++)
                {
                    Cell cell = room.cells[cellIndex];
                    if (cell == null || cell.doors == DirFlags.None)
                        continue;

                    foreach (DirFlags direction in DirFlagsEx.AllCardinals)
                    {
                        if ((cell.doors & direction) == 0)
                            continue;

                        int resolvedDoorId = Build(cell.pos, direction);
                        if (resolvedDoorId != doorId)
                            continue;

                        Vector2Int throughCell = cell.pos + direction.ToVector2Int();
                        results.Add(new DoorLookupInfo(
                            resolvedDoorId,
                            ResolveRoomId(room),
                            cell.pos,
                            direction,
                            throughCell,
                            ResolveRoomIdAtCell(rooms, throughCell)));
                    }
                }
            }
        }

        private static void Canonicalize(ref Vector2Int a, ref Vector2Int b)
        {
            if (a.x < b.x)
                return;

            if (a.x == b.x && a.y <= b.y)
                return;

            (a, b) = (b, a);
        }

        private static int ResolveRoomId(Room room)
        {
            return room != null ? room.my_room_number : -1;
        }

        private static int ResolveRoomIdAtCell(IReadOnlyList<Room> rooms, Vector2Int cell)
        {
            if (rooms == null)
                return -1;

            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                Room room = rooms[roomIndex];
                if (room == null || room.cells == null)
                    continue;

                for (int cellIndex = 0; cellIndex < room.cells.Count; cellIndex++)
                {
                    Cell roomCell = room.cells[cellIndex];
                    if (roomCell != null && roomCell.pos == cell)
                        return ResolveRoomId(room);
                }
            }

            return -1;
        }
    }
}
