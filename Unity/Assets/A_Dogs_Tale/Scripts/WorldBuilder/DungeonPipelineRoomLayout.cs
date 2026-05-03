using System.Collections;
using UnityEngine;

public partial class DungeonGenerator
{
    private IEnumerator GenerateRoomLayout()
    {
        // TAVERN
        //            if (tavern.enabled)
        //            {
        //                yield return StartCoroutine(BuildTavern(tm: null));
        //            }

        if (cfg.usePackedRooms)
        {
            yield return StartCoroutine(GeneratePackedRooms());
        }
        if (cfg.useCellularAutomata) // Cellular Automata generation
        {
            BottomBanner.LogBuildProgress("Cellular Automata cavern generation iterating...");
            yield return StartCoroutine(RunCellularAutomation(tm: null));
            DrawWalls();
        }
        if (cfg.useScatterRooms)
        {
            BottomBanner.LogBuildProgress("Scattering rooms...");
            yield return StartCoroutine(ScatterRooms(tm: null));
            Debug.Log("ScatterRooms done, room_rects.Count = " + room_rects.Count);
            //DrawMapByRects(room_rects, room_rects_color);
            //DrawWalls();
        }
    }

    private IEnumerator LocateAndMergeGeneratedRooms(TimeTask tm)
    {
        BottomBanner.LogBuildProgress("Locate Discrete rooms...");
        if (cfg.useCellularAutomata) // locate rooms from cellular automata
        {
            BottomBanner.LogBuildProgress("Remove tiny rocks...");
            yield return StartCoroutine(RemoveTinyRocksCoroutine(tm: null));

            // For Cellular Automata, find rooms from the map
            BottomBanner.LogBuildProgress("Locate Discrete rooms...");
            yield return StartCoroutine(FindClustersCoroutine(map, FLOOR, rooms, tm: null));

            BottomBanner.LogBuildProgress("Remove tiny rooms...");
            yield return StartCoroutine(RemoveTinyRoomsCoroutine(tm: null));
        }
        if (cfg.useScatterRooms)
        {
            BottomBanner.LogBuildProgress("Convert all Rects to Rooms...");
            rooms = ConvertAllRectToRooms(room_rects, room_rects_color, SetTile: true);
            DrawMapByRooms(rooms);
            DrawWalls();
            if (tm.IfYield()) yield return null;     // cooperative yield decision

            yield return tm.YieldOrDelay(cfg.stepDelay);
            // Step 4: Merge overlapping rooms
            BottomBanner.LogBuildProgress("Merging Overlapping Rooms...");
            if (cfg.MergeScatteredRooms)
                rooms = MergeOverlappingRooms(rooms, considerAdjacency: true, eightWay: false);
            DrawMapByRooms(rooms);
            DrawWalls();
            yield return tm.YieldOrDelay(cfg.stepDelay); // depends on cfg.showBuildProcess
        }
    }

    private IEnumerator PostProcessGeneratedRooms(TimeTask tm)
    {
        if (!HasGeneratedRooms())
            yield break;

        ApplyRoomHeightVariation();

        // ======== End Rooms, Begin Corridors ========
        if (!NeedsRoomCorridors())
            yield break;

        DrawMapByRooms(rooms);
        DrawWalls();

        // Step 5: Connect rooms with corridors
        BottomBanner.LogBuildProgress("Connecting Rooms with Corridors...");
        yield return StartCoroutine(ConnectRoomsByCorridors(tm: null));

        DrawMapByRooms(rooms);
        DrawWalls();
        yield return tm.YieldOrDelay(cfg.stepDelay);
    }

    private void ApplyRoomHeightVariation()
    {
        // Optionally add Perlin noise to floor heights
        perlinSeedX = cfg.GlobalPerlinSeed ? UnityEngine.Random.Range(0f, 9999f) : 0f;
        perlinSeedY = cfg.GlobalPerlinSeed ? UnityEngine.Random.Range(0f, 9999f) : 0f;

        if (cfg.perlinFloorHeights > 0)
        {
            for (int r = 0; r < rooms.Count; r++)
            {
                rooms[r] = AddPerlinToFloorHeights(rooms[r]);
            }
        }

        // Optionally slope entire rooms in a random direction
        if (cfg.slopeRoomMaxAngle > 0)
        {
            for (int r = 0; r < rooms.Count; r++)
            {
                Vector2 topDir = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)).normalized;
                rooms[r] = TiltRoom(rooms[r], topDir, cfg.slopeRoomMaxAngle, heightUnitsPerTile: cfg.unitHeight);
            }
        }
    }
}
