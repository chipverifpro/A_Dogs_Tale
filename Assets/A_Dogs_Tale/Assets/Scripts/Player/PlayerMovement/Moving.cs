using System;
using UnityEngine;

public partial class Player : MonoBehaviour
{
    void Move_Start()
    {
        var p = agent.transform.position;   // grab object position and set it in variable
        if (useXZPlane) agent.pos2 = World_to_Map(new Vector2(p.x, p.z));
        else agent.pos2 = World_to_Map(new Vector2(p.x, p.y));

        // Initialize yaw from current rotation
        agent.yawDeg = useXZPlane ? agent.transform.eulerAngles.y - yawCorrection : agent.transform.eulerAngles.z - yawCorrection;
    }

    // called by Input_Update to move in response to inputs.
    void Move_Update(float turn, float thrust)
    {
        // round to nearest .01 to reduce cumulative errors.
        CleanupFloat(ref turn, false);
        CleanupFloat(ref thrust);
        Cleanup(ref agent.pos2);

        agent.prevYawDeg = agent.yawDeg;
        if (Math.Abs(turn) > 1e-10f)
        {
            // Rotate the Player
            agent.yawDeg += turn * turnSpeedDegPerSec * Time.deltaTime;
            CleanupFloat(ref agent.yawDeg);

            //leaderTravelling = false; // if player is turning, stop travelling to click target
            // commit rotation ALWAYS (even if thrust == 0)
            //if (useXZPlane) agent.transform.rotation = Quaternion.Euler(0f, agent.yawDeg + yawCorrection, 0f);
            //else agent.transform.rotation = Quaternion.Euler(0f, 0f, agent.yawDeg + yawCorrection);
        }
        else // player not rotating
        {
            if (Math.Abs(thrust) > 1e-5f)    // only snap if turning=false but moving=true
            {
                agent.yawDeg = SnapToCardinals(agent.yawDeg, snapToCardinalDegrees);
                //leaderTravelling = false; // if player is moving, stop travelling to click target

                //if (useXZPlane) agent.transform.rotation = Quaternion.Euler(0f, agent.yawDeg + yawCorrection, 0f);
                //else agent.transform.rotation = Quaternion.Euler(0f, 0f, agent.yawDeg + yawCorrection);
            }
        }
        // done with turning, next is moving...

        // Forward direction unit vector in 2D plane
        float yawRad = -agent.yawDeg * Mathf.Deg2Rad;
        Vector2 fwd2 = new Vector2(Mathf.Cos(yawRad), Mathf.Sin(yawRad)); // XY forward (or XZ’s X/Z)
        Cleanup(ref fwd2);

        // Desired 2D motion (no strafing here)
        Vector2 desiredDir2 = fwd2 * Mathf.Clamp(thrust, -1f, 1f);
        float speed = baseSpeed * SampleSlopeMultiplier(agent.pos2, desiredDir2);

        // 2) Integrate and resolve against grid edges
        Vector2 p_from = agent.pos2;  // pos2 is the Vector2 of the player location
        Vector2 p_to = p_from + desiredDir2 * (speed * Time.deltaTime);
        Vector2 p_new = ResolveGridConstraints(p_from, p_to, agent.radius, constraintIters);

        // 3) Commit position & rotation to Transform
        agent.pos2 = p_new;
        bool skipHeight = false;
        if (gen == null)
        {
            skipHeight = true;
            Debug.LogError("Move_Update: gen is null!");
        }
        else
        {
            if (gen.cellGrid == null)
            {
                skipHeight = true;
                Debug.LogError("Move_Update: gen.cellGrid is null!");
            }
            if (gen.cfg == null)
            {
                skipHeight = true;
                Debug.LogError("Move_Update: gen.cfg is null!");
            }
        }
        if (!skipHeight)
        {
            //agent.height = gen.cfg.unitHeight * gen.cellGrid[Mathf.FloorToInt(agent.pos2.x), Mathf.FloorToInt(agent.pos2.y)].height;
            agent.height = Agent.SampleAgentHeight(agent.pos2, gen.cellGrid, gen.cfg.unitHeight);
        }
        //leaderTravelling = false; // if player is moving, stop travelling to click target
        TransformPosition(agent);
    }

