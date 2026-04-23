using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    // ======================= Growth: CreditWavefrontStrips =======================

    // ROOM READY
    // Grow_CreditWavefrontStrips() repeatedly expands rooms in one direction by a full length row or column.
    //   This keeps rooms rectangular (as opposed to just CreditWavefront (obsolete/removed)).
    // allowedRoomIds list can optionally be provided to limit which rooms to grow.
    //   allowedRoomIds is unused in first pass of growth.  Used in repeated passes of growth to only grow new seeds.
    IEnumerator Grow_CreditWavefrontStrips(List<int> allowedRoomIds = null)
    {
        BottomBanner.Show("Growth: CreditWavefrontStrips");
        // PRECONDITIONS:
        // - rooms is a global variable that contains all Room objects
        // - Each Room has at least one seed Cell in cellGrid[,] with cell.roomId = room.id
        // - Corridors already painted (cell.isCorridor = true)
        // - We will preserve an N-cell wall moat (N = cfg.grow.wallMoat) around rooms & corridors

        // sanity checks
        int moat = cfg.GetEffectiveGrowWallMoat();
        int nRooms = rooms.Count;
        if (nRooms == 0) yield break;

        // allocate and assign credits per room (random range based on cfg)
        var credits = new int[nRooms];
        for (int i = 0; i < nRooms; i++)
            credits[i] = rng.Next(cfg.grow.areaCreditMin, cfg.grow.areaCreditMax + 1);

        // Allocate and then Precompute frontier set for all rooms
        var frontiers = new List<HashSet<(int x, int y)>>(nRooms);
        for (int i = 0; i < nRooms; i++)
            frontiers.Add(new HashSet<(int, int)>());

        // Initialize frontiers = perimeter list of all rooms (where room is allowed to grow)
        for (int ri = 0; ri < nRooms; ri++)
        {
            foreach (var c in rooms[ri].cells)
                foreach (var nb in FourNeighbors(c.x, c.y))
                    if (CanClaim(ri, nb.x, nb.y, moat))
                        frontiers[ri].Add((nb.x, nb.y));
        }

        int round; // for loop index

        // Build initial bounding box and per-room side cooldowns
        //var aabbs = new List<RectInt>(rooms.Count);
        var cooldown = new Dictionary<int, int[]>(rooms.Count); // 0:E 1:W 2:N 3:S

        for (int ri = 0; ri < rooms.Count; ri++)
        {
            rooms[ri].GetBounds();  // pre-calculates bounds (per room)
            cooldown[ri] = new int[4];  // allocate cooldown direction array (per room)
        }

        int touched = 0;  // counter used in determining how many passes before yielding.

        // ================= 1) STRIP ROUNDS (rectangular growth) =================

        // these are config parameters...
        int stripRounds = cfg.grow.stripRounds; // number of growth passes.  multiplied by 1/passesBeforeSplit
        int targetAspect = cfg.grow.targetAspect; // tune: try to keep rooms from going too skinny
        int percentSkipGrowth = cfg.grow.percentSkipGrowth; // more means more varied room sizes.  50% = half of rooms will be skipped each round
        int passesBeforeSplit = cfg.grow.passesBeforeSplit; // checks for splitrooms after this many rounds
        int maxAspect = 2 * targetAspect;   // tune: if exceeded, cool long axis
        // // these are not config parameters...
        int cooldownOnFail = 3;             // tune: how long to cool a side that failed to grow
        int yieldEvery = 256;               // yields every this many passes (tracked by variable touched)

        // sanity check
        percentSkipGrowth = Math.Max(10, percentSkipGrowth); // zero would give divide by zero error.  Low numbers will make for very long number of passes

        if (stripRounds > 0)
        {
            BottomBanner.Show($"Growth: Strip rounds (x{stripRounds})");
            //Debug.Log($"stripRounds #{stripRounds} begins with {rooms.Count} rooms.");
            for (round = 0; round < stripRounds * (100 / percentSkipGrowth); round++) // increase the rounds because we randomly skip rooms
            {
                bool anyGrewThisRound = false;

                for (int ri = 0; ri < rooms.Count; ri++)
                {
                    if (allowedRoomIds != null)         // allow list exists and room is not on it, then skip room.
                        if (!allowedRoomIds.Contains(ri)) continue;

                    if (rooms[ri].cells.Count > credits[ri]) continue;  // room is out of credits

                    if (rng.Next(0, 100) < percentSkipGrowth) continue; // randomly skip a room.
                    Room room = rooms[ri]; // shortcut
                    if (room.cells.Count == 0) continue;    // no cells in this room

                    // determine room aspect
                    RectInt bounds = room.GetBounds();
                    int width = Mathf.Max(1, bounds.width);
                    int height = Mathf.Max(1, bounds.height);
                    float aspect = (float)Mathf.Max(width, height) / Mathf.Max(1, Mathf.Min(width, height));

                    // Score sides (E,W,N,S). Prefer short axis; skip cooled sides.  Returns in order of best score first.
                    var order = ScoreSidesForStrip(ri, bounds, targetAspect, aspect, cooldown[ri]);
                    //bool grown = false;

                    for (int k = 0; k < order.Count; k++)  // check sides for growth in order of score
                    {
                        int side = order[k];
                        if (cooldown[ri][side] > 0) continue;

                        RectInt before_growth_bounds = bounds;  // DEBUG
                        if (TryGrowFullStrip(ri, ref bounds, side, moat))
                        {
                            // success: update bounds & cooldown bookkeeping
                            bounds = room.GetBounds();  // is this already done in TryGrowFullStrip?
                            //Debug.Log($"Successful TryGrowFullStrip room {ri}: bounds({before_growth_bounds.ToString()}) -> ({bounds.ToString()})");
                            anyGrewThisRound = true;

                            // Small guard: if aspect exploded, roll back by cooling the long axis next time
                            width = Mathf.Max(1, bounds.width); height = Mathf.Max(1, bounds.height);
                            aspect = (float)Mathf.Max(width, height) / Mathf.Max(1, Mathf.Min(width, height));
                            if (aspect > maxAspect)
                            {
                                // cool the long axis sides for a bit
                                if (width > height) { cooldown[ri][2] = Mathf.Max(cooldown[ri][2], cooldownOnFail); cooldown[ri][3] = Mathf.Max(cooldown[ri][3], cooldownOnFail); }
                                else { cooldown[ri][0] = Mathf.Max(cooldown[ri][0], cooldownOnFail); cooldown[ri][1] = Mathf.Max(cooldown[ri][1], cooldownOnFail); }
                            }

                            break; // grow only one strip per room per round and then we are done.
                        }
                        else
                        {
                            cooldown[ri][side] = Mathf.Max(cooldown[ri][side], cooldownOnFail);
                        }
                    } // end for k

                    // decay cooldowns
                    var cd = cooldown[ri];
                    for (int i = 0; i < 4; i++) if (cd[i] > 0) cd[i]--;

                    // breathe
                    if ((++touched % yieldEvery) == 0) yield return null;
                }

                // Split oversized rooms every few rounds
                if ((round % passesBeforeSplit) == 0)
                {
                    // Initialize frontier = perimeter of current room seeds
                    for (int rf = 0; rf < nRooms; rf++)
                    {
                        frontiers[rf].Clear();
                        foreach (var c in rooms[rf].cells)
                            foreach (var nb in FourNeighbors(c.x, c.y))
                                if (CanClaim(rf, nb.x, nb.y, moat))
                                    frontiers[rf].Add((nb.x, nb.y));
                    }

                    bool useSplitRooms = false;     // DEBUG
                    int num_splits;
                    if (useSplitRooms)
                        num_splits = SplitOversizedRooms(moat, frontiers);
                    else
                        num_splits = 0;
                    //Debug.Log($"num_splits = {num_splits}");

                    // calculate room bounds and allocate cooldown for all new rooms.
                    for (var j = 0; j < num_splits; j++)
                    {
                        rooms[nRooms + j].GetBounds();
                        cooldown[nRooms + j] = new int[4];
                    }
                    nRooms += num_splits;

                    // Initialize frontier = perimeter of all rooms
                    for (int rf = 0; rf < nRooms; rf++)
                    {
                        frontiers[rf].Clear();
                        foreach (var c in rooms[rf].cells)
                            foreach (var nb in FourNeighbors(c.x, c.y))
                                if (CanClaim(rf, nb.x, nb.y, moat))
                                    frontiers[rf].Add((nb.x, nb.y));
                    }
                }

                // Optionally draw the map
                if (anyGrewThisRound && cfg.showBuildProcess)
                {
                    DrawMapByRooms(rooms, clearscreen: true);
                    yield return null;
                    //yield return new WaitForSeconds(0.025f); // should use show-build config option
                }
                else
                {
                    yield return null; // breathe
                }
            }
        }

        DrawMapByRooms(rooms, clearscreen: true);
        yield return null;   // new WaitForSeconds(0.1f);
    }
}
