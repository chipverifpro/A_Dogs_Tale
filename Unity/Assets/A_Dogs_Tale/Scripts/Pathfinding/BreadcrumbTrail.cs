using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Crumb
{
    public bool valid = false;
    public Vector2 pos2;
    public float height;
    public float yawDeg;
}

[System.Serializable]
public class FollowerCrumbTrail
{
    public int followerObjectId;
    public List<Crumb> crumbs = new();
}

[DisallowMultipleComponent]
public class BreadcrumbTrail : MonoBehaviour
{
    [Header("Breadcrumb Trail")]
    [Tooltip("Drop a new crumb when the leader has moved at least this far since last drop.")]
    public float dropDistance = 0.5f;

    [Tooltip("Hard cap on stored crumbs per trail. Oldest crumbs are dropped when full.")]
    public int maxCrumbs = 256;

    public Pack pack;
    public int numFollowers => CountPackFollowers();

    [Tooltip("Reference-only leader path. Followers do not consume this list.")]
    public List<Crumb> leaderCrumbs = new(256);

    [Tooltip("Per-follower paths. These crumbs already include each follower's formation offset.")]
    public List<FollowerCrumbTrail> followerCrumbTrails = new();

    public Vector2 lastDropPos;
    public bool hasAny = false;

    private int syncedPackSignature;
    private WorldObject PackLeader => pack != null ? pack.packLeader : null;

    private void Awake()
    {
        hasAny = false;
        leaderCrumbs ??= new();
        followerCrumbTrails ??= new();
    }

    private void Update()
    {
        RecordIfNeeded();
    }

    public void ClearCrumbs()
    {
        leaderCrumbs.Clear();
        followerCrumbTrails.Clear();
        hasAny = false;
    }

    public void BindPack(Pack newPack)
    {
        bool packChanged = pack != newPack;
        pack = newPack;

        int currentSignature = ComputePackSignature();
        if (packChanged || currentSignature != syncedPackSignature)
            ClearCrumbs();

        syncedPackSignature = currentSignature;
    }

    /// <summary>
    /// Records a leader breadcrumb, and records one offset breadcrumb per follower.
    /// Follower crumbs are consumed independently; leader crumbs are retained as reference history.
    /// </summary>
    public void RecordIfNeeded(bool forceDrop = false)
    {
        WorldObject leader = PackLeader;
        if (leader == null || leader.locationModule == null)
            return;

        if (!hasAny)
        {
            AddCrumb();
            lastDropPos = leader.locationModule.pos2_f;
            hasAny = true;
            return;
        }

        if (forceDrop && leader.locationModule.pos2_f != lastDropPos)
        {
            AddCrumb();
            lastDropPos = leader.locationModule.pos2_f;
            return;
        }

        if ((leader.locationModule.pos2_f - lastDropPos).sqrMagnitude >= dropDistance * dropDistance)
        {
            AddCrumb();
            lastDropPos = leader.locationModule.pos2_f;
        }
    }

    private void AddCrumb()
    {
        WorldObject leader = PackLeader;
        if (leader == null || leader.locationModule == null)
            return;

        leaderCrumbs ??= new();
        followerCrumbTrails ??= new();

        Vector2 leaderPos2 = leader.locationModule.pos2_f;
        float leaderHeight = leader.locationModule.height;
        float leaderYawDeg = leader.locationModule.yawDeg;

        AddLeaderCrumb(leaderPos2, leaderHeight, leaderYawDeg);
        AddFollowerCrumbs(leaderPos2, leaderHeight, leaderYawDeg);
    }

    private void AddLeaderCrumb(Vector2 leaderPos2, float leaderHeight, float leaderYawDeg)
    {
        leaderCrumbs.Add(CreateCrumb(leaderPos2, leaderHeight, leaderYawDeg));
        PruneCrumbList(leaderCrumbs);
    }

    private void AddFollowerCrumbs_OLD(Vector2 leaderPos2, float leaderHeight, float leaderYawDeg)
    {
        if (pack == null || pack.packAgentList == null || pack.packAgentList.Count <= 1)
            return;

        PackFormations formations = Dir.Instance != null ? Dir.Instance.packFormations : null;

        for (int i = 1; i < pack.packAgentList.Count; i++)
        {
            WorldObject follower = pack.packAgentList[i];
            if (follower == null)
                continue;

            int followerKey = GetFollowerKey(follower);
            if (followerKey == 0)
                continue;

            Vector2 followerPos2 = leaderPos2;
            if (formations != null)
            {
                Vector2 offset = formations.GetOffsetForFormation(pack.formation, i, pack.packAgentList.Count);
                followerPos2 += formations.RotateAndScaleOffset(offset, leaderYawDeg, pack.formationSpacing);
            }

            FollowerCrumbTrail followerTrail = GetOrCreateFollowerTrail(followerKey);
            followerTrail.crumbs.Add(CreateCrumb(followerPos2, leaderHeight, leaderYawDeg));
            PruneCrumbList(followerTrail.crumbs);
        }
    }

