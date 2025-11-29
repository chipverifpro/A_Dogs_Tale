using System.Collections.Generic;
using UnityEngine;

public class Pack : MonoBehaviour
{
    public Directory dir;
    public Player player;   // reference to player class, which handles all the player inputs
    public DungeonGenerator gen;
    public Transform PackParentObject;  // Parent object that already exists in the scene.  All the PlayerAgents will be attached under it.
    public GameObject agentVisual;  // Optional visual (e.g., Capsule/Cube). Can be null.
    public BreadcrumbTrail trail;   // The BreadcrumbTrail class
    //public BreadcrumbTrail breadcrumbTrailPrefab; // Assign this in the Inspector.  It is a prefab visualization for what?  Breadcrumb trail or each breadcrumb?
    //public BreadcrumbTrail trailPrefabObj; // An object representing the trail (I think)

    [Header("Current Pack")]
    // pack related parameters:
    public Agent PackLeader;            // current leader, (usually controlled by player)
    public List<Agent> packList;        // All pack members
    public bool inFollowFormation = true;
    public bool inGroupFormation = false;
    public bool soloMode = false;       // not travelling as a pack
    public FormationsEnum formation = FormationsEnum.Wedge;
    public float formationSpacing = 1.5f;  // spacing between members in formation
    

    void Start()
    {
        if (PackParentObject == null)
        {
            Debug.LogError("Pack (parent) is not assigned.");
            return;
        }
        // --- Dungeon Generator ---
        if (!gen)
        {
            gen = FindFirstObjectByType<DungeonGenerator>();
            if (!gen)
                Debug.LogError("[Start:Pack] Could not find DungeonGenerator in scene!");
            else
                Debug.Log($"[Start:Pack] Connected to DungeonGenerator: {gen.name}");
        }
    }

    void Awake()
    {
        InitializeConnections();
    }

    public void InitializeConnections()
    {
        // --- Player ---
        if (!player)
        {
            player = FindFirstObjectByType<Player>();
            if (!player)
                Debug.LogError("[Pack] Could not find Player in scene!");
            else
                Debug.Log($"[Pack] Connected to Player: {player.name}");
        }

        // --- Dungeon Generator ---
        if (!gen)
        {
            gen = FindFirstObjectByType<DungeonGenerator>();
            if (!gen)
                Debug.LogError("[Pack] Could not find DungeonGenerator in scene!");
            else
                Debug.Log($"[Pack] Connected to DungeonGenerator: {gen.name}");
        }

        // --- Breadcrumb Trail ---
        if (!trail)
        {
            trail = FindFirstObjectByType<BreadcrumbTrail>();
            if (!trail)
                Debug.LogWarning("[Pack] No BreadcrumbTrail found — trail tracking disabled.");
            else
                Debug.Log($"[Pack] Connected to BreadcrumbTrail: {trail.name}");
        }

        // --- Parent object for agents ---
        if (!PackParentObject)
        {
            var parent = GameObject.Find("PackParent");
            if (parent)
            {
                PackParentObject = parent.transform;
                Debug.Log($"[Pack] Found PackParentObject: {PackParentObject.name}");
            }
            else
            {
                // Create one if missing
                GameObject newParent = new GameObject("PackParent");
                PackParentObject = newParent.transform;
                Debug.Log($"[Pack] Created PackParentObject: {PackParentObject.name}");
            }
        }

        // --- Optional: find a default visual prefab ---
        if (!agentVisual)
        {
            agentVisual = Resources.Load<GameObject>("Prefabs/AgentVisual");
            if (agentVisual)
                Debug.Log("[Pack] Loaded agentVisual prefab from Resources/Prefabs/AgentVisual");
        }
    }

    public void TeleportToLeader()
    {
        if (PackLeader == null)
        {
            Debug.LogWarning("PackLeader is null; cannot teleport pack members.");
            return;
        }

        Vector2 leaderPos2 = PackLeader.pos2;
        float leaderHeight = PackLeader.height;
        Crumb leaderCrumb = new Crumb()
        {
            pos2 = leaderPos2,
            height = leaderHeight,
            valid = true,
            yawDeg = PackLeader.yawDeg
        };

        foreach (var member in packList)
        {
            if (member != PackLeader)
            {
                member.pos2 = leaderPos2;
                member.height = leaderHeight;
                member.camera_refresh_needed = true;
                member.next_formationCrumb.valid = false; // clear formation target
                Debug.Log($"Teleported {member.name} to leader at {leaderPos2.x}, {leaderPos2.y}, {leaderHeight}");
            }
        }
        trail.ClearCrumbs();
        trail.RecordIfNeeded(true); // force record after teleport

    }
    
}

