using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Random = System.Random;
using UnityEngine.Tilemaps;

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

        //UpdateCellGridFromRooms(rooms);
        UpdateRoomsFromCellGrid();
        DrawMapByRooms(rooms);
        yield return null;

        CheckRoomsToGridConsistancy();
        
        BottomBanner.Show($"Done seed={seed} in {(Time.realtimeSinceStartup - t0):F2}s");
    }

    // ---------- Stage switches ----------
    IEnumerator RunCorridors()
    {
        switch (cfg.corridorAlgo)
        {
            case DungeonSettings.CorridorAlgo.WanderingMST: return Corridors_WanderingMST();
            case DungeonSettings.CorridorAlgo.MedialAxis: return Corridors_MedialAxis();
            case DungeonSettings.CorridorAlgo.GridMazes: return Corridors_GridMazes();
            case DungeonSettings.CorridorAlgo.DrunkardsWalk:
                return Corridors_DrunkardsWalk(
                walkers: cfg.corridor.drunkWalkers,
                stepsPerWalker: cfg.corridor.drunkStepsPerWalker,
                minimumStraight: cfg.corridor.drunkMinimumStraight,
                wander: cfg.corridor.wanderiness,
                corridorWidth: cfg.corridor.corridorWidth
            );
            default: return Corridors_WanderingMST();
        }
    }

    IEnumerator RunRoomSeeding()
    {
        switch (cfg.roomSeedAlgo)
        {
            case DungeonSettings.RoomSeedAlgo.AlongCorridors: return Seed_AlongCorridors();
            case DungeonSettings.RoomSeedAlgo.PoissonAlongCorridors: return Seed_PoissonAlongCorridors();
            case DungeonSettings.RoomSeedAlgo.UniformGrid: return Seed_UniformGrid();
            default: return Seed_AlongCorridors();
        }
    }
    IEnumerator RunRoomGrowth()
    {
        switch (cfg.roomGrowAlgo)
        {
            case DungeonSettings.RoomGrowAlgo.CreditWavefrontStrips: return Grow_CreditWavefrontStrips();
            //case DungeonSettings.RoomGrowAlgo.StripThenWavefront: return Grow_StripThenWavefront();
            case DungeonSettings.RoomGrowAlgo.PressureField: return Grow_PressureField();
            case DungeonSettings.RoomGrowAlgo.OrthogonalRays: return Grow_OrthogonalRays();
            default: return Grow_CreditWavefrontStrips();
        }
    }
    IEnumerator RunScraps()
    {
        switch (cfg.scrapAlgo)
        {
            case DungeonSettings.ScrapAlgo.VoronoiFill: return Scraps_VoronoiFill();
            case DungeonSettings.ScrapAlgo.SeedAndGrowUntilPacked: return Scraps_SeedAndGrowUntilPacked(ScrapSeedMode.RandomScatter);//.PerimeterEveryN);
            case DungeonSettings.ScrapAlgo.ClosetsOnly: return Scraps_ClosetsOnly();
            case DungeonSettings.ScrapAlgo.NearestRoom: return Scraps_NearestRoom();
            default: return Scraps_VoronoiFill();
        }
    }
    IEnumerator RunDoors()
    {
        switch (cfg.doorAlgo)
        {
            case DungeonSettings.DoorAlgo.EnsureConnectivity: return PlaceDoors();
            case DungeonSettings.DoorAlgo.SparseLoops: return Doors_SparseLoops();
            case DungeonSettings.DoorAlgo.ManyLoops: return Doors_ManyLoops();
            default: return Doors_EnsureConnectivity();
        }
    }

    // ---------- Stage implementations (skeletons to fill) ----------

    //  IEnumerator Corridors_DrunkardsWalk()
    //  {
    //      BottomBanner.Show("Corridors: DrunkardsWalk");
    //      // 1) lay 'cfg.corridor.spineCount' biased random walks with width 'cfg.corridor.corridorWidth'
    //      // 2) optionally connect keypoints with loops
    //      // 3) write into PackMap.corridors and lock a 1-cell moat if you keep thin walls
    //      yield return null;
    //     }

    //    IEnumerator Corridors_WanderingMST()
    //    {
    //        BottomBanner.Show("Corridors: WanderingMST");
    //        // 1) lay 'cfg.corridor.spineCount' biased random walks with width 'cfg.corridor.corridorWidth'
    //        // 2) connect keypoints with MST + add loops with probability cfg.corridor.loopChance
    //        // 3) write into PackMap.corridors and lock a 1-cell moat if you keep thin walls
    //        yield return null;
    //    }
    IEnumerator Corridors_MedialAxis()
    {
        BottomBanner.Show("Corridors: MedialAxis");
        // derive corridors from skeleton of blocked mask; prune branches; width locked
        yield return null;
    }
    IEnumerator Corridors_GridMazes()
    {
        BottomBanner.Show("Corridors: GridMazes");
        // uniform or weighted recursive backtracker / Wilson; keep width = cfg.corridor.corridorWidth
        yield return null;
    }

    //    IEnumerator Seed_AlongCorridors()
    //    {
    //        BottomBanner.Show("Seeding: AlongCorridors");
    //        // place seeds along corridor sides every cfg.seed.spacing with jitter cfg.seed.jitter
    //        // alternate left/right by cfg.seed.alternateSides
    //        yield return null;
    //    }
    IEnumerator Seed_PoissonAlongCorridors()
    {
        BottomBanner.Show("Seeding: PoissonAlongCorridors");
        // run 1-D Poisson sampling along paths, project seeds to sides
        yield return null;
    }
    IEnumerator Seed_UniformGrid()
    {
        BottomBanner.Show("Seeding: UniformGrid");
        // grid cells at spacing; skip if too near corridors
        yield return null;
    }

    //    IEnumerator Grow_CreditWavefront()
    //    {
    //        BottomBanner.Show("Growth: CreditWavefront");
    //        // each room gets random credit in [cfg.grow.areaCreditMin..Max]
    //        // round-robin claimable frontier respecting moat = cfg.grow.wallMoat
    //        // split if area>cfg.grow.splitArea or aspect>cfg.grow.splitAspect
    //        yield return null;
    //    }
    IEnumerator Grow_PressureField()
    {
        BottomBanner.Show("Growth: PressureField");
        // maintain a pressure scalar; rooms expand into lowest-pressure valid neighbor
        yield return null;
    }
    IEnumerator Grow_OrthogonalRays()
    {
        BottomBanner.Show("Growth: OrthogonalRays");
        // extend axis-aligned slabs until 1-cell before collision; merge slabs
        yield return null;
    }

    //IEnumerator Scraps_VoronoiFill()
    //{
    //    BottomBanner.Show("Scraps: VoronoiFill");
    //    // assign leftovers to nearest room with 1-cell peel for walls; tiny islands -> closets
    //    yield return null;
    //}
    IEnumerator Scraps_ClosetsOnly()
    {
        BottomBanner.Show("Scraps: ClosetsOnly");
        // mark small unassigned blobs (<= cfg.scraps.closetMaxArea) as closets; leave others as wall
        yield return null;
    }
    IEnumerator Scraps_NearestRoom()
    {
        BottomBanner.Show("Scraps: NearestRoom");
        // simply flood to nearest room but preserve 1-cell wall between different owners
        yield return null;
    }

    IEnumerator Doors_EnsureConnectivity()
    {
        BottomBanner.Show("Doors: EnsureConnectivity");
        // ensure every room hits a corridor; add minimal doors to connect all components
        yield return null;
    }
    IEnumerator Doors_SparseLoops()
    {
        BottomBanner.Show("Doors: SparseLoops");
        // ensure connectivity + add few room-room doors with far-bias cfg.doors.loopBias
        yield return null;
    }
    IEnumerator Doors_ManyLoops()
    {
        BottomBanner.Show("Doors: ManyLoops");
        // like SparseLoops but add up to cfg.doors.maxRoomToRoomDoors extra room-room doors
        yield return null;
    }

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
        //Cell tmp_real_cell;

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

            //var tmp_room = new Room { cells = new List<Cell>(), isCorridor = true };
            //tmp_room.setColorFloor(highlight: false);

            // Start near a random border
            Vector2Int p = RandomEdgeStart(W, H);
            Vector2Int dir = DirAwayFromEdge(p);

            int steps = (int)(0.7f * (W + H)); // long-ish 0.7
            //steps = 50;
            int sampleEvery = 12; //12
            int sinceSample = 0;

            for (int i = 0; i < steps; i++)
            {
                Debug.Log($"  step {i + 1} of {steps} at {p.x},{p.y} dir={dir.x},{dir.y}");
                yield return null;
                //if (i == 250) break; // DEBUG CHECK to prevent infinite hang
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

            /*
                        foreach (var loc in nodes)
                        {
                            var cell = new Cell(loc.x, loc.y);
                            cell.colorFloor = tmp_room.colorFloor;
                            tmp_room.cells.Add(cell);
                        }
                        rooms.Add(tmp_room);    // Add to master room list
            */

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

        // Helpers...

        IEnumerator ComputeMST_Yield(List<Vector2Int> pts,
        List<(Vector2Int a, Vector2Int b)> outEdges,
        int yieldEvery = 5000)
        {
            int n = pts.Count;
            if (n <= 1) yield break;

            var inTree = new bool[n];
            var best = new int[n];
            var parent = new int[n];

            // Start at 0
            inTree[0] = true;
            for (int j = 1; j < n; j++)
            {
                best[j] = Manhattan(pts[0], pts[j]);
                parent[j] = 0;
            }
            best[0] = int.MaxValue; parent[0] = -1;

            int ops = 0;
            for (int e = 0; e < n - 1; e++)
            {
                // pick the non-tree vertex with smallest best[j]
                int k = -1, bk = int.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (!inTree[i] && best[i] < bk) { bk = best[i]; k = i; }
                    if ((++ops % yieldEvery) == 0) yield return null; // time-slice the scan
                }
                if (k == -1) break; // disconnected (shouldn’t happen with full graph)
                inTree[k] = true;
                outEdges.Add((pts[k], pts[parent[k]]));

                // relax edges from k
                for (int j = 0; j < n; j++)
                {
                    if (inTree[j]) continue;
                    int c = Manhattan(pts[k], pts[j]);
                    if (c < best[j]) { best[j] = c; parent[j] = k; }
                    if ((++ops % yieldEvery) == 0) yield return null;
                }
            }
        }

        IEnumerator CarveLineWithYield(Room tmp_room, Vector2Int a, Vector2Int b, int width, int yieldEvery = 256)
        {
            int count = 0;
            foreach (var p in RasterizeLineSafe(a, b))
            {
                CarveDisk(ref tmp_room, p, width);  // your existing painter
                if ((++count % yieldEvery) == 0) yield return null;
            }
        }
    }

    // ======================= Seeding: AlongCorridors =======================
    IEnumerator Seed_AlongCorridors()
    {
        BottomBanner.Show("Seeding: AlongCorridors");

        // sanity range check the parameters...
        int moat = Mathf.Max(0, cfg.grow.wallMoat);
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
            BottomBanner.Show("  (No valid corridor anchors; seeding skipped)");
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

        BottomBanner.Show($"  Seeded {created} room(s) from {anchors.Count} corridor anchors.");
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
        int moat = Mathf.Max(0, cfg.grow.wallMoat);
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
        int moat = (moatOverride >= 0) ? moatOverride : Mathf.Max(0, cfg.grow.wallMoat);
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

    // ======================= Scraps: Seed & Grow Until Packed =======================
    // Usage example:
    //   yield return StartCoroutine(Scraps_SeedAndGrowUntilPacked(
    //       mode: ScrapSeedMode.PerimeterEveryN,
    //       perimeterSpacing: 10,
    //       randomSeedsPerRegion: 3,
    //       randomMinSpacing: 6,
    //       maxRounds: 6,
    //       moatOverride: -1,             // -1 uses cfg.grow.wallMoat
    //       yieldEvery: 2048
    //   ));

    public enum ScrapSeedMode { PerimeterEveryN, RandomScatter }

    IEnumerator Scraps_SeedAndGrowUntilPacked(
        ScrapSeedMode mode,
        int perimeterSpacing = 10,
        int randomSeedsPerRegion = 3,
        int randomMinSpacing = 6,
        int maxRounds = 4,
        int moatOverride = -1,
        int yieldEvery = 2048
    )
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;
        int moat = (moatOverride >= 0) ? moatOverride : Mathf.Max(0, cfg.grow.wallMoat);

        for (int round = 0; round < maxRounds; round++)
        {
            // 1) Build scrap mask
            bool[,] scrap = new bool[W, H];
            int scrapsCount = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = cellGrid[x, y];
                    bool isScrap = !c.isCorridor && c.room_number < 0;
                    scrap[x, y] = isScrap;
                    if (isScrap) scrapsCount++;
                }
            if (scrapsCount == 0) yield break;

            // 2) Extract scrap regions (flood fill)
            var regions = new List<List<(int x, int y)>>();
            var seen = new bool[W, H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!scrap[x, y] || seen[x, y]) continue;
                    var cells = new List<(int, int)>();
                    //var q = new Queue<(int, int)>();
                    var q = new Queue<(int x, int y)>();
                    //q.Enqueue((x, y)); seen[x, y] = true;
                    q.Enqueue((x: x, y: y));   // named tuple

                    while (q.Count > 0)
                    {
                        var p = q.Dequeue();
                        cells.Add(p);
                        foreach (var nb in FourNeighbors(p.x, p.y))
                        {
                            if (!In(nb.x, nb.y)) continue;
                            if (!scrap[nb.x, nb.y] || seen[nb.x, nb.y]) continue;
                            seen[nb.x, nb.y] = true;
                            q.Enqueue(nb);
                        }
                    }
                    regions.Add(cells);
                    if (regions.Count % 16 == 0) yield return null;
                }

            // 3) For each region, compute perimeter (for perimeter seeding) and place seeds
            var newRoomIds = new List<int>();  // track brand-new rooms for filtered growth
            int createdSeeds = 0;

            foreach (var reg in regions)
            {
                if (reg.Count == 0) continue;

                // Quick perimeter extraction
                var perimeter = new List<(int x, int y)>();
                foreach (var p in reg)
                {
                    bool onEdge = false;
                    // perimeter if any 4-neighbor is non-scrap or OOB
                    if (p.x == 0 || p.x == W - 1 || p.y == 0 || p.y == H - 1) onEdge = true;
                    else
                    {
                        if (!scrap[p.x - 1, p.y] || !scrap[p.x + 1, p.y] || !scrap[p.x, p.y - 1] || !scrap[p.x, p.y + 1])
                            onEdge = true;
                    }
                    if (onEdge) perimeter.Add(p);
                }

                // Seed set for this region (positions)
                var seeds = new List<(int x, int y)>();

                if (mode == ScrapSeedMode.PerimeterEveryN)
                {
                    if (perimeter.Count == 0) continue;
                    // walk around perimeter pseudo-order: just iterate by index spacing
                    int step = Mathf.Max(1, perimeterSpacing);
                    for (int i = 0; i < perimeter.Count; i += step)
                    {
                        var s = perimeter[i];
                        if (CanPlaceSeed(s.x, s.y, moat))
                            seeds.Add(s);
                    }
                }
                else // RandomScatter
                {
                    // uniform pick from region, enforce min spacing between seeds
                    int want = Mathf.Max(1, randomSeedsPerRegion);
                    var tried = 0;
                    var rngPick = new System.Random(reg.Count * 73856093 ^ regions.Count);
                    while (seeds.Count < want && tried < reg.Count * 3)
                    {
                        var p = reg[rngPick.Next(reg.Count)];
                        tried++;
                        if (!CanPlaceSeed(p.x, p.y, moat)) continue;
                        bool far = true;
                        for (int j = 0; j < seeds.Count; j++)
                        {
                            if (Manhattan(p, seeds[j]) < randomMinSpacing) { far = false; break; }
                        }
                        if (far) seeds.Add(p);
                    }
                }

                // Create a room per seed and claim the seed cell
                foreach (var s in seeds)
                {
                    int id = rooms.Count;
                    Room room = new Room { my_room_number = id, cells = new List<Cell>() };
                    room.setColorFloor(highlight: true);
                    Cell c = cellGrid[s.x, s.y];
                    if (c.room_number >= 0 || c.isCorridor) continue; // safety
                    c.room_number = room.my_room_number;
                    c.colorFloor = room.colorFloor;
                    c.height = 0; //50 * (round + 1);    // DEBUG: new cells are raised above others.
                    room.cells.Add(c);
                    room.bounds = new RectInt(s.x, s.y, 1, 1);
                    rooms.Add(room);
                    newRoomIds.Add(id); // will be passed to next round of Grow
                    createdSeeds++;
                }

                if (createdSeeds % 64 == 0) yield return null;   // breathe
            }

            // If nothing seeded this round, bail to avoid infinite loop
            if (createdSeeds == 0) yield break;

            if (cfg.showBuildProcess)       // draw the seeds
            {
                DrawMapByRooms(rooms, clearscreen: true);
                yield return new WaitForSeconds(0.025f);
            }
            // 4) Grow *only* the newly created rooms with a filtered credit wavefront
            //yield return StartCoroutine(Grow_CreditWavefront_Filtered(newRoomIds, moat, yieldEvery));
            yield return StartCoroutine(Grow_CreditWavefrontStrips(newRoomIds));
            // 5) Loop and see if more scraps remain next round
        }
        yield break;
    }

    // ---------------- helpers ----------------

    // ROOM READY
    // Allocate the memory for cellGrid.  Fill it with cells containing location only.
    //   Do it once at the beginning.
    void InitializeCellGrid()
    {
        cellGrid = new Cell[cfg.mapWidth, cfg.mapHeight]; // allocate memory for this array
        Cell cell;
        // fill the array with allocated Cells
        for (int y = 0; y < cfg.mapHeight; y++)
            for (int x = 0; x < cfg.mapWidth; x++)
            {
                // initialize a cell for it
                cell = new Cell(x, y);
                cell.room_number = -1;
                cell.isCorridor = false;
                cell.colorFloor = colorDefault;
                // assign it
                cellGrid[x, y] = cell;
            }
    }

    // take every cell in Rooms, and make the references in cellGrid point to the same cell for automatic cross updating.
    void UpdateCellGridFromRooms(List<Room> rooms)
    {
        cellGrid = new Cell[cfg.mapWidth, cfg.mapHeight]; // allocate memory for this array
        Cell cell;

        foreach (Room room in rooms)
        {
            foreach (Cell rc in room.cells)
            {
                cellGrid[rc.pos.x, rc.pos.y] = rc;
            }
        }
        // fill the array with allocated Cells
        for (int y = 0; y < cfg.mapHeight; y++)
        {
            for (int x = 0; x < cfg.mapWidth; x++)
            {
                if (cellGrid[x, y] == null)
                {
                    // initialize a cell for unused parts of the map.
                    cell = new Cell(x, y);
                    cell.room_number = -1;
                    cell.isCorridor = false;
                    cell.colorFloor = colorDefault;
                    // assign it
                    cellGrid[x, y] = cell;
                }
            }
        }
    }

    void UpdateRoomsFromCellGrid()
    {
        Room room;
        Cell cell;
        for (int num_r = 0; num_r < rooms.Count; num_r++)
        {
            room = rooms[num_r];
            for (int num_c = 0; num_c < room.cells.Count; num_c++)
            {
                cell = room.cells[num_c];
                rooms[num_r].cells[num_c] = cellGrid[cell.pos.x, cell.pos.y];
            }
        }
    }

    // unused.  Obsolete. Use CanPlaceSeed() instead.
    bool CanPlaceReSeed(int x, int y, int moatCells)
    {
        if (!In(x, y)) return false;
        var c = cellGrid[x, y];
        if (c.isCorridor) return false;
        if (c.room_number >= 0) return false;

        // keep distance from corridors & existing rooms (moat)
        for (int dy = -moatCells; dy <= moatCells; dy++)
            for (int dx = -moatCells; dx <= moatCells; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (!In(nx, ny)) continue;
                var n = cellGrid[nx, ny];
                if (n.isCorridor) return false;
                if (n.room_number >= 0) return false;
            }
        return true;
    }

    // ROOM READY
    // used in Scraps_SeedAndGrowUntilPacked() to verify seeds are far enough apart.
    // simple Manhattan distance (delta x + delta y) between two points.    
    int Manhattan((int x, int y) a, (int x, int y) b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);


    // =========================================================
    // ==================== HELPERS ============================
    // =========================================================

    // These all need to be cleaned up, commented, and organized better....

    // NOTE: these next three functions are doing similar things: (maybe combine in future)
    //       IsNearCorridor, ClearOfForeign, TouchesDifferentOrCorridor

    // ROOM READY
    // used in Scraps_VoronoiFill.
    // returns true if (x,y is at least moatCells away from a corridor)
    bool IsNearCorridor(int x, int y, int moatCells)
    {
        for (int dy = -moatCells; dy <= moatCells; dy++)
            for (int dx = -moatCells; dx <= moatCells; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (!In(nx, ny)) continue;
                if (cellGrid[nx, ny].isCorridor) return true;
            }
        return false;
    }

    // ROOM READY
    // used in Scraps_VoronoiFill.
    // returns true if (x,y) is at least moatCells away from non-empty cells (other rooms and any corridor).
    bool ClearOfForeign(int x, int y, int moatCells)
    {
        for (int dy = -moatCells; dy <= moatCells; dy++)
            for (int dx = -moatCells; dx <= moatCells; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (!In(nx, ny)) continue;
                var n = cellGrid[nx, ny];
                if (n.isCorridor) return false;
                if (n.room_number >= 0) return false; // keep clearance from *current* rooms
            }
        return true;
    }

    // ROOM READY
    // used in Scraps_VoroniFill during peel.
    // determines if we are adjacent to a room or corridor (with moat between).
    // variables are named weird: label=?  my=label[x,y] ? what do these do?
    bool TouchesDifferentOrCorridor(int[,] label, int x, int y, int my, int moatCells)
    {
        for (int dy = -moatCells; dy <= moatCells; dy++)
            for (int dx = -moatCells; dx <= moatCells; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (!In(nx, ny)) continue;

                if (cellGrid[nx, ny].isCorridor) return true;
                int l = label[nx, ny];
                if (l >= 0 && l != my) return true;
            }
        return false;
    }

    // unused.  Probably replaced by frontiers?
    // checks for a cell on each side of the room's bounding box until it finds an open cell.
    Vector2Int LookForOpenNeighborCell(int room_num, int moat)
    {
        Vector2Int starter = new();
        //Vector2Int pt = new();
        RectInt box = rooms[room_num].GetBounds();
        starter.x = rng.Next(box.x, box.x + box.width);
        starter.y = rng.Next(box.y, box.y + box.height);

        Vector2Int NN, SS, EE, WW;
        NN = new Vector2Int(starter.x, box.y - moat - 1);
        SS = new Vector2Int(starter.x, box.y + box.width + moat + 1);
        WW = new Vector2Int(box.x - moat - 1, starter.y);
        EE = new Vector2Int(starter.x, box.y - moat - 1);

        Cell N, S, E, W;
        N = new Cell(NN.x, NN.y);   // unnecessary assignments
        S = new Cell(SS.x, SS.y);
        E = new Cell(EE.x, EE.y);
        W = new Cell(WW.x, WW.y);

        if (In(NN.x, NN.y)) N = cellGrid[NN.x, NN.y];
        if (In(SS.x, SS.y)) S = cellGrid[SS.x, SS.y];
        if (In(WW.x, WW.y)) W = cellGrid[WW.x, WW.y];
        if (In(EE.x, EE.y)) E = cellGrid[EE.x, EE.y];

        if (In(NN.x, NN.y) && N.isCorridor == false && N.room_number > 0) return NN;
        if (In(SS.x, SS.y) && S.isCorridor == false && S.room_number > 0) return SS;
        if (In(WW.x, WW.y) && W.isCorridor == false && W.room_number > 0) return WW;
        if (In(EE.x, EE.y) && E.isCorridor == false && E.room_number > 0) return EE;
        return new Vector2Int(-1, -1);
    }

    // unused (was used in wavefront, which has been replaced by strips)
    (int x, int y) PickFrontier(HashSet<(int x, int y)> frontier, int ri)
    {
        int bestScore = int.MinValue;
        (int x, int y) best = (-1, -1);
        foreach (var p in frontier)
        {
            int s = 0;
            // score by how many owned neighbors (smooth boundary) and how far from corridors
            foreach (var nb in FourNeighbors(p.x, p.y))
            {
                if (!In(nb.x, nb.y)) continue;
                if (cellGrid[nb.x, nb.y].room_number == ri) s += 2;
                if (cellGrid[nb.x, nb.y].isCorridor) s -= 3;
            }
            if (s > bestScore) { bestScore = s; best = p; }
        }
        // fallback if set somehow empty
        if (best.x < 0 && frontier.Count > 0) foreach (var p in frontier) { best = p; break; }
        return best;
    }

    // ROOM READY
    // Checks if the room is big enough and stretched enough to split.
    // Determines where to cut the room.
    // Performs the cut: moat cells removed, left side kept in old room, right side moved to a new room.
    // Add new room to master rooms list
    // Update the screen, calculate new bounds and frontiers.
    int SplitOversizedRooms(int moatCells, List<HashSet<(int, int)>> frontiers)
    {
        int cuts_made = 0;
        for (int i = 0; i < rooms.Count; i++)
        {
            var r = rooms[i];
            int area = r.cells.Count;
            if (area <= cfg.grow.splitArea) continue; // Don't cut room

            RectInt old_bounding_box = r.GetBounds(); // save for debug message at end.

            // Compute room bounds and aspect ratio
            RectInt bounds = r.GetBounds();
            int minx = bounds.x, miny = bounds.y, maxx = bounds.xMax, maxy = bounds.yMax;
            int w = Mathf.Max(1, bounds.width); //maxx - minx + 1);
            int h = Mathf.Max(1, bounds.height); //maxy - miny + 1);
            float aspect = (float)Mathf.Max(w, h) / Mathf.Max(1, Mathf.Min(w, h));
            if (aspect < cfg.grow.splitAspect) continue; // Don't cut room

            // decide where to split and which direction
            int splitPercent = rng.Next(25, 75);   // 25%-75%
            bool splitVert = (w >= h); // cut along long axis
            int cut = splitVert ? (minx + w * splitPercent / 100) : (miny + h * splitPercent / 100);
            //Debug.Log($"Cut box: min={minx},{miny} max={maxx},{maxy}");

            // Create new room
            Room newRoom = new Room { my_room_number = rooms.Count, cells = new List<Cell>() };
            newRoom.setColorFloor(highlight: true);  // DEBUG: set false to make it look like a corridor
            newRoom.my_room_number = rooms.Count;
            newRoom.isCorridor = false;

            // reset lists of cells for the old and new rooms after split.
            var keep = new List<Cell>();
            var newer = new List<Cell>();

            // Reassign cells and carve a moat wall along the cut line
            for (int old_cell_num = 0; old_cell_num < r.cells.Count; old_cell_num++)
            {
                // for classifying which part this cell will belong to: cut, left, right
                bool leftSide;  // true=left, false=right (except for cut cells)
                bool onCut;     // true=cell in moat, turn into a wall

                Cell c = r.cells[old_cell_num];   // cell we are working on
                // decide limit of bins cut/left/right or just left/right if thin walls
                if (cfg.useThinWalls)   // thin walls => no cut, only left/right
                {
                    leftSide = splitVert ? (c.x < cut) : (c.y < cut);
                    onCut = false;
                }
                else    // cut = in the moat
                {
                    leftSide = splitVert ? (c.x < cut) : (c.y < cut);
                    onCut = splitVert ? ((c.x >= cut) && (c.x < cut + moatCells))
                                      : ((c.y >= cut) && (c.y < cut + moatCells));
                }

                // do the cutting...
                if (onCut)
                {
                    // leave as wall: unassign
                    int old_room_id = c.room_number;
                    c.room_number = -1;

                    // remove the tile on the screen
                    if (cfg.showBuildProcess)
                    {
                        Vector3Int pos3 = new Vector3Int(c.x, c.y, 0);
                        tilemap.SetTile(pos3, null); // clear the tile
                    }
                }
                else if (leftSide) // keep in original room
                {
                    keep.Add(c);

                }
                else // rightSide, move to new room
                {
                    newer.Add(c);

                    // re-color the tile on the screen
                    if (cfg.showBuildProcess)
                    {
                        Vector3Int pos3 = new Vector3Int(c.x, c.y, 0); // required for tilemap
                        tilemap.SetTile(pos3, floorTile); // should already be set
                        tilemap.SetTileFlags(pos3, TileFlags.None); // Allow color changes
                        tilemap.SetColor(pos3, newRoom.colorFloor); // Set default color
                    }
                }
            } // end for old_cell_num

            r.cells = keep;         // replace old list with left side half of cells
            newRoom.cells = newer;  // replace empty list with right side half of cells

            foreach (Cell cell in newRoom.cells)
            {   // change moved cell contents to match new room
                cell.room_number = newRoom.my_room_number;
                cell.colorFloor = newRoom.colorFloor;
            }

            if (newRoom.cells.Count > 0)    // add to master rooms list
            {
                rooms.Add(newRoom);
                //Debug.Log($"adding newRoom #{rooms.Count} with {newRoom.cells.Count} cells");
            }

            cuts_made++;

            // recalculate bounds and display what we did.
            RectInt new_box_a = r.GetBounds();          // recalculate bounding box for old room.
            RectInt new_box_b = newRoom.GetBounds();    // recalculate bounding box for new room.

            Debug.Log($"Split room {r.my_room_number} into newroom {newRoom.my_room_number}; splitvert {splitVert}; cutline = {cut} ({splitPercent}%)");
            Debug.Log($"original_box = {old_bounding_box.x},{old_bounding_box.y},{old_bounding_box.xMax},{old_bounding_box.yMax}");
            Debug.Log($"new_box_a    = {new_box_a.x},{new_box_a.y},{new_box_a.xMax},{new_box_a.yMax}");
            Debug.Log($"new_box_b    = {new_box_b.x},{new_box_b.y},{new_box_b.xMax},{new_box_b.yMax}");

            // refresh frontiers roughly (cheap approach: recompute perimeter for both rooms)
        }

        // Re-compute all room bounds after all cuttings made (could do above for only cut rooms, but didn't)
        foreach (Room r in rooms) r.GetBounds();

        // Rebuild all frontiers also. (list of cells adjacent to rooms)
        // NOTE: frontiers list size must match rooms; expand if we added new rooms
        while (frontiers.Count < rooms.Count) frontiers.Add(new HashSet<(int, int)>());
        for (int fi = 0; fi < frontiers.Count; fi++)
            RebuildFrontierFor(fi, moatCells, frontiers[fi]);

        return cuts_made;
    }

    // Unused.
    // Temporary hack for getting rid of lost cells.
    int DeleteAllCellsAtPos(Vector2Int pos)
    {
        int num_deleted = 0;
        for (int try_room_id = 0; try_room_id < rooms.Count; try_room_id++)
        {
            int cell_index = rooms[try_room_id].cells.FindIndex(cell => cell.pos.x == pos.x && cell.pos.y == pos.y);
            if (cell_index != -1)
            {
                Cell cell = rooms[try_room_id].cells[cell_index];
                //Debug.Log($"DeleteAllCellsAtPos: cell_index = {cell_index}, room_number = {try_room_id}");//, old_room_id = {old_room_id}");
                rooms[try_room_id].cells.RemoveAt(cell_index);
                num_deleted++;
            }
        }
        return num_deleted;
    }

    // ROOMS READY
    // Builds one HashSet frontier for room (ri) containing all the cells that the room
    //   could grow to.
    void RebuildFrontierFor(int ri, int moatCells, HashSet<(int, int)> dst)
    {
        dst.Clear();
        foreach (var c in rooms[ri].cells)
            foreach (var nb in FourNeighbors(c.x, c.y))
                if (CanClaim(ri, nb.x, nb.y, moatCells))
                    dst.Add((nb.x, nb.y));
    }

    // ComputeAabb() gets the bounding rectangle of all cell locations in a Room
    // same as Room.GetBounds()?????????????????????
    RectInt ComputeAabb(Room r)
    {
        int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;
        foreach (var c in r.cells)
        {
            if (c.x < minx) minx = c.x; if (c.x > maxx) maxx = c.x;
            if (c.y < miny) miny = c.y; if (c.y > maxy) maxy = c.y;
        }
        if (minx == int.MaxValue) return new RectInt(0, 0, 0, 0);
        return new RectInt(minx, miny, maxx - minx + 1, maxy - miny + 1);
    }

    // ROOM READY
    // score depends on the length-to-height ratio modified by the targetAsp parameter,
    //                  the cooldown penalty for that direction,
    //                  a preference for the short axis.
    // Return sides in best-first order: 0:E,1:W,2:N,3:S
    List<int> ScoreSidesForStrip(int ri, RectInt bb, float targetAsp, float currentAsp, int[] cd)
    {
        int w = Mathf.Max(1, bb.width), h = Mathf.Max(1, bb.height);
        bool preferShortAxis = (w > h * targetAsp); // true => grow N/S; false => E/W preferred if h > w*targetAsp

        var list = new List<(int side, int score)>(4);
        int baseScoreE = (h); // E/W adds a column of 'h' cells
        int baseScoreW = (h);
        int baseScoreN = (w); // N/S adds a row of 'w' cells
        int baseScoreS = (w);

        int cooldownPenalty(int side) => (cd[side] > 0) ? (cd[side] * 1000) : 0;

        // start with base gain
        int sE = baseScoreE - cooldownPenalty(0);
        int sW = baseScoreW - cooldownPenalty(1);
        int sN = baseScoreN - cooldownPenalty(2);
        int sS = baseScoreS - cooldownPenalty(3);

        // compactness bias: push short axis first
        if (preferShortAxis) { sN += 10; sS += 10; }
        else { sE += 10; sW += 10; }

        list.Add((0, sE)); list.Add((1, sW)); list.Add((2, sN)); list.Add((3, sS));
        list.Sort((a, b) => b.score.CompareTo(a.score));
        var order = new List<int>(4) { list[0].side, list[1].side, list[2].side, list[3].side };
        return order;
    }

    // ROOM READY
    // Try to grow a full 1-cell strip on the chosen side.
    // Returns true if the whole strip was claimed.
    // side: 0=E (x=max+1), 1=W (x=min-1), 2=N (y=max+1), 3=S (y=min-1)
    bool TryGrowFullStrip(int ri, ref RectInt bounds, int side, int moatCells)
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;
        int minx = bounds.xMin, maxx = bounds.xMax - 1;
        int miny = bounds.yMin, maxy = bounds.yMax - 1;

        if (side == 0) // E
        {
            int x = maxx + 1;
            //if ((uint)x >= (uint)W) return false;  // bounds checking in CanClaim
            for (int y = miny; y <= maxy; y++)
                if (!CanClaim(ri, x, y, moatCells)) return false;

            for (int y = miny; y <= maxy; y++) ClaimCell(ri, x, y);
            bounds.width += 1;
            return true;
        }
        if (side == 1) // W
        {
            int x = minx - 1;
            //if (x < 0) return false;  // bounds checking in CanClaim
            for (int y = miny; y <= maxy; y++)
                if (!CanClaim(ri, x, y, moatCells)) return false;

            for (int y = miny; y <= maxy; y++) ClaimCell(ri, x, y);
            bounds.x -= 1; bounds.width += 1;
            return true;
        }
        if (side == 2) // N
        {
            int y = maxy + 1;
            //if ((uint)y >= (uint)H) return false;  // bounds checking in CanClaim
            for (int x = minx; x <= maxx; x++)
                if (!CanClaim(ri, x, y, moatCells)) return false;

            for (int x = minx; x <= maxx; x++) ClaimCell(ri, x, y);
            bounds.height += 1;
            return true;
        }
        else // 3:S
        {
            int y = miny - 1;
            //if (y < 0) return false;  // bounds checking in CanClaim
            for (int x = minx; x <= maxx; x++)
                if (!CanClaim(ri, x, y, moatCells)) return false;

            for (int x = minx; x <= maxx; x++) ClaimCell(ri, x, y);
            bounds.y -= 1; bounds.height += 1;
            return true;
        }
    }

    // Unused.
    // Wavefront helpers (compactness-biased pick).
    // Selects best cell in frontier, based on score:
    //    SCORE: -3: not close to corridor
    //           +2: adjacent to more cells in the same room
    (int x, int y) PickFrontier_CompactBias(HashSet<(int x, int y)> frontier, int ri)
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;    // Debug was -1
        int bestScore = int.MinValue; (int x, int y) best = (-1, -1);
        foreach (var p in frontier)
        {
            int s = 0;
            foreach (var nb in FourNeighbors(p.x, p.y))
            {
                if (!In(nb.x, nb.y)) continue;                      // off-map
                if (cellGrid[nb.x, nb.y].room_number == ri) s += 2; // prefer filling along our boundary
                if (cellGrid[nb.x, nb.y].isCorridor) s -= 3;        // keep distance to corridors
            }
            if (s > bestScore) { bestScore = s; best = p; }
        }
        if (best.x < 0 && frontier.Count > 0) foreach (var p in frontier) { best = p; break; }
        return best;
    }

    // ROOM READY
    // Easy little routine to return location of the cells in 4 directions, skipping those that point off-map.
    IEnumerable<(int x, int y)> FourNeighbors(int x, int y)
    {
        int W = cfg.mapWidth - 1, H = cfg.mapHeight - 1;
        if (x > 0) yield return (x - 1, y);
        if (x < W - 1) yield return (x + 1, y);
        if (y > 0) yield return (x, y - 1);
        if (y < H - 1) yield return (x, y + 1);
    }

    // ROOM READY
    // Nearly the same as Can Place Seed.  Can easily merge functionality.  TODO.
    // Only difference is that ri is passed in and may return true if the 
    //   room already owns the cell or neighbors which is useful here.
    bool CanClaim(int ri, int x, int y, int moatCells)
    {
        if (!In(x, y)) return false;
        var c = cellGrid[x, y];
        if (c.isCorridor) return false;
        if (c.room_number >= 0 /*&& c.room_number != ri*/) return false;

        // keep a moat around corridors and other rooms
        for (int dy = -moatCells; dy <= moatCells; dy++)
            for (int dx = -moatCells; dx <= moatCells; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (!In(nx, ny)) return false;
                var n = cellGrid[nx, ny];
                if (n.isCorridor) return false;
                if (n.room_number >= 0 /*&& n.room_number != ri*/) return false;
            }
        return true;
    }


    // ROOM READY
    // Grabs the cell from cellGrid, and adds it to a given room (ri).
    // Does not check previous owner except to determine if it already owns it.
    void ClaimCell(int ri, int x, int y)
    {
        //Debug.Log($"ClaimCell(ri:{ri}, x:{x}, y:{y})");
        var c = cellGrid[x, y];
        //if (c.room_number == ri) return;  // already owned
        c.room_number = rooms[ri].my_room_number;
        c.colorFloor = rooms[ri].colorFloor;
        c.isCorridor = rooms[ri].isCorridor;
        rooms[ri].cells.Add(c);
    }

    // ROOM READY
    // Never used, algorithm instead removes duplicates later.
    // Looks for an existing Room that is a duplicate of the one being added.
    bool tryAddRoom(List<Room> rooms, Room room)
    {
        foreach (Room r in rooms)
        {
            // TODO: check contents instead ?????
            if (r == room) return false;
        }
        rooms.Add(room);
        return true;
    }

    // ROOM READY
    // Never used, algorithm instead removes duplicates later.
    // Looks for an existing Cell that is a duplicate of the one being added.
    bool tryAddCell(List<Cell> cells, Cell cell)
    {
        foreach (Cell c in cells)
        {
            if (c == cell) return false;        // exact match ?  check contents
        }
        cells.Add(cell);
        return true;
    }

    // Unused
    bool tryAddPackRoom(List<Room> rooms, Room room)
    {
        foreach (Room r in rooms)
        {
            // TODO: check contents instead ?????
            if (r == room) return false;
        }
        rooms.Add(room);
        return true;
    }

    // Unused
    bool tryAddPackCell(List<Cell> cells, Cell cell)
    {
        foreach (Cell c in cells)
        {
            if (c == cell) return false;
        }
        cells.Add(cell);
        return true;
    }

    // ROOM READY
    // Searches a list of cells and removes duplicates matching X,Y,AND Z (usually list is of a single room)
    // See the function RemoveDuplicateCellsFromAllRooms() for the global search.
    public static int RemoveDuplicateCells(List<Cell> cells)
    {
        if (cells == null || cells.Count == 0) return 0;

        int originalCount = cells.Count;

        // Seen coordinates
        var seen = new HashSet<(int, int, int)>();
        // New list preserving order
        var unique = new List<Cell>(cells.Count);

        foreach (var c in cells)
        {
            var key = (c.x, c.y, c.z);
            if (!seen.Contains(key))
            {
                seen.Add(key);
                unique.Add(c);   // preserve first occurrence order
            }
        }

        // Replace original contents
        cells.Clear();
        cells.AddRange(unique);

        int num_removed = originalCount - cells.Count;
        if (num_removed > 0) Debug.Log($"RemoveDuplicateCells removed {num_removed}");
        return num_removed; // number removed
    }

    // ROOMS READY
    // Searches all Cells in all Rooms and removes duplicates matching X,Y,AND Z.
    public static int RemoveDuplicateCellsFromAllRooms(List<Room> rooms)
    {
        int originalCount = 0;
        int afterCount = 0;

        // Seen coordinates
        var seen = new HashSet<(int, int, int)>();  // Seen hash set
        // New list preserving order 
        var unique = new List<Cell>(1024);          // Only the unique Cells

        foreach (Room room in rooms)
        {
            List<Cell> cells = room.cells;

            if (cells == null || cells.Count == 0) return 0;

            originalCount += cells.Count;

            foreach (var c in cells)
            {
                var key = (c.x, c.y, c.z);
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    unique.Add(c);   // preserve first occurrence order
                }
            }

            // Replace original contents
            cells.Clear();
            cells.AddRange(unique);
            afterCount += cells.Count;
        }
        int num_removed = originalCount - afterCount;
        if (num_removed > 0) Debug.Log($"RemoveDuplicateCellsFromAllRooms removed {num_removed}");
        return num_removed; // number removed
    }

    // ROOM READY
    // Returns a count of the directions that have corridors of greater than corridorWidth long
    // Works with wide corridors.
    int CountCorridorNeighbors(int x, int y)
    {
        int minStraight = cfg.corridor.corridorWidth;
        int scanLen = minStraight + 1;
        int c = 0;
        int L = CountRun(x, y, -1, 0, scanLen);
        int R = CountRun(x, y, 1, 0, scanLen);
        int D = CountRun(x, y, 0, -1, scanLen);
        int U = CountRun(x, y, 0, 1, scanLen);
        if (L > minStraight) c++;
        if (R > minStraight) c++;
        if (D > minStraight) c++;
        if (U > minStraight) c++;
        return c;
    }

    // ROOM READY
    // Pick a tangent direction at (x,y) by counting how many contiguous corridor
    // cells exist to the Left/Right/Down/Up (L/R/D/U). Wider corridors are handled
    // because we look past a single neighbor. Returns (1,0) for horizontal,
    // (0,1) for vertical, or Vector2Int.zero if nothing usable is found.
    Vector2Int PickTangentDir(int x, int y, int scanLen = 12, int minStraight = 2)
    {
        int W = cfg.mapWidth - 1, H = cfg.mapHeight - 1;

        int L = CountRun(x, y, -1, 0, scanLen);
        int R = CountRun(x, y, 1, 0, scanLen);
        int D = CountRun(x, y, 0, -1, scanLen);
        int U = CountRun(x, y, 0, 1, scanLen);

        int horizontal = L + R;
        int vertical = D + U;

        // Strong signal: both sides present along an axis
        bool hasHoriz = (L >= minStraight && R >= minStraight);
        bool hasVert = (D >= minStraight && U >= minStraight);

        if (hasHoriz && !hasVert) return new Vector2Int(1, 0);
        if (!hasHoriz && hasVert) return new Vector2Int(0, 1);

        // If both (junction) or neither (corner/dead-end), pick the axis with more total run.
        if (horizontal > vertical) return new Vector2Int(1, 0);
        if (vertical > horizontal) return new Vector2Int(0, 1);

        // Tie-breakers:
        // 1) prefer the side with the single longest run
        int longest = Mathf.Max(Mathf.Max(L, R), Mathf.Max(D, U));
        if (longest == 0) return Vector2Int.zero; // isolated or no corridors around

        if (longest == L || longest == R) return new Vector2Int(1, 0);
        if (longest == D || longest == U) return new Vector2Int(0, 1);

        return Vector2Int.zero; // very rare fallback
    }

    // ROOMS ready
    // Returns the number of cells that continue to be a corridor in a given
    //   direction (d) from starting point (s).
    // Only looks as far as maxSteps.
    // Used in Seed_AlongCorridors via the PickTangentDir and CountCorridorNeighbors functions
    //   to determine which directions the corridor goes from a given location
    //   (since corridors can be wide we need to look farther).
    int CountRun(int sx, int sy, int dx, int dy, int maxSteps)
    {
        int c = 0;
        for (int i = 1; i <= maxSteps; i++)
        {
            int nx = sx + dx * i, ny = sy + dy * i;
            if (!In(nx, ny)) break;

            // Fast path using your grid flag:
            if (!cellGrid[nx, ny].isCorridor) break;

            c++;
        }
        return c;
    }

    // ROOM ready
    // Used by Seed_AlongCorridors algorithm
    // Return a direction 90 degrees left or right.
    Vector2Int Perp(Vector2Int t, bool left)
    {
        // left: (x,y)->(-y,x) ; right: (x,y)->(y,-x)
        return left ? new Vector2Int(-t.y, t.x) : new Vector2Int(t.y, -t.x);
    }

    // In-place shuffle of a list of Vector2Int
    void Shuffle(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; --i)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // CONVERTED to ROOM
    // Used by Seed_AlongCorridors algorithm and Scraps_SeedAndGrowUntilPacked algorithm
    // Checks if location is unoccupied and moat cells around it are clear also.
    bool CanPlaceSeed(int x, int y, int moatCells)
    {
        // check location for availability
        if (!In(x, y)) return false;            // is off-map
        Cell c = cellGrid[x, y];
        if (c.isCorridor) return false;         // is a corridor
        if (c.room_number >= 0) return false;   // is a room (or corridor)

        // Enforce a moat around corridors and other rooms
        // check neighbors for availability
        for (int dy = -moatCells; dy <= moatCells; dy++)
            for (int dx = -moatCells; dx <= moatCells; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (!In(nx, ny)) continue;              // neighbor off-map, ok to continue other cells
                Cell n = cellGrid[nx, ny]; // neighbor
                if (n.isCorridor) return false;         // neighbor is a corridor
                if (n.room_number >= 0) return false;   // neighbor is a room (or corridor)
            }
        return true;
    }

    // CONVERTED to ROOM
    // Used by Seed_AlongCorridors algorithm
    // assumes location is a valid seed location = passed CanPlaceSeed()
    void CreateRoomSeedAt(int x, int y)
    {
        // create an empty room
        Room new_room = new Room { my_room_number = rooms.Count, cells = new List<Cell>() };
        new_room.setColorFloor(highlight: true);
        new_room.isCorridor = false;

        // place a room cell in it at x,y
        Cell new_cell = new Cell(x, y);
        cellGrid[x, y] = new_cell;
        //Cell new_cell = cellGrid[x, y];
        new_cell.room_number = new_room.my_room_number;
        new_cell.height = 0;
        new_cell.colorFloor = new_room.colorFloor;
        new_room.cells.Add(new_cell);

        // room bounds are easy, don't bother calculating it.
        new_room.bounds = new RectInt(x, y, 1, 1);
        rooms.Add(new_room);   // add room to master list of rooms

        //Debug.Log($"Created Room {new_cell.room_number} seed at {x},{y}");
    }

    public Room ExtractRoomFromVectors(List<Vector2Int> vect)
    {
        Color color;
        Debug.Log($"Extracting {vect.Count} vectors..");

        var result = new Room();
        //HashSet<(int, int)> corridorHash = new();
        // Convert corridors
        foreach (var pr in vect)
        {
            List<Cell> cells = new();

            //if (pr.cells.Count == 0) continue;
            // Finalize room bounds
            int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;

            // Create this Room's cell list (x,y) only
            foreach (var c in vect)
            {
                cells.Add(new Cell(c.x, c.y));
                { if (c.x < minx) minx = c.x; if (c.x > maxx) maxx = c.x; if (c.y < miny) miny = c.y; if (c.y > maxy) maxy = c.y; }
            }

            // Create Room object
            var r = new Room
            {
                my_room_number = 1,
                area = cells.Count,
                bounds = new RectInt(minx, miny, Mathf.Max(1, maxx - minx + 1), Mathf.Max(1, maxy - miny + 1)),
                cells = cells,
                isCorridor = true,
            };
            r.setColorFloor(highlight: false);
            color = r.colorFloor;
            foreach (Cell cell in r.cells) cell.colorFloor = color;

            return r;
        }

        return result;
    }


    // ======================= Shared Utility functions =======================
    // shared functions pulled out of above
    Vector2Int RandomCardinal()
    {
        switch (rng.Next(0, 4))
        {
            case 0: return new Vector2Int(1, 0);
            case 1: return new Vector2Int(-1, 0);
            case 2: return new Vector2Int(0, 1);
            default: return new Vector2Int(0, -1);
        }
    }

    // This replaces RandomCardinal for starting positions to keep from following borders too much.
    Vector2Int DirAwayFromEdge(Vector2Int pos)
    {
        int border = 10;    // distance to edge that is considered too close
        int W = cfg.mapWidth - 1, H = cfg.mapHeight - 1;
        if ((W - pos.x) < border) return new Vector2Int(-1, 0);
        if ((pos.x) < border) return new Vector2Int(1, 0);
        if ((H - pos.y) < border) return new Vector2Int(0, -1);
        if ((pos.y) < border) return new Vector2Int(0, 1);
        return RandomCardinal();  // not near any edge
    }

    Vector2Int TurnLeft(Vector2Int d, bool left)
    {
        // left: (x,y)->(-y,x) ; right: (x,y)->(y,-x)
        return left ? new Vector2Int(-d.y, d.x) : new Vector2Int(d.y, -d.x);
    }

    // checks if the location is inside map bounds (with keepout)
    public bool In(int x, int y) => (x < (cfg.mapWidth - cfg.borderKeepout))
                                 && (y < (cfg.mapHeight - cfg.borderKeepout))
                                 && (x >= cfg.borderKeepout)
                                 && (y >= cfg.borderKeepout);

    // If you want random edge starts instead of center:
    Vector2Int RandomEdgeStart(int w, int h)
    {
        int ko = cfg.borderKeepout;
        int edge = rng.Next(0, 4);
        return edge switch
        {
            0 => new Vector2Int(rng.Next(ko, w - ko - 1), ko),
            1 => new Vector2Int(rng.Next(ko, w - ko - 1), h - ko - 1),
            2 => new Vector2Int(ko, rng.Next(ko, h - ko - 1)),
            _ => new Vector2Int(w - ko - 1, rng.Next(ko, h - ko - 1)),
        };
    }

    // Used for creating tunnels.
    void CarveDisk(ref Room tmp_room, Vector2Int c, int penWidth)
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;
        int min = -(int)Math.Floor(penWidth / 2f); // makes the negative more to zero
        int max = min + penWidth - 1;

        // Debug.Log($"  CarveDisk at {c.x},{c.y} width={penWidth}");
        for (int dy = min; dy <= max; dy++)
            for (int dx = min; dx <= max; dx++)
            {
                int x = c.x + dx, y = c.y + dy;
                if (!In(x, y)) continue;
                // use square pen or trim corners of round one?
                if (cfg.useRoundPen && (dx * dx + dy * dy > (penWidth / 2f) * (penWidth / 2f))) continue; // disk; swap to diamond if you prefer

                // create a new cell and add it to the room that was passed in
                var tmp_cell = new Cell(x, y);
                tmp_cell.colorFloor = tmp_room.colorFloor;
                tmp_cell.room_number = tmp_room.my_room_number;
                tmp_cell.isCorridor = true;
                tmp_room.cells.Add(tmp_cell); // ??don't add the room here, or you will get hundreds of duplicate rooms.
                // also add it to corridors list
                corridors.Add((x, y));
                // also add it to the cellGrid
                cellGrid[x, y] = tmp_cell;
            }
    }

    Vector2Int MaybeTurn(Vector2Int d, Random r, float wander)
    {
        // with some prob, keep going; else turn 90° left/right
        if (r.NextDouble() < (wander / 1000f)) return d;
        return (r.Next(2) == 0) ? TurnLeft(d, true) : TurnLeft(d, false);
    }

    int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    // RasterizeLineSafe() looks to be a Bresenham line algorithm for any point to point lines
    IEnumerable<Vector2Int> RasterizeLineSafe(Vector2Int a, Vector2Int b)
    {
        int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        // Hard cap: the line cannot be longer than dx+|dy|+1 steps
        int maxSteps = dx + (-dy) + 1;

        for (int steps = 0; steps < maxSteps; steps++)
        {
            yield return new Vector2Int(x0, y0);
            if (x0 == x1 && y0 == y1) yield break;

            int e2 = err << 1; // 2*err
            bool stepped = false;

            if (e2 >= dy) { err += dy; x0 += sx; stepped = true; }
            if (e2 <= dx) { err += dx; y0 += sy; stepped = true; }

            // Safety: if neither branch moved (shouldn’t happen), force a move toward target
            if (!stepped)
            {
                if (x0 != x1) x0 += sx;
                else if (y0 != y1) y0 += sy;
            }
        }
        // If we ever fall out by hitting maxSteps, just stop
        yield break;
    }

    // used only for debug
    void ClearMapBorders(List<Room> rooms)  // in case the routines here spill out into border like they aren't supposed to, clear them out.
    {                                       // NOTE: Problem seems to be in wall add routine at last x and last y.
        int r_num, c_num;
        int removed = 0;
        for (r_num = rooms.Count - 1; r_num >= 0; r_num--)
        {
            for (c_num = rooms[r_num].cells.Count - 1; c_num >= 0; c_num--) // must count cells backwards because we are deleting as we go.
            {
                if (!In(rooms[r_num].cells[c_num].x, rooms[r_num].cells[c_num].y))
                    rooms[r_num].cells.RemoveAt(c_num);
                removed++;
            }
        }
        Debug.Log($"ClearMapBorders cleared {removed} cells.");  // BUG: WOW big numbers even when the preceeding function did nothing.  What's wrong?
    }


    bool CheckRoomsToGridConsistancy()
    {
        int mismatches = 0;
        int W = cellGrid.GetLength(0);
        int H = cellGrid.GetLength(1);

        for (int ri = 0; ri < rooms.Count; ri++)
        {
            var room = rooms[ri];
            if (room.my_room_number != ri)
            {
                Debug.LogWarning($"Room index mismatch: rooms[{ri}].my_room_number == {room.my_room_number}");
                mismatches++;
            }

            foreach (var cell in room.cells)
            {
                int x = cell.pos.x, y = cell.pos.y;

                // Bounds / null checks
                if ((uint)x >= (uint)W || (uint)y >= (uint)H)
                {
                    Debug.LogWarning($"Room→Grid: cell {x},{y} out of bounds for grid {W}x{H} (room {ri}).");
                    mismatches++;
                    continue;
                }
                var gridCell = cellGrid[x, y];
                if (gridCell == null)
                {
                    Debug.LogWarning($"Room→Grid: grid cell null at {x},{y} (room {ri}).");
                    mismatches++;
                    continue;
                }

                // Deep compare; count each differing field
                mismatches += ReportCellDifferences(cell, gridCell, ri);
            }
        }

        if (mismatches > 0)
            Debug.LogWarning($"CheckRoomsToGridConsistancy: found {mismatches} mismatches.");

        return mismatches == 0;
    }

    // Compares all properties with tolerance where appropriate.
    // Returns the number of field-level differences found (0 = perfect match).
    int ReportCellDifferences(Cell a, Cell b, int expectedRoom)
    {
        int diffs = 0;
        const float EPS = 1e-4f;

        // Identity / indexing
        if (a.room_number != expectedRoom)
        {
            Debug.Log($"Cell {a.pos.x},{a.pos.y}: room_number {a.room_number} != expected {expectedRoom}");
            diffs++;
        }
        if (b.room_number != expectedRoom)
        {
            Debug.Log($"Grid {b.pos.x},{b.pos.y}: room_number {b.room_number} != expected {expectedRoom}");
            diffs++;
        }

        // Position & height
        if (a.pos != b.pos)
        {
            Debug.Log($"Cell {a.pos} vs Grid {b.pos}: pos mismatch");
            diffs++;
        }
        if (a.height != b.height)
        {
            Debug.Log($"Cell {a.pos}: height {a.height} vs Grid {b.height}");
            diffs++;
        }

        // Type
        if (a.type != b.type)
        {
            Debug.Log($"Cell {a.pos}: type {a.type} vs Grid {b.type}");
            diffs++;
        }

        // Walls/Doors
        if (a.walls != b.walls)
        {
            Debug.Log($"Cell {a.pos}: walls {a.walls} vs Grid {b.walls}");
            diffs++;
        }
        if (a.doors != b.doors)
        {
            Debug.Log($"Cell {a.pos}: doors {a.doors} vs Grid {b.doors}");
            diffs++;
        }
        // Door sides matching.
        foreach (DirFlags dir in DirFlagsEx.AllCardinals)
        {
            DirFlags opp_dir = DirFlagsEx.Opposite(dir);
            Vector2Int dirVec = DirFlagsEx.ToVector2Int(dir);
            if (dirVec == Vector2Int.zero) continue; // skip None
            if (!In(a.pos.x + dirVec.x, a.pos.y + dirVec.y)) continue; // skip off-map  

            if (a.doors.HasFlag(dir) != cellGrid[a.pos.x + dirVec.x, a.pos.y + dirVec.y].doors.HasFlag(opp_dir))
            {
                Debug.LogError($"Grid {a.pos}: door {dir} has no match in Grid {a.pos.x + dirVec.x},{a.pos.y + dirVec.y} door {opp_dir}");
                diffs++;
            }
            if (a.walls.HasFlag(dir) != cellGrid[a.pos.x + dirVec.x, a.pos.y + dirVec.y].walls.HasFlag(opp_dir))
            {
                Debug.LogError($"Grid {a.pos}: wall {dir} has no match in Grid {a.pos.x + dirVec.x},{a.pos.y + dirVec.y}: wall {opp_dir}");
                diffs++;
            }
        }
        // Color (tolerant)
        if (!ColorApprox(a.colorFloor, b.colorFloor))
        {
            Debug.Log($"Cell {a.pos}: colorFloor {a.colorFloor} vs Grid {b.colorFloor}");
            diffs++;
        }

        // Tilt (tolerant by angle)
        if (!QuatApprox(a.tiltFloor, b.tiltFloor, 0.1f)) // ~0.1° tolerance
        {
            Debug.Log($"Cell {a.pos}: tiltFloor {a.tiltFloor.eulerAngles} vs Grid {b.tiltFloor.eulerAngles}");
            diffs++;
        }

        // Travel cost
        if (Mathf.Abs(a.travel_cost - b.travel_cost) > EPS)
        {
            Debug.Log($"Cell {a.pos}: travel_cost {a.travel_cost} vs Grid {b.travel_cost}");
            diffs++;
        }

        // Corridor flag
        if (a.isCorridor != b.isCorridor)
        {
            Debug.Log($"Cell {a.pos}: isCorridor {a.isCorridor} vs Grid {b.isCorridor}");
            diffs++;
        }

        // Optional: warn if the two references are different (not required to be identical)
        if (!ReferenceEquals(a, b))
        {
            // Uncomment if you want to track reference sharing issues:
            // Debug.Log($"Cell {a.pos}: instance differs from cellGrid reference (values compared above).");
            // (No diff increment; value equality is what matters.)
        }

        return diffs;
    }

    bool ColorApprox(Color a, Color b, float eps = 1e-3f)
    {
        return Mathf.Abs(a.r - b.r) <= eps &&
            Mathf.Abs(a.g - b.g) <= eps &&
            Mathf.Abs(a.b - b.b) <= eps &&
            Mathf.Abs(a.a - b.a) <= eps;
    }

    bool QuatApprox(Quaternion a, Quaternion b, float maxAngleDeg)
    {
        // Handles double-cover and tiny numerical differences.
        return Quaternion.Angle(a, b) <= maxAngleDeg;
    }
}
