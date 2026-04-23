using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    // ======================= Corridors: WanderingMST =======================
    IEnumerator Corridors_WanderingMST()
    {
        BottomBanner.Show("Corridors: WanderingMST");
        int W = cfg.mapWidth - 1, H = cfg.mapHeight - 1;

        // clamp params to reasonable ranges
        int width = Mathf.Clamp(cfg.corridor.corridorWidth, 0, 5);
        int spines = Mathf.Max(1, cfg.corridor.spineCount);
        float wander = Mathf.Clamp(cfg.corridor.wanderiness, 0f, 100f);
        float loopChance = Mathf.Clamp01(cfg.corridor.loopChance);

        // 1) Make wandering spines starting near PackMap edges
        var rngf = new System.Func<float>(() => (float)rng.NextDouble());
        var nodes = new List<Vector2Int>();  // sampled waypoints along spines

        List<Room> rooms_temp = new(); // temporary Room list for compatibility with DrawMapByRooms
        Room room_temp;

        Debug.Log("Corridors WanderingMST: Beginning Drawing rooms = " + rooms.Count);
        DrawMapByRooms(rooms, clearscreen: true);
        yield return null;  // new WaitForSeconds(0.1f);

        var tmp_room = new Room { cells = new List<Cell>(), isCorridor = true };
        tmp_room.setColorFloor(highlight: false);

        for (int s = 0; s < spines; s++)
        {
            Debug.Log($"Corridor spine {s + 1} of {spines}");
            yield return null;
            int min_straightRounds = 20;
            int straightRounds = 0;

            // Start near a random border
            Vector2Int p = RandomEdgeStart(W, H);
            Vector2Int dir = DirAwayFromEdge(p);

            int steps = (int)(0.7f * (W + H)); // long-ish 0.7
            int sampleEvery = 12; //12
            int sinceSample = 0;

            for (int i = 0; i < steps; i++)
            {
                Debug.Log($"  step {i + 1} of {steps} at {p.x},{p.y} dir={dir.x},{dir.y}");
                yield return null;
                straightRounds++;

                CarveDisk(ref tmp_room, p, width); // paint corridor cell(s)
                sinceSample++;

                // Randomly sample nodes along the walk (used by MST)
                if (sinceSample >= sampleEvery)
                {
                    nodes.Add(p);
                    sinceSample = 0;
                }

                // Wander the direction a bit, but verify we went a minimum distance straight
                if (straightRounds >= min_straightRounds)
                {
                    Vector2Int predir = dir;
                    if (rngf() < wander / 1000) dir = MaybeTurn(dir, rng, wander);
                    if (predir != dir) straightRounds = 0;
                }

                // Step forward; clamp to PackMap
                Vector2Int np = p + dir;
                if (!In(np.x, np.y))
                {
                    // bounce off wall by turning left or right
                    dir = TurnLeft(dir, rngf() < 0.5f);
                    np = p + dir;
                    if (!In(np.x, np.y)) break;
                }
                p = np;

                // Cooperative yield
                if ((i & 127) == 0) yield return null;
            }

            yield return null; // new WaitForSeconds(0.1f);
        }

        room_temp = ExtractRoomFromVectors(nodes);
        Debug.Log("nodes = " + nodes.Count + " after steps, before thinned ");
        room_temp.setColorFloor(highlight: false);
        foreach (var cell in room_temp.cells) { cell.colorFloor = room_temp.colorFloor; }
        rooms_temp.Add(room_temp);
        DrawMapByRooms(rooms_temp);
        yield return null; // new WaitForSeconds(0.1f);

        // ---- before computing MST: dedupe + thin + cap ----
        if (nodes.Count < 2) yield break;

        // 2a) Deduplicate exact duplicates (cheap)
        var seen = new HashSet<int>();
        var dedup = new List<Vector2Int>(nodes.Count);
        foreach (var p in nodes)
        {
            int key = (p.y << 16) ^ p.x;
            if (seen.Add(key)) dedup.Add(p);
        }

        // 2b) Blue-noise thin the node set (enforce Manhattan spacing)
        int minNodeSpacing = 10;                     // tune: larger = fewer nodes
        int maxNodes = 20; //600                   // safety cap to keep MST cheap
        var thinned = new List<Vector2Int>(Mathf.Min(maxNodes, dedup.Count));
        foreach (var p in dedup)
        {
            bool ok = true;
            // small linear check is fine with cap; if you expect bigger, bucket on a coarse grid
            for (int i = 0; i < thinned.Count; i++)
            {
                if (Mathf.Abs(thinned[i].x - p.x) + Mathf.Abs(thinned[i].y - p.y) < minNodeSpacing) { ok = false; break; }
            }
            if (ok) thinned.Add(p);
            if (thinned.Count >= maxNodes) break;
        }
        nodes = thinned;

        room_temp = ExtractRoomFromVectors(nodes);
        Debug.Log("nodes = " + nodes.Count + " after thinned ");
        rooms_temp.Add(room_temp);
        DrawMapByRooms(rooms_temp);
        yield return null; // new WaitForSeconds(0.1f);

        // 2c) Build MST in a time-sliced way
        List<(Vector2Int a, Vector2Int b)> mstEdges = new List<(Vector2Int, Vector2Int)>(nodes.Count - 1);
        yield return StartCoroutine(ComputeMST_Yield(nodes, mstEdges, yieldEvery: 2000));  // yields during O(n²)

        Debug.Log($"  MST has {mstEdges.Count} edges connecting {nodes.Count} nodes");

        // 2d) Carve MST edges with yielding (so long lines don’t block)
        foreach (var e in mstEdges)
        {
            tmp_room = new Room { cells = new List<Cell>(), isCorridor = true };
            tmp_room.setColorFloor(highlight: false);

            yield return StartCoroutine(CarveLineWithYield(tmp_room, e.a, e.b, width, yieldEvery: 256));
            rooms.Add(tmp_room);
        }

        // 3) Add a few loop edges, but be gentle
        int extraTarget = Mathf.Min(48, Mathf.CeilToInt(nodes.Count * loopChance * 0.4f)); // hard cap
        int maxLoopLen = Mathf.Max(16, (W + H) / 12); // don’t add megascale chords
        for (int k = 0; k < extraTarget; k++)
        {
            var a = nodes[rng.Next(nodes.Count)];
            var b = nodes[rng.Next(nodes.Count)];
            if (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) > maxLoopLen) continue; // skip long chords

            tmp_room = new Room { cells = new List<Cell>(), isCorridor = true };
            tmp_room.setColorFloor(highlight: false);

            yield return StartCoroutine(CarveLineWithYield(tmp_room, a, b, width, yieldEvery: 256));
            rooms.Add(tmp_room);

            if ((k & 3) == 0) yield return null;
        }

        DrawMapByRooms(rooms, clearscreen: true);
        yield return new WaitForSeconds(.05f);
    }

}
