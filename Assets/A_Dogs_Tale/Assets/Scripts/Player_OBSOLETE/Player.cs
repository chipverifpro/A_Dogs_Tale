using System;
using System.Collections;
using UnityEngine;


// TODO list:
//   DONE: add movement on diagonal walls
//   DONE add start on map floor tile
//   DONE: add height to movement
//   switch to heightmap instead of grid to allow movement with vertical stacking
//   move all params into this file.


public partial class Player : MonoBehaviour
{
    [Header("Refs")]
    public Directory dir;
    public DungeonGenerator gen;         // assign in Inspector (has cellGrid, rooms, etc.)
    public BottomBanner bottomBanner;

    public GameObject PackGameObject;   // Assign your parent GameObject in the Inspector
    //public GameObject DogPrefab;        // Optional: prefab to give each agent a visible model

    public Pack pack;                   // pack structure

    [Header("Current Player position")]
    public Agent agent;             // LEGACY: Everything to do with the currently active player
    public WorldObject agentObject; // module based

    //public Vector2 pos2;          // XY or XZ (depending on useXZPlane)
    //public float yawDeg;          // facing yaw in degrees (around Z for XY, around Y for XZ)
    //public int floorHeight = 1;   // height of current tile.

    public Vector3 destination;      // world position we are moving toward

    [Header("Unique Agent Parameters")]
    public float baseSpeed = 6.0f;       // W/S movement world units per second
    public float turnSpeedDegPerSec = 180f;     // A/D rotate speed
    [Range(0.1f, 0.49f)]
    public float radius = 0.30f;         // collision radius inside a 1x1 cell
    public Color color1 = Color.black;  // top color
    public Color color2 = Color.white;  // bottom color (or outline)

    [Header("Movement")]
    public float snapToCardinalDegrees = 10f;
    public bool snapEightWay = true;            // if false, only snap to 4 cardinal directions
    public float slopeUphillFactor = 0.85f; // (stub) scale speed a bit uphill
    public float slopeDownhillFactor = 1.08f;

    [Header("Player to Walls adjustment")]
    public float xCorrection = 0.5f;
    public float yCorrection = 0.5f;
    public float yawCorrection = 90f;
    public float heightCorrection = 1f;

    [HideInInspector]
    public bool camera_refresh_needed = true;   // self-clears after camera updates

    // Tuning internal parameters
    
    public bool useXZPlane = true;      // false = XY floor (tilemap), true = XZ floor (3D)
    [HideInInspector]
    public int constraintIters = 3;      // how many passes to resolve against edges

    public void Awake()
    {
        // if references are missing, find them.
        InitializeConnections();
        if (!gen)
            gen = FindAnyObjectByType<DungeonGenerator>();
        if (!bottomBanner)
            bottomBanner = FindAnyObjectByType<BottomBanner>();
        //if (agent == null)
        //ChangePlayerAgent(pack.packLeaderLegacy);
        AwakeMouseInput();
    }

    void InitializeConnections()
    {
        // --- DungeonGenerator ---
        if (!gen)
        {
            gen = FindAnyObjectByType<DungeonGenerator>();
            if (!gen)
                Debug.LogError("[Player] Could not find DungeonGenerator in scene!");
            else
                Debug.Log($"[Player] Connected to DungeonGenerator: {gen.name}");
        }

        // --- BottomBanner (UI) ---
        if (!bottomBanner)
        {
            bottomBanner = FindAnyObjectByType<BottomBanner>();
            if (!bottomBanner)
                Debug.LogWarning("[Player] BottomBanner not found — UI updates will be skipped.");
            else
                Debug.Log($"[Player] Connected to BottomBanner: {bottomBanner.name}");
        }

        // --- Pack GameObject ---
        if (!PackGameObject)
        {
            // Try to find an existing Pack object in the scene
            var foundPackGO = GameObject.Find("PackParent") ??
                              GameObject.Find("Pack") ??
                              GameObject.FindWithTag("Pack");

            if (foundPackGO)
            {
                PackGameObject = foundPackGO;
                Debug.Log($"[Player] Found Pack GameObject: {PackGameObject.name}");
            }
            else
            {
                // Create one if it doesn’t exist yet
                PackGameObject = new GameObject("PackParent");
                Debug.Log("[Player] Created new PackParent GameObject.");
            }
        }

        // --- Pack component ---
        if (!pack)
        {
            // Try to find one in scene or on the PackGameObject
            pack = FindAnyObjectByType<Pack>();
            if (!pack && PackGameObject)
                pack = PackGameObject.GetComponent<Pack>();

            // Create one if still missing
            if (!pack && PackGameObject)
            {
                pack = PackGameObject.AddComponent<Pack>();
                Debug.Log("[Player] Created new Pack component on PackParent.");
            }

            if (pack)
            {
                pack.player = this;   // link player reference
                //if (gen && !pack.gen)
                //    pack.gen = gen;       // link generator
                pack.PackParentObject = PackGameObject.transform;
                //pack.InitializeConnections?.Invoke(); // optional if Pack has its own init
                Debug.Log($"[Player] Linked Pack: {pack.name}");
            }
            else
            {
                Debug.LogWarning("[Player] Pack could not be found or created!");
            }
        }
    }

