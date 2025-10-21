using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour
{

    // === MAIN ENTRY ===
    public IEnumerator PlaceDoors()
    {
        int doorsYieldEvery = 300;
        int W = cfg.mapWidth, H = cfg.mapHeight;
        int moat = cfg.useThinWalls ? 0 : Mathf.Max(0, cfg.wallThickness);

        // 1) Collect candidate door sites (room-edge touching room/corridor within ≤ moat cells in a straight line)
        var candidates = CollectDoorCandidates(W, H, moat, cfg.doors.minDoorSpacing, doorsYieldEvery);

        // 2) ConnectLooseEnds first (fix ugly dead-end corridors)
        yield return StartCoroutine(ConnectLooseEnds(candidates, cfg.doors.deadEndReach, moat, doorsYieldEvery));

        // 3) EnsureConnectivity with minimal doors (Kruskal-like: pick cheapest that bridges components)
        yield return StartCoroutine(EnsureConnectivity(candidates, moat, cfg.doors.maxDoorsPerRoom, doorsYieldEvery));

        // 4) Add extra loop doors for interest
        //int extraTarget = 0; //DEBUG
        int extraTarget = Mathf.RoundToInt(candidates.Count * cfg.doors.loopiness * 0.25f);
        yield return StartCoroutine(AddLoopDoors(candidates, extraTarget, moat, cfg.doors.minDoorSpacing, cfg.doors.maxDoorsPerRoom, doorsYieldEvery));

        DrawMapByRooms(rooms);
        //yield return new WaitForSeconds(1f);
        UpdateDoorsInRooms(candidates);
        DrawMapByRooms(rooms);
        //yield return new WaitForSeconds(1f);

        PrintCandidates(candidates);
    }

    // ============================= CANDIDATES =============================
    class DoorCandidate
    {
        public int x, y;            // room cell location (the near side anchor)
        public DirFlags dir;        // direction the door faces from (x,y)
        public int span;            // number of empty cells to punch through (0..moat)
        public bool toCorridor;     // true if target is corridor
        public int targetRoomId;    // if !toCorridor, room id on the far side
        public int roomId;          // source room id
        public int score;           // lower is better for connectivity
        public bool placed;         // shows if this was successfully placed
        public Cell cellA;
        public Cell cellB;
    }

    List<DoorCandidate> CollectDoorCandidates(int W, int H, int moat, int minSpacing, int yieldEvery)
    {
        var list = new List<DoorCandidate>(1024);
        int touched = 0;

        // quick per-room per-edge spacing (avoid doors too close on the same edge)
        var lastOnEdge = new Dictionary<(int roomId, int edgeKey), int>(); // edgeKey = pack (dir + projected index)

        foreach (var room in rooms)
        {
            int ri = room.my_room_number;
            foreach (var c in room.cells)
            {
                // Only room cells (ignore corridor tiles even if room-dedicated)
                if (c.isCorridor) continue;

                foreach (var d in AllDirs())
                {
                    // find straight line to first non-empty cell within ≤ moat
                    var dv = DirVec(d);
                    int nx = c.x + dv.x, ny = c.y + dv.y;
                    int span = 0;
                    //bool blocked = false;
                    while (span <= moat)
                    {
                        if (!In(nx, ny, W, H)) { /*blocked = true;*/ break; }
                        var n = cellGrid[nx, ny];

                        if (n == null) { /*blocked = true*/; break; } // sanity
                        if (!n.isCorridor && n.room_number < 0)
                        {
                            // empty; keep drilling
                            span++;
                            nx += dv.x; ny += dv.y;
                            continue;
                        }

                        // hit something: corridor or another room
                        if (n.isCorridor)
                        {
                            // enforce edge spacing on source room edge
                            int edgeKey = EdgeKey(ri, d, c.x, c.y);
                            if (TooClose(lastOnEdge, edgeKey, c, minSpacing)) break;

                            list.Add(new DoorCandidate
                            {
                                x = c.x,
                                y = c.y,
                                dir = d,
                                span = span,
                                toCorridor = true,
                                targetRoomId = -1,
                                roomId = ri,
                                score = span,
                                cellA = c,
                                cellB = n
                            }); // prefer short punches
                            // remember last door location along this edge
                            lastOnEdge[(ri, edgeKey)] = EdgeMeasure(d, c.x, c.y);
                        }
                        else if (n.room_number != ri) // different room
                        {
                            int edgeKey = EdgeKey(ri, d, c.x, c.y);
                            if (TooClose(lastOnEdge, edgeKey, c, minSpacing)) break;

                            list.Add(new DoorCandidate
                            {
                                x = c.x,
                                y = c.y,
                                dir = d,
                                span = span,
                                toCorridor = false,
                                targetRoomId = n.room_number,
                                roomId = ri,
                                score = span,
                                placed = false,
                                cellA = c,
                                cellB = n
                            });

                            lastOnEdge[(ri, edgeKey)] = EdgeMeasure(d, c.x, c.y);
                        }
                        // if same room, ignore (internal edge)
                        break;
                    }
                    //if (blocked) Debug.Log("Blocked");  // just added to get rid of warning that the variable was assigned but never used.
                    if ((++touched % yieldEvery) == 0) { /* cooperative yield point */ }
                }
            }
        }
        // sort by rising score (shortest punches first)
        list.Sort((a,b) => a.score.CompareTo(b.score));
        return list;
    }

    // ============================= CONNECT LOOSE ENDS =============================
    IEnumerator ConnectLooseEnds(List<DoorCandidate> candidates, int reach, int moat, int yieldEvery)
    {
        int W = cellGrid.GetLength(0), H = cellGrid.GetLength(1);
        int touched = 0;

        foreach (var tip in CorridorDeadEnds(W, H))
        {
            Cell c = cellGrid[tip.x, tip.y];
            // look outward in 4 dirs up to 'reach' cells for a room boundary we can punch to
            foreach (var d in AllDirs())
            {
                var dv = DirVec(d);
                int nx = tip.x + dv.x, ny = tip.y + dv.y;
                int span = 0;
                while (span < reach)
                {
                    if (!In(nx, ny, W, H)) break;
                    var n = cellGrid[nx, ny];
                    if (!n.isCorridor && n.room_number >= 0)
                    {
                        // Create door candidate into that room (door faces from room back toward corridor)
                        var dcand = new DoorCandidate
                        {
                            x = nx,
                            y = ny,
                            dir = Opp(d),
                            span = Mathf.Min(span, moat),
                            toCorridor = true,
                            targetRoomId = -1,
                            roomId = n.room_number,
                            score = 0,
                            cellA = c,
                            cellB = n
                        };
                        bool placed = TryPlaceDoor(dcand, moat);
                        //Debug.Log($"ConnectLooseEnds: Tried to place door at {dcand.x},{dcand.y} placed = {placed}");

                        break;
                    }
                    if (!n.isCorridor && n.room_number < 0)
                    {
                        span++; nx += dv.x; ny += dv.y; continue; // wall space
                    }
                    // hit another corridor – that’s fine, no need for a door
                    break;
                }

                if ((++touched % yieldEvery) == 0) yield return null;
            }
        }
    }

    // ============================= ENSURE CONNECTIVITY =============================
    IEnumerator EnsureConnectivity(List<DoorCandidate> candidates, int moat, int maxDoorsPerRoom, int yieldEvery)
    {
        int nRooms = rooms.Count;
        var uf = new UnionFind(nRooms);

        // Existing door edges? If you already mark doors, union rooms connected by door or room↔corridor.
        // (Optional: union corridor components to a single virtual node for stronger guarantees.)

        var doorsUsedPerRoom = new int[nRooms];
        int chosen = 0;

        foreach (var c in candidates)
        {
            if (c.toCorridor)
            {
                // corridor acts like a hub; allow up to maxDoorsPerRoom into corridor
                if (doorsUsedPerRoom[c.roomId] >= maxDoorsPerRoom) continue;
                bool placed = TryPlaceDoor(c, moat);
                c.placed = placed;
                //Debug.Log($"EnsureConnectivity1: Tried to place door at {c.x},{c.y} placed = {placed}");
                
                if (placed) { doorsUsedPerRoom[c.roomId]++; chosen++; }
            }
            else
            {
                // room↔room: only if they are in different components
                if (uf.Connected(c.roomId, c.targetRoomId)) continue;
                if (doorsUsedPerRoom[c.roomId] >= maxDoorsPerRoom) continue;
                if (doorsUsedPerRoom[c.targetRoomId] >= maxDoorsPerRoom) continue;

                bool placed = TryPlaceDoor(c, moat);
                c.placed = placed;
                //Debug.Log($"EnsureConnectivity2: Tried to place door at {c.x},{c.y} placed = {placed}");
                if (placed)
                {
                    uf.Union(c.roomId, c.targetRoomId);
                    doorsUsedPerRoom[c.roomId]++;
                    doorsUsedPerRoom[c.targetRoomId]++;
                    chosen++;
                    // Early exit when all rooms connected
                    if (uf.Components == 1) break;
                }
            }
            if (chosen % yieldEvery == 0) yield return null;
        }
        yield break;
    }

    // ============================= EXTRA LOOPS =============================
    IEnumerator AddLoopDoors(List<DoorCandidate> candidates, int extraTarget, int moat, int minSpacing, int maxDoorsPerRoom, int yieldEvery)
    {
        if (extraTarget <= 0) yield break;

        // simple randomized pass over remaining candidates
        Shuffle(candidates);

        int added = 0;
        var perRoom = new int[rooms.Count];

        foreach (var c in candidates)
        {
            if (added >= extraTarget) break;

            if (!c.toCorridor && (perRoom[c.roomId] >= maxDoorsPerRoom || perRoom[c.targetRoomId] >= maxDoorsPerRoom))
                continue;
            if (c.toCorridor && perRoom[c.roomId] >= maxDoorsPerRoom)
                continue;
            bool placed;
            if (placed=TryPlaceDoor(c, moat))
            {
                //Debug.Log($"AdddLoopDoors: Tried to place door at {c.x},{c.y} placed = {placed}");
                if (c.toCorridor) perRoom[c.roomId]++; else { perRoom[c.roomId]++; perRoom[c.targetRoomId]++; }
                added++;
            }
            c.placed = placed;
            if (added % yieldEvery == 0) yield return null;
        }
    }

    // ============================= DOOR PLACEMENT CORE =============================
    // Punches through 'span' empty cells (≤ moat) and sets door flags symmetrically.
    bool TryPlaceDoor(DoorCandidate d, int moat)
    {
        int W = cellGrid.GetLength(0), H = cellGrid.GetLength(1);
        var dv = DirVec(d.dir);

        // source must be a room cell and match roomId
        var a = cellGrid[d.x, d.y];
        if (a == null || a.room_number != d.roomId || a.isCorridor) return false;

        // 1) Carve straight tunnel of length 'span' (turn empty cells into corridor)
        int cx = d.x + dv.x, cy = d.y + dv.y;
        for (int i = 0; i < d.span; i++)
        {
            if (!In(cx, cy, W, H)) return false;
            var w = cellGrid[cx, cy];
            if (w.isCorridor) { /* already open */ }
            else if (w.room_number >= 0) return false; // ran into a room too soon
            else
            {
                // turn wall space into corridor
                w.isCorridor = true;
                w.room_number = -1;
                corridors.Add((cx, cy));
                // clear walls/doors on corridor cell edge opposite the tunnel direction
                w.walls &= ~Opp(d.dir);
            }
            cx += dv.x; cy += dv.y;
        }

        // 2) Determine far-side cell (target)
        if (!In(cx, cy, W, H)) return false;
        var b = cellGrid[cx, cy];
        if (d.toCorridor)
        {
            if (!b.isCorridor) return false;
        }
        else
        {
            if (b.room_number != d.targetRoomId || b.isCorridor) return false;
        }

        // 3) Set door flags on the two boundary cells that touch the tunnel
        // Near side door on 'a' in direction d.dir
        a.doors |= d.dir;
        //a.walls &= ~d.dir; // door replaces wall edge

        // The cell on the tunnel’s first corridor tile (if span>0) or the far-side cell if span==0
        int bx = d.span > 0 ? (d.x + dv.x) : cx;
        int by = d.span > 0 ? (d.y + dv.y) : cy;
        var near = cellGrid[bx, by];
        DirFlags opp = Opp(d.dir);

        // Corridor cells can also carry door info for rendering; if you prefer, attach only to room sides.
        near.doors |= opp;
        //near.walls &= ~opp;

        // If connecting room↔room with span==0 (thin walls), also set door on far room edge
        if (!d.toCorridor && d.span == 0)
        {
            b.doors |= Opp(d.dir);
            //b.walls &= ~Opp(d.dir);
        }

        return true;
    }

    // ============================= SUPPORT: graph, neighbors, utils =============================
    struct Int2 { public int x,y; public Int2(int x,int y){this.x=x;this.y=y;} }

    IEnumerable<Int2> CorridorDeadEnds(int W, int H)
    {
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            var c = cellGrid[x, y];
            if (!c.isCorridor) continue;
            int deg = 0;
            foreach (var nb in FourNeighbors(x, y, W, H))
                if (cellGrid[nb.x, nb.y].isCorridor) deg++;
            if (deg == 1) yield return new Int2(x,y);
        }
    }

    IEnumerable<Int2> FourNeighbors(int x,int y,int W,int H)
    {
        if (x > 0)       yield return new Int2(x-1,y);
        if (x < W-1)     yield return new Int2(x+1,y);
        if (y > 0)       yield return new Int2(x,y-1);
        if (y < H-1)     yield return new Int2(x,y+1);
    }

    bool In(int x,int y,int W,int H) => (uint)x < (uint)W && (uint)y < (uint)H;

    IEnumerable<DirFlags> AllDirs()
    {
        yield return DirFlags.N; yield return DirFlags.E; yield return DirFlags.S; yield return DirFlags.W;
    }
    (int x,int y) DirVec(DirFlags d)
    {
        switch (d)
        {
            case DirFlags.N: return (0, 1);
            case DirFlags.E: return (1, 0);
            case DirFlags.S: return (0,-1);
            default:         return (-1,0);
        }
    }
    DirFlags Opp(DirFlags d)
    {
        switch (d)
        {
            case DirFlags.N: return DirFlags.S;
            case DirFlags.E: return DirFlags.W;
            case DirFlags.S: return DirFlags.N;
            default:         return DirFlags.E;
        }
    }

    // “Edge key”/measure used only to keep doors spaced along an edge; merge with your existing spacing util if you have one.
    int EdgeKey(int roomId, DirFlags d, int x, int y) => ((int)d << 20) ^ roomId;
    int EdgeMeasure(DirFlags d, int x, int y) => (d == DirFlags.N || d == DirFlags.S) ? x : y;
    bool TooClose(Dictionary<(int,int),int> last, int edgeKey, Cell c, int minSpacing)
    {
        var k = (c.room_number, edgeKey);
        if (last.TryGetValue(k, out int lastPos))
        {
            int cur = EdgeMeasure((DirFlags)(edgeKey>>20), c.x, c.y);
            if (Mathf.Abs(cur - lastPos) < minSpacing) return true;
        }
        return false;
    }

    void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    void PrintCandidates(List<DoorCandidate> candidates)
    {
        int num = 0;
        int complete = 0;
        foreach (DoorCandidate c in candidates)
        {
            //Debug.Log($"Candidate {num}: @{c.x},{c.y} {c.dir} placed={c.placed} A={c.roomId} -> B={c.targetRoomId}");
            //Debug.Log($"room {c.roomId} : doors = {rooms[c.roomId].doors.Count}");
            //Debug.Log($"{c.cellA.x},{c.cellA.y} doors={c.cellA.doors} -> {c.cellB.x},{c.cellB.y} doors={c.cellB.doors}");
            num++;
            if (c.placed) complete++;
        }
        //Debug.Log($"Door Candidates = {num}, Doors Complete = {complete}");
    }

    void UpdateDoorsInRooms(List<DoorCandidate> candidates)
    {
        int num = 0;
        int complete = 0;
        int num_changes = 0;
        foreach (DoorCandidate c in candidates)
        {
            num++;
            //Debug.Log($"Candidate {num}: @{c.x},{c.y} {c.dir} placed={c.placed} A={c.roomId} -> B={c.targetRoomId}");
            //Debug.Log($"room {c.roomId} : doors = {rooms[c.roomId].doors.Count}");
            if (!c.placed)
            {
                DirFlags before_doors = c.cellA.doors;
                DirFlags before_walls = c.cellA.walls;
                c.cellA.doors &= ~c.dir;    // clear the door bit if not placed.
                //c.cellA.walls |= c.dir;     // set the wall bit if not placed.
                if ((before_doors != c.cellA.doors) || (before_walls != c.cellA.walls))
                {
                    Debug.Log($"before_doors = {before_doors}, after_doors = {c.cellA.doors}");
                    Debug.Log($"before_walls = {before_walls}, after_walls = {c.cellA.walls}");
                    num_changes++;
                }
                DirFlags before_doorsB = c.cellB.doors;
                DirFlags before_wallsB = c.cellB.walls;
                c.cellB.doors &= ~Opp(c.dir);    // clear the door bit if not placed.
                //c.cellB.walls |= Opp(c.dir);     // set the wall bit if not placed.
                if ((before_doorsB != c.cellB.doors) || (before_wallsB != c.cellB.walls))
                {
                    Debug.Log($"before_doorsB = {before_doorsB}, after_doorsB = {c.cellB.doors}");
                    Debug.Log($"before_wallsB = {before_wallsB}, after_wallsB = {c.cellB.walls}");
                    num_changes++;
                }
                //Debug.Log($"{c.cellA.x},{c.cellA.y} doors={c.cellA.doors} -> {c.cellB.x},{c.cellB.y} doors={c.cellB.doors}");

            }
            else complete++;
        }
        //if (num_changes != 0) Debug.Log($"num_changes = {num_changes}");
        Debug.Log($"Door Candidates = {num}, Doors Complete = {complete}, num_changes = {num_changes}");
    }

    class UnionFind
    {
        int[] p, r; int comps;
        public UnionFind(int n) { p = new int[n]; r = new int[n]; comps = n; for (int i = 0; i < n; i++) p[i] = i; }
        int Find(int x) { return p[x] == x ? x : (p[x] = Find(p[x])); }
        public bool Connected(int a, int b) => Find(a) == Find(b);
        public void Union(int a, int b) { a = Find(a); b = Find(b); if (a == b) return; if (r[a] < r[b]) p[a] = b; else if (r[a] > r[b]) p[b] = a; else { p[b] = a; r[a]++; } comps--; }
        public int Components => comps;
    }
}