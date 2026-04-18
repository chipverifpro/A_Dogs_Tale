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
    public Vector3 currentForceOnA;
    public Vector3 currentForceOnB;
    public Vector3 nextForceOnA;
    public Vector3 nextForceOnB;

    public string LeashToString()
    {
        string str;
        str = $"Leash between {a.DisplayName} and {b.DisplayName} is {maxLength} long";
        return str;
    }
}

// ============== LeashSystem ================
[DefaultExecutionOrder(-900)]
public class LeashSystem : MonoBehaviour // or your Subsystem base
{
    private struct ForceContribution
    {
        public LeashLink link;
        public bool targetIsA;
        public Vector3 forceVector;
    }

    private Dir dir;

    public List<LeashLink> leashes = new();

    [Header("Leash Pull Tuning")]
    [SerializeField] private float forcePerExceededMeterPerMass = 1f;
    [SerializeField] private float movementPerForce = 1f;
    [SerializeField] private float minimumForceToMove = 0.05f;
    [SerializeField] private float minimumMovementDistance = 0.005f;
    [SerializeField] private float feedbackForceRatio = 1f;
    [SerializeField] private float minimumMassResistance = 0.1f;
    [SerializeField] private float maxPullMovementPerTick = 0.75f;
    [SerializeField] private bool debugPullLogging = false;

    public void Start()
    {
        if (dir==null) dir=Dir.Instance;
        // preload prefab
        if (leashPrefab==null) leashPrefab = LoadLeashPrefab();

        // Create from existing instances in hierarchy
        CreateInitialLeashesFromEndpoints();

        // DEBUG TEST
        CreateTestLeash ("germanshepherd", "cur", 3.0f);
    }

    public LeashLink CreateTestLeash (string a, string b, float length)
    {
        // Manually create a test leash between germanshepherd and cur
        LeashLink leash;
        WorldObject walkerWorldObject;
        WorldObject dogWorldObject;
        if ((!WorldObjectRegistry.Instance.TryGetByDisplayName(a, out walkerWorldObject))
            || (!WorldObjectRegistry.Instance.TryGetByDisplayName(b, out dogWorldObject)))
        {
            Debug.LogWarning($"Failed to get {a} and {b} for leash creation");
            return null;
        }

        bool created = Dir.Instance.leashSystem.TryCreateLeash(
            a: walkerWorldObject,
            roleA: LeashEndRole.Handle,
            b: dogWorldObject,
            roleB: LeashEndRole.Clip,
            maxLength: 3.0f,
            out leash);
        if (created) 
            Debug.Log(leash.LeashToString());
        else
            Debug.LogWarning($"Failed to create test leash from {a} to {b}");
        
        return leash;
    }

    private void Update()
    {
        if (dir == null) dir = Dir.Instance;

        PromoteQueuedPullForces();
        ApplyPendingPullForces(Time.deltaTime);
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
                        Debug.Log($"{agent.DisplayName} Created matching LeashEndpoint in {ep.otherAgent.DisplayName}: {otherRole} {ep.maxLength}m");
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

        float currentDistance = Vector3.Distance(a.transform.position, b.transform.position);
        if (currentDistance > maxLength)
        {
            Debug.LogError(
                $"Tried to create a leash between {a.DisplayName} and {b.DisplayName}, " +
                $"but they are {currentDistance:0.###}m apart and leash length is only {maxLength:0.###}m.");
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
        Debug.Log($"Leash between {a.DisplayName} and {b.DisplayName} created.");
        
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
                Vector3 clampedPosition = otherPos + dir * link.maxLength;
                float exceededDistance = Vector3.Distance(result, clampedPosition);

                if (exceededDistance > 0f)
                {
                    float moverStrengthMass = Mathf.Max(minimumMassResistance, mover.mass);
                    float storedForceMagnitude = exceededDistance * moverStrengthMass * forcePerExceededMeterPerMass;
                    Vector3 storedForce = dir * storedForceMagnitude;

                    if (storedForceMagnitude >= minimumForceToMove)
                    {
                        if (moverIsA) link.nextForceOnB += storedForce;
                        else link.nextForceOnA += storedForce;

                        if (debugPullLogging)
                        {
                            Debug.Log(
                                $"[LeashSystem] {mover.DisplayName} exceeded leash to {other.DisplayName} by {exceededDistance:0.###}m, stored force {storedForce}.",
                                this);
                        }
                    }
                }

                result = clampedPosition;
            }
        }

