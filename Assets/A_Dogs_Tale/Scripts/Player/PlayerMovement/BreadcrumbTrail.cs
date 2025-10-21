using System;
using System.Collections.Generic;
using System.Data.Common;
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

    public Agent leader;                // who is making the trail
    public List<Agent> followers;       // who is following the trail (in order)
    public int numFollowers => followers.Count;     // shortcut


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
        Vector3 leader_pos3 = new(leader.pos2.x, leader.height, leader.pos2.y);
        //Debug.Log($"RecordIfNeeded: numFollowers = {numFollowers}, numCrumbs = {crumbs.Count}, hasAny={hasAny}, forceDrop={forceDrop}");
        if (numFollowers == 0) return;

        if (!hasAny)
        {
            AddCrumb();
            lastDropPos = leader.pos2;
            hasAny = true;
            return;
        }

        if (forceDrop && (leader.pos2 != lastDropPos))
        {
            AddCrumb();
            lastDropPos = leader.pos2;
            return;
        }
        //Debug.Log($"RecordIfNeeded: leader.pos3={leader_pos3}, lastDropPos={lastDropPos}, distSquared = {(leader_pos3 - lastDropPos).sqrMagnitude}");
        if ((leader.pos2 - lastDropPos).sqrMagnitude >= dropDistance * dropDistance)
        {
            AddCrumb();
            lastDropPos = leader.pos2;
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
        Crumb new_crumb = new() { pos2 = leader.pos2, height = leader.height, yawDeg = leader.yawDeg, valid = true };
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

    public void AddFollower(Agent agent)
    {
        FindFollowerIndex(agent, addIfNotFollowing: true); // if not found, adds missing follower
    }

    public void RemoveFollower(Agent agent)
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

    public int FindFollowerIndex(Agent agent, bool addIfNotFollowing = true)
    {
        int eater_index;
        int eater_id = agent.id;

        if (followers == null) followers = new();

        for (eater_index = 0; eater_index < numFollowers; eater_index++)
        {
            if (eater_id == followers[eater_index].id)
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

    public Crumb GetNextCrumb(Agent agent)
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
        
        Crumb leader_pos_crumb = new()
        {
            valid = true,
            pos2 = new(leader.pos2.x, leader.pos2.y),
            height = leader.height,
            yawDeg = leader.yawDeg
        };
        agent.next_actualCrumb = leader_pos_crumb; // default to leader position if no crumbs
        //return agent.next_actualCrumb;
        

        eater_index = FindFollowerIndex(agent);
        if (eater_index < 0) return invalid_crumb;
        // scan through the crumb list to find the first one that the eater has not eaten
        //for (crumb_index = crumbs.Count-1; crumb_index >=0; crumb_index--)
        for (crumb_index = 0; crumb_index < crumbs.Count; crumb_index++)
        {
            ///if (crumbs[crumb_index].position == lastEaten[eater_index])
            //if (crumbs == null) return invalid_crumb;
            if (crumbs[crumb_index].whichFollowersArrived == null)
                crumbs[crumb_index].whichFollowersArrived = new();

            agent.next_actualCrumb = crumbs[crumb_index];

            if (!crumbs[crumb_index].whichFollowersArrived.Contains(eater_index))
            {
                if (crumb_index == 0) // this was most recent crumb
                {
                    //return leader_pos_crumb;  // next would be off end of list
                    //return invalid_crumb;  // next would be off end of list
                }

                // we located the last crumb this follower ate.  Now give follower the next one    
                //crumb_index--;
                //agent.next_actualCrumb = crumbs[crumb_index];

                // update the crumb to know it was eaten.
                crumbs[crumb_index].whichFollowersArrived.Add(eater_index);

                // if every follower has eaten here, remove crumb from trail.
                if (crumbs[crumb_index].whichFollowersArrived.Count == followers.Count)
                {
                    //Debug.Log($"Follower {agent.name} ate crumb {crumb_index} at {crumbs[crumb_index].pos2}, all followers have eaten it, removing crumb.");
                    crumbs.RemoveAt(crumb_index);
                }
                // return that position
                return agent.next_actualCrumb;
            }
        }
        return invalid_crumb;   // did not find an uneaten crumb.
    }
}