    public void TransformPosition(Agent agent)
    {
        Cleanup(ref agent.pos2);

        if (useXZPlane)
        {
            Vector3 t; // = transform.position; // not necessary, we overwrite this value completely
            Vector2 t_World = Map_to_World(agent.pos2);
            t.x = t_World.x; t.z = t_World.y; // XZ location
            t.y = agent.height + 1;
            agent.transform.position = t;
            transform.position = t;
            agent.transform.rotation = Quaternion.Euler(0f, agent.yawDeg + yawCorrection, 0f); // rotate around Y for 3D
            transform.rotation = Quaternion.Euler(0f, agent.yawDeg + yawCorrection, 0f); // rotate around Y for 3D
        }
        else
        {
            Vector3 t; // = transform.position; // not necessary, we overwrite this value completely
            Vector2 t_World = Map_to_World(agent.pos2);
            t.x = t_World.x; t.y = t_World.y; // XY location
            t.z = agent.height + 1;
            agent.transform.position = t;
            transform.position = t;
            agent.transform.rotation = Quaternion.Euler(0f, 0f, agent.yawDeg + yawCorrection); // rotate around Z for XY
            transform.rotation = Quaternion.Euler(0f, 0f, agent.yawDeg + yawCorrection); // rotate around Z for XY
        }
    }

