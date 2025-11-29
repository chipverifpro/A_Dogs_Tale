using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;


public enum Dir8 { N=0, NE=1, E=2, SE=3, S=4, SW=5, W=6, NW=7 }

public class Pathfinding : MonoBehaviour
{
    public Directory dir;

    // Map Dir8 -> integer offsets
    static readonly Vector2Int[] Offsets = new Vector2Int[8]
    {
        new Vector2Int( 0,  1), // N
        new Vector2Int( 1,  1), // NE
        new Vector2Int( 1,  0), // E
        new Vector2Int( 1, -1), // SE
        new Vector2Int( 0, -1), // S
        new Vector2Int(-1, -1), // SW
        new Vector2Int(-1,  0), // W
        new Vector2Int(-1,  1), // NW
    };

    // Directional step multipliers: orthogonal=1, diagonal=√2
    static readonly float[] StepLen = new float[8]
    {
        1f, 1.41421356f, 1f, 1.41421356f,
        1f, 1.41421356f, 1f, 1.41421356f
    };

    // Inject your existing function here
    public delegate float CanMoveFn(Vector2 start, Dir8 dir);

    /// <summary>
    /// A* path from start to goal across 8 directions.
    /// Uses CanMoveInDirection(start, dir): returns >0 cost if open, 0 if blocked.
    /// Returns a list of tile coords including start and goal. Empty if no path.
    /// </summary>
    public List<Vector2Int> FindPath(
        Vector2Int start,
        Vector2Int goal,
        int maxNodesExpanded = 10000)
    {
        var result = new List<Vector2Int>();
        if (start == goal) { result.Add(start); return result; }

        // Open set (min-heap by fScore)
        var open = new MinHeap();
        open.Push(start, Heuristic(start, goal));

        // For reconstructing path
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>(256);

        // gScore: cost from start to node
        var gScore = new Dictionary<Vector2Int, float>(256)
        {
            [start] = 0f
        };

        // fScore: g + h
        var fScore = new Dictionary<Vector2Int, float>(256)
        {
            [start] = Heuristic(start, goal)
        };

        var closed = new HashSet<Vector2Int>();

        int expanded = 0;
        while (open.Count > 0)
        {
            var current = open.Pop();
            if (current == goal)
            {
                Reconstruct(cameFrom, current, result);
                return result;
            }

            if (expanded++ > maxNodesExpanded)
            {
                Debug.Log("Max nodes expanded");
                break; // safety bail-out
            }

            closed.Add(current);

            // Explore 8 neighbors
            for (int d = 0; d < 8; d++)
            {
                var dir = (Dir8)d;
                // Your function: returns >0 cost if the edge is traversable, else 0
                float edgeCost = DirectionMoveCost(new Vector2Int(current.x, current.y), Offsets[d], d);
                if (edgeCost <= 0f) continue; // blocked

                var neighbor = current + Offsets[d];

                if (closed.Contains(neighbor))
                    continue;

                // Combine your edge cost with geometric step length
                float tentative = gScore[current] + edgeCost * StepLen[d];

                if (!gScore.TryGetValue(neighbor, out float oldG) || tentative < oldG)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentative;

                    float f = tentative + Heuristic(neighbor, goal);
                    fScore[neighbor] = f;

                    open.PushOrDecrease(neighbor, f);
                }
            }
        }

        // No path found → empty list
        return result;
    }

    // Octile heuristic (admissible for 8-way grids)
    static float Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        int min = Math.Min(dx, dy);
        int max = Math.Max(dx, dy);
        // diagonal * min + straight * (max - min)
        return 1.41421356f * min + 1f * (max - min);
    }

    static void Reconstruct(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current, List<Vector2Int> outPath)
    {
        outPath.Clear();
        outPath.Add(current);
        while (cameFrom.TryGetValue(current, out var prev))
        {
            current = prev;
            outPath.Add(current);
        }
        outPath.Reverse();
    }

    // --- Tiny binary min-heap for (node, priority) ---

    class MinHeap
    {
        // Heap arrays
        readonly List<Vector2Int> nodes = new List<Vector2Int>(256);
        readonly List<float> prios = new List<float>(256);
        // Map node -> index in heap for DecreaseKey behavior
        readonly Dictionary<Vector2Int, int> indexOf = new Dictionary<Vector2Int, int>(256);

        public int Count => nodes.Count;

        public void Push(Vector2Int node, float priority)
        {
            indexOf[node] = nodes.Count;
            nodes.Add(node);
            prios.Add(priority);
            SiftUp(nodes.Count - 1);
        }

        public void PushOrDecrease(Vector2Int node, float priority)
        {
            if (indexOf.TryGetValue(node, out int idx))
            {
                if (priority < prios[idx])
                {
                    prios[idx] = priority;
                    SiftUp(idx);
                }
            }
            else
            {
                Push(node, priority);
            }
        }

        public Vector2Int Pop()
        {
            int last = nodes.Count - 1;
            var topNode = nodes[0];

            Swap(0, last);
            nodes.RemoveAt(last);
            prios.RemoveAt(last);
            indexOf.Remove(topNode);

            if (nodes.Count > 0) SiftDown(0);
            return topNode;
        }

        void SiftUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (prios[i] >= prios[p]) break;
                Swap(i, p);
                i = p;
            }
        }

        void SiftDown(int i)
        {
            int n = nodes.Count;
            while (true)
            {
                int l = (i << 1) + 1;
                int r = l + 1;
                int smallest = i;
                if (l < n && prios[l] < prios[smallest]) smallest = l;
                if (r < n && prios[r] < prios[smallest]) smallest = r;
                if (smallest == i) break;
                Swap(i, smallest);
                i = smallest;
            }
        }

        void Swap(int a, int b)
        {
            (nodes[a], nodes[b]) = (nodes[b], nodes[a]);
            (prios[a], prios[b]) = (prios[b], prios[a]);
            indexOf[nodes[a]] = a;
            indexOf[nodes[b]] = b;
        }
    }

    float DirectionMoveCost(Vector2Int start, Vector2Int direction, int dir_num)
    {
        Cell startCell;
        Cell destCell;

        // if either endpoint is out-of-bounds, return blocked.
        if (!dir.gen.In(start.x, start.y)) return 0f;
        if (!dir.gen.In(start.x + direction.x, start.y + direction.y)) return 0f;

        startCell = dir.gen.cellGrid[start.x, start.y];
        destCell = dir.gen.cellGrid[start.x + direction.x, start.y + direction.y];

        if (startCell == null || destCell == null) return 0f; // if either endpoint is not a valid Cell

        switch (dir_num)
        {
            case 0:  // N
                if (startCell.walls.HasFlag(DirFlags.N))
                    if (!startCell.doors.HasFlag(DirFlags.N)) // assume door is unlocked for now
                        return 0f;
                return 1f;
            case 1:  // NE
                if (startCell.walls.HasFlag(DirFlags.N) || startCell.walls.HasFlag(DirFlags.E))
                    return 0f;  // one or the other direction had a wall, don't go diagonal
                return 1f;
            case 2:  // E
                if (startCell.walls.HasFlag(DirFlags.E))
                    if (!startCell.doors.HasFlag(DirFlags.E)) // assume door is unlocked for now
                        return 0f;
                return 1f;
            case 3:  // SE
                if (startCell.walls.HasFlag(DirFlags.E) || startCell.walls.HasFlag(DirFlags.S))
                    return 0f;  // one or the other direction had a wall, don't go diagonal
                return 1f;
            case 4:  // S
                if (startCell.walls.HasFlag(DirFlags.S))
                    if (!startCell.doors.HasFlag(DirFlags.S)) // assume door is unlocked for now
                        return 0f;
                return 1f;
            case 5:  // SW
                if (startCell.walls.HasFlag(DirFlags.S) || startCell.walls.HasFlag(DirFlags.W))
                    return 0f;  // one or the other direction had a wall, don't go diagonal
                return 1f;
            case 6:  // W
                if (startCell.walls.HasFlag(DirFlags.W))
                    if (!startCell.doors.HasFlag(DirFlags.W)) // assume door is unlocked for now
                        return 0f;
                return 1f;
            case 7:  // NW
                if (startCell.walls.HasFlag(DirFlags.N) || startCell.walls.HasFlag(DirFlags.W))
                    return 0f;  // one or the other direction had a wall, don't go diagonal
                return 1f;
            default:    // didn't understand the direction vector
                return 0f;
        }
    }

    public void TrySkippingWaypoints(Agent agent)
    {
        Vector3 startPos3;
        Vector3 targetPos3;
        bool los;
        Room room;
        Cell startCell;

        startPos3.x = agent.pos2.x;
        startPos3.y = 0;
        startPos3.z = agent.pos2.y;

        startCell = dir.gen.cellGrid[Mathf.FloorToInt(agent.pos2.x), Mathf.FloorToInt(agent.pos2.y)];
        room = dir.gen.rooms[startCell.room_number];

        while (agent.routeWaypoints.Count > 1)
        {
            targetPos3.x = agent.routeWaypoints[0].x;
            targetPos3.y = 0;
            targetPos3.z = agent.routeWaypoints[0].y;
            los = HasLineOfSightInRoom(room, startPos3, targetPos3, agent.radius);
            if (los)
            {
                // can skip this waypoint
                agent.routeWaypoints.RemoveAt(0);
            }
            else
            {
                break; // can't skip any more waypoints
            }
        }
    }

    // GPT produced two versions of the Line-of-Sight algorithm.
    // One works on tile coordinates within a Room (RoomLOS).


    /// // Suppose you have a RoomCells that implements IRoomCells
    /// 
    /// How to use:
    /// 
    /// IRoomCells room = currentRoom;
    ///
    /// bool los = LOS2D.HasLineOfSightInRoom(
    ///    room,
    ///    startWorldPosition,
    ///    endWorldPosition,
    ///    agentRadius: 0.4f // optional: expands walls by ~one cell if cellSize≈0.5
    /// );


    /// Line of sight inside a single room grid on the XZ plane.
    /// - Ignores height (Y).
    /// - Uses supercover Bresenham across cells to test blockage.
    /// - agentRadius expands blockers by ceil(radius / cellSize) cells.
    public bool HasLineOfSightInRoom(Room room, Vector3 aWorld, Vector3 bWorld, float agentRadius = 0f)
    {
        Vector2Int aPos2 = new();
        Vector2Int bPos2 = new();
        aPos2.x = Mathf.FloorToInt(aWorld.x);
        aPos2.y = Mathf.FloorToInt(aWorld.z);
        bPos2.x = Mathf.FloorToInt(bWorld.x);
        bPos2.y = Mathf.FloorToInt(bWorld.z);
        // Ensure both endpoints are in this room (caller said "only consider within a single Room").
        if (room == null) return false;
        if (!dir.gen.In(aPos2.x, aPos2.y) || !dir.gen.In(bPos2.x, bPos2.y)) return false;

        // Convert world → cell coordinates (XZ only)
        float s = 1.0f;
        Vector2 a2 = new Vector2(aWorld.x, aWorld.z);
        Vector2 b2 = new Vector2(bWorld.x, bWorld.z);

        int ax = WorldToCell(a2.x, 0, s);
        int az = WorldToCell(a2.y, 0, s);
        int bx = WorldToCell(b2.x, 0, s);
        int bz = WorldToCell(b2.y, 0, s);

        // Optional: early exit if start or end in a blocked cell (common policy)
        if (!dir.gen.In(ax, az) || !dir.gen.In(bx, bz)) return false;

        int inflate = Mathf.CeilToInt(agentRadius / Mathf.Max(0.0001f, s));

        // Walk all cells crossed by the segment (supercover → includes edge-adjacent cells)
        foreach (var c in SupercoverLine(ax, az, bx, bz))
        {
            if (!dir.gen.In(c.x, c.y)) return false; // treat out-of-bounds as blocked

            if (inflate <= 0)
            {
                if (IsBlocked(c.x, c.y, aPos2.x, aPos2.y)) return false;
            }
            else
            {
                // Check a small square neighborhood for clearance
                for (int dx = -inflate; dx <= inflate; dx++)
                    for (int dz = -inflate; dz <= inflate; dz++)
                    {
                        int nx = c.x + dx, nz = c.y + dz;
                        if (!dir.gen.In(nx, nz) || IsBlocked(nx, nz, aPos2.x, aPos2.y))
                            return false;
                    }
            }
        }

        return true;
    }

    // --- Helpers ---

    static int WorldToCell(float w, float origin, float cellSize)
        => Mathf.FloorToInt((w - origin) / cellSize);

    /// Supercover Bresenham in 2D (returns every grid cell the segment touches).
    static IEnumerable<Vector2Int> SupercoverLine(int x0, int y0, int x1, int y1)
    {
        int dx = x1 - x0, dy = y1 - y0;
        int sx = Math.Sign(dx), sy = Math.Sign(dy);
        dx = Math.Abs(dx); dy = Math.Abs(dy);

        int x = x0, y = y0;
        yield return new Vector2Int(x, y);

        if (dx >= dy)
        {
            int err = dx / 2;
            int yStepErr = dx; // threshold for supercover
            int e2;
            while (x != x1)
            {
                x += sx;
                err -= dy;
                e2 = err;
                yield return new Vector2Int(x, y);
                if (e2 < 0) // crossed a row boundary → include adjacent cell
                {
                    y += sy;
                    err += dx;
                    yield return new Vector2Int(x, y);
                }
            }
        }
        else
        {
            int err = dy / 2;
            int xStepErr = dy;
            int e2;
            while (y != y1)
            {
                y += sy;
                err -= dx;
                e2 = err;
                yield return new Vector2Int(x, y);
                if (e2 < 0) // crossed a column boundary → include adjacent cell
                {
                    x += sx;
                    err += dy;
                    yield return new Vector2Int(x, y);
                }
            }
        }
    }

    bool IsBlocked(int cx, int cy, int ax, int ay)
    {
        Cell c;

        if (!dir.gen.In(cx, cy)) return true;
        c = dir.gen.cellGrid[cx, cy];

        if (c == null) return true;
        if (cx < ax) // c is west of a
        {
            if (c.walls.HasFlag(DirFlags.E))
                if (!c.doors.HasFlag(DirFlags.E))
                    return true;
        }
        else if (cx > ax) // c is east of a
        {
            if (c.walls.HasFlag(DirFlags.W))
                if (!c.doors.HasFlag(DirFlags.W))
                    return true;
        }
        if (cy < ay) // c is south of a
        {
            if (c.walls.HasFlag(DirFlags.N))
                if (!c.doors.HasFlag(DirFlags.N))
                    return true;
        }
        else if (cy > ay) // c is north of a
        {
            if (c.walls.HasFlag(DirFlags.S))
                if (!c.doors.HasFlag(DirFlags.S))
                    return true;
        }
        return false;
    }
}

/// Usage:
/// // aWorld and bWorld in world XZ? Convert to tile coords first:
/// Vector2Int ToTile(Vector3 world, Vector3 origin, float cellSize) =>
///     new(Mathf.FloorToInt((world.x - origin.x)/cellSize),
///        Mathf.FloorToInt((world.z - origin.z)/cellSize));

// If you already track tile coords:
/// bool los = RoomLOS.HasLineOfSight(currentRoom, startTile, endTile);


public static class RoomLOS
{
    /// <summary>
    /// Returns true if there is line-of-sight between two tile positions within the same Room.
    /// Ignores height. Blocks when the segment crosses a walled edge (unless that edge has a door).
    /// </summary>
    public static bool HasLineOfSight(Room room, Vector2Int aTile, Vector2Int bTile)
    {
        if (room == null) return false;
        if (!room.IsTileInRoom(aTile) || !room.IsTileInRoom(bTile)) return false;

        // Degenerate case
        if (aTile == bTile) return true;

        foreach (var step in SupercoverLine(aTile, bTile))
        {
            // For each transition from prev -> curr, check the shared edge walls.
            if (!step.hasPrev) continue;

            var prev = step.prev;
            var curr = step.curr;

            // Step direction in tile space
            int dx = curr.x - prev.x;
            int dy = curr.y - prev.y;

            // Sanity: both tiles must exist in this room
            if (!room.IsTileInRoom(prev) || !room.IsTileInRoom(curr))
                return false; // treat out of room as blocked

            int iPrev = room.GetCellInRoom(prev);
            int iCurr = room.GetCellInRoom(curr);
            if (iPrev < 0 || iCurr < 0) return false;

            var cellPrev = room.cells[iPrev];
            var cellCurr = room.cells[iCurr];

            // Crossing a vertical edge? (dx != 0)
            if (dx > 0)
            {
                // Crossing from prev to the EAST: check prev.East / curr.West
                if (EdgeBlocked(cellPrev.walls, cellPrev.doors, DirFlags.E) ||
                    EdgeBlocked(cellCurr.walls, cellCurr.doors, DirFlags.W))
                    return false;
            }
            else if (dx < 0)
            {
                // Crossing to the WEST
                if (EdgeBlocked(cellPrev.walls, cellPrev.doors, DirFlags.W) ||
                    EdgeBlocked(cellCurr.walls, cellCurr.doors, DirFlags.E))
                    return false;
            }

            // Crossing a horizontal edge? (dy != 0)
            if (dy > 0)
            {
                // Crossing to the NORTH
                if (EdgeBlocked(cellPrev.walls, cellPrev.doors, DirFlags.N) ||
                    EdgeBlocked(cellCurr.walls, cellCurr.doors, DirFlags.S))
                    return false;
            }
            else if (dy < 0)
            {
                // Crossing to the SOUTH
                if (EdgeBlocked(cellPrev.walls, cellPrev.doors, DirFlags.S) ||
                    EdgeBlocked(cellCurr.walls, cellCurr.doors, DirFlags.N))
                    return false;
            }

            // Diagonal steps (dx != 0 && dy != 0) will trigger both checks above,
            // which correctly handles corner pinches. If you want stricter corner
            // blocking (no "corner cutting"), keep as-is. If you prefer permissive
            // behavior, only block if BOTH orthogonal edges are walls without doors.
        }

        return true;
    }

