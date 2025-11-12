using System;
using System.Collections;
using UnityEngine;


//[RequireComponent(typeof(BreadcrumbTrail))]
public partial class Agent : MonoBehaviour
{
    // ==============================================================
    // An Agent is a character.  Specific types inherit these behaviors
    //   PlayerAgent is a member of the player's party.
    //   NPCAgent is a non-player-charactger (friendly / neutral / hostile).

    //public String name;   // already inherited from MonoBehavior
    //public bool enabled;  // already inherited from MonoBehavior

    //DungeonSettings cfg;
    public ObjectDirectory dir;

    public int id;                          // unique id number

    // controls map movement:
    public float baseSpeed = 6.0f;          // W/S movement world units per second
    public float turnSpeedDegPerSec = 180f; // A/D rotate speed
    public float radius = 0.30f;            // collision radius inside a 1x1 cell

    // current status
    //public Vector3 pos3;// => new() { x=pos2.x, y=pos2.y, z=height};
    public Vector2 pos2;
    public float height;
    public float yawDeg;
    public float targetYawDeg;
    public float prevYawDeg;

    // while in a pack formation, these are the target positions as calculated from leader's position.
    //public Vector2 formationTargetPos;     // position we should be at in formation
    //public float formationTargetYaw;     // direction we should be facing in formation
    //public Vector2 formationCrumbPos;      // allows finding Crumbs to continue following while holding formation.

    public GameObject DogPrefab;        // prefab to give each agent a visible model

    public Animator anim;

    // next crumb in trail we are following
    public Crumb next_actualCrumb;
    public Crumb next_formationCrumb;

    // for routing to destination around obstacles.
    public System.Collections.Generic.List<Vector2Int> routeWaypoints;

    // add other properties...
    public Color color1;// = Color.black;  // top color
    public Color color2;// = Color.white;  // bottom color (or outline)
    public int healthPoints;
    public String racialType;           // Humanoid, Shifter, Animal, Monster
    public String race;                 // Human, Elf, Werewolf, Cat, Mimic, etc.
    public Pack pack;
    public BreadcrumbTrail trail;       // 
    public bool trailLeader = false;    //
    public bool trailFollower = false;  //
    public bool camera_refresh_needed = true;   // one-time request for vcam to refresh visibility settings

    [Header("Player to Walls adjustment")]
    public float xCorrection = 0.5f;
    public float yCorrection = 0.5f;
    public float yawCorrection = 90f;
    public float heightCorrection = 1f;

    // Tuning internal parameters

    public bool useXZPlane = true;      // false = XY floor (tilemap), true = XZ floor (3D)

    protected virtual void Awake()
    {
        //trail = GetComponent<BreadcrumbTrail>();
    }

    protected virtual void Start()
    {
        if (!dir) dir = FindFirstObjectByType<ObjectDirectory>();
        if (!dir) Debug.LogWarning($"[Agent {name}] ObjectDirectory not found.");
        StartCoroutine(CycleAnimations());
    }


    protected virtual void Update()
    {
        if (trailLeader) // Leave crumbs
        {
            if (pack.gen.buildComplete)
                LeaderTravelToTarget();
        }
        if (trailFollower)
        {
            //FollowTrail();
            if (pack.gen.buildComplete)
                FollowTrailInFormation();
        }
    }

