using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    // ======================= Seeding: AlongCorridors =======================
    IEnumerator Seed_AlongCorridors()
    {
        BottomBanner.LogBuildProgress("Seeding: AlongCorridors");

        // sanity range check the parameters...
        int moat = cfg.GetEffectiveGrowWallMoat();
        int spacing = Mathf.Max(2, cfg.RoomSeeding.spacing);     // min spacing between seeds along corridors
        int jitter = Mathf.Clamp(cfg.RoomSeeding.jitter, 0, spacing - 1);
        float altProb = Mathf.Clamp01(cfg.RoomSeeding.alternateSides); // probability to alternate sides L/R

        // 1) Collect candidate corridor cells that are "good" for hanging rooms:
        //    Prefer straight or gently curved segments (2 corridor neighbors).
        var shuffledCorridorList = new List<Vector2Int>(corridors.Count);
        foreach (var (x, y) in corridors)
            shuffledCorridorList.Add(new Vector2Int(x, y));

        // Shuffle to avoid directional bias (blue-noise style selection later)
        Shuffle(shuffledCorridorList);

        // 2) Blue-noise pick: accept a candidate if it's ≥ spacing away (Manhattan) from other chosen anchors
        var anchors = new List<Vector2Int>();
        foreach (var p in shuffledCorridorList)
        {
            // skip junctions with 3+ corridor neighbors (doors are better placed by the door pass)
            int nbCorr = CountCorridorNeighbors(p.x, p.y);
            if (nbCorr == 0) continue; // single cell corridor
            if (nbCorr >= 3) continue; // big junctions: skip as anchors

            bool farEnough = true;
            for (int i = 0; i < anchors.Count; i++)
            {
                if (Manhattan(p, anchors[i]) < spacing)
                {
                    farEnough = false;
                    break;
                }
            }
            if (!farEnough) continue;

            // Jitter forward along local corridor tangent to avoid a grid feel
            Vector2Int tangent = PickTangentDir(p.x, p.y);
            if (tangent != Vector2Int.zero && jitter > 0)
            {
                int j = rng.Next(-jitter, jitter + 1);
                var pj = p + tangent * j;
                if (In(pj.x, pj.y) && cellGrid[pj.x, pj.y].isCorridor)
                    anchors.Add(pj);
                else
                    anchors.Add(p);
            }
            else
            {
                anchors.Add(p);
            }

            // cooperative yield
            if ((anchors.Count & 127) == 0) yield return null;
        }

        if (anchors.Count == 0)
        {
            BottomBanner.LogBuildProgress("  (No valid corridor anchors; seeding skipped)");
            yield break;
        }

        // 3) For each anchor, choose a side (left/right normal to corridor),
        //    find the edge of the corridor, offset off the corridor by moat+1,
        //    and plant a single seed cell there (unless it is a bad spot).

        bool flip = false; // alternate sides deterministically, with randomness via altProb
        int created = 0; // count of created seeds
        bool found_seed_candidate = false; // assume it is bad until we find a space adjacent to corridor.
        int step; // tiles away from anchor
        int sx, sy; // tile we are looking at
        int try_side; // try one side, and then second pass try the other

        foreach (var a in anchors)
        {
            Vector2Int t = PickTangentDir(a.x, a.y);
            if (t == Vector2Int.zero) t = RandomCardinal(); // fallback

            // choose side: alternate with probability, else random
            if (rng.NextDouble() < altProb) flip = !flip;
            Vector2Int n = Perp(t, flip); // left/right normal

            // find the edge of the corridor...

            // a is the anchor location
            // n is the perpendicular direction (already randomized left or right)
            //   (second pass we will invert this and try the other side)
            // s is the location after stepping several times in the n direction away from anchor
            for (try_side = 1; try_side <= 2; try_side++) // try one side and then the other if first doesn't work.
            {
                if (try_side == 2)
                {
                    // swap search direction to other side of anchor for second try
                    n = new Vector2Int(-n.x, -n.y);
                }

                // First, find the edge of the corridor, one step (in direction n) at a time.
                for (step = 1; step <= cfg.corridor.corridorWidth; step++)
                {
                    sx = a.x + n.x * step;
                    sy = a.y + n.y * step;
                    if (!In(sx, sy)) { break; } // this anchor + direction went off the map.
                    if (cellGrid[sx, sy].isCorridor == false)
                    {
                        found_seed_candidate = true;
                        break;
                    }   // found first tile that isn't a corridor
                }
                if (found_seed_candidate == false) continue; // no edge of corridor found, go to other try_side

                // keep going with this side and check if we found a good spot for a seed?
                step += moat;   // jump ahead over the moat
                sx = a.x + n.x * step;
                sy = a.y + n.y * step;
                if (In(sx, sy) && CanPlaceSeed(sx, sy, moat))
                {
                    CreateRoomSeedAt(sx, sy);
                    created++;
                    break;  // no need to check the other side, we are done.
                }
            } // end try_side
        } // end foreach anchor

        BottomBanner.LogBuildProgress($"  Seeded {created} room(s) from {anchors.Count} corridor anchors.");
        if (cfg.showBuildProcess)
        {
            DrawMapByRooms(rooms, clearscreen: true);
            yield return new WaitForSeconds(0.1f);
        }
        else
        {
            yield return null;
        }
    } // end function Seed_AlongCorridors()
}