    /// <summary> True if a wall bit is present and there's NOT a door on that edge. </summary>
    private static bool EdgeBlocked(DirFlags walls, DirFlags doors, DirFlags dir)
    {
        bool hasWall  = (walls & dir) != 0;
        bool hasDoor  = (doors & dir) != 0;
        return hasWall && !hasDoor;
    }

    /// <summary>
    /// Supercover Bresenham: yields every tile the segment touches (including edge-adjacent).
    /// Emits pairs of (prev -> curr) transitions to check shared edges.
    /// </summary>
    private static IEnumerable<(bool hasPrev, Vector2Int prev, Vector2Int curr)> SupercoverLine(Vector2Int a, Vector2Int b)
    {
        int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;

        int x = x0, y = y0;
        //bool first = true;
        Vector2Int prev = new(x, y);
        yield return (false, prev, prev);

        if (dx >= dy)
        {
            int err = dx / 2;
            while (x != x1)
            {
                x += sx;
                err -= dy;
                Vector2Int curr = new(x, y);
                yield return (true, prev, curr);
                prev = curr;

                if (err < 0)
                {
                    y += sy;
                    err += dx;
                    curr = new Vector2Int(x, y);
                    yield return (true, prev, curr);
                    prev = curr;
                }
            }
        }
        else
        {
            int err = dy / 2;
            while (y != y1)
            {
                y += sy;
                err -= dx;
                Vector2Int curr = new(x, y);
                yield return (true, prev, curr);
                prev = curr;

                if (err < 0)
                {
                    x += sx;
                    err += dy;
                    curr = new Vector2Int(x, y);
                    yield return (true, prev, curr);
                    prev = curr;
                }
            }
        }
    }
}
