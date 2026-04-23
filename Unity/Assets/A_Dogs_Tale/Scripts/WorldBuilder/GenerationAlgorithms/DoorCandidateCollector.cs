using System.Collections.Generic;
using UnityEngine;

public static class DoorCandidateCollector
{
    public static List<DoorCandidate> Collect(List<Room> rooms, Cell[,] cellGrid, int width, int height, int moat, int minSpacing)
    {
        var list = new List<DoorCandidate>(1024);
        var lastOnEdge = new Dictionary<(int roomId, int edgeKey), int>();

        foreach (var room in rooms)
        {
            int roomId = room.my_room_number;
            foreach (var cell in room.cells)
            {
                if (cell.isCorridor) continue;

                foreach (var dir in DirFlagsEx.AllCardinals)
                {
                    Vector2Int dirVec = dir.ToVector2Int();
                    int nx = cell.x + dirVec.x;
                    int ny = cell.y + dirVec.y;
                    int span = 0;

                    while (span <= moat)
                    {
                        if (!DungeonGridUtility.InBounds(nx, ny, width, height)) break;

                        var neighbor = cellGrid[nx, ny];
                        if (neighbor == null) break;

                        if (!neighbor.isCorridor && neighbor.room_number < 0)
                        {
                            span++;
                            nx += dirVec.x;
                            ny += dirVec.y;
                            continue;
                        }

                        if (neighbor.isCorridor)
                        {
                            int edgeKey = EdgeKey(roomId, dir);
                            if (TooClose(lastOnEdge, edgeKey, cell, minSpacing)) break;

                            list.Add(new DoorCandidate
                            {
                                x = cell.x,
                                y = cell.y,
                                dir = dir,
                                span = span,
                                toCorridor = true,
                                targetRoomId = -1,
                                roomId = roomId,
                                score = span,
                                cellA = cell,
                                cellB = neighbor
                            });

                            lastOnEdge[(roomId, edgeKey)] = EdgeMeasure(dir, cell.x, cell.y);
                        }
                        else if (neighbor.room_number != roomId)
                        {
                            int edgeKey = EdgeKey(roomId, dir);
                            if (TooClose(lastOnEdge, edgeKey, cell, minSpacing)) break;

                            list.Add(new DoorCandidate
                            {
                                x = cell.x,
                                y = cell.y,
                                dir = dir,
                                span = span,
                                toCorridor = false,
                                targetRoomId = neighbor.room_number,
                                roomId = roomId,
                                score = span,
                                placed = false,
                                cellA = cell,
                                cellB = neighbor
                            });

                            lastOnEdge[(roomId, edgeKey)] = EdgeMeasure(dir, cell.x, cell.y);
                        }

                        break;
                    }
                }
            }
        }

        list.Sort((a, b) => a.score.CompareTo(b.score));
        return list;
    }

    private static int EdgeKey(int roomId, DirFlags dir) => ((int)dir << 20) ^ roomId;

    private static int EdgeMeasure(DirFlags dir, int x, int y) => (dir == DirFlags.N || dir == DirFlags.S) ? x : y;

    private static bool TooClose(Dictionary<(int, int), int> last, int edgeKey, Cell cell, int minSpacing)
    {
        var key = (cell.room_number, edgeKey);
        if (last.TryGetValue(key, out int lastPos))
        {
            int cur = EdgeMeasure((DirFlags)(edgeKey >> 20), cell.x, cell.y);
            if (Mathf.Abs(cur - lastPos) < minSpacing) return true;
        }

        return false;
    }
}