    // ---- Grid constraint solver against DirFlags walls/doors ----
    Vector2 ResolveGridConstraints(Vector2 from, Vector2 to, float r, int maxIters)
    {
        Cleanup(ref from);
        Cleanup(ref to);

        int W = gen.cfg.mapWidth;
        int H = gen.cfg.mapHeight;

        int from_i;
        int from_j;

        Vector2 final = from;   // initialize with current position

        // clamp destination to world bounds
        float xmin = r, xmax = W - r;
        float ymin = r, ymax = H - r;
        to.x = Mathf.Clamp(to.x, xmin, xmax);
        to.y = Mathf.Clamp(to.y, ymin, ymax);

        if ((from - to).sqrMagnitude < 1e-10f)
            return to; // not moving

        // iterate if we cross into a new tile so we always use correct wall bounds.
        for (int iter = 0; iter < maxIters; iter++)
        {
            //if (iter > 0) Debug.Log($"Iteration {iter}: from={from.x},{from.y} to={to.x},{to.y}");    // Debug

            // Recompute cell after bounds clamp
            from_i = Mathf.FloorToInt(from.x);
            from_j = Mathf.FloorToInt(from.y);

            // Define the maximum we allow movement in each direction.
            float cxmin = Mathf.Max(from_i - 1f + r, 0 + r);
            float cxmax = Mathf.Min(from_i + 2f - r, W - r);  // big enough to get into neighbor cell, but not through it without checking next iteration first.
            float cymin = Mathf.Max(from_j - 1f + r, 0 + r);  // also clamp at world bounds
            float cymax = Mathf.Min(from_j + 2f - r, H - r);

            // debug display:
            //var c = gen.cellGrid[from_i, from_j];                                     // Debug
            //Debug.Log($"pos={from},{from}  Walls={c.walls}, Doors={c.doors}");    // Debug


            // deal with Diagonal walls if present
            Vector3 start = new(from.x - from_i, from.y - from_j, 0f);
            Vector2 dir = (to - from).normalized; // should be normalized
            Vector3 dir3 = new(dir.x, dir.y, 0f);
            float diagonalDist;
            float cellSize = 1f; // something like: gen.cfg.cellSize;
            Vector2 on_the_way;
            Cell cell = gen.cellGrid[from_i, from_j];
            Room room = gen.rooms[cell.room_number];

            // Apply edge block constraints for current 'from'
            if (EdgeBlocked(from_i, from_j, DirFlags.E)) cxmax = Mathf.Min(cxmax, from_i + 1f - r);
            if (EdgeBlocked(from_i, from_j, DirFlags.W)) cxmin = Mathf.Max(cxmin, from_i + 0f + r);
            if (EdgeBlocked(from_i, from_j, DirFlags.N)) cymax = Mathf.Min(cymax, from_j + 1f - r);
            if (EdgeBlocked(from_i, from_j, DirFlags.S)) cymin = Mathf.Max(cymin, from_j + 0f + r);

            //Debug.Log($"p={from_i},{from_j} cxmin/max={cxmin}-{cxmax} cymin/max={cymin}-{cymax}");    // Debug

            Vector2 temp_on_the_way = new Vector2(        // move toward destination (within clamps)
                Mathf.Clamp(to.x, cxmin, cxmax),
                Mathf.Clamp(to.y, cymin, cymax)
            );

            float temp_distance = (temp_on_the_way - from).magnitude;
            on_the_way = temp_on_the_way; // initialize

            if (TryDistanceToDiagonalWall(room, new Vector2Int(from_i, from_j), start, dir3, cellSize, r, out diagonalDist))
            {
                // Point where we contact the shrunken diagonal
                Vector2 hit = from + dir * diagonalDist;

                // Remaining intent this iteration (toward the clamped target)
                Vector2 remVec = temp_on_the_way - hit;

                // Get diagonal tangent (unit) and normal (unit)
                Vector2 tan, nrm;
                GetDiagonalTangentAndNormal(room, new Vector2Int(from_i, from_j), out tan, out nrm);

                //Debug.Log($" tan=({tan.x},{tan.y}) nrm=({nrm.x},{nrm.y})");

                // Slide only by the perpendicular projection: component along tangent
                float slideLen = Vector2.Dot(remVec, tan);      // signed; can be negative
                Vector2 slide  = tan * slideLen;

                // New target = hit + allowed slide along the diagonal
                Vector2 slideTarget = hit + slide;

                // Clamp into current cell’s movement limits; prevents skipping edge checks
                slideTarget = new Vector2(
                    Mathf.Clamp(slideTarget.x, cxmin, cxmax),
                    Mathf.Clamp(slideTarget.y, cymin, cymax)
                );

                // Tiny nudge off the wall so next iteration doesn't re-hit due to FP error
                const float EPS = 1e-4f;
                slideTarget += (-nrm) * EPS;

                on_the_way = slideTarget;
                temp_distance = (on_the_way - from).magnitude;
            }
            else
            {
                // No diagonal in that cell, ray misses or parallel → treat as no hit here
                //Debug.Log($"   Diagonal wall missed at distance {diagonalDist} before reaching {temp_distance}");

                on_the_way = temp_on_the_way;
            }


            if ((on_the_way - to).sqrMagnitude < 1e-10f)     // are we at destination?
            {
                final = on_the_way;
                break;              // arrived, no more iterations
            }

            if ((from - to).sqrMagnitude < 1e-10f)     // did we move this iteration?
            {
                final = on_the_way;
                break;      // settled (no change this iteration)
            }
            from = on_the_way; // Advance 'from' to the current position for next iteration
            final = on_the_way; // set final position in case we are at last iteration
        }

        Cleanup(ref final);
        return final;    // final is how far we were able to move 'to' within wall limits
    }

    // Is walking in this direction blocked by anything? (walls, closed doors, and end-of-walls)
    bool EdgeBlocked(int i, int j, DirFlags dir)
    {
        var c = gen.cellGrid[i, j];
        bool hasWall = (c.walls & dir) != 0;

        bool hasDoor = (c.doors & dir) != 0;
        bool doorOpen = hasDoor && GetDoorOpenState(i, j, dir);

        DirFlags endWallBlockers = EndOfWallBlockers(agent.pos2, i, j);
        bool blockedByEndWall = (endWallBlockers & dir) != 0;

        //Debug.Log($"EdgeBlocked({i}, {j}, dir={dir} = {wall})");
        if (hasDoor) return !doorOpen; // door present → blocked if closed
        return (hasWall || blockedByEndWall);
    }




    // Prevent walking into the end of a thin wall:
    //   If we are close to an edge of the current cell, then block to the left and right if there is a wall end there.
    //   This also prevents walking off the edge of a door, which is covered as a subset of this check.
    DirFlags EndOfWallBlockers(Vector2 pos2, int i, int j)
    {
        Cell cell_off_grid = new(-1, -1);  // use this for off-grid cells (no walls or doors set)
        cell_off_grid.doors = DirFlags.None;
        cell_off_grid.walls = DirFlags.None;

        Cleanup(ref pos2); // eliminate cumulative errors

        float InGrid_x = pos2.x % 1f;   // WARNING: results in inexact fractions
        float InGrid_y = pos2.y % 1f;
        float One_Minus_Radius = 1f - radius; // no extra fractions
        CleanupFloat(ref InGrid_x);
        CleanupFloat(ref InGrid_y);

        // Which edges of current tile is the player near to?
        bool S_Edge = (InGrid_y < radius);
        bool N_Edge = (InGrid_y > One_Minus_Radius);
        bool W_Edge = (InGrid_x < radius);
        bool E_Edge = (InGrid_x > One_Minus_Radius);
        //Debug.Log($"{pos2}->{InGrid_x},{InGrid_y} in {i},{j}  Near Edges: N={N_Edge}, S={S_Edge}, W={W_Edge}, E={E_Edge}.  radius={radius}, 1-radius={One_Minus_Radius}");

        // grab a cell to each side of current cell, using dummy cell when off-grid
        Cell C_South = gen.In(i, j - 1) ? gen.cellGrid[i, j - 1] : cell_off_grid;
        Cell C_North = gen.In(i, j + 1) ? gen.cellGrid[i, j + 1] : cell_off_grid;
        Cell C_West = gen.In(i - 1, j) ? gen.cellGrid[i - 1, j] : cell_off_grid;
        Cell C_East = gen.In(i + 1, j) ? gen.cellGrid[i + 1, j] : cell_off_grid;

        // Do we have the end of a wall in this direction? Initialize to no
        bool S_End_Wall = false;
        bool N_End_Wall = false;
        bool W_End_Wall = false;
        bool E_End_Wall = false;

        // If we are by the edge of the cell, look right and left along that same edge for a wall or door.
        if (E_Edge)
        {
            S_End_Wall = ((C_South.walls | C_South.doors) & DirFlags.E) != 0;
            N_End_Wall = ((C_North.walls | C_North.doors) & DirFlags.E) != 0;
        }
        if (W_Edge)
        {
            S_End_Wall = ((C_South.walls | C_South.doors) & DirFlags.W) != 0;
            N_End_Wall = ((C_North.walls | C_North.doors) & DirFlags.W) != 0;
        }
        if (N_Edge)
        {
            W_End_Wall = ((C_West.walls | C_West.doors) & DirFlags.N) != 0;
            E_End_Wall = ((C_East.walls | C_East.doors) & DirFlags.N) != 0;
        }
        if (S_Edge)
        {
            W_End_Wall = ((C_West.walls | C_West.doors) & DirFlags.S) != 0;
            E_End_Wall = ((C_East.walls | C_East.doors) & DirFlags.S) != 0;
        }

        // create DirFlags for all the end walls that would get in our way in that direction.
        DirFlags End_Walls = (N_End_Wall ? DirFlags.N : 0)
                           | (S_End_Wall ? DirFlags.S : 0)
                           | (W_End_Wall ? DirFlags.W : 0)
                           | (E_End_Wall ? DirFlags.E : 0);

        //Debug.Log($"End_Walls = {End_Walls}");
        return End_Walls;
    }

