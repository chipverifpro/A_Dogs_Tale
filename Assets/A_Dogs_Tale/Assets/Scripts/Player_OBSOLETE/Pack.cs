using System.Collections.Generic;
using UnityEngine;
using DogGame.Modules;
using System;

public class Pack : MonoBehaviour
{
    public Directory dir;
    //public Player player;   // reference to player class, which handles all the player inputs
    public DungeonGenerator gen;
    public Transform PackParentObject;  // Parent object that already exists in the scene.  All the PlayerAgents will be attached under it.
    //public GameObject agentVisual;  // Optional visual (e.g., Capsule/Cube). Can be null.
    public BreadcrumbTrail trail;   // The BreadcrumbTrail class
    //public BreadcrumbTrail breadcrumbTrailPrefab; // Assign this in the Inspector.  It is a prefab visualization for what?  Breadcrumb trail or each breadcrumb?
    //public BreadcrumbTrail trailPrefabObj; // An object representing the trail (I think)

    [Header("Current Pack")]
    public String packName = "Unnamed Pack";
    // pack related parameters:
    //public Agent packLeaderLegacy;            // LEGACY current leader, (usually controlled by player)
    //public List<Agent> packListLegacy;        // LEGACY: All pack members
    public WorldObject packLeader;            // WorldObjects
    public List<WorldObject> packAgentList;   // WorldObjects
    //public bool inFollowFormation = true;
    //public bool inGroupFormation = false;
    //public bool soloMode = false;       // not travelling as a pack
    public FormationsEnum formation = FormationsEnum.Wedge;
    public float formationSpacing = 1.5f;  // spacing between members in formation
    

    void Start()
    {
        if (PackParentObject == null)
        {
            Debug.LogError("Pack (parent) is not assigned.");
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
    }

    public void TeleportToLeader()
    {
        if (packLeader == null)
        {
            Debug.LogWarning("packLeader is null; cannot teleport pack members.");
            return;
        }

        Vector2 leaderPos2 = packLeader.locationModule.pos2;
        float leaderHeight = packLeader.locationModule.height;
        Crumb leaderCrumb = new Crumb()
        {
            pos2 = leaderPos2,
            height = leaderHeight,
            valid = true,
            yawDeg = packLeader.locationModule.yawDeg
        };

        foreach (var member in packAgentList)
        {
            if (member != null && member != packLeader)
            {
                member.motionModule.Teleport(new Vector3(leaderPos2.x, leaderHeight, leaderPos2.y));
                member.appearanceModule.camera_refresh_needed = true;
                //member.next_formationCrumb.valid = false; // clear formation target
                Debug.Log($"Teleported {member.name} to leader at {leaderPos2.x}, {leaderPos2.y}, {leaderHeight}");
            }
        }
        trail.ClearCrumbs();
        trail.RecordIfNeeded(true); // force record after teleport

    }

    // ===== Interface Functions =====

    // returns true if the new agent was added to the pack.
    public bool AddMember(WorldObject agent, bool setAsLeader)
    {
        if ((agent==null) || (agent.agentPackMemberModule==null))
            return false;   // not a valid pack member

        if (!packAgentList.Contains(agent))
        {
            packAgentList.Add(agent);
            agent.agentPackMemberModule.currentPack = this;
            // TODO: set leader: agent.agentPackMemberModule.IsLeader
            Debug.Log($"Pack added member {agent.DisplayName}. Remaining {this.ToString()}");
            return true;
        }
        return false;
    }

    public bool RemoveMember(WorldObject agent)
    {
        if ((agent==null) || (agent.agentPackMemberModule==null))
            return false;   // not a valid pack member
        
        if (!packAgentList.Contains(agent))
        {
            packAgentList.Remove(agent);
            agent.agentPackMemberModule.currentPack = null;
            // TODO: clear leader: agent.agentPackMemberModule.IsLeader
            //.      pick a new leader?
            Debug.Log($"Pack removed member {agent.DisplayName}. Remaining {this.ToString()}");
            return true;
        }
        return false;
    }

    public override String ToString()
    {
        String str;
        str = $"Pack {packName}";
        str += $" Leader {packLeader.DisplayName}";
        str += $" [{packAgentList.Count} members]:";
        foreach (var member in packAgentList)
        {
            str += $" {member.DisplayName},";
        }
        return str;
    }

    public void SetFormation(FormationsEnum new_formation)
    {
        formation = new_formation;
    }

    public FormationsEnum GetFormation()
    {
        return formation;
    }

    // returns -1 if not in the pack.
    public int GetPositionInPack(WorldObject agent)
    {
        for (int pos=0; pos<packAgentList.Count; pos++)
        {
            if (packAgentList[pos] == agent) return pos;
        }
        return -1;
    }

}