    void Start()
    {
        //StartCoroutine(DetermineStartPosition());   // background task waits for generator to complete before choosing starting location
        Move_Start();           // grab initial position from Unity object
                                //agent.trail = GetComponent<BreadcrumbTrail>();
                                //BuildPackObjects(3);    // This exists in Pack class.
        //pack.packAgentList.Add(pack.packLeaderLegacy); // leader agent needs to be added to the packlist.
        ChangePlayerAgent(pack.packLeader);
    }

    void Update()
    {
        if (!gen.buildComplete) return; // wait until build is complete

        Input_Update();  // this is the update for inputs and resulting movement
                         // Input_Update will call Move_Update with the appropriate parameters.
    }

    public IEnumerator DetermineStartPosition()
    {
        // wait until build completes
        yield return null;
        yield return new WaitUntil(() => gen.buildComplete);
        if (dir.gen.hf==null)
        {
            Debug.LogError($"DetermineStartPosition needs a valid heightfield (dir.gen.hf==null).  Fallback to 0,0");
            agentObject?.motionModule?.Teleport(new(0f,0f,0.5f));   // move to near origin
            yield break;    // exit
        }
        // randomly pick a start location and direction.
        int x = -1;             // random
        int y = -1;             // random
        int nearHeight = 0;     // random: heightfield will look for the floor nearest to this height.
        int height = 0;         // this is the found height.
        int newYawDeg = 0;      // random: facing direction
        bool facingWall = true; // heightfield will determine if there is a wall in facing direction.
        DungeonGenerator.NeighborMatch match; // heightfield result
        int iterations = 0;
        int max_iterations = 100;
        while ((!gen.In(x, y)) || (gen.cellGrid[x, y].room_number < 0) || (facingWall))
        {
            iterations++;
            if (iterations>=max_iterations)
            {
                Debug.LogError($"DetermineStartPosition failed {iterations} times.  Fallback to 0,0");
                agentObject?.motionModule?.Teleport(new(0f,0f,0.5f));   // move to near origin
                yield break;    // exit
            }
            // try a new random location
            x = UnityEngine.Random.Range(0, gen.cfg.mapWidth);
            y = UnityEngine.Random.Range(0, gen.cfg.mapHeight);
            nearHeight = UnityEngine.Random.Range(-200, 200);   // should be reasonable min/max height
            newYawDeg = UnityEngine.Random.Range(0, 4) * 90;
            // check if yawDeg is facing a wall
            DirFlags facingDirFlag = DirFlagsEx.YawToDirFlag(newYawDeg);
            DirFlags wallFlags = DirFlags.None;
            bool success = dir.gen.hf.TryQueryAt(x, y, nearHeight, 9999, out match);
            if (success)
            {
                height = match.z;
                wallFlags = match.walls;
                facingWall = (facingDirFlag & wallFlags) != DirFlags.None;
            }
        }
        
        Vector3 worldPosition = new(x+0.5f, height, y+0.5f);
        Quaternion yawQuat = Quaternion.Euler(0, newYawDeg, 0);
        agentObject?.motionModule?.TeleportUpright(worldPosition, yawQuat);

        //agentObject.locationModule.pos2.x = x + 0.5f;  // center of cell
        //agentObject.locationModule.pos2.y = y + 0.5f;

        //agent.height = gen.cellGrid[x, y].height + (int)heightCorrection;  // height of current cell floor.
         //Debug.Log($"Start pos = {agent.pos2.x}, {agent.pos2.y}, height={agent.height}");
        //agentObject.motionModule.Teleport(agentObject.locationModule.pos3d_world);    // move the player's agent
        
        //agentObject.appearanceModule.camera_refresh_needed=true;
        Debug.Log($"Set StartPosition to Grid {x}, {y}, {height}");
        pack.TeleportToLeader();
    }

