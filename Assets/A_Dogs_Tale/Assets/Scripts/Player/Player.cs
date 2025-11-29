using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

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
    public Agent agent;             // Everything to do with the currently active player
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
        //ChangePlayerAgent(pack.PackLeader);
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
        //pack.packList.Add(pack.PackLeader); // leader agent needs to be added to the packlist.
        ChangePlayerAgent(pack.PackLeader);
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

        // randomly pick a start location.
        int x = -1;
        int y = -1;
        //TODO fix this to use Heightmap instead of cellGrid
        while ((!gen.In(x, y)) || (gen.cellGrid[x, y].room_number < 0))
        {
            // try a new random location
            x = UnityEngine.Random.Range(0, gen.cfg.mapWidth);
            y = UnityEngine.Random.Range(0, gen.cfg.mapHeight);
        }
        agent.pos2.x = x + 0.5f;  // center of cell
        agent.pos2.y = y + 0.5f;

        //agent.height = gen.cellGrid[x, y].height + (int)heightCorrection;  // height of current cell floor.
        agent.height = Agent.SampleAgentHeight(agent.pos2, gen.cellGrid, gen.cfg.unitHeight);
         //Debug.Log($"Start pos = {agent.pos2.x}, {agent.pos2.y}, height={agent.height}");
        agent.TransformPosition(agent);    // move the player's agent
        
        agent.camera_refresh_needed=true;
        Debug.Log($"Set StartPosition to {agent.pos2.x}, {agent.pos2.y}, {agent.height}");
        pack.TeleportToLeader();
    }

    // Change which agent the player is controlling...
    void ChangePlayerAgent(Agent new_agent)
    {
        bool old_active = agent.DogPrefab.activeSelf;
        agent.DogPrefab.SetActive(true);    // if old prefab was hidden by the first-person camera, bring it back
        agent = new_agent;
        agent.trailLeader = true;
        agent.trailFollower = false;
        agent.camera_refresh_needed = true;   // camera visibility refresh
        agent.DogPrefab.SetActive(old_active);  // if old was invisible, make new one also invisible.
        pack.PackLeader = agent;
        pack.trail.leader = agent;
        agent.TransformPosition(agent);    // move the player's agent
        //Move_Update(0f, 0f);    // screen refresh
        BottomBanner.ShowFor($"New leader = {agent.name}", 5f);
    }

    // Change which agent the player is controlling...
    // old leader agent becomes a follower, and new agent becomes leader.
    // new agent moves to front of pack order.
    void ChangePlayerAgentById(int new_agent_id)
    {
        int old_leader_id = 0;
        int old_leader_index = -1;
        for (int i = 0; i < pack.packList.Count; i++)
            if (pack.packList[i].trailLeader == true)   // old trailLeader
            {
                old_leader_id = pack.packList[i].id;
                old_leader_index = i;

                //pack.packList[i].trailLeader = false;
                //pack.packList[i].trailFollower = true;
                break;
            }

        for (int i = 0; i < pack.packList.Count; i++)
            if (pack.packList[i].id == new_agent_id)    // new agent becomes leader
            {
                // remove old leader from trailLeader
                pack.packList[old_leader_index].trailLeader = false;
                pack.packList[old_leader_index].trailFollower = true;

                Agent new_leader_agent = pack.packList[i];
                // move new leader to front of list.
                pack.packList.RemoveAt(i);
                pack.packList.Insert(0, new_leader_agent);
                ChangeTrailEater(new_agent_id, old_leader_id);

                ChangePlayerAgent(pack.packList[0]);    // player agent is now front of list
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
            if (pack.trail.crumbs[c].whichFollowersArrived.Count == pack.packList.Count)
            {
                pack.trail.crumbs.RemoveAt(c);
                c--;
            }
        }
    }
}