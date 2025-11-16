using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
public partial class DungeonGenerator : MonoBehaviour
{
    public System.Random rng;

    // 2D assets defined in Unity.  These are verified and re-connected if necessary.
    public Tilemap tilemap; // floors
    public Tilemap tilemap_walls;
    public Tilemap tilemap_doors;
    public TileBase floorTile;
    public TileBase wallTile;
    public TileBase doorClosedTile;
    public TileBase doorOpenTile;

    // 3D assets defined in Unity.  These are verified and re-connected if necessary.
    public Grid grid;                         // same Grid as the 2D Tilemap
    public GameObject floorPrefab;
    public GameObject rampPrefab;             // oriented to face +Z
    public GameObject cliffPrefab;            // a 1x1x1 pillar you can scale in Y
    public GameObject diagonalWallPrefab;    // thin strip or quad oriented along +Z
    public GameObject triangleFloorPrefab;   // half of a floor tile
    public GameObject doorClosedPrefab;
    public GameObject doorOpenPrefab;
    public GameObject fogPrefab;
    public GameObject scentAirPrefb;
    public GameObject scentGroundPrefab;
    public Transform root;                    // parent for spawned meshes

    public static readonly Color colorDefault = new(1f, 0.4f, 0.7f, 0.5f); // semi-transparent pink

    // Master list of Rooms makes the current map
    public List<Room> rooms = new(); // Master List of rooms including list of points and metadata


    // These global lists help lookup things quickly
    // These are handled in Room class now.
    //public HashSet<Vector2Int> floor_hash_map = new();
    //public HashSet<Vector2Int> wall_hash_map = new();

    // Future use by Door capability...
    Dictionary<int, Door> doorById; // partner lookup and save/load

    // global perlin seed
    public float perlinSeedX = 0;
    public float perlinSeedY = 0;

    // global variables for return of success and failure results (some functions only)
    [HideInInspector] public bool success;    // global generic return value from various tasks
    [HideInInspector] public string failure;    // global failure description string

    // globally incremented counter across all Agents.
    private int lastIssuedAgentId = 0;      // allows giving agents unique ID's

    // list of directions for neighbor checks
    public Vector2Int[] directions_xy = { Vector2Int.up,
                                   Vector2Int.down,
                                   Vector2Int.left,
                                   Vector2Int.right, };
    //       Vector2Int.up + Vector2Int.left,
    //       Vector2Int.up + Vector2Int.right,
    //       Vector2Int.down + Vector2Int.left,
    //       Vector2Int.down + Vector2Int.right };

    public ScentAirGround scent;

    public ScentRegistry scentRegistry;

    public List<Agent> agentRegistry;

    // Sets the agent id to a unique number, and returns that value.
    // Can be called without an agent, and caller must assign the number themselves.
    // ID is used to (a) determine equivalence match, (b) track id of some event in a list.
    public int GetNewAgentId(Agent agent)
    {
        lastIssuedAgentId++;
        if (agent != null) agent.id = lastIssuedAgentId;

        // add to the agentRegistry
        if (agentRegistry==null) agentRegistry=new();   // make sure list exists

        agentRegistry.Add(agent);
        if (agentRegistry.Count != agent.id)    // consistancy check
            Debug.LogError($"GetNewAgentId: agentRegistry (size={agentRegistry.Count}) has wrong number of agentIds for new agent {agent.id}");

        Debug.Log($"GetNewAgenId({agent}) = {agent.id})");
        return lastIssuedAgentId;
    }
}



public partial class DungeonGenerator : MonoBehaviour
{
    // ---- call this from Awake() or keep this whole block in this file ----
    [Header("Auto-Init")]
    [SerializeField] string rootName = "DungeonGenerator";
    [SerializeField] string floorsName = "Floors3D";
    [SerializeField] string wallsName = "Walls3D";
    [SerializeField] string doorsName = "Doors3D";
    [SerializeField] int rngSeed = 0; // 0 = random

    // runtime containers
    Transform floors3D, walls3D, doors3D;

    void Awake_Tilemap()
    {
        InitializeTilemapConnections();
        BuildRuntimeParents();
        InitRng();
        SceneManager.sceneLoaded += OnSceneLoaded_Reconnect;
    }

    void OnDestroy_Tilemap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded_Reconnect;
    }

    void OnSceneLoaded_Reconnect(Scene s, LoadSceneMode m)
    {
        // Rebind scene objects if the scene changed
        InitializeTilemapConnections();
        BuildRuntimeParents();
    }

    void InitializeTilemapConnections()
    {
        // --- 2D Grid & Tilemaps ---
        if (!grid) grid = FindFirstObjectByType<Grid>(FindObjectsInactive.Include);

        if (!tilemap || !tilemap_walls || !tilemap_doors)
        {
            var tms = FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var tm in tms)
            {
                var name = tm.name.ToLowerInvariant();
                if (!tilemap && (name.Contains("floor") || name.Contains("ground"))) tilemap = tm;
                if (!tilemap_walls && name.Contains("wall")) tilemap_walls = tm;
                if (!tilemap_doors && name.Contains("door")) tilemap_doors = tm;
            }
        }

        // --- Prefabs (optional Resources fallback) ---
        TryLoadIfNull(ref floorPrefab, "Prefabs/Terrain/PF_Floor");
        TryLoadIfNull(ref rampPrefab, "Prefabs/Terrain/PF_Ramp");
        TryLoadIfNull(ref cliffPrefab, "Prefabs/Terrain/PF_Cliff");
        TryLoadIfNull(ref diagonalWallPrefab, "Prefabs/Terrain/PF_Diagonal");
        TryLoadIfNull(ref triangleFloorPrefab, "Prefabs/Terrain/PF_Triangle_Floor");
        TryLoadIfNull(ref doorClosedPrefab, "Prefabs/Terrain/PF_DoorClosed");
        TryLoadIfNull(ref doorOpenPrefab, "Prefabs/Terrain/PF_DoorOpen");
        TryLoadIfNull(ref fogPrefab, "Prefabs/Terrain/PF_Fog");

        TryLoadTileIfNull(ref floorTile, "Tiles/floorTile");
        TryLoadTileIfNull(ref wallTile, "Tiles/wallTile");
        TryLoadTileIfNull(ref doorClosedTile, "Tiles/doorClosedTile");
        TryLoadTileIfNull(ref doorOpenTile, "Tiles/doorOpenTile");

        // --- Root parent for spawned meshes ---
        if (!root)
        {
            var go = GameObject.Find(rootName);
            if (!go) go = new GameObject(rootName);
            root = go.transform;
        }
        root.gameObject.SetActive(true);
    }

    void BuildRuntimeParents()
    {
        floors3D = EnsureChild(root, floorsName);
        walls3D = EnsureChild(root, wallsName);
        doors3D = EnsureChild(root, doorsName);
    }

    void InitRng()
    {
        rng = (rngSeed == 0) ? new System.Random() : new System.Random(rngSeed);
    }

    static void TryLoadIfNull(ref GameObject field, string resourcesPath)
    {
        if (!field)
        {
            //Debug.Log($"Loading resource {resourcesPath}");
            var loaded = Resources.Load<GameObject>(resourcesPath);
            if (loaded)
            {
                field = loaded;
                //Debug.Log($"Success {resourcesPath}");
            }
            else
            {
                Debug.Log($"Failure to load {resourcesPath}");
            }
        }
    }

    static void TryLoadTileIfNull(ref TileBase tile, string path)
    {
        if (!tile)
        {
            var loaded = Resources.Load<TileBase>(path);
            if (loaded)
                tile = loaded;
            else
                Debug.Log($"[DungeonGenerator] Tile not found at Resources/{path}");
        }
    }
    static Transform EnsureChild(Transform parent, string childName)
    {
        var t = parent.Find(childName);
        if (!t)
        {
            var go = new GameObject(childName);
            t = go.transform;
            t.SetParent(parent, false);
        }
        return t;
    }

    /*
    #if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying)
            {
                // light-touch editor wiring
                if (!grid) grid = FindFirstObjectByType<Grid>(FindObjectsInactive.Include);
                if (root && root.parent != null) root.SetParent(null); // keep root at top level
            }
        }
    #endif
    */

}