    IEnumerator CycleAnimations()
    {
        yield return new WaitForSeconds(.5f);

        while (true)
        {
            anim.SetInteger("AnimationID", UnityEngine.Random.Range(0, 15));
            yield return new WaitForSeconds(2);
        }
    }

    
    void LeaderTravelToTarget()
    {
        Vector2 originalPos;   // starting position of this call
        Vector2 targetPos;    // where we want to go (from crumb)
        Vector2 loc_clamped;    // location we can get to limited by move_credit
        Vector2 final_dest_pos; // where we ended up
        Vector2 unit_vector;    // for converting into yawDeg

        float dist_to_target;   // oringinal position -> target_pos
        float move_credit;      // how far we can move this frame


        // if we have no valid destination, abort
        if (next_formationCrumb.valid == false)
        {
            return; // no target to follow, do nothing
        }
        //prevYawDeg = yawDeg;

        originalPos = pos2;
        move_credit = baseSpeed * Time.deltaTime;
        targetPos = next_formationCrumb.pos2;
        dist_to_target = Mathf.Sqrt((pos2 - targetPos).sqrMagnitude);

        if (dist_to_target < .01)
        {
            //Debug.Log("EE");
            next_formationCrumb.valid = false;
            return;  // already arrived at destination.  No move necessary.
        }
        
        Vector2Int pos2Int = VectFloat2Int(pos2);
        Vector2Int targetPosInt = VectFloat2Int(targetPos);
        if (pos2Int != targetPosInt)
        {
            //Debug.Log("DD");
            // create a route and follow it...
            routeWaypoints = dir.pathfinding.FindPath(pos2Int, targetPosInt);
            if (routeWaypoints.Count == 0)
            {
                Debug.Log("No path to target found.");
                next_formationCrumb.valid = false;
                return; // No path to target found.
            }
            routeWaypoints.RemoveAt(0); // remove start position
            //Debug.Log($"pos2 {pos2} -> target {targetPos}, routeWaypoints = {routeWaypoints.Count}");
            dir.pathfinding.TrySkippingWaypoints(this);
            move_credit = FollowWaypoints(move_credit);
            pos2Int = VectFloat2Int(pos2);
        }

        if (pos2Int - targetPosInt == Vector2Int.zero)
        {
            // we are in target tile, but maybe not at exact target position.
            dist_to_target = Mathf.Sqrt((pos2 - targetPos).sqrMagnitude);
            if ((move_credit > .01))// && (dist_to_target > .01))
            {
                //Debug.Log($"Distance to target: {dist_to_target}, Move credit: {move_credit}");
                if (move_credit < dist_to_target)
                {
                    //Debug.Log("AA");
                    // cannot go all the way, so travel by move_credit distance and continue next frame.
                    loc_clamped = LerpVector2(pos2, targetPos, move_credit / dist_to_target);
                    pos2.x = loc_clamped.x;
                    pos2.y = loc_clamped.y;
                }
                else
                {
                    //Debug.Log("BB");
                    // we have enough move_credit to go all the way.  Do it.
                    pos2.x = targetPos.x;
                    pos2.y = targetPos.y;
                    next_formationCrumb.valid = false; // arrived
                }

                //Debug.Log("CC");
                // turn to correct angle of movement.
                final_dest_pos = new(pos2.x, pos2.y);
                prevYawDeg = yawDeg;
                unit_vector = (final_dest_pos - originalPos).normalized;
                yawDeg = UnitVectorToYaw(unit_vector);
                targetYawDeg = yawDeg;
                TurnTowards(ref yawDeg, prevYawDeg, targetYawDeg, turnSpeedDegPerSec);
                prevYawDeg = yawDeg;
                TransformPosition(this);    // move the agent object to it's new location.
            }
        }
    }

    Vector2Int VectFloat2Int(Vector2 vect)
    {
        return new Vector2Int(Mathf.FloorToInt(vect.x), Mathf.FloorToInt(vect.y));
    }

    void FollowTrailInFormation()
    {
        Vector2 originalPos;
        Vector2 targetPos;
        Vector2 loc_clamped;

        Vector2 final_dest_pos; // where we ended up
        Vector2 unit_vector;    // for converting into yawDeg

        float dist_to_target;
        float move_credit;
        bool use_crumb_yaw = false;

        // if we have no valid destination, see if we can get a new crumb, else abort
        if (next_formationCrumb.valid == false)
        {
            next_actualCrumb = trail.GetNextCrumb(this);
            next_formationCrumb = GetFormationPosition(pack, id, next_actualCrumb);
            if (next_formationCrumb.valid == false) return; // no trail to follow, do nothing
        }

        originalPos = pos2;  // current agent position
        // calculate the move_credit;
        move_credit = baseSpeed * Time.deltaTime;
        targetPos = next_formationCrumb.pos2;
        //Debug.Log($"Player {name} following trail towards {next_crumb.position}, move_credit={move_credit}");

        //loop until move_credit is gone
        while (move_credit > 0.001)
        {
            //Debug.Log($"Player {name} following trail towards {next_crumb.position}, move_credit={move_credit}");
            use_crumb_yaw = false;

            targetPos = next_formationCrumb.pos2;
            dist_to_target = Mathf.Sqrt((pos2 - targetPos).sqrMagnitude);
            if (dist_to_target < .001)
            {
                // arrived at crumb target, get next crumb
                next_actualCrumb = trail.GetNextCrumb(this);    // this is the leader crumb
                next_formationCrumb = GetFormationPosition(pack, id, next_actualCrumb); // shift it by follower offset
                if (next_formationCrumb.valid == false)
                {
                    use_crumb_yaw = true;
                    break;  // arrived at last available crumb.
                }
                dist_to_target = Mathf.Sqrt((pos2 - targetPos).sqrMagnitude);

                if (dist_to_target < .01)
                {
                    use_crumb_yaw = true;
                    //if (next_formationCrumb.yawDeg == pack.PackLeader.yawDeg)
                        //return;
                    //Debug.LogWarning($"Two sequential crumbs on top of each other.  At leader, who isn't moving.");
                    break; // we are also at the next crumb. This happens when fallback was to leader position, but leader didn't move.
                }
            }

            targetPos = next_formationCrumb.pos2;

            // move up to target, maximum move is move_credit.

            // do the move.  Travel dist towards target position
            if (move_credit < dist_to_target)
            {
                // cannot go all the way, so travel by move_credit distance
                loc_clamped = LerpVector2(pos2, targetPos, move_credit / dist_to_target);
                pos2 = loc_clamped;
                move_credit = 0;
                use_crumb_yaw = false;
                break;
            }
            else
            {
                // we have enough move_credit to go all the way.  Do it and repeat the loop.
                pos2 = targetPos;
                move_credit -= dist_to_target;    // continue while loop getting next crumb
                use_crumb_yaw = true;   // just in case we break from loop.
            }
        } // continue while credits remain

        final_dest_pos = pos2;
        unit_vector = (final_dest_pos - originalPos).normalized;
        if (use_crumb_yaw)
        {
            if (pack.formation == FormationsEnum.Circle) // face outwards only in this formation
            {
                unit_vector = (pos2 - pack.PackLeader.pos2).normalized;
                yawDeg = UnitVectorToYaw(unit_vector);
            }
            else
            {
                yawDeg = pack.PackLeader.yawDeg; // face the leader's direction on arrival
            }
        }
        else
        {
            // on-the-way, use the current motion direction
            yawDeg = UnitVectorToYaw(unit_vector);
        }

        TransformPosition(this);    // move the agent object to it's new location.
    }


    public float FollowWaypoints(float move_credit) // returns remaining move_credit if any
    {
        Vector2 originalPos;    // starting position of this call
        Vector2 targetPos;      // where we want to go (from waypoints)
        Vector2 loc_clamped;    // location we can get to limited by move_credit
        Vector2 final_dest_pos; // where we ended up
        Vector2 unit_vector;    // for converting into yawDeg

        float dist_to_target;   // oringinal position -> target_pos
        //float move_credit;      // how far we can move this frame

        // if we have no valid destination, abort
        if (routeWaypoints.Count == 0) return move_credit;

        originalPos = pos2;
        //move_credit = baseSpeed * Time.deltaTime;
        dir.pathfinding.TrySkippingWaypoints(this);
        targetPos = routeWaypoints[0];
        targetPos += new Vector2(0.5f, 0.5f);   // head to center of tile
        // if there is exactly one waypoint, skip it and go to exact target (next_formationCrumb)
        if (routeWaypoints.Count == 1)
        {
            targetPos = next_formationCrumb.pos2;
        }
        dist_to_target = Mathf.Sqrt((pos2 - targetPos).sqrMagnitude);

        //Debug.Log($"FollowWaypoints({move_credit}), dist_to_target = {dist_to_target}, waypoints = {routeWaypoints.Count}");

        if (dist_to_target < .001)
        {
            if (routeWaypoints.Count == 0) return move_credit;
            //Debug.Log("A");
            routeWaypoints.RemoveAt(0);
            if (routeWaypoints.Count == 0) return move_credit;
            dir.pathfinding.TrySkippingWaypoints(this);
            targetPos = routeWaypoints[0];
            targetPos += new Vector2(0.5f, 0.5f);   // head to center of tile
            Debug.Log($"(1) waypoint targetPos = {targetPos}");
            //Debug.Log("B");
            dist_to_target = Mathf.Sqrt((pos2 - targetPos).sqrMagnitude);
            if (dist_to_target < .001) return move_credit;
            //Debug.Log("C");
        }

        while (move_credit > 0.001)
        {
            //Debug.Log("D");
            if (move_credit < dist_to_target)
            {
                //Debug.Log("E");
                // cannot go all the way, so travel by move_credit distance and continue next frame.
                loc_clamped = LerpVector2(pos2, targetPos, move_credit / dist_to_target);
                pos2.x = loc_clamped.x;
                pos2.y = loc_clamped.y;
                move_credit = 0;
            }
            else
            {
                //Debug.Log("F");
                // we have enough move_credit to go all the way.  Do it and continue to next waypoint
                pos2.x = targetPos.x;
                pos2.y = targetPos.y;
                move_credit -= dist_to_target;

                // advance to the next waypoint
                routeWaypoints.RemoveAt(0);
                if (routeWaypoints.Count == 0)
                {
                    break; // no more waypoints
                }
                dir.pathfinding.TrySkippingWaypoints(this);
                targetPos = routeWaypoints[0];
                targetPos += new Vector2(0.5f, 0.5f);   // head to center of tile
                Debug.Log($"(2) waypoint targetPos = {targetPos}");
                dist_to_target = Mathf.Sqrt((pos2 - targetPos).sqrMagnitude);
            }
        }
        //Debug.Log("G");
        // turn to correct angle of movement.
        final_dest_pos = new(pos2.x, pos2.y);
        unit_vector = (final_dest_pos - originalPos).normalized;
        yawDeg = UnitVectorToYaw(unit_vector);
        targetYawDeg = yawDeg;
        TurnTowards(ref yawDeg, prevYawDeg, targetYawDeg, turnSpeedDegPerSec);
        prevYawDeg = yawDeg;
        TransformPosition(this);    // move the agent object to it's new location.
        return move_credit;
    }

    // Apply Lerp to all 3 dimensions of a vector.
    // Moves character t percent of the way along the vector.
    Vector3 LerpVector3(Vector3 a, Vector3 b, float t)
    {
        Vector3 result;
        result.x = Mathf.Lerp(a.x, b.x, t);
        result.y = Mathf.Lerp(a.y, b.y, t);
        result.z = Mathf.Lerp(a.z, b.z, t);
        return result;
    }

    Vector3 LerpVector2(Vector2 a, Vector2 b, float t)
    {
        Vector2 result;
        result.x = Mathf.Lerp(a.x, b.x, t);
        result.y = Mathf.Lerp(a.y, b.y, t);
        return result;
    }

    Vector3 GetPositionOfAgentBeforeMe(int my_id)
    {
        Vector3 ahead_pos = new();
        //int ahead_id = -1;
        int i;

        for (i = 1; i < pack.packList.Count; i++)
        {
            if (pack.packList[i].id == my_id)
                break;
            //ahead_id = pack.packList[i].id;
        }
        ahead_pos.x = pack.packList[i - 1].pos2.x;
        ahead_pos.y = pack.packList[i - 1].height;
        ahead_pos.z = pack.packList[i - 1].pos2.y;

        return ahead_pos;
    }

    // Convert from map location to world location and apply that to the agent's ojbect
    public bool TransformPosition(Agent agent)
    {
        agent.prevYawDeg = agent.yawDeg;
        if (pack.gen.buildComplete == false)
        {
            Debug.Log("TransformPosition: Dungeon generation not complete yet.  Don't bother moving agent now.");
            return false;
        }
        Cell cell = new(0,0);
        bool skipHeight = false;
        while (true)
        {
            if (agent == null)
            {
                Debug.LogError("TransformPosition: agent is null");
                skipHeight = true;
                break;
            }
            else
            {
                if (agent.transform == null)
                {
                    Debug.LogError("TransformPosition: agent.transform is null");
                    skipHeight = true;
                    break;
                }

                if (agent.pos2 == null)
                {
                    Debug.LogError("TransformPosition: agent.pos2 is null");
                    skipHeight = true;
                    break;
                }
            }
            if (pack == null)
            {
                Debug.LogError("TransformPosition: pack is null");
                skipHeight = true;
                break;
            }
            else if (pack.gen == null)
            {
                Debug.LogError("TransformPosition: pack.gen is null");
                skipHeight = true;
                break;
            }
            else
            {
                if (pack.gen.cellGrid == null)
                {
                    Debug.LogError("TransformPosition: pack.gen.cellGrid is null");
                    skipHeight = true;
                    break;
                }
                if (pack.gen.cfg == null)
                {
                    Debug.LogError("TransformPosition: pack.gen.cfg is null");
                    skipHeight = true;
                    break;
                }
                if (pack.gen.rooms == null)
                {
                    Debug.LogError("TransformPosition: pack.gen.rooms is null");
                    skipHeight = true;
                    break;
                }
            }
            break;  // break from while loop 
        }
        
        // 1) Hard guards with precise logs
        //if (!DungeonGenerator.Check(agent, "agent", this)) skipHeight = true;

        //var tr = (agent as MonoBehaviour)?.transform ?? null;
        //if (!DungeonGenerator.Check(tr, "agent.transform", agent as MonoBehaviour)) skipHeight = true;

        // if you keep a generator / grid on Agent:
        //if (!DungeonGenerator.Check(pack.gen, "gen", this)) skipHeight = true;
        //if (!DungeonGenerator.Check(pack.gen.cellGrid, "gen.cellGrid", this)) skipHeight = true;
        //if (!DungeonGenerator.Check(pack.gen.rooms, "gen.rooms", this)) skipHeight = true;


        //if (!DungeonGenerator.Check(pack.gen.cfg, "cfg", this)) skipHeight = true;

        if (!skipHeight)
        {
            int xx = Mathf.FloorToInt(agent.pos2.x);
            int yy = Mathf.FloorToInt(agent.pos2.y);
            cell = null;
            if (pack.gen.In(xx,yy))
                cell = pack.gen.cellGrid[xx, yy];
            if (cell != null)
            {
                agent.height = pack.gen.cfg.unitHeight * cell.height;
                //agent.height = SampleAgentHeight(agent.pos2, pack.gen.cellGrid, pack.gen.cfg.unitHeight);
            }
        }

        Cleanup(ref agent.pos2);

        if (useXZPlane)
        {
            Vector3 t; // = transform.position; // not necessary, we overwrite this value completely
            Vector2 t_World = Map_to_World(agent.pos2);
            t.x = t_World.x; t.z = t_World.y; // XZ location
            t.y = agent.height + 1;
            agent.transform.position = t;
            //pack.player.transform.position = t;
            agent.targetYawDeg = agent.yawDeg;
            TurnTowards(ref agent.yawDeg, agent.prevYawDeg, agent.targetYawDeg, agent.turnSpeedDegPerSec);
            agent.prevYawDeg = agent.yawDeg;
            agent.transform.rotation = Quaternion.Euler(0f, agent.yawDeg + yawCorrection, 0f); // rotate around Y for 3D
            //pack.player.transform.rotation = Quaternion.Euler(0f, agent.yawDeg + yawCorrection, 0f); // rotate around Y for 3D
            if (pack.player.agent == agent)
            {
                pack.player.transform.position = t;
                pack.player.transform.rotation = Quaternion.Euler(0f, agent.yawDeg + yawCorrection, 0f); // rotate around Y for 3D
            }
        }
        else
        {
            Vector3 t; // = transform.position; // not necessary, we overwrite this value completely
            Vector2 t_World = Map_to_World(agent.pos2);
            t.x = t_World.x; t.y = t_World.y; // XY location
            t.z = agent.height + 1;
            agent.transform.position = t;
            agent.targetYawDeg = agent.yawDeg;
            TurnTowards(ref agent.yawDeg, agent.prevYawDeg, agent.targetYawDeg, agent.turnSpeedDegPerSec);
            agent.prevYawDeg = agent.yawDeg;
            agent.transform.rotation = Quaternion.Euler(0f, 0f, agent.yawDeg + yawCorrection); // rotate around Z for XY
            if (pack.player.agent == agent)
            {
                pack.player.transform.position = t;
                pack.player.transform.rotation = Quaternion.Euler(0f, 0f, agent.yawDeg + yawCorrection); // rotate around Y for 3D
            }                                                                                       //pack.player.transform.rotation = Quaternion.Euler(0f, 0f, agent.yawDeg + yawCorrection); // rotate around Z for XY

        }
        return true;
    }

