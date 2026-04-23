using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour
{

    // === MAIN ENTRY ===
    public IEnumerator PlaceDoors()
    {
        int doorsYieldEvery = 300;
        int W = cfg.mapWidth, H = cfg.mapHeight;
        int moat = cfg.GetEffectivePackedWallMoat();

        // 1) Collect candidate door sites (room-edge touching room/corridor within ≤ moat cells in a straight line)
        var candidates = CollectDoorCandidates(W, H, moat, cfg.doors.minDoorSpacing, doorsYieldEvery);

        // 2) ConnectLooseEnds first (fix ugly dead-end corridors)
        yield return StartCoroutine(ConnectLooseEnds(candidates, cfg.doors.deadEndReach, moat, doorsYieldEvery));

        // 3) EnsureConnectivity with minimal doors (Kruskal-like: pick cheapest that bridges components)
        yield return StartCoroutine(EnsureConnectivity(candidates, moat, cfg.doors.maxDoorsPerRoom, doorsYieldEvery));

        // 4) Add extra loop doors for interest
        //int extraTarget = 0; //DEBUG
        int extraTarget = Mathf.RoundToInt(candidates.Count * cfg.doors.loopiness * 0.25f);
        yield return StartCoroutine(AddLoopDoors(candidates, extraTarget, moat, cfg.doors.minDoorSpacing, cfg.doors.maxDoorsPerRoom, doorsYieldEvery));

        DrawMapByRooms(rooms);
        //yield return new WaitForSeconds(1f);
        UpdateDoorsInRooms(candidates);
        DrawMapByRooms(rooms);
        //yield return new WaitForSeconds(1f);

        PrintCandidates(candidates);
    }

    List<DoorCandidate> CollectDoorCandidates(int W, int H, int moat, int minSpacing, int yieldEvery)
    {
        return DoorCandidateCollector.Collect(rooms, cellGrid, W, H, moat, minSpacing);
    }

    // ============================= CONNECT LOOSE ENDS =============================
    IEnumerator ConnectLooseEnds(List<DoorCandidate> candidates, int reach, int moat, int yieldEvery)
    {
        yield return DoorWorkflowUtility.ConnectLooseEnds(cellGrid, corridors, reach, moat, yieldEvery);
    }

    // ============================= ENSURE CONNECTIVITY =============================
    IEnumerator EnsureConnectivity(List<DoorCandidate> candidates, int moat, int maxDoorsPerRoom, int yieldEvery)
    {
        yield return DoorWorkflowUtility.EnsureConnectivity(rooms, cellGrid, corridors, candidates, maxDoorsPerRoom, yieldEvery);
    }

    // ============================= EXTRA LOOPS =============================
    IEnumerator AddLoopDoors(List<DoorCandidate> candidates, int extraTarget, int moat, int minSpacing, int maxDoorsPerRoom, int yieldEvery)
    {
        yield return DoorWorkflowUtility.AddLoopDoors(rooms, cellGrid, corridors, candidates, extraTarget, maxDoorsPerRoom, yieldEvery);
    }

    // ============================= DOOR PLACEMENT CORE =============================
    // Punches through 'span' empty cells (≤ moat) and sets door flags symmetrically.
    bool TryPlaceDoor(DoorCandidate d, int moat)
    {
        return DoorPlacementUtility.TryPlaceDoor(d, cellGrid, corridors);
    }

    void PrintCandidates(List<DoorCandidate> candidates)
    {
        DoorCandidateReporter.PrintCandidates(candidates);
    }

    void UpdateDoorsInRooms(List<DoorCandidate> candidates)
    {
        DoorCandidateReporter.UpdateDoorsInRooms(candidates);
    }

}