    // If we are near a cardinal direction, tweak yaw to go exactly the cardinal direction
    public float SnapToCardinals(float yawDeg, float snapToCardinalDegrees = 10f)
    {
        // Normalize into [0,360)
        yawDeg = Mathf.Repeat(yawDeg, 360f);
        //Debug.Log($"yawDeg = {yawDeg}");

        // Cardinal angles
        float[] cardinals4 = { 0f, 90f, 180f, 270f };
        float[] cardinals8 = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };
        float[] cardinals;

        // support snapping to diagonals if configured in parameters.
        if (snapEightWay) cardinals = cardinals8;
        else cardinals = cardinals4;

        foreach (float c in cardinals)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(yawDeg, c)) <= snapToCardinalDegrees)
                return c; // Snap!
        }

        return yawDeg; // leave unchanged if no snap
    }

    // Rounds a number to nearest .01 to eliminate tiny cumulative errors
    // Option to keep the destination within the same integer value
    public static void CleanupFloat(ref float num, bool same_tile = true)
    {
        float new_num;
        new_num = Mathf.Round(num * 100f) / 100f;   // round to 0.01

        if (same_tile)      // prevent going into different tile
        {
            float tile_num = Mathf.Floor(num); // tile to stay in

            new_num = Mathf.Clamp(new_num, tile_num, tile_num + 0.99f);
        }
        num = new_num;  // update the ref to the cleaned up num
    }

    // Rounds Vector2 x,y to nearest .01 to eliminate tiny cumulative errors
    public static void Cleanup(ref Vector2 vect, bool same_tile = true)
    {
        CleanupFloat(ref vect.x, same_tile);
        CleanupFloat(ref vect.y, same_tile);
    }

    // apply offset from world coordinates to map coordinates
    public Vector2 World_to_Map(Vector2 world_loc)
    {
        Vector2 map_loc;
        map_loc.x = world_loc.x + xCorrection;
        map_loc.y = world_loc.y + yCorrection;
        return map_loc;
    }

    // apply offset from map coordinates to world coordinates
    public Vector2 Map_to_World(Vector2 map_loc)
    {
        Vector2 world_loc;
        world_loc.x = map_loc.x - xCorrection;
        world_loc.y = map_loc.y - yCorrection;
        return world_loc;
    }

    // ---- Stubs to wire into systems later ----

    // Return whether the door on edge (i,j,dir) is open. For now: consider doors open by default.
    bool GetDoorOpenState(int i, int j, DirFlags dir)
    {
        // TODO: hook into your Door objects on cells
        return true;
    }

    // Use height/tilt/travel_cost for real speed scaling later.
    float SampleSlopeMultiplier(Vector2 pos, Vector2 dir)
    {
        // e.g., read gen.cellGrid[floorX,floorY].travel_cost, tiltFloor, etc.
        // return uphill ? slopeUphillFactor : downhill ? slopeDownhillFactor : 1f;
        return 1f;
    }

    // Handle diagonal walls

    /// <summary>
    /// Distance from startWorld along dirWorld (normalized XZ) to the diagonal-corner wall
    /// inside the given cell, already accounting for playerRadius (no extra subtract needed).
    /// Returns true and sets 'distance' if hit; false if no diagonal in cell or ray misses.
    /// </summary>
    public static bool TryDistanceToDiagonalWall(
        Room room,
        Vector2Int cellXY,          // which cell in this room we're testing
        Vector3 startWorldXY,         // start position (XZ used)
        Vector3 dirWorldXY,           // direction (XZ), should be normalized
        float cellSize,           // room.Cell size in world units
        float playerRadius,       // radius to keep away from walls
        out float distance)
    {
        //playerRadius = 0f; // TEMP disable radius for testing

        distance = 0f;
        Vector3 startWorld = new(startWorldXY.x, 0, startWorldXY.y);
        Vector3 dirWorld = new(dirWorldXY.x, 0, dirWorldXY.y);

        int cellIndex = room.GetCellInRoom(cellXY);
        if (cellIndex < 0) return false;

        var cell = room.cells[cellIndex];
        //Debug.Log($"startWorld=({startWorld.x},{startWorld.z}) dirWorld=({dirWorld.x},{dirWorld.z}) in cell {cellXY} of room {room.my_room_number}");
        //Debug.Log($"   Checking diagonal in cell {cellXY} walls={cell.walls} doors={cell.doors}");
        // Determine which diagonal corner this cell blocks (two walls, no doors)
        var diag = GetDiagonalOpenDirection(cell.walls, cell.doors);
        //Debug.Log($"   Diagonal open direction = {diag}");
        if (diag == DiagonalOpenDirection.None) return false;

        // Build diagonal line in cell-local (u,v) with u=(x-xMin)/s, v=(z-zMin)/s in [0,1]
        // General form: a*u + b*v = c
        float a, b, c;

        // Offset amount in (u,v) units for radius: move by radius along the line normal.
        // For u+v=k or u-v=k, the unit normal length in (u,v) space is sqrt(2),
        // so c shifts by (playerRadius / cellSize) * sqrt(2).
        float k = (playerRadius / Mathf.Max(1e-5f, cellSize)) * 1.41421356f;

        switch (diag)
        {
            case DiagonalOpenDirection.NE: // S+W walls
                // Corner at (u=0,v=0). Blocking line is u + v = 1 - k  (pulled in by radius)
                a = 1f; b = 1f; c = 1f + k;
                break;
            case DiagonalOpenDirection.SW: // N+E walls
                // Corner at (1,1). Blocking line is u + v = k
                a = 1f; b = 1f; c = k;
                break;
            case DiagonalOpenDirection.SE: // N+W walls
                // Corner at (1,0). Blocking line is u - v = k
                a = 1f; b = -1f; c = k;
                break;
            case DiagonalOpenDirection.NW: // S+E walls
                // Corner at (0,1). Blocking line is u - v = -(1 - k)
                a = 1f; b = -1f; c = -(1f - k);
                break;
            default:
                return false;
        }

        // Ray/line intersection in WORLD units.
        // Let u = (x - xMin)/s, v = (z - zMin)/s.
        // Solve a*u + b*v = c for T where x = x0 + dir.x*T, z = z0 + dir.z*T.
        Vector2 xz0 = new Vector2((startWorld.x - Mathf.Floor(startWorld.x)) * cellSize, (startWorld.z - Mathf.Floor(startWorld.z)) * cellSize); // local coords in cell
        //Cleanup(ref xz0, true);

        Vector2 d = new Vector2(dirWorld.x, dirWorld.z);

        // Denominator: a*(dx/s) + b*(dz/s) -> simplified into world units:
        float denom = a * d.x + b * d.y;
        if (Mathf.Abs(denom) < 1e-6f)
        {
            distance = 999;
            //Debug.Log($"   Diagonal wall line parallel to ray, denom={denom}");
            return false; // ray parallel to diagonal
        }
        // Numerator: s*c - [a*(x0 - xMin) + b*(z0 - zMin)]
        float num = cellSize * c - (a * (xz0.x) + b * (xz0.y));

        float T = num / denom;         // distance along 'dirWorld' (since 'denom' is in world units)

        // Intersection point
        Vector2 hitXZ = xz0 + (d * T);
        //Debug.Log($"   Diagonal wall line hit at T={T}, world=({hitXZ.x},{hitXZ.y}), xz0=({xz0.x},{xz0.y}), dir=({d.x},{d.y})");

        if (T <= 0f)
        {
            distance = T;     // a negative nummer means intersection behind start
            //Debug.Log($"   Diagonal wall line hit behind start. at T={T}, world=({hitXZ.x},{hitXZ.y}), xz0=({xz0.x},{xz0.y}), dir=({d.x},{d.y})");
            //return false;     // intersection behind start or at start, back up the player
        }

        // Must lie within the cell’s square [xMin,xMax] x [zMin,zMax]
        float xMin = 0f;
        float zMin = 0f;
        float xMax = xMin + cellSize;
        float zMax = zMin + cellSize;
        //Debug.Log($"   Cell bounds: x={xMin}-{xMax}, z={zMin}-{zMax}, hitXZ=({hitXZ.x},{hitXZ.y})");

        if (hitXZ.x < xMin - 1e-4f || hitXZ.x > xMax + 1e-4f ||
            hitXZ.y < zMin - 1e-4f || hitXZ.y > zMax + 1e-4f)
        {
            //Debug.Log($"   Diagonal wall hit outside cell bounds -- use it anyway");
            distance = T;
            //return false; // The infinite line was hit, but not the segment inside the cell
        }

        distance = T; // Already accounts for playerRadius via 'k'
        //Debug.Log($"   Diagonal wall hit confirmed at distance {distance}");
        return true;
    }

    // Your diagonal detector – rewritten to accept flags directly.
    private static DiagonalOpenDirection GetDiagonalOpenDirection(DirFlags walls, DirFlags doors)
    {
        // exactly two walls, no doors
        int wallBits = walls.Count();
        if (wallBits != 2 || doors != DirFlags.None) return DiagonalOpenDirection.None;

        if ((walls & (DirFlags.N | DirFlags.E)) == (DirFlags.N | DirFlags.E)) return DiagonalOpenDirection.SW;
        if ((walls & (DirFlags.S | DirFlags.E)) == (DirFlags.S | DirFlags.E)) return DiagonalOpenDirection.NW;
        if ((walls & (DirFlags.S | DirFlags.W)) == (DirFlags.S | DirFlags.W)) return DiagonalOpenDirection.NE;
        if ((walls & (DirFlags.N | DirFlags.W)) == (DirFlags.N | DirFlags.W)) return DiagonalOpenDirection.SE;
        return DiagonalOpenDirection.None;
    }
    public static Vector3 DirFromYawDeg(float yawDeg)
    {
        float yawRad = yawDeg * Mathf.Deg2Rad;
        // Unity uses a left-handed Y-up system:
        // X = cos(yaw), Z = sin(yaw)
        return new Vector3(Mathf.Cos(yawRad), 0f, Mathf.Sin(yawRad));
    }
    
    // Returns a unit tangent (direction along the diagonal line within the cell) and a unit normal pointing toward the blocked corner.
    void GetDiagonalTangentAndNormal(Room room, Vector2Int cellXY, out Vector2 tangent, out Vector2 normal)
    {
        var cell = room.cells[room.GetCellInRoom(cellXY)];
        var diag = GetDiagonalOpenDirection(cell.walls, cell.doors); // your function

        switch (diag)
        {
            case DiagonalOpenDirection.NE:
                // diagonal is u+v = const, blocked corner is (1,1)
                tangent = new Vector2( 1f, -1f).normalized;   // slope -1
                normal  = new Vector2( 1f,  1f).normalized;   // toward NE corner
                break;
            case DiagonalOpenDirection.SW:
                tangent = new Vector2( 1f, -1f).normalized;   // same line, other side
                normal  = new Vector2(-1f, -1f).normalized;   // toward SW corner
                break;
            case DiagonalOpenDirection.SE:
                // diagonal is u-v = const, blocked corner is (1,0)
                tangent = new Vector2( 1f,  1f).normalized;   // slope +1
                normal  = new Vector2( 1f, -1f).normalized;   // toward SE corner
                break;
            case DiagonalOpenDirection.NW:
                tangent = new Vector2( 1f,  1f).normalized;
                normal  = new Vector2(-1f,  1f).normalized;   // toward NW corner
                break;
            default:
                tangent = Vector2.zero;  // fallback
                normal  = Vector2.zero;
                break;
        }
    }
}