        return result;
    }

    private void PromoteQueuedPullForces()
    {
        for (int i = 0; i < leashes.Count; i++)
        {
            var link = leashes[i];
            if (link == null) continue;

            link.currentForceOnA = link.nextForceOnA;
            link.currentForceOnB = link.nextForceOnB;
            link.nextForceOnA = Vector3.zero;
            link.nextForceOnB = Vector3.zero;
        }
    }

    private void ApplyPendingPullForces(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        List<WorldObject> receivers = new();

        for (int i = 0; i < leashes.Count; i++)
        {
            var link = leashes[i];
            if (link == null || link.a == null || link.b == null) continue;

            if (link.currentForceOnA.sqrMagnitude > 0.000001f && !receivers.Contains(link.a))
                receivers.Add(link.a);

            if (link.currentForceOnB.sqrMagnitude > 0.000001f && !receivers.Contains(link.b))
                receivers.Add(link.b);
        }

        for (int i = 0; i < receivers.Count; i++)
        {
            ApplyPendingPullForReceiver(receivers[i], deltaTime);
        }
    }

    private void ApplyPendingPullForReceiver(WorldObject receiver, float deltaTime)
    {
        if (receiver == null)
            return;

        List<ForceContribution> contributions = new();
        Vector3 totalForce = Vector3.zero;

        for (int i = 0; i < leashes.Count; i++)
        {
            var link = leashes[i];
            if (link == null) continue;

            if (link.a == receiver && link.currentForceOnA.sqrMagnitude > 0.000001f)
            {
                contributions.Add(new ForceContribution { link = link, targetIsA = true, forceVector = link.currentForceOnA });
                totalForce += link.currentForceOnA;
            }

            if (link.b == receiver && link.currentForceOnB.sqrMagnitude > 0.000001f)
            {
                contributions.Add(new ForceContribution { link = link, targetIsA = false, forceVector = link.currentForceOnB });
                totalForce += link.currentForceOnB;
            }
        }

        if (contributions.Count == 0)
            return;

        float totalForceMagnitude = totalForce.magnitude;
        if (totalForceMagnitude < minimumForceToMove)
        {
            ClearCurrentForces(contributions);
            return;
        }

        float resistanceMass = Mathf.Max(minimumMassResistance, receiver.mass);
        Vector3 desiredMove = totalForce * (movementPerForce / resistanceMass);
        desiredMove.y = 0f;

        float desiredMoveMagnitude = desiredMove.magnitude;
        if (desiredMoveMagnitude < minimumMovementDistance)
        {
            ClearCurrentForces(contributions);
            return;
        }

        if (maxPullMovementPerTick > 0f && desiredMoveMagnitude > maxPullMovementPerTick)
        {
            desiredMove = desiredMove.normalized * maxPullMovementPerTick;
            desiredMoveMagnitude = desiredMove.magnitude;
        }

        Vector3 actualMove = Vector3.zero;

        if (!receiver.immovable)
        {
            if (receiver.motionModule != null)
            {
                actualMove = receiver.motionModule.ApplyExternalWorldDisplacement(desiredMove, deltaTime, applyLeashConstraints: true);
            }
            else
            {
                Vector3 startPosition = receiver.transform.position;
                Vector3 constrainedPosition = ConstrainDesiredPosition(receiver, startPosition + desiredMove);
                receiver.transform.position = constrainedPosition;
                actualMove = constrainedPosition - startPosition;
            }
        }

        float achievedDistance = 0f;
        if (desiredMoveMagnitude > 0.0001f)
        {
            achievedDistance = Mathf.Max(0f, Vector3.Dot(actualMove, desiredMove.normalized));
        }

        float failureRatio = desiredMoveMagnitude > 0.0001f
            ? Mathf.Clamp01(1f - (achievedDistance / desiredMoveMagnitude))
            : 0f;

        if (receiver.immovable)
            failureRatio = 1f;

        if (failureRatio > 0f && feedbackForceRatio > 0f)
        {
            for (int i = 0; i < contributions.Count; i++)
            {
                ForceContribution contribution = contributions[i];
                Vector3 feedbackForce = -contribution.forceVector * (failureRatio * feedbackForceRatio);

                if (feedbackForce.magnitude < minimumForceToMove)
                    continue;

                if (contribution.targetIsA) contribution.link.nextForceOnB += feedbackForce;
                else contribution.link.nextForceOnA += feedbackForce;
            }
        }

        if (debugPullLogging)
        {
            Debug.Log(
                $"[LeashSystem] Applied pull to {receiver.DisplayName}: totalForce={totalForce}, desiredMove={desiredMove}, actualMove={actualMove}, failureRatio={failureRatio:0.###}.",
                this);
        }

        ClearCurrentForces(contributions);
    }

    private void ClearCurrentForces(List<ForceContribution> contributions)
    {
        for (int i = 0; i < contributions.Count; i++)
        {
            ForceContribution contribution = contributions[i];
            if (contribution.targetIsA) contribution.link.currentForceOnA = Vector3.zero;
            else contribution.link.currentForceOnB = Vector3.zero;
        }
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
