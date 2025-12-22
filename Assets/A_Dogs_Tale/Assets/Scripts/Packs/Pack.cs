using System.Collections.Generic;
using UnityEngine;
using DogGame.Modules;
using System;

public class Pack : MonoBehaviour
{
    public Directory dir;

    //public GameObject icon;           // Optional visual symbol representing this pack.
    public BreadcrumbTrail trail;       // List of locations to follow.  Normally left by leader, but maybe from pathfinding or patrol points

    [Header("Current Pack")]
    public String packName = "Unnamed Pack";
    // pack related parameters:
    public List<WorldObject> packAgentList;   // list of agents (WorldObjects)
    public FormationsEnum formation = FormationsEnum.Wedge;
    public AgentDecisionType leadershipType = AgentDecisionType.Immobile;   // DecisionType assigned to leader on re-arrange.
    public AgentDecisionType followerType = AgentDecisionType.Follower;     // DecisionType assigned to followers on re-arrange.
    public float formationSpacing = 1.5f;  // spacing between members in formation
    
    public bool isPlayerPack => this==dir.playerPack;   // only one pack is controlled by player
    public WorldObject packLeader => packAgentList[0];  // Leader is always first pack member           
    
    void Start()
    {
        if (dir!=null && dir.packManager!=null && dir.packManager.PackParentObject!=null)
        // move this Pack object to under the "Packs" object in the hierarchy
        this.gameObject.transform.SetParent(dir.packManager.PackParentObject.transform, worldPositionStays: false);
    }

    void Awake()
    {
        // --- packAgentsList ---
        if (packAgentList==null) 
            packAgentList=new();
        
        // --- Breadcrumb Trail ---
        if (!trail)
        {
            trail = FindFirstObjectByType<BreadcrumbTrail>();
            if (!trail)
                Debug.LogWarning("[Pack] No BreadcrumbTrail found — trail tracking disabled.");
            //else
            //    Debug.Log($"[Pack] Connected to BreadcrumbTrail: {trail.name}");
        }
    }

    public void InitializeConnections()
    {

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
        //Debug.Log($"AddMember({agent.DisplayName}, {setAsLeader})");
        if ((agent==null) || (agent.packMemberModule==null))
            return false;   // not a valid pack member

        //if (packAgentList==null) 
            //packAgentList=new();
        if (!packAgentList.Contains(agent))
        {
            packAgentList.Add(agent);
            agent.packMemberModule.currentPack = this;
            Debug.Log($"Pack {packName} added member {agent.DisplayName}");
            if(setAsLeader) 
                SetLeader(agent);
            else 
                SetFollower(agent);
            
            // Move agent under pack in object hierarcy.
            agent.gameObject.transform.SetParent(this.gameObject.transform,false);

            return true;
        }
        return false;
    }

    public bool SetFollower(WorldObject agent)
    {
        if (agent==null) return false;  // cannot have a null agent.
        if (agent==packLeader) return false;  // cannot follow self.

        agent.agentModule.SwitchDecisionModule(AgentDecisionType.Follower);
    
        int index = GetPositionInPack(agent);
        float distance = formationSpacing * index;
        agent.followerDecisionModule.SetFollowTarget(packLeader.transform, distance);

        return true;
    }

    public bool SetLeader(WorldObject agent)
    {
        if (agent==null)
        {
            Debug.LogError($"[Pack {packName} SetLeader]  agent==null]");
            return false;  // cannot have a null agent.
        }
        if (!packAgentList.Contains(agent)) 
        {
            Debug.LogError($"Agent {agent.DisplayName} requested to be leader, but is not a member of this pack {packName}");
            return false; // not already part of this pack
        }
        // demote current leader to be a follower.
        if (packLeader != null)
        {
            packLeader.agentModule.SwitchDecisionModule(AgentDecisionType.Follower);
        }

        // set behavior for leader (Player if this is PlayerPack, otherwise Wanderer)
        agent.agentModule.SwitchDecisionModule(leadershipType);
        
        // move leader to front of packList
        packAgentList.Remove(agent);
        packAgentList.Insert(0,agent);

        SetPackFollowChain();
        return true;
    }

    // Changes leader and followers to appropriate types,
    // and to follow at correct interval spacing.
    public void SetPackFollowChain()
    {
        packLeader.agentModule.SwitchDecisionModule(leadershipType);
        // change all followers to follow this new leader in order, spaced apart.
        float distance = 0f;
        WorldObject member;
        for (int idx=1; idx<packAgentList.Count; idx++)
        {
            member = packAgentList[idx];
            distance = formationSpacing * idx;
            member.followerDecisionModule.SetFollowTarget(packLeader.transform, distance);
        }
    }

    public bool RemoveMember(WorldObject agent)
    {
        Debug.Log($"RemoveMember({agent.DisplayName})");
        if ((agent==null) || (agent.packMemberModule==null))
            return false;   // not a valid pack member

        if (packAgentList.Contains(agent))
        {
            // don't remove last member of PlayerPack
            if (isPlayerPack && packAgentList.Count==1)
            {
                Debug.Log("Only member of PlayerPack cannot leave the pack");
                return false;
            }
            
            packAgentList.Remove(agent);
            agent.packMemberModule.currentPack = null;
            // TODO: clear leader: agent.packMemberModule.IsLeader
            //.      pick a new leader?
            Debug.Log($"Pack removed member {agent.DisplayName}. Remaining {this.ToString()}");
            
            SetPackFollowChain();
            return true;
        }
        return false;
    }

    public override String ToString()
    {
        String str;
        Debug.Log("Pack.ToString()");
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

