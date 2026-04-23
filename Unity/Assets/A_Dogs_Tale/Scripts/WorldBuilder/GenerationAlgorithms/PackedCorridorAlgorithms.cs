using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    // ======================== Corridors: Drunkard's Walk (revised) ========================

    IEnumerator Corridors_DrunkardsWalk(
        int walkers = 2,
        int stepsPerWalker = 400,
        int minimumStraight = 10,
        float wander = 30f,              // determines chance to turn 90° each step
        int corridorWidth = 1,           // 1..2 to keep it skinny
        bool bounceAtEdges = true,       // if false, pick new random start when we hit an edge
        int yieldEvery = 256,            // cooperative yield cadence
        bool allCorridorsAreOneRoom = true // makes overlapping corridors merged.
    )
    {
        BottomBanner.Show("Corridors: Drunkard's Walk");
        int W = cfg.mapWidth, H = cfg.mapHeight;
        //corridorWidth = Mathf.Clamp(corridorWidth <= 0 ? cfg.corridor.corridorWidth : corridorWidth, 1, 5);

        List<Cell> corridorCells = new(); // to pass to DrawMapByRooms
        Room tmp_room;

        // Simple RNG fallback: use your 'rng' if you have it; else UnityEngine.Random
        System.Func<float> R01 = () => (rng != null) ? (float)rng.NextDouble() : UnityEngine.Random.value;
        System.Func<int, int, int> RInt = (a, b) => (rng != null) ? rng.Next(a, b) : UnityEngine.Random.Range(a, b);

        int carved = 0;
        // prepare a new room for the corridor(s):
        tmp_room = new();
        tmp_room.cells = new();
        tmp_room.setColorFloor(highlight: false);
        tmp_room.my_room_number = rooms.Count;
        tmp_room.isCorridor = true;

        for (int wlk = 0; wlk < walkers; wlk++)
        {
            // Start near center (stable) or random edge if you prefer
            // Vector2Int p = new Vector2Int(W / 2, H / 2);
            Vector2Int p = RandomEdgeStart(W, H); // alternative start

            //Vector2Int dir = RandomCardinal();
            Vector2Int dir = DirAwayFromEdge(p);
            int straightRounds = (int)((R01() + 1) * minimumStraight);
            for (int step = 0; step < stepsPerWalker; step++)
            {
                // Carve corridor at p
                CarveDisk(ref tmp_room, p, corridorWidth); // paint corridor cell(s)
                carved++;

                // Maybe turn 90°
                // Wander the direction a bit, but verify we went a minimum distance straight
                straightRounds--;
                if (straightRounds <= 0)
                {
                    Vector2Int predir = dir;
                    if (R01() < wander / 1000f) // odds of turning
                        dir = (R01() < 0.5f) ? TurnLeft(dir, true) : TurnLeft(dir, false);
                    if (predir != dir) straightRounds = (int)((R01() + 1) * minimumStraight);
                }

                // Advance
                Vector2Int np = p + dir;

                if (!In(np.x, np.y))   // must turn or teleport
                {
                    straightRounds = (int)((R01() + 1f) * minimumStraight); // between min and 2*min
                    if (bounceAtEdges)
                    {
                        // bounce: straight back
                        dir = DirAwayFromEdge(p);
                        np = p + dir;

                        if (!In(np.x, np.y))
                        {
                            // fully stuck: pick a fresh random in-bounds location
                            np = new Vector2Int(RInt(cfg.borderKeepout, W - cfg.borderKeepout), RInt(cfg.borderKeepout, H - cfg.borderKeepout));
                            dir = DirAwayFromEdge(np);
                        }
                    }
                    else
                    {
                        // restart from new random position
                        np = new Vector2Int(RInt(cfg.borderKeepout, W - cfg.borderKeepout), RInt(cfg.borderKeepout, H - cfg.borderKeepout));
                        dir = DirAwayFromEdge(np);
                    }
                }

                p = np;

                // Periodic yield to keep Editor responsive
                if ((carved % yieldEvery) == 0) yield return null;
            }
            // to make this one room per walker, add it here...
            if (!allCorridorsAreOneRoom)
            {
                rooms.Add(tmp_room);  // Add this room to the rooms list
                Debug.Log($"Added a corridor room {tmp_room.my_room_number} with {tmp_room.cells.Count} cells.");
                // setup a new room for the next corridor walker
                tmp_room = new();
                tmp_room.cells = new();
                tmp_room.setColorFloor(highlight: false);
                tmp_room.my_room_number = rooms.Count;
                tmp_room.isCorridor = true;
            }
        }
        // to make one room for all corridors, add it here...
        if (allCorridorsAreOneRoom)
        {
            rooms.Add(tmp_room);
            Debug.Log($"Added unified corridor room {tmp_room.my_room_number} with {tmp_room.cells.Count} cells.");
        }

        Debug.Log("Drawing rooms = " + rooms.Count);
        DrawMapByRooms(rooms, clearscreen: true);
        yield return null; // new WaitForSeconds(0.1f);

        BottomBanner.Show($"Corridors: Drunkard's Walk done. Carved ~{carved} cells.");
        yield return new WaitForSeconds(.1f);
    }
}
