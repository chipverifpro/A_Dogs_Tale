using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class Crumb
{
    public bool valid = false;
    //public Vector3 position;        // point creator was at
    public Vector2 pos2;
    public float height;
    public float yawDeg;       // angle player was at: helps followers turn?
    public List<int> whichFollowersArrived;
}

[DisallowMultipleComponent]
public class BreadcrumbTrail : MonoBehaviour
{
    [Header("Breadcrumb Trail (for leader)")]
    [Tooltip("Drop a new crumb when we've moved at least this far since last drop.")]
    public float dropDistance = 0.5f;

    [Tooltip("Hard cap on stored crumbs (acts as ring buffer ceiling).")]
    public int maxCrumbs = 256;

    public Pack pack;
    public int numFollowers => CountPackFollowers();


    public List<Crumb> crumbs = new List<Crumb>(256);
    public Vector2 lastDropPos;
    public bool hasAny = false;
    private int syncedPackSignature;
    private WorldObject PackLeader => pack != null ? pack.packLeader : null;

    void Awake()
    {
        hasAny = false;
        if (crumbs == null) crumbs = new();
    }

    void Update()
    {
        RecordIfNeeded();
    }

    public void ClearCrumbs()
    {
        crumbs.Clear();
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

    /// Call once per frame by the owner to record position if moved enough.
    /// Can be forced in the case of a sharp turn that we want included.
    public void RecordIfNeeded(bool forceDrop = false)
    {
        WorldObject currentLeader = PackLeader;
        if (currentLeader == null || currentLeader.locationModule == null) return;
        //Debug.Log($"RecordIfNeeded: numFollowers = {numFollowers}, numCrumbs = {crumbs.Count}, hasAny={hasAny}, forceDrop={forceDrop}");
        if (numFollowers == 0) return;

        if (!hasAny)
        {
            AddCrumb();
            lastDropPos = currentLeader.locationModule.pos2_f;
            hasAny = true;
            return;
        }

        if (forceDrop && (currentLeader.locationModule.pos2 != lastDropPos))
        {
            AddCrumb();
            lastDropPos = currentLeader.locationModule.pos2;
            return;
        }
        if ((currentLeader.locationModule.pos2 - lastDropPos).sqrMagnitude >= dropDistance * dropDistance)
        {
            AddCrumb();
            lastDropPos = currentLeader.locationModule.pos2;
        }
    }

    private void AddCrumb()
    {
        WorldObject currentLeader = PackLeader;
        if (currentLeader == null || currentLeader.locationModule == null)
            return;

        if (crumbs == null) crumbs = new();

        if (crumbs.Count >= maxCrumbs)
        {
            // Drop oldest when full
            crumbs.RemoveAt(0);
        }
        Crumb new_crumb = new() { pos2 = currentLeader.locationModule.pos2_f, height = currentLeader.locationModule.height, yawDeg = currentLeader.locationModule.yawDeg, valid = true };
        new_crumb.whichFollowersArrived = new();
        crumbs.Add(new_crumb);
    }

    private int FindFollowerIndex(WorldObject agent)
    {
        if (agent == null || pack == null || pack.packAgentList == null)
            return -1;

        int followerIndex = 0;
        for (int i = 1; i < pack.packAgentList.Count; i++)
        {
            WorldObject follower = pack.packAgentList[i];
            if (follower == null)
                continue;

            if (follower == agent)
                return followerIndex;

            followerIndex++;
        }

        return -1;
    }

    public Crumb GetNextCrumb(WorldObject agent, float arrivalDistance = 0.35f, bool markArrivals = true)
    {
        int eater_index;
        int crumb_index;
        // for returning an invalid crumb
        Crumb invalid_crumb = new()
        {
            valid = false,
            pos2 = new(999f, 999f),
            height = 999f
        };

        WorldObject currentLeader = PackLeader;
        if (agent == null || agent.agentMovementModule == null || currentLeader == null || currentLeader.locationModule == null)
            return invalid_crumb;
        
        Crumb currentTarget = agent.agentMovementModule.next_actualCrumb;
        

        eater_index = FindFollowerIndex(agent);
        if (eater_index < 0) return invalid_crumb;

        if (crumbs == null || crumbs.Count == 0)
            return invalid_crumb;

        if (currentTarget != null && currentTarget.valid)
        {
            int currentCrumbIndex = FindMatchingCrumbIndex(currentTarget);
            if (currentCrumbIndex >= 0)
            {
                Crumb currentCrumb = crumbs[currentCrumbIndex];
                if (currentCrumb.whichFollowersArrived == null)
                    currentCrumb.whichFollowersArrived = new();

                if (!markArrivals || !IsAgentAtCrumb(agent, currentCrumb, arrivalDistance))
                    return currentCrumb;

                MarkFollowerArrivedAtCrumb(currentCrumbIndex, eater_index);
            }
        }

        // scan through the crumb list to find the first one that the eater has not eaten
        //for (crumb_index = crumbs.Count-1; crumb_index >=0; crumb_index--)
        for (crumb_index = 0; crumb_index < crumbs.Count; crumb_index++)
        {
            ///if (crumbs[crumb_index].position == lastEaten[eater_index])
            //if (crumbs == null) return invalid_crumb;
            if (crumbs[crumb_index].whichFollowersArrived == null)
                crumbs[crumb_index].whichFollowersArrived = new();

            agent.agentMovementModule.next_actualCrumb = crumbs[crumb_index];

            if (!crumbs[crumb_index].whichFollowersArrived.Contains(eater_index))
            {
                if (markArrivals && IsAgentAtCrumb(agent, crumbs[crumb_index], arrivalDistance))
                {
                    MarkFollowerArrivedAtCrumb(crumb_index, eater_index);
                    crumb_index--;
                    continue;
                }

                // return that position
                return agent.agentMovementModule.next_actualCrumb;
            }
        }
        return invalid_crumb;   // did not find an uneaten crumb.
    }

    public void MarkCurrentCrumbArrived(WorldObject agent)
    {
        if (agent == null || agent.agentMovementModule == null)
            return;

        Crumb currentTarget = agent.agentMovementModule.next_actualCrumb;
        if (currentTarget == null || !currentTarget.valid)
            return;

        int followerIndex = FindFollowerIndex(agent);
        if (followerIndex < 0)
            return;

        int currentCrumbIndex = FindMatchingCrumbIndex(currentTarget);
        if (currentCrumbIndex < 0)
            return;

        MarkFollowerArrivedAtCrumb(currentCrumbIndex, followerIndex);
        agent.agentMovementModule.next_actualCrumb = new Crumb();
        agent.agentMovementModule.next_formationCrumb = new Crumb();
    }

    private int FindMatchingCrumbIndex(Crumb target)
    {
        if (target == null || crumbs == null)
            return -1;

        for (int i = 0; i < crumbs.Count; i++)
        {
            Crumb crumb = crumbs[i];
            if (crumb == target)
                return i;

            if (crumb != null &&
                crumb.valid == target.valid &&
                crumb.pos2 == target.pos2 &&
                Mathf.Approximately(crumb.height, target.height))
            {
                return i;
            }
        }

        return -1;
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

    private void MarkFollowerArrivedAtCrumb(int crumbIndex, int followerIndex)
    {
        if (crumbs == null || crumbIndex < 0 || crumbIndex >= crumbs.Count)
            return;

        Crumb crumb = crumbs[crumbIndex];
        if (crumb.whichFollowersArrived == null)
            crumb.whichFollowersArrived = new();

        if (!crumb.whichFollowersArrived.Contains(followerIndex))
            crumb.whichFollowersArrived.Add(followerIndex);

        if (crumb.whichFollowersArrived.Count >= numFollowers)
            crumbs.RemoveAt(crumbIndex);
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
