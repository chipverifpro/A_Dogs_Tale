using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// At the end of the function GeneratePackedRooms add a function to
/// Start with room 0 (the corridor).  For each door in the room,
/// mark the room it goes to as Rooms[i].connectedToCorridor=true.
/// continue this through all rooms that are already marked
/// connected to corridor, marking all the rooms connected by
/// doors as also connectedToCorridor.  Keep repeating this
/// loop until no more rooms get marked during a pass.
/// Then, for each room that is not marked connectedToCorridor,
/// Look for all rooms adjacent to it that are marked
/// connectedToCorridor.  When one is found, add a door between
/// them.  If none are found (like when a group of rooms are
/// not connected), skip it and mark that you need another pass.
/// Finish all rooms and repeat it only if another pass is needed.
/// Now all rooms should connect to room 0 by at least one door.
/// </summary>

public partial class DungeonGenerator : MonoBehaviour
{
    // This is all that is left from PackMap:
    public Cell[,] cellGrid;    // grid
    public HashSet<(int, int)> corridors = new();

    public IEnumerator GeneratePackedRooms(int? seedOverride = null)
    {
        // Setup
        int seed = cfg.randomizeSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : (seedOverride ?? cfg.seed);
        rng = new Random(seed);
        //BottomBanner.Show = cfg.showBuildProcess ? (Action<string>)BottomBanner.Show : (_)=>{};
        //packMap = new PackMap(cfg.mapWidth, cfg.mapHeight);
        //List<Room> rooms_temp = new(); // temporary Room list for compatibility with DrawMapByRooms
        rooms = new(); // reset this list also
        InitializeCellGrid();
        //InitializeCellGridFromRooms(rooms);
        float t0 = Time.realtimeSinceStartup;

        // 1) Corridors
        yield return StartCoroutine(RunCorridors());
        // ClearMapBorders(rooms);   // DEBUG

        //RemoveDuplicateCellsFromAllRooms(rooms);
        //RemoveDuplicatePackCellsFromAllRooms(packMap.rooms);
        if (cfg.showBuildProcess)
        {
            DrawMapByRooms(rooms);
            yield return new WaitForSeconds(1f);
        }

        // 2) Room seeding
        yield return StartCoroutine(RunRoomSeeding());
        //ClearMapBorders(rooms);   // DEBUG
        //RemoveDuplicateCellsFromAllRooms(rooms);
        Debug.Log("After room seeding, rooms = " + rooms.Count);
        if (cfg.showBuildProcess)
        {
            DrawMapByRooms(rooms);
            yield return new WaitForSeconds(1f);
        }

        // 3) Room growth
        yield return StartCoroutine(RunRoomGrowth());
        //ClearMapBorders(rooms);   // DEBUG
        //RemoveDuplicateCellsFromAllRooms(rooms);
        Debug.Log("After room growth, rooms = " + rooms.Count);
        if (cfg.showBuildProcess)
        {
            DrawMapByRooms(rooms);
            yield return new WaitForSeconds(1f);
        }

        // 4) Scraps
        yield return StartCoroutine(RunScraps());
        //ClearMapBorders(rooms);   // DEBUG
        //RemoveDuplicateCellsFromAllRooms(rooms);
        Debug.Log("After scraps, rooms = " + rooms.Count);
        if (cfg.showBuildProcess)
        {
            DrawMapByRooms(rooms);
            yield return new WaitForSeconds(1f);
        }

        // 5) Doors/connectivity
        yield return StartCoroutine(RunDoors());
        //ClearMapBorders(rooms);   // DEBUG
        //RemoveDuplicateCellsFromAllRooms(rooms);
        Debug.Log("After doors, rooms = " + rooms.Count);
        if (cfg.showBuildProcess)
        {
            DrawMapByRooms(rooms);
            yield return new WaitForSeconds(1f);
        }

        yield return StartCoroutine(EnsurePackedRoomsConnectToCorridor());
        StripPackedRoomDoorsForOpenAirThemes();

        //UpdateCellGridFromRooms(rooms);
        UpdateRoomsFromCellGrid();
        DrawMapByRooms(rooms);
        yield return null;

        AssignRoomUses();

        CheckRoomsToGridConsistancy();
        
        BottomBanner.Show($"Done seed={seed} in {(Time.realtimeSinceStartup - t0):F2}s");
    }

}
