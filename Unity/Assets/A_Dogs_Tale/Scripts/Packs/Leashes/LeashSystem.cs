using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UnityEngine;

[Serializable]
public class LeashEndpoint
{
    public WorldObject otherAgent;        // the other agent
    public LeashEndRole myRole;      // Handle or Clip
    public float maxLength = 3.0f;

    // Optional authoring convenience:
    public bool autoCreateOnStart = true;
}

public enum LeashEndRole
{
    Handle,   // “I’m holding the leash”
    Clip      // “I’m attached / clipped”
}

[Serializable]
public class LeashLink
{
    public WorldObject a;
    public LeashEndRole roleA;
    public WorldObject b;
    public LeashEndRole roleB;
    public float maxLength;
    public LeashVisualizer leashVisualizer;     // script attached to leashGo
    public GameObject leashGo;
}

// ============== LeashSystem ================
public class LeashSystem : MonoBehaviour // or your Subsystem base
{
    private Dir dir;

    public List<LeashLink> leashes = new();

    public void Start()
    {
        if (dir==null) dir=Dir.Instance;
        // preload prefab
        if (leashPrefab==null) leashPrefab = LoadLeashPrefab();

        // Assumes packs are already created.
        CreateInitialLeashesFromEndpoints();
    }

    private void CreateInitialLeashesFromEndpoints()
    {
        foreach(Pack pack in Dir.Instance.packManager.packs)
        {
            foreach (var agent in pack.packAgentList)
            {
                if (agent == null) continue;

                var endpoints = agent.packMemberModule?.leashEndpoints;
                if (endpoints == null) continue;

                foreach (var ep in endpoints)
                {
                    if (ep == null || !ep.autoCreateOnStart) continue;
                    if (ep.otherAgent == null) continue;

                    // Require same pack membership:
                    if (!pack.packAgentList.Contains(ep.otherAgent))
                    {
                        Debug.LogWarning($"[Pack {pack.packName}] Leash endpoint ignored: {agent.DisplayName} -> {ep.otherAgent.DisplayName} not in same pack.");
                        continue;
                    }

                    // Create leash where agent is ep.myRole, other is opposite by default
                    var otherRole = (ep.myRole == LeashEndRole.Handle) ? LeashEndRole.Clip : LeashEndRole.Handle;

                    LeashEndpoint ep_other = FindEndpoint(ep.otherAgent.packMemberModule.leashEndpoints, ep.otherAgent);
                    if (ep_other==null)
                    {
                        // create ep_other
                        ep_other = new()
                        {
                            maxLength = ep.maxLength,
                            myRole = otherRole,
                            otherAgent = agent,
                            autoCreateOnStart = false   // I just created the link, don't try it again from this new endpooint.
                        };
                        ep.otherAgent.packMemberModule.leashEndpoints.Add(ep_other);
                        Debug.Log($"{agent.DisplayName} Created matching LeashEndpoint in {ep.otherAgent.DisplayName}: {otherRole} {ep.maxLength}");
                    }
                    LeashLink newLeashLink;
                    TryCreateLeash(agent, ep.myRole, ep.otherAgent, otherRole, ep.maxLength, out newLeashLink);
                }
            }
        }
    }

    public bool TryCreateLeash(WorldObject a, LeashEndRole roleA, WorldObject b, LeashEndRole roleB, float maxLength, out LeashLink newLeashLink)
    {
        newLeashLink = null;
        if (a == null || b == null || a == b)
        { 
            Debug.LogError("Tried to create a leash without two different valid WorldObjects");
            return false;
        }
        if (maxLength <= 0f)
        {
            Debug.LogError($"Tried to create a leash with invalid length = {maxLength}");
            return false;
        }


        var ap = a.packMemberModule.currentPack;
        var bp = b.packMemberModule.currentPack; 
        if (ap == null || bp == null)
        {
            Debug.LogError($"Tried to create leash but an agent is not in a pack. " +
                        $"{a.DisplayName} pack={(ap != null ? ap.packName : "NULL")} " +
                        $"to {b.DisplayName} pack={(bp != null ? bp.packName : "NULL")}");
            return false;
        }
        if (ap!=bp)
        {
            Debug.LogError($"Tried to create a leash between two agents not in same pack {a.DisplayName} in {ap.packName} to {b.DisplayName} in {bp.packName}");
            return false;
        }
        if (FindLeash(a, b) != null)
        {
            Debug.Log($"Leash between {a.DisplayName} and {b.DisplayName} already exists.");
            return false;
        }

        //Debug.Log($"Creating leash between {a.DisplayName} and {b.DisplayName}.");
        GameObject leashObject;
        LeashVisualizer leashVisualizer;
        newLeashLink = new LeashLink { a = a, roleA = roleA, b = b, roleB = roleB, maxLength = maxLength };
        TryCreateLeashVisualInstance(a.gameObject, b.gameObject, out leashObject, out leashVisualizer);
        newLeashLink.leashGo = leashObject;
        newLeashLink.leashVisualizer = leashVisualizer;
        leashes.Add(newLeashLink);
        Debug.Log($"Leash between {a.DisplayName} and {b.DisplayName} created in pack {ap.packName}.");
        
        return true;
    }


    public Vector3 ConstrainDesiredPosition(WorldObject mover, Vector3 desiredPosition)
    {
        if (mover == null) return desiredPosition;

        Vector3 result = desiredPosition;

        // Apply all leashes involving mover, in creation order (deterministic).
        for (int i = 0; i < leashes.Count; i++)
        {
            var link = leashes[i];
            if (link.a == null || link.b == null) continue;

            bool moverIsA = (link.a == mover);
            bool moverIsB = (link.b == mover);
            if (!moverIsA && !moverIsB) continue;

            var other = moverIsA ? link.b : link.a;
            Vector3 otherPos = other.transform.position;

            float dist = Vector3.Distance(result, otherPos);
            if (dist > link.maxLength && dist > 0.0001f)
            {
                Vector3 dir = (result - otherPos) / dist;
                result = otherPos + dir * link.maxLength;
            }
        }

        return result;
    }

    private LeashLink FindLeash(WorldObject a, WorldObject b)
    {
        for (int i = 0; i < leashes.Count; i++)
        {
            var link = leashes[i];
            if ((link.a == a && link.b == b) || (link.a == b && link.b == a))
                return link;
        }
        return null;
    }

    // At the end of every tick, update all the leashes.
    private void LateUpdate()
    {
        for (int i = 0; i < leashes.Count; i++)
        {
            var link = leashes[i];
            if (link.leashVisualizer == null || link.a == null || link.b == null) continue;
            link.leashVisualizer.SetEndpoints(link.a.transform.position, link.b.transform.position);
        }
    }


    public GameObject leashPrefab;
    // fallback if leashPrefab not assigned in GUI.
    private const string LeashPrefabResourcesPath = "Prefabs/Leash/Leash";
    [SerializeField] public Transform leashVisualParent; // optional

    private GameObject LoadLeashPrefab()
    {
        GameObject prefab = Resources.Load<GameObject>(LeashPrefabResourcesPath);
        if (prefab == null)
        {
            Debug.LogError($"[LeashSystem] Resources.Load failed for '{LeashPrefabResourcesPath}'. " +
                           $"Prefab must be at '.../Resources/{LeashPrefabResourcesPath}.prefab'");
        }
        return prefab;
    }

    private bool TryCreateLeashVisualInstance(GameObject parentA, GameObject parentB, out GameObject leashObject, out LeashVisualizer leashVisualizer)
    {
        leashObject = null;
        leashVisualizer = null;

        if (leashPrefab==null) leashPrefab = LoadLeashPrefab();
        if (leashPrefab == null) 
        {
            Debug.LogError("Leash prefab is null.");
            return false;
        }
        leashObject = Instantiate(leashPrefab);
        if (leashObject==null)
        {
            Debug.LogError("Failed to create a leash GameObject.");
            return false;
        }
        leashObject.transform.SetParent(parentA.transform, worldPositionStays: true);
        leashObject.name = $"Leash {parentA.name} to {parentB.name}";

        leashVisualizer = leashObject.GetComponent<LeashVisualizer>();
        if (leashVisualizer==null)
            leashVisualizer = leashObject.AddComponent<LeashVisualizer>();
        leashObject.SetActive(true);

        return true;
    }

    private LeashEndpoint FindEndpoint(IReadOnlyList<LeashEndpoint> endpoints, WorldObject other)
    {
        for (int i = 0; i < endpoints.Count; i++)
        {
            var ep = endpoints[i];
            if (ep != null && ep.otherAgent == other)
                return ep;
        }
        return null;
    }

    public static string MyLeashToLLM(WorldObject observer)
    {
        StringBuilder sb = new();
        // is observer on a leash?
        if (observer.packMemberModule.leashEndpoints.Count>0)
        {
            foreach(LeashEndpoint ep in observer.packMemberModule.leashEndpoints)
            {
                if (ep.myRole == LeashEndRole.Handle)
                {
                    sb.Append($"I am holding a {ep.maxLength}m long leash connected to {ep.otherAgent.DisplayName} at [{ep.otherAgent.pos3d_world.x},{ep.otherAgent.pos3d_world.z}]. ");
                }
                else
                {
                    sb.Append($"I am clipped to a {ep.maxLength}m long leash held by {ep.otherAgent.DisplayName} at [{ep.otherAgent.pos3d_world.x},{ep.otherAgent.pos3d_world.z}]. ");
                }
            }
        }
        // future? can we see any other leashes?
        return sb.ToString();
    }
}