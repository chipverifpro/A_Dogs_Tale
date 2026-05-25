using System.Collections;
using System.Collections.Generic;

public static partial class DoorWorkflowUtility
{
    public static IEnumerator EnsureConnectivity(List<Room> rooms, Cell[,] cellGrid, HashSet<(int, int)> corridors, List<DoorCandidate> candidates, int maxDoorsPerRoom, int yieldEvery)
    {
        int roomCount = rooms.Count;
        var unionFind = new UnionFind(roomCount);
        var doorsUsedPerRoom = new int[roomCount];
        int chosen = 0;

        foreach (var candidate in candidates)
        {
            if (candidate.toCorridor)
            {
                if (doorsUsedPerRoom[candidate.roomId] >= maxDoorsPerRoom) continue;

                bool placed = DoorPlacementUtility.TryPlaceDoor(candidate, cellGrid, corridors);
                candidate.placed = placed;

                if (placed)
                {
                    doorsUsedPerRoom[candidate.roomId]++;
                    chosen++;
                }
            }
            else
            {
                if (unionFind.Connected(candidate.roomId, candidate.targetRoomId)) continue;
                if (doorsUsedPerRoom[candidate.roomId] >= maxDoorsPerRoom) continue;
                if (doorsUsedPerRoom[candidate.targetRoomId] >= maxDoorsPerRoom) continue;

                bool placed = DoorPlacementUtility.TryPlaceDoor(candidate, cellGrid, corridors);
                candidate.placed = placed;

                if (placed)
                {
                    unionFind.Union(candidate.roomId, candidate.targetRoomId);
                    doorsUsedPerRoom[candidate.roomId]++;
                    doorsUsedPerRoom[candidate.targetRoomId]++;
                    chosen++;

                    if (unionFind.Components == 1) break;
                }
            }

            if (chosen % yieldEvery == 0) yield return null;
        }
    }

    public static IEnumerator AddLoopDoors(List<Room> rooms, Cell[,] cellGrid, HashSet<(int, int)> corridors, List<DoorCandidate> candidates, int extraTarget, int maxDoorsPerRoom, int yieldEvery)
    {
        if (extraTarget <= 0) yield break;

        Shuffle(candidates);

        int added = 0;
        var perRoom = new int[rooms.Count];

        foreach (var candidate in candidates)
        {
            if (added >= extraTarget) break;

            if (!candidate.toCorridor && (perRoom[candidate.roomId] >= maxDoorsPerRoom || perRoom[candidate.targetRoomId] >= maxDoorsPerRoom))
                continue;
            if (candidate.toCorridor && perRoom[candidate.roomId] >= maxDoorsPerRoom)
                continue;

            bool placed;
            if (placed = DoorPlacementUtility.TryPlaceDoor(candidate, cellGrid, corridors))
            {
                if (candidate.toCorridor)
                    perRoom[candidate.roomId]++;
                else
                {
                    perRoom[candidate.roomId]++;
                    perRoom[candidate.targetRoomId]++;
                }

                added++;
            }

            candidate.placed = placed;
            if (added % yieldEvery == 0) yield return null;
        }
    }
}