    public void TurnTowards(ref float currentYaw, float prevYawDeg, float targetYaw, float turnSpeedDegPerSec)
    {
        // Signed shortest angular delta (-180 to 180)
        float delta = Mathf.DeltaAngle(prevYawDeg, targetYaw);

        // If already aligned (or extremely close), snap to target
        if (Mathf.Abs(delta) < 0.01f)
        {
            currentYaw = targetYaw;
            return;
        }

        // Calculate how much we can turn this frame
        float maxStep = turnSpeedDegPerSec * Time.deltaTime;

        //Debug.Log($"vcamPerspective: {dir.vcamPerspective.Priority}, vcamFP: {dir.vcamFP.Priority}, vcamOverhead: {dir.vcamOverhead.Priority}");
        if (dir.vcamOverhead.Priority > Mathf.Max(dir.vcamFP.Priority, dir.vcamPerspective.Priority))
            maxStep *= 3f; // with Overhead camera, speed up turn or it looks odd.

        // Clamp the rotation so we don't overshoot
        float step = Mathf.Clamp(delta, -maxStep, maxStep);

        // Apply rotation
        currentYaw = prevYawDeg + step;
    }

    /// <summary>
    /// Determines whether to turn left (-1) or right (+1) to reach targetDir from startDir
    /// using the shortest angular direction. Returns 0 if already facing (within epsilon).
    /// </summary>
    public static int GetTurnDirection(float startDir, float targetDir, float epsilon = 0.01f)
    {
        // Normalize angles to 0–360
        startDir = Mathf.Repeat(startDir, 360f);
        targetDir = Mathf.Repeat(targetDir, 360f);

        // Difference in range -180 to +180
        float delta = Mathf.DeltaAngle(startDir, targetDir);

        if (Mathf.Abs(delta) < epsilon)
            return 0; // Already aligned (or close enough)

        return (delta > 0f) ? -1 : +1;
        // Positive delta means target is to the "left" (counterclockwise),
        // so return -1 to indicate turning left.
    }
    