    private void AddFollowerCrumbs(Vector2 leaderPos2, float leaderHeight, float leaderYawDeg)
    {
        if (pack == null || pack.packAgentList == null || pack.packAgentList.Count <= 1)
            return;

        PackFormations formations = Dir.Instance != null ? Dir.Instance.packFormations : null;

        for (int i = 1; i < pack.packAgentList.Count; i++)
        {
            WorldObject follower = pack.packAgentList[i];
            if (follower == null)
                continue;

            int followerKey = GetFollowerKey(follower);
            if (followerKey == 0)
                continue;

            Vector2 followerPos2 = leaderPos2;
            if (formations != null)
            {
                Vector2 offset = formations.GetOffsetForFormation(pack.formation, i, pack.packAgentList.Count);
                int rank = Mathf.FloorToInt(-offset.y); 
                if (rank>0) // negative offsets are behind leader so treat y as the rank, not an offset.
                {
                    if (leaderCrumbs.Count<=rank) continue; // not enough leader crumbs for this rank.  Don't add a crumb.
                    offset.y = 0; // make offset just (x,0) relative to old leaderCrumb.
                    int rankindex = leaderCrumbs.Count-1 - rank;  // position 0 is the oldest position, so go from list end.
                    //if (rankindex <0) continue;
                    followerPos2  = leaderCrumbs[rankindex].pos2;
                    leaderYawDeg = leaderCrumbs[rankindex].yawDeg;
                } // else use offsets in front of leader like we used to.
                followerPos2 += formations.RotateAndScaleOffset(offset, leaderYawDeg, pack.formationSpacing);
            }

            FollowerCrumbTrail followerTrail = GetOrCreateFollowerTrail(followerKey);
            followerTrail.crumbs.Add(CreateCrumb(followerPos2, leaderHeight, leaderYawDeg));
            PruneCrumbList(followerTrail.crumbs);
        }
    }

    private static Crumb CreateCrumb(Vector2 pos2, float height, float yawDeg)
    {
        return new Crumb
        {
            valid = true,
            pos2 = pos2,
            height = height,
            yawDeg = yawDeg
        };
    }

    public Crumb GetNextCrumb(WorldObject agent, float arrivalDistance = 0.35f)
    {
        Crumb invalidCrumb = new()
        {
            valid = false,
            pos2 = new Vector2(999f, 999f),
            height = 999f
        };

        if (agent == null || agent.agentMovementModule == null)
            return invalidCrumb;

        FollowerCrumbTrail followerTrail = FindFollowerTrail(agent);
        if (followerTrail == null || followerTrail.crumbs == null || followerTrail.crumbs.Count == 0)
            return invalidCrumb;

        Crumb currentTarget = agent.agentMovementModule.next_actualCrumb;
        if (currentTarget != null && currentTarget.valid && followerTrail.crumbs.Contains(currentTarget))
        {
            if (!IsAgentAtCrumb(agent, currentTarget, arrivalDistance))
                return currentTarget;

            followerTrail.crumbs.Remove(currentTarget);
        }

        while (followerTrail.crumbs.Count > 0)
        {
            Crumb nextCrumb = followerTrail.crumbs[0];
            agent.agentMovementModule.next_actualCrumb = nextCrumb;

            if (!IsAgentAtCrumb(agent, nextCrumb, arrivalDistance))
                return nextCrumb;

            followerTrail.crumbs.RemoveAt(0);
        }

        agent.agentMovementModule.next_actualCrumb = new Crumb();
        return invalidCrumb;
    }

    private FollowerCrumbTrail FindFollowerTrail(WorldObject follower)
    {
        int followerKey = GetFollowerKey(follower);
        if (followerKey == 0 || followerCrumbTrails == null)
            return null;

        for (int i = 0; i < followerCrumbTrails.Count; i++)
        {
            FollowerCrumbTrail followerTrail = followerCrumbTrails[i];
            if (followerTrail != null && followerTrail.followerObjectId == followerKey)
                return followerTrail;
        }

        return null;
    }

    private FollowerCrumbTrail GetOrCreateFollowerTrail(int followerKey)
    {
        for (int i = 0; i < followerCrumbTrails.Count; i++)
        {
            FollowerCrumbTrail followerTrail = followerCrumbTrails[i];
            if (followerTrail != null && followerTrail.followerObjectId == followerKey)
            {
                followerTrail.crumbs ??= new();
                return followerTrail;
            }
        }

        FollowerCrumbTrail newTrail = new()
        {
            followerObjectId = followerKey,
            crumbs = new List<Crumb>(Mathf.Max(0, maxCrumbs))
        };
        followerCrumbTrails.Add(newTrail);
        return newTrail;
    }

    private bool IsAgentAtCrumb(WorldObject agent, Crumb crumb, float arrivalDistance)
    {
        if (agent == null || crumb == null || !crumb.valid)
            return false;

        Vector2 agentPos = agent.locationModule != null
            ? agent.locationModule.pos2_f
            : new Vector2(agent.pos3d_map.x, agent.pos3d_map.z);
        return (agentPos - crumb.pos2).sqrMagnitude <= arrivalDistance * arrivalDistance;
    }

    private void PruneCrumbList(List<Crumb> crumbList)
    {
        if (crumbList == null)
            return;

        int cappedMaxCrumbs = Mathf.Max(0, maxCrumbs);
        while (crumbList.Count > cappedMaxCrumbs)
            crumbList.RemoveAt(0);
    }

    private int ComputePackSignature()
    {
        if (pack == null || pack.packAgentList == null)
            return 0;

        unchecked
        {
            int hash = 17;
            for (int i = 0; i < pack.packAgentList.Count; i++)
            {
                WorldObject member = pack.packAgentList[i];
                int memberId = member != null
                    ? member.ObjectId != 0 ? member.ObjectId : member.GetInstanceID()
                    : 0;
                hash = hash * 31 + memberId;
            }
            return hash;
        }
    }

    private static int GetFollowerKey(WorldObject follower)
    {
        if (follower == null)
            return 0;

        return follower.ObjectId != 0 ? follower.ObjectId : follower.GetInstanceID();
    }

    private int CountPackFollowers()
    {
        if (pack == null || pack.packAgentList == null || pack.packAgentList.Count <= 1)
            return 0;

        int count = 0;
        for (int i = 1; i < pack.packAgentList.Count; i++)
        {
            if (pack.packAgentList[i] != null)
                count++;
        }
        return count;
    }
}
