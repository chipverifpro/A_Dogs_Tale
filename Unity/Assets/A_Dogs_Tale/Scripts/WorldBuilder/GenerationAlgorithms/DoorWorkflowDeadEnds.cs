using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static partial class DoorWorkflowUtility
{
    public static IEnumerator ConnectLooseEnds(Cell[,] cellGrid, HashSet<(int, int)> corridors, int reach, int moat, int yieldEvery)
    {
        int width = cellGrid.GetLength(0);
        int height = cellGrid.GetLength(1);
        int touched = 0;

        foreach (var tip in CorridorDeadEnds(cellGrid, width, height))
        {
            Cell corridorTip = cellGrid[tip.x, tip.y];

            foreach (var dir in DirFlagsEx.AllCardinals)
            {
                Vector2Int dirVec = dir.ToVector2Int();
                int nx = tip.x + dirVec.x;
                int ny = tip.y + dirVec.y;
                int span = 0;

                while (span < reach)
                {
                    if (!DungeonGridUtility.InBounds(nx, ny, width, height)) break;

                    Cell neighbor = cellGrid[nx, ny];
                    if (!neighbor.isCorridor && neighbor.room_number >= 0)
                    {
                        var candidate = new DoorCandidate
                        {
                            x = nx,
                            y = ny,
                            dir = dir.Opposite(),
                            span = Mathf.Min(span, moat),
                            toCorridor = true,
                            targetRoomId = -1,
                            roomId = neighbor.room_number,
                            score = 0,
                            cellA = corridorTip,
                            cellB = neighbor
                        };

                        DoorPlacementUtility.TryPlaceDoor(candidate, cellGrid, corridors);
                        break;
                    }

                    if (!neighbor.isCorridor && neighbor.room_number < 0)
                    {
                        span++;
                        nx += dirVec.x;
                        ny += dirVec.y;
                        continue;
                    }

                    break;
                }

                if ((++touched % yieldEvery) == 0) yield return null;
            }
        }
    }

    private static IEnumerable<Vector2Int> CorridorDeadEnds(Cell[,] cellGrid, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Cell cell = cellGrid[x, y];
                if (!cell.isCorridor) continue;

                int degree = 0;
                foreach (var neighbor in DungeonGridUtility.FourNeighbors(x, y, width, height))
                {
                    if (cellGrid[neighbor.x, neighbor.y].isCorridor)
                        degree++;
                }

                if (degree == 1)
                    yield return new Vector2Int(x, y);
            }
        }
    }
}