    // Change which agent the player is controlling...
    void ChangePlayerAgent(WorldObject new_agent)
    {
        if (agentObject==null || new_agent==null) return;
        bool old_active = agentObject.appearanceModule.IsEnabled();
        agentObject = new_agent;
        //agentObject.appearanceModule.SetVisible(true);    // if old prefab was hidden by the first-person camera, bring it back
        agentObject.agentPackMemberModule.RequestBecomeLeader();
        //agentObject.agentPackMemberModule.trailFollower = false;
        agentObject.appearanceModule.camera_refresh_needed = true;   // camera visibility refresh
        agentObject.appearanceModule.SetVisible(old_active);  // if old was invisible, make new one also invisible.
        pack.packLeader = agentObject;
        pack.trail.leader = agentObject;
        agentObject.motionModule.Teleport(agentObject.locationModule.pos3d_world);    // move the player's agent
        //Move_Update(0f, 0f);    // screen refresh
        BottomBanner.ShowFor($"New leader = {agentObject.DisplayName}", 5f);
    }

    // Change which agent the player is controlling...
    // old leader agent becomes a follower, and new agent becomes leader.
    // new agent moves to front of pack order.
    void ChangePlayerAgentById(int new_agent_id)
    {
        int old_leader_id = 0;
        int old_leader_index = -1;
        for (int i = 0; i < pack.packAgentList.Count; i++)
            if (pack.packAgentList[i].agentPackMemberModule.IsLeader == true)   // old trailLeader
            {
                old_leader_id = pack.packAgentList[i].ObjectId;
                old_leader_index = i;

                //pack.packAgentList[i].trailLeader = false;
                //pack.packAgentList[i].trailFollower = true;
                break;
            }

        for (int i = 0; i < pack.packAgentList.Count; i++)
            if (pack.packAgentList[i].ObjectId == new_agent_id)    // new agent becomes leader
            {
                // remove old leader from trailLeader
                pack.packAgentList[old_leader_index].agentModule.BecomeFollower();

                WorldObject new_leader_agent = pack.packAgentList[i];
                // move new leader to front of list.
                pack.packAgentList.RemoveAt(i);
                pack.packAgentList.Insert(0, new_leader_agent);
                ChangeTrailEater(new_agent_id, old_leader_id);

                pack.packAgentList[0].agentPackMemberModule.RequestBecomeLeader();

                ChangePlayerAgent(pack.packAgentList[0]);    // player agent is now front of list
                break;
            }
    }

    // clean up the crumb list when new leader takes over.
    void ChangeTrailEater(int new_leader_id, int old_leader_id)
    {
        // experimental: just delete the whole crumbs list on leader change...
        pack.trail.crumbs.Clear();

        for (int c = 0; c < pack.trail.crumbs.Count; c++)
        {
            // put old and new followers on every breadcrumb as having eaten it.
            if (!pack.trail.crumbs[c].whichFollowersArrived.Contains(old_leader_id))
                pack.trail.crumbs[c].whichFollowersArrived.Add(old_leader_id);
            if (!pack.trail.crumbs[c].whichFollowersArrived.Contains(new_leader_id))
                pack.trail.crumbs[c].whichFollowersArrived.Add(new_leader_id);

            // remove the crumb if all followers have seen it.
            if (pack.trail.crumbs[c].whichFollowersArrived.Count == pack.packAgentList.Count)
            {
                pack.trail.crumbs.RemoveAt(c);
                c--;
            }
        }
    }
}