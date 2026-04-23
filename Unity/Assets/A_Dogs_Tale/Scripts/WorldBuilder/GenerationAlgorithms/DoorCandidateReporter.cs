using System.Collections.Generic;
using UnityEngine;

public static class DoorCandidateReporter
{
    public static void PrintCandidates(List<DoorCandidate> candidates)
    {
        int num = 0;
        int complete = 0;
        foreach (DoorCandidate candidate in candidates)
        {
            num++;
            if (candidate.placed) complete++;
        }
    }

    public static void UpdateDoorsInRooms(List<DoorCandidate> candidates)
    {
        int num = 0;
        int complete = 0;
        int numChanges = 0;

        foreach (DoorCandidate candidate in candidates)
        {
            num++;

            if (!candidate.placed)
            {
                DirFlags beforeDoors = candidate.cellA.doors;
                DirFlags beforeWalls = candidate.cellA.walls;
                candidate.cellA.doors &= ~candidate.dir;

                if ((beforeDoors != candidate.cellA.doors) || (beforeWalls != candidate.cellA.walls))
                    numChanges++;

                DirFlags beforeDoorsB = candidate.cellB.doors;
                DirFlags beforeWallsB = candidate.cellB.walls;
                candidate.cellB.doors &= ~candidate.dir.Opposite();

                if ((beforeDoorsB != candidate.cellB.doors) || (beforeWallsB != candidate.cellB.walls))
                    numChanges++;
            }
            else
            {
                complete++;
            }
        }

        Debug.Log($"Door Candidates = {num}, Doors Complete = {complete}, num_changes = {numChanges}");
    }
}
