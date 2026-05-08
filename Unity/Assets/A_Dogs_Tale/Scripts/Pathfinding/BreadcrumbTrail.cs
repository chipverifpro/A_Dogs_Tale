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

    // The following should be grabbed directly from pack...
    public WorldObject leader;                // who is making the trail
    public List<WorldObject> followers;       // who is following the trail (in order)
    public int numFollowers => followers != null ? followers.Count : 0;     // shortcut


    public List<Crumb> crumbs = new List<Crumb>(256);
    public Vector2 lastDropPos;
    public bool hasAny = false;

    void Awake()
    {
        hasAny = false;
        if (followers == null) followers = new();
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

    /// Call once per frame by the owner to record position if moved enough.
    /// Can be forced in the case of a sharp turn that we want included.
    public void RecordIfNeeded(bool forceDrop = false)
    {
        if (leader==null || leader.locationModule==null) return;
        //Debug.Log($"RecordIfNeeded: numFollowers = {numFollowers}, numCrumbs = {crumbs.Count}, hasAny={hasAny}, forceDrop={forceDrop}");
        if (numFollowers == 0) return;

        if (!hasAny)
        {
            AddCrumb();
            lastDropPos = leader.locationModule.pos2_f;
            hasAny = true;
            return;
        }

        if (forceDrop && (leader.locationModule.pos2 != lastDropPos))
        {
            AddCrumb();
            lastDropPos = leader.locationModule.pos2;
            return;
        }
        //Debug.Log($"RecordIfNeeded: leader.pos3={leader_pos3}, lastDropPos={lastDropPos}, distSquared = {(leader_pos3 - lastDropPos).sqrMagnitude}");
        if ((leader.locationModule.pos2 - lastDropPos).sqrMagnitude >= dropDistance * dropDistance)
        {
            AddCrumb();
            lastDropPos = leader.locationModule.pos2;
        }
    }

    private void AddCrumb()
    {
        if (crumbs == null) crumbs = new();

        if (crumbs.Count >= maxCrumbs)
        {
            // Drop oldest when full
            crumbs.RemoveAt(0);
        }
        //Vector3 agent_pos_3 = new(leader.pos2.x, leader.height, leader.pos2.y);
        Crumb new_crumb = new() { pos2 = leader.locationModule.pos2_f, height = leader.locationModule.height, yawDeg = leader.locationModule.yawDeg, valid = true };
        new_crumb.whichFollowersArrived = new();
        crumbs.Add(new_crumb);
    }

    /*
        /// Returns the newest crumb if any; else returns current transform position.
        public Vector2 GetLatestPositionFallback()
        {
            if (crumbs.Count > 0) return crumbs[crumbs.Count - 1].pos2;
            return leader.pos2;
        }
    */

    public void AddFollower(WorldObject agent)
    {
        FindFollowerIndex(agent, addIfNotFollowing: true); // if not found, adds missing follower
    }

    public void RemoveFollower(WorldObject agent)
    {
        int index = FindFollowerIndex(agent, addIfNotFollowing: false);
        if (index >= 0)
        {
            followers.RemoveAt(index);
            if (followers.Count == 0)   // if nobody left, clear the crumbs trail
            {
                crumbs.Clear();
                hasAny = false;
            }
        }
    }

    public int FindFollowerIndex(WorldObject agent, bool addIfNotFollowing = true)
    {
        int eater_index;
        int eater_id = agent.ObjectId;

        if (followers == null) followers = new();

        for (eater_index = 0; eater_index < numFollowers; eater_index++)
        {
            if (eater_id == followers[eater_index].ObjectId)
            {
                break;
            }
        }
        if (eater_index == numFollowers)
        {
            // eater not found
            if (addIfNotFollowing)
            {
                // add the follower.
                followers.Add(agent);
            }
            else
            {
                // or, return not found
                return -1;
            }
        }
        return eater_index;
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

        if (agent == null || agent.agentMovementModule == null || leader == null || leader.locationModule == null)
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

        int followerIndex = FindFollowerIndex(agent, addIfNotFollowing: false);
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
}
