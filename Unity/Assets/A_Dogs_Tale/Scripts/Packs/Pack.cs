using System.Collections.Generic;
using UnityEngine;
using DogGame.Modules;
using System;

// Functionality owned by...  (left column is implementation, right is closer to user)
//
// CreatePack - PackManager.cs
// FindPackByName - PackManager.cs
//
//* HandleRequestToJoinPack - PackMemberModule.cs << USER
//          --uses: LeaveCurrentPack - PackMemberModule.cs
//                  CreateNewPack - PackManager.cs
//                  AddMember - Pack.cs
//      * RequestLeavePack - PackMemberModule.cs << USER
//*  AddMember(isLeader) - Pack.cs
//*  RemoveMember - Pack.cs
//      * RequestLeadershipChange - PackMemberModule.cs << USER
//*  MoveAgentToLeader - Pack.cs
//*  ResetPackFollowChain - Pack.cs (included after any change)
//
//*  SetFormation - Pack.cs << USER
//*  GetFormation - Pack.cs << USER
//*  GetPositionInPack - Pack.cs
//      * RequestFormationOffset - PackMember.cs << Pathfinding.cs
//   GetFormationOffset - Pack.cs
//
//      * RequestGoToLeader(Teleport?) - PackMemberModule.cs << STARTUP / DecosionModule.csd
//      * RequestDistanceFromLeader - PackMemberModule
//   GetLeaderPosition - Pack.cs
//
// SetCameraFollower - CameraModeSwitcher.cs
//
// CreateModulesIfNeeded - WorldObject.cs
// EnsureComponent - WorldObject.cs


public class Pack : MonoBehaviour
{
    public Dir dir;

    //public GameObject icon;           // Future visual symbol representing this pack.
    public BreadcrumbTrail trail;       // List of locations to follow.  Normally left by leader, but maybe from pathfinding or patrol points

    [Header("Current Pack")]
    public String packName = "Unnamed Pack";
    public List<WorldObject> packAgentList;   // list of agents (WorldObjects)
    public FormationsEnum formation = FormationsEnum.Wedge;
    public AgentDecisionType leadershipType = AgentDecisionType.Wanderer;   // DecisionType assigned to leader on re-arrange.
    public AgentDecisionType followerType = AgentDecisionType.Follower;     // DecisionType assigned to followers on re-arrange.
    public float formationSpacing = 1.5f;  // spacing between members in formation

    // 'expression-bodied read-only properties' (yeah, that's what they are called)
    public bool isPlayerPack => this==dir.playerPack;   // only one pack is controlled by player
    public int agentCount => packAgentList.Count; // number of pack members.
    public WorldObject packLeader =>
        packAgentList != null && packAgentList.Count > 0 ? packAgentList[0] : null;
    
    void Start()
    {
        if (dir!=null && dir.packManager!=null && dir.packManager.PackParentObject!=null)
        {
            // move this Pack object to under the "Packs" object in the hierarchy
            this.gameObject.transform.SetParent(dir.packManager.PackParentObject.transform, worldPositionStays: false);
            //Debug.LogWarning($"[Pack.Start {this.gameObject.name}] set parent to {dir.packManager.PackParentObject.name}");
        } else
        {
            //problem.  Something isn't configured.
            Debug.LogError($"[Pack.Start {this.gameObject.name}] dir="+dir+" packManager="+dir.packManager+" PackParentObject="+dir.packManager.PackParentObject);
        }
    }

