using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static partial class PackedRoomDoorConnectivityUtility
{
    public static IEnumerator EnsureRoomsConnectToCorridor(
        List<Room> rooms,
        Cell[,] cellGrid,
        HashSet<(int, int)> corridors,
        int width,
        int height,
        int borderKeepout,
        int moat,
        int minDoorSpacing,
        int yieldEvery)
    {
        if (rooms == null || rooms.Count == 0)
            yield break;

        int maxPasses = Mathf.Max(1, rooms.Count * 2);

        for (int pass = 0; pass < maxPasses; pass++)
        {
            var candidates = DoorCandidateCollector.Collect(rooms, cellGrid, width, height, moat, minDoorSpacing);
            RefreshRoomsConnectedToCorridor(rooms, cellGrid, candidates, width, height, borderKeepout);

            int connectedBefore = CountRoomsConnectedToCorridor(rooms);
            if (connectedBefore >= rooms.Count)
                yield break;

            bool placedAnyDoorThisPass = false;
            bool needAnotherPass = false;
            int processed = 0;

            for (int roomId = 1; roomId < rooms.Count; roomId++)
            {
                if (rooms[roomId] == null || rooms[roomId].connectedToCorridor)
                    continue;

                if (TryFindBridgeCandidateToConnectedRoom(rooms, cellGrid, candidates, roomId, width, height, borderKeepout, out DoorCandidate repairCandidate))
                {
                    if (DoorPlacementUtility.TryPlaceDoor(repairCandidate, cellGrid, corridors))
                    {
                        repairCandidate.placed = true;
                        placedAnyDoorThisPass = true;
                        Debug.Log($"Placed a door into room {roomId}");
                    }
                    else
                    {
                        needAnotherPass = true;
                    }
                }
                else
                {
                    needAnotherPass = true;
                }

                if ((++processed % yieldEvery) == 0)
                    yield return null;
            }

            candidates = DoorCandidateCollector.Collect(rooms, cellGrid, width, height, moat, minDoorSpacing);
            RefreshRoomsConnectedToCorridor(rooms, cellGrid, candidates, width, height, borderKeepout);

            int connectedAfter = CountRoomsConnectedToCorridor(rooms);
            if (connectedAfter >= rooms.Count)
            {
                Debug.Log($"[PackedRooms] Corridor connectivity repaired in {pass + 1} pass(es).");
                yield break;
            }

            if (!placedAnyDoorThisPass || connectedAfter <= connectedBefore)
            {
                Debug.LogWarning($"[PackedRooms] Unable to fully connect packed rooms to corridor. Connected {connectedAfter}/{rooms.Count} rooms.");
                yield break;
            }

            if (!needAnotherPass)
                yield break;
        }

        var finalCandidates = DoorCandidateCollector.Collect(rooms, cellGrid, width, height, moat, minDoorSpacing);
        RefreshRoomsConnectedToCorridor(rooms, cellGrid, finalCandidates, width, height, borderKeepout);
        Debug.LogWarning($"[PackedRooms] Connectivity repair hit max passes. Connected {CountRoomsConnectedToCorridor(rooms)}/{rooms.Count} rooms.");
    }

    public static bool TryFindBridgeCandidateToConnectedRoom(
        List<Room> rooms,
        Cell[,] cellGrid,
        List<DoorCandidate> candidates,
        int roomId,
        int width,
        int height,
        int borderKeepout,
        out DoorCandidate bestCandidate)
    {
        bestCandidate = null;
        int bestRank = int.MaxValue;
        int bestScore = int.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            DoorCandidate candidate = candidates[i];
            if (IsDoorCandidatePlacedByGrid(cellGrid, candidate, width, height, borderKeepout))
                continue;

            bool bridgesToConnectedRoom = false;
            int rank = 1;

            if (candidate.toCorridor)
            {
                bridgesToConnectedRoom = candidate.roomId == roomId;
                rank = 0;
            }
            else if (candidate.roomId == roomId && candidate.targetRoomId >= 0 && rooms[candidate.targetRoomId].connectedToCorridor)
            {
                bridgesToConnectedRoom = true;
            }
            else if (candidate.targetRoomId == roomId && candidate.roomId >= 0 && rooms[candidate.roomId].connectedToCorridor)
            {
                bridgesToConnectedRoom = true;
            }
            if (!bridgesToConnectedRoom)
                continue;

            if (rank < bestRank || (rank == bestRank && candidate.score < bestScore))
            {
                bestCandidate = candidate;
                bestRank = rank;
                bestScore = candidate.score;
            }
        }

        return bestCandidate != null;
    }
}
