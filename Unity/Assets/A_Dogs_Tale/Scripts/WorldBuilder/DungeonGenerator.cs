using System.Collections;
using System;
using UnityEngine;


/* DONE list...
-- DONE; Round world including fast oval room bounds checking
-- DONE: Nested Perlin / Stacked Perlin
-- DONE: Filter small wall areas
-- DONE: 3D walls
-- DONE: Code cleanup for organization and optimization
-- DONE: Use Rooms list to draw 3D, not a copy of the 2D map.
   (allows corridors above/below rooms)
-- DONE: Clamp ramp slopes at 1.
-- DONE: Don't draw diagonals when 3 walls on one tile.
-- DONE: Smart hash calculations
-- DONE: Don't show build progress option implementation
-- DONE: Reorganize files
-- DONE: Use more intelligent yielding to accomplish more each refresh.
-- DONE: Multi-layer map generation
-- DONE: Gently sloping floors without stairs (perlin heights)
-- DONE: Change long 3D routines to coroutines.
-- DONE: Presets of interesting dungeons - menu or random selection
-- DONE: Fix corridors between stacked rooms

   TODO list...
-- Simplex Noise
-- Adding extra corridors to break up tree
-- More tile types: stairs, doors, traps (with properties)
-- Fix early regeneration button (abort in-progress)
-- Fix pulldown after recompile
-- Enforce minimum width room connectivity
-- Walkthrough capability
-- Camera flight controls
-- Ceiling height minimum and room merging

-- Hex tiles
-- Remove dead code
-- Multiple passes of room generation layers
-- Doors
 */

// Master Dungeon Generation Class...
public partial class DungeonGenerator : MonoBehaviour
{
    #region parameters
    [Header("Dir Object")]
    public Dir dir;

    //public RandomSceneryScatter sceneryScatterer;

    // Reference to external classes is maintained here
    public DungeonSettings cfg;     // This is used lots of places!

    public bool buildComplete = false;
    [NonSerialized]
    public Coroutine regenerateCoroutine = null;

    //void OnEnable()  => Debug.Log($"[DG] OnEnable in scene '{gameObject.scene.name}' (id {GetInstanceID()})");
    //void OnDisable() => Debug.Log($"[DG] OnDisable in scene '{gameObject.scene.name}' (id {GetInstanceID()})");
    //void OnDestroy() => Debug.LogWarning($"[DG] OnDestroy in scene '{gameObject.scene.name}' (id {GetInstanceID()})");

    #endregion
    
    // RegenerateDungeon is the main coroutine that handles dungeon generation.
    // It orchestrates the various steps involved in creating the dungeon layout.
    // Step 0: Select settings
    // Step 1: Initialize the dungeon
    // Step 2: Place rooms (ScatterRooms or CellularAutomata)
    // Step 3: Convert rooms to a list of floor tiles (ConvertRectToRoomPoints or findRoomTiles for CA)
    // Step 4: Combine overlapping rooms (MergeOverlappingRooms)
    // Step 5: Connect rooms by corridors (DrawCorridors)

    // Other routines:
    //  Draw Map by Rooms
    //  Draw Walls

    #region RegenerateDungeon
    // call this instead of the coroutine directly to manage stopping previous runs.
    public void RegenerateDungeon()
    {
        if (regenerateCoroutine != null)
        {
            Debug.Log("RegenerateDungeon: Stopping previous in-progress regeneration coroutine.");
            StopCoroutine(regenerateCoroutine);
            regenerateCoroutine = null;
        }
        regenerateCoroutine = StartCoroutine(RegenerateDungeonCoroutine(null));
    }
    public IEnumerator RegenerateDungeonCoroutine(TimeTask tm = null)
    {
        buildComplete = false;
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("RegenerateDungeon"); local_tm = true; }
        try
        {
            yield return null;  // give time for all allocations to complete
            ResetGenerationState();
            if (tm.IfYield()) yield return null;     // cooperative yield decision

            BottomBanner.LogBuildProgress("Generating dungeon...");

            // Step 0: Select settings
            DungeonGenerationModeApplier.ApplyRoomAlgorithmFlags(cfg);

            BottomBanner.LogBuildProgress("Initialize dungeon...");


            // ===== Step 1. Initialize the dungeon
            InitializeDungeonTiles();
            yield return tm.YieldOrDelay(cfg.stepDelay);

            // ===== Step 2. Place rooms
            yield return StartCoroutine(GenerateRoomLayout());

            yield return tm.YieldOrDelay(cfg.stepDelay);

            // Step 3: Combine overlapping rooms
            yield return StartCoroutine(LocateAndMergeGeneratedRooms(tm));

            yield return StartCoroutine(PostProcessGeneratedRooms(tm));
            yield return StartCoroutine(RebuildWallLists());
            yield return StartCoroutine(ApplyOptionalFloorTileTilt());
            yield return StartCoroutine(BuildFinalDungeonOutput());

            BottomBanner.LogBuildProgress("Dungeon generation complete!");
            Debug.Log("buildComplete");
            buildComplete = true;
            regenerateCoroutine = null;

            // this must be after buildComplete = true;
            //yield return StartCoroutine(dir.player.DetermineStartPosition());
        }
        finally { if (local_tm) tm.End(); }
        TimeManager.Instance.DumpStats();
    }

    #endregion

} // End class DungeonGenerator
