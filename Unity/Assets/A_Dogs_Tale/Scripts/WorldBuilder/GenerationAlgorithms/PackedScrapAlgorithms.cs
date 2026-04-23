using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    // ======================= Scraps: VoronoiFill (with 1-cell peel) =======================
    // Usage:
    //   yield return StartCoroutine(Scraps_VoronoiFill(
    //       moatOverride: -1,      // -1 => use cfg.grow.wallMoat
    //       useCentroids: true,    // false => use first seed cell as proxy
    //       peelIterations: 1,     // run peel pass N times (1–2 is enough)
    //       yieldEvery: 2048));
    IEnumerator Scraps_VoronoiFill(int moatOverride = -1, bool useCentroids = true, int peelIterations = 1, int yieldEvery = 2048)
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;
        int moat = (moatOverride >= 0) ? moatOverride : cfg.GetEffectiveGrowWallMoat();
        // clamp parameters to useful ranges.
        peelIterations = Mathf.Clamp(peelIterations, 1, 4);

        if (rooms == null || rooms.Count == 0) yield break;

        // --- 0) Build proxies (one point per room) ---
        var proxies = new List<Vector2Int>(rooms.Count);
        for (int ri = 0; ri < rooms.Count; ri++)
        {
            if (rooms[ri].cells.Count == 0) { proxies.Add(new Vector2Int(-99999, -99999)); continue; }

            if (useCentroids)
            {
                long sx = 0, sy = 0;
                foreach (var c in rooms[ri].cells) { sx += c.x; sy += c.y; }
                int cx = (int)(sx / rooms[ri].cells.Count);
                int cy = (int)(sy / rooms[ri].cells.Count);
                proxies.Add(new Vector2Int(cx, cy));
            }
            else
            {
                var s = rooms[ri].cells[0];
                proxies.Add(new Vector2Int(s.x, s.y));
            }
            if ((ri & 63) == 0) yield return null;
        }

        // --- 1) Make a working label grid for assignments: -1 = unassigned scrap, -2 = blocked/wall/corridor, >=0 = room id ---
        int[,] label = new int[W, H];

        // Initialize labels from current map
        int touched = 0;
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                var cell = cellGrid[x, y];
                if (cell.isCorridor) { label[x, y] = -2; continue; }        // permanent corridor/no fill
                if (cell.room_number >= 0) { label[x, y] = cell.room_number; continue; } // already part of a room (seed/grown)

                // Optional early corridor clearance: block cells within moat of corridors so we never fill them
                if (IsNearCorridor(x, y, moat)) { label[x, y] = -2; continue; }

                label[x, y] = -1; // scrap candidate
            }
            if (((touched += W) % yieldEvery) == 0) yield return null;
        }

        // --- 2) Assign each scrap to nearest room proxy (Voronoi) while respecting a moat from existing rooms ---
        touched = 0;
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (In(x, y)) continue;  // skip off map and borders.
                if (label[x, y] != -1) continue; // skip non-scraps

                // Keep at least 'moat' cells away from existing rooms & corridors
                if (!ClearOfForeign(x, y, moat)) { label[x, y] = -2; continue; }

                int bestRi = -1;
                int bestD = int.MaxValue;
                for (int ri = 0; ri < proxies.Count; ri++)
                {
                    var p = proxies[ri];
                    if (p.x < -10000) continue; // invalid room
                    int d = Mathf.Abs(p.x - x) + Mathf.Abs(p.y - y); // Manhattan
                    if (d < bestD) { bestD = d; bestRi = ri; }
                }

                if (bestRi >= 0) label[x, y] = bestRi; else label[x, y] = -2; // if no proxy, treat as blocked
            }

            if (((touched += W) % yieldEvery) == 0) yield return null;
        }

        // --- 3) Peel pass: convert boundary cells back to wall so rooms don’t touch (preserve thin walls) ---
        for (int iter = 0; iter < peelIterations; iter++)
        {
            int changes = 0;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int r = label[x, y];
                    if (r < 0) continue;

                    // If any neighbor within moat is corridor or a different label, peel this to wall
                    if (TouchesDifferentOrCorridor(label, x, y, r, moat))
                    {
                        label[x, y] = -2; // wall/blocked
                        changes++;
                    }
                }
                if (((touched += W) % yieldEvery) == 0) yield return null;
            }
            if (changes == 0) break; // done
        }

        // --- 4) Commit labels: add newly assigned cells to their rooms ---
        for (int ri = 0; ri < rooms.Count; ri++)
        {
            // ensure list exists
            if (rooms[ri].cells == null) rooms[ri].cells = new List<Cell>();
        }

        touched = 0;
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int r = label[x, y];
                if (r >= 0)
                {
                    var c = cellGrid[x, y];
                    if (c.room_number == r) continue;      // already owned
                    if (c.room_number >= 0 && c.room_number != r) continue; // shouldn’t happen, but be safe
                    c.room_number = r;
                    rooms[r].cells.Add(c);
                }
            }
            if (((touched += W) % yieldEvery) == 0) yield return null;
        }

        // --- 5) (Optional) Recompute bounds quickly (AABB) for rooms that got new cells ---
        for (int ri = 0; ri < rooms.Count; ri++)
        {
            var r = rooms[ri];
            r.GetBounds(); // recalculate bounds
            /*
            if (r.cells == null || r.cells.Count == 0) { r.bounds = new RectInt(0, 0, 0, 0); continue; }
            int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;
            foreach (var c in r.cells) { if (c.x < minx) minx = c.x; if (c.x > maxx) maxx = c.x; if (c.y < miny) miny = c.y; if (c.y > maxy) maxy = c.y; }
            r.bounds = new RectInt(minx, miny, maxx - minx + 1, maxy - miny + 1);
            */
            if ((ri & 31) == 0) yield return null;
        }


        // update the Rooms lists for drawing...
        for (int x = 0; x < cfg.mapWidth; x++)
        {
            for (int y = 0; y < cfg.mapHeight; y++)
            {
                var c = cellGrid[x, y];
                if (c.room_number < 0) continue;
                // find room by id and add cell to that room
                foreach (var r in rooms)
                {
                    if (r.my_room_number == c.room_number)
                    {
                        c.colorFloor = r.colorFloor;
                        c.isCorridor = r.isCorridor;
                        r.cells.Add(c);
                        break;
                    }
                }
            }
            yield return null;
        }

        DrawMapByRooms(rooms, clearscreen: true);

        yield return new WaitForSeconds(0.1f); // should use show-build config option



        yield break;
    }
}