    public float UnitVectorToYaw(Vector2 unit_vector)
    {
        return Mathf.Atan2(unit_vector.x, unit_vector.y) * Mathf.Rad2Deg - yawCorrection;
    }

    // apply offset from map coordinates to world coordinates
    public Vector2 Map_to_World(Vector2 map_loc)
    {
        Vector2 world_loc;
        world_loc.x = map_loc.x - xCorrection;
        world_loc.y = map_loc.y - yCorrection;
        return world_loc;
    }

    // Rounds a number to nearest .01 to eliminate tiny cumulative errors
    // Option to keep the destination within the same integer value
    public void CleanupFloat(ref float num, bool same_tile = true)
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
    public void Cleanup(ref Vector2 vect, bool same_tile = true)
    {
        CleanupFloat(ref vect.x, same_tile);
        CleanupFloat(ref vect.y, same_tile);
    }
    
    // Assumptions:
    // - agentPos is in tile/grid coordinates (1 unit per cell) on the XZ plane → (x,z) == (agentPos.x, agentPos.y)
    // - cellGrid[x,y] gives you the Room.Cell that contains:
    //     int height;                // base floor height at the cell center
    //     Quaternion tiltFloor;      // tilt of the floor plane
    // - Y is world up.
    // cellGrid dimensions are W x H
    public static float SampleAgentHeight(Vector2 agentPos, Cell[,] cellGrid, float unitHeight)
    {
        int W = cellGrid.GetLength(0);
        int H = cellGrid.GetLength(1);

        int cx = Mathf.FloorToInt(agentPos.x);
        int cz = Mathf.FloorToInt(agentPos.y);

        // Out-of-bounds guard (return 0 or whatever default you prefer)
        if (cx < 0 || cz < 0 || cx >= W || cz >= H)
            return 0f;

        var cell = cellGrid[cx, cz];

        // Plane normal from the tilt (rotate "up" by tilt)
        Quaternion q = cell.tiltFloor;
        Vector3 n = (q * Vector3.up).normalized;

        // Reference point P0: cell center at base height
        // (center is (cx+0.5, cz+0.5) in tile units; height is along world Y)
        Vector3 P0 = new Vector3(cx + 0.5f, cell.height, cz + 0.5f);

        // Point X we want: (agentPos.x, y, agentPos.y). Solve n · (X - P0) = 0 for y.
        // n.x*(x - P0.x) + n.y*(y - P0.y) + n.z*(z - P0.z) = 0
        // => y = P0.y - (n.x*(x-P0.x) + n.z*(z-P0.z)) / n.y
        float x = agentPos.x;
        float z = agentPos.y;

        // Avoid division by ~0 if someone gave an extreme tilt
        float ny = Mathf.Abs(n.y) < 1e-5f ? Mathf.Sign(n.y) * 1e-5f : n.y;

        float y =
            P0.y - (n.x * (x - P0.x) + n.z * (z - P0.z)) / ny;

        return y * unitHeight; // convert from height units to world units
    }

}