    void Awake()
    {
        // --- packAgentsList ---
        if (packAgentList==null) 
            packAgentList=new();
        
        // --- Breadcrumb Trail ---
        if (!trail)
        {
            trail = GetComponent<BreadcrumbTrail>();  // look for existing one first
            if (!trail)
            {
                // attach a Breadcrumb trail MonoBehavior
                trail = gameObject.AddComponent<BreadcrumbTrail>();
                trail.pack = this;
            }
        }

        //Move all initial pack members to under this pack in Unity Hierarchy
        // and any other initialization.
        foreach (WorldObject agent in packAgentList)
        {
            //Debug.LogWarning($"[Packs.Awake {gameObject.name}] setting parent of {agent.DisplayName} to {this.name}");
            agent.gameObject.transform.SetParent(this.gameObject.transform,false);
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
                Vector3 leaderMapPosition = new Vector3(leaderPos2.x, leaderHeight, leaderPos2.y);
                member.motionModule.Teleport(member.MapToWorldPosition(leaderMapPosition));
                member.appearanceModule.camera_refresh_needed = true;
                //member.next_formationCrumb.valid = false; // clear formation target
                Debug.Log($"Teleported {member.name} to leader at {leaderPos2.x}, {leaderPos2.y}, {leaderHeight}");
            }
        }
        trail.ClearCrumbs();
        trail.RecordIfNeeded(true); // force record after teleport

    }

    // ===== Interface Functions =====

    // returns true if the agent is now in the pack (including was already there).
    // if setAsLeader and the agent is already there but wasn't the leader, then moves agent to leader position.
    public bool AddMember(WorldObject agent, bool setAsLeader = false)
    {
        //Debug.Log($"AddMember({agent.DisplayName}, {setAsLeader})");
        if ((agent==null) || (agent.packMemberModule==null))
            return false;   // not a valid pack member

        if (!packAgentList.Contains(agent))
        {
            WorldObject leaderBeforeJoin = packLeader;
            bool shouldInheritLeaderWalkMode = leaderBeforeJoin != null;
            WalkMode leaderWalkMode = shouldInheritLeaderWalkMode
                ? GetWalkMode(leaderBeforeJoin, WalkMode.Walk)
                : WalkMode.Walk;

            // either insert or append agent to pack list.
            if (setAsLeader) packAgentList.Insert(0,agent);
            else packAgentList.Add(agent);

            // Notify agent of new pack.
            agent.packMemberModule.currentPack = this;
            // Move agent under pack in object hierarcy.
            agent.gameObject.transform.SetParent(this.gameObject.transform,false);

            if (shouldInheritLeaderWalkMode)
                SetWalkModeForMember(agent, leaderWalkMode);
            
            Debug.Log($"Pack {packName} added member {agent.DisplayName}");
            
            SetPackFollowChain();

            return true;
        } 
        else
        {
            if (setAsLeader && GetPositionInPack(agent)!=0)
            {
                MoveAgentToLeader(agent);
                SetPackFollowChain();
                Debug.Log($"Pack {packName} promoted member {agent.DisplayName} to leader");
                return true;
            }
            
        }
        return false;
    }

    // Changes leader and followers to appropriate types,
    // and to follow at correct interval spacing.
    // NOTE: If followerType is not Follower, add to the if below.
    public bool SetPackFollowChain()
    {
        if (packAgentList.Count == 0) return false;

        packLeader.agentModule.SwitchDecisionModule(leadershipType);
        // make sure cameras are following playerPack.packLeader
        if (isPlayerPack)
        {
            Debug.Log($"SetPackFollowChain: packLeader of playerPack = {packLeader.DisplayName}");
            if (packLeader.appearanceModule==null)
            {
                packLeader.CreateModulesIfNeeded(ModuleFlags.appearanceModule);
                Debug.Log($"SetPackFollowChain: packLeader of playerPack = {packLeader.DisplayName}.  Added missing appearanceModule");
            }
            packLeader.appearanceModule.SetCameraFollow();
        };
        // change all followers to follow this new leader in order, spaced apart.
        float distance = 0f;
        WorldObject member;
        for (int idx=1; idx<packAgentList.Count; idx++)
        {
            member = packAgentList[idx];
            member.agentModule.SwitchDecisionModule(followerType);
            distance = formationSpacing * idx;
            if (followerType == AgentDecisionType.Follower)
                member.followerDecisionModule.SetFollowTarget(packLeader, distance);
        }
        return true;
    }

    public int SetWalkMode(WalkMode walkMode)
    {
        if (packAgentList == null)
            return 0;

        int changedCount = 0;
        foreach (WorldObject member in packAgentList)
        {
            if (member == null)
                continue;

            if (SetWalkModeForMember(member, walkMode))
                changedCount++;
        }

        return changedCount;
    }

    private static bool SetWalkModeForMember(WorldObject member, WalkMode walkMode)
    {
        if (member == null)
            return false;

        if (member.agentMovementModule == null || member.motionModule == null)
            member.CreateModulesIfNeeded(ModuleFlags.agentMovementModule | ModuleFlags.motionModule);

        if (member.agentMovementModule != null)
        {
            member.agentMovementModule.SetWalkMode(walkMode);
            return true;
        }

        if (member.motionModule != null)
        {
            member.motionModule.SetWalkMode(walkMode);
            return true;
        }

        return false;
    }

    private static WalkMode GetWalkMode(WorldObject member, WalkMode fallback)
    {
        if (member == null)
            return fallback;

        if (member.motionModule != null)
            return member.motionModule.currentWalkMode;

        if (member.agentMovementModule != null)
            return member.agentMovementModule.walkMode;

        return fallback;
    }

    // returns true if the pack no longer contains the member (or never did)
    // returns false if the agent is null or doesn't contain a PackMemberModule.
    public bool RemoveMember(WorldObject agent)
    {
        if ((agent==null) || (agent.packMemberModule==null))
            return false;   // not a valid pack member
        
        Debug.Log($"RemoveMember({agent.DisplayName})");

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
            Debug.Log($"Pack removed member {agent.DisplayName}. Remaining {this.ToString()}");
            
            SetPackFollowChain();   // do this after any change to packAgentList
            return true;
        }
        return true;
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

    // called by function of same name in PackMemberModule
    public void SetFormation(FormationsEnum new_formation)
    {
        formation = new_formation;
    }

    // called by function of same name in PackMemberModule
    public FormationsEnum GetFormation()
    {
        return formation;
    }

    // called by function of same name in PackMemberModule
    // returns -1 if not in the pack.
    public int GetPositionInPack(WorldObject agent)
    {
        for (int pos=0; pos<packAgentList.Count; pos++)
        {
            if (packAgentList[pos] == agent) return pos;
        }
        return -1;
    }

    // returns true unless agent not found.
    public bool MoveAgentToLeader(WorldObject agent)
    {
        int oldPosition = GetPositionInPack(agent);
        if (oldPosition==-1) return false;  // agent not found.
        if (oldPosition==0) return true;    // already leader.

        WorldObject newLeader = packAgentList[oldPosition];

        // Shift [0..index-1] right by one. // faster than remove+insert list operations
        for (int i = oldPosition; i > 0; i--)
            packAgentList[i] = packAgentList[i - 1];

        packAgentList[0] = newLeader;

        SetPackFollowChain();   // do this after any change to packAgentList
        
        return true;
    }

    public bool MoveLeaderToFollower()
    {
        if (agentCount == 0) return false; // no members means no leader
        if (agentCount == 1) return false; // leader is only member, do nothing

        WorldObject oldLeader = packLeader;
        // Shift [0..index-2] left by one. // faster than remove+insert list operations
        for (int i = 0; i < agentCount - 2; i++)
            packAgentList[i] = packAgentList[i + 1];

        // put leader at the tail.
        packAgentList[agentCount-1] = oldLeader;

        SetPackFollowChain();   // do this after any change to packAgentList
        
        return true;

    }
}
