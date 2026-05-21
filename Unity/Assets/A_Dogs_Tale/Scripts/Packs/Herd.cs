using System;
using System.Collections.Generic;
using DogGame.Modules;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class HerdMemberProfile
{
    public WorldObject member;

    [Range(0f, 1f)]
    public float leadership01;

    public bool hasIndependentMotivationTarget;
    public Vector3 independentMotivationTargetMap;
}

public enum HerdAgentClassification
{
    Neutral = 0,
    HerdMember,
    Shepherd,
    GuardianDog,
    Safe,
    Dangerous
}

public readonly struct HerdSensedAgent
{
    public HerdSensedAgent(WorldObject agent, HerdAgentClassification classification, float distanceMeters)
    {
        this.agent = agent;
        this.classification = classification;
        this.distanceMeters = distanceMeters;
    }

    public readonly WorldObject agent;
    public readonly HerdAgentClassification classification;
    public readonly float distanceMeters;
}

public readonly struct HerdSteeringResult
{
    public HerdSteeringResult(Vector3 directionMap, float speedFactor, WalkMode walkMode, bool dangerNearby)
    {
        this.directionMap = directionMap;
        this.targetMap = default;
        this.usePathTarget = false;
        this.speedFactor = speedFactor;
        this.walkMode = walkMode;
        this.dangerNearby = dangerNearby;
    }

    public HerdSteeringResult(Vector3 targetMap, float speedFactor, WalkMode walkMode, bool dangerNearby, bool usePathTarget)
    {
        this.directionMap = Vector3.zero;
        this.targetMap = targetMap;
        this.usePathTarget = usePathTarget;
        this.speedFactor = speedFactor;
        this.walkMode = walkMode;
        this.dangerNearby = dangerNearby;
    }

    public readonly Vector3 directionMap;
    public readonly Vector3 targetMap;
    public readonly bool usePathTarget;
    public readonly float speedFactor;
    public readonly WalkMode walkMode;
    public readonly bool dangerNearby;
}

public class Herd : Pack
{
    private const int DefaultHerdSize = 20;

    [Header("Herd Membership")]
    [SerializeField, Min(1)] private int targetHerdSize = DefaultHerdSize;
    [SerializeField, Min(1)] private int performanceSoftLimit = 40;
    [SerializeField] private bool autoAssignLeadershipRatings = true;
    [SerializeField, Range(0f, 1f)] private float naturalLeaderCutoff01 = 0.65f;

    [Header("Known Safe Agents")]
    [SerializeField] private WorldObject shepherdHuman;
    [SerializeField] private WorldObject guardianDog;
    [SerializeField] private List<WorldObject> additionalSafeAgents = new();
    [SerializeField] private List<WorldObject> additionalDangerousAgents = new();

    [Header("Boids Radius")]
    [SerializeField, Min(0.1f)] private float neighborRadiusMeters = 4.5f;
    [SerializeField, Min(0.1f)] private float separationRadiusMeters = 0.85f;
    [SerializeField, Min(0.1f)] private float gatherToLeaderDistanceMeters = 6.0f;
    [SerializeField, Min(0.1f)] private float agentAwarenessRadiusMeters = 7.0f;
    [SerializeField, Min(0.1f)] private float safeAgentComfortRadiusMeters = 3.0f;

    [Header("Boids Weights")]
    [SerializeField, Min(0f)] private float cohesionWeight = 1.15f;
    [SerializeField, Min(0f)] private float alignmentWeight = 0.85f;
    [SerializeField, Min(0f)] private float separationWeight = 1.75f;
    [SerializeField, Min(0f)] private float dangerAvoidanceWeight = 3.0f;
    [SerializeField, Min(0f)] private float safeAgentInfluenceWeight = 0.25f;
    [SerializeField, Min(0f)] private float independentLeaderMotivationWeight = 0.35f;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float relaxedSpeedFactor = 0.65f;
    [SerializeField, Min(0.1f)] private float flockingSpeedFactor = 0.9f;
    [SerializeField, Min(0.1f)] private float dangerSpeedFactor = 1.3f;

    [Header("Debug")]
    [FormerlySerializedAs("HerdDebugLogging")]
    [SerializeField] private bool herdDebugLogging = false;
    [SerializeField, Min(1)] private int debugLogEveryFrames = 60;

    [Header("Runtime Profiles")]
    [SerializeField] private List<HerdMemberProfile> memberProfiles = new();
    private readonly List<HerdSensedAgent> sensedAgentScratch = new();

    public int TargetHerdSize => targetHerdSize;
    public int PerformanceSoftLimit => performanceSoftLimit;
    public WorldObject ShepherdHuman => shepherdHuman;
    public WorldObject GuardianDog => guardianDog;
    public bool HasReachedTargetSize => agentCount >= targetHerdSize;
    public bool IsPastPerformanceSoftLimit => agentCount >= performanceSoftLimit;
    public bool DebugLoggingEnabled => herdDebugLogging;
    public int DebugLogEveryFrames => Mathf.Max(1, debugLogEveryFrames);

    protected override void Awake()
    {
        base.Awake();

        packName = string.IsNullOrWhiteSpace(packName) || packName == "Unnamed Pack"
            ? "Herd"
            : packName;
        followerType = AgentDecisionType.Herd;
        leadershipType = AgentDecisionType.Herd;

        RefreshMemberProfiles();
        LogDebug(
            $"Awake: members={DescribeMemberCount()} leader={DescribeWorldObject(packLeader)} " +
            $"followerType={followerType} leadershipType={leadershipType}");
    }

    protected override void Start()
    {
        base.Start();
        bool activated = SetPackFollowChain();
        LogDebug(
            $"Start: SetPackFollowChain={activated} members={DescribeMemberCount()} " +
            $"leader={DescribeWorldObject(packLeader)} shepherd={DescribeWorldObject(shepherdHuman)} guardian={DescribeWorldObject(guardianDog)}");
    }

    private void OnValidate()
    {
        targetHerdSize = Mathf.Max(1, targetHerdSize);
        performanceSoftLimit = Mathf.Max(targetHerdSize, performanceSoftLimit);
        neighborRadiusMeters = Mathf.Max(0.1f, neighborRadiusMeters);
        separationRadiusMeters = Mathf.Clamp(separationRadiusMeters, 0.1f, neighborRadiusMeters);
        gatherToLeaderDistanceMeters = Mathf.Max(neighborRadiusMeters, gatherToLeaderDistanceMeters);
        agentAwarenessRadiusMeters = Mathf.Max(0.1f, agentAwarenessRadiusMeters);
        safeAgentComfortRadiusMeters = Mathf.Max(0.1f, safeAgentComfortRadiusMeters);
        debugLogEveryFrames = Mathf.Max(1, debugLogEveryFrames);
    }

    public override bool AddMember(WorldObject agent, bool setAsLeader = false)
    {
        if (agent != null && !IsSheepAgent(agent))
        {
            Debug.LogWarning($"[Herd] Adding non-sheep agent '{agent.DisplayName}' to herd '{packName}'.", this);
        }

        bool changed = base.AddMember(agent, setAsLeader);
        RefreshMemberProfiles();
        LogDebug($"AddMember: member={DescribeWorldObject(agent)} changed={changed} setAsLeader={setAsLeader} members={DescribeMemberCount()}");
        return changed;
    }

    public override bool RemoveMember(WorldObject agent)
    {
        bool removed = base.RemoveMember(agent);
        RefreshMemberProfiles();
        LogDebug($"RemoveMember: member={DescribeWorldObject(agent)} removed={removed} members={DescribeMemberCount()}");
        return removed;
    }

    public override bool SetPackFollowChain()
    {
        return ReassertHerdDecisionModules("SetPackFollowChain");
    }

    public bool ReassertHerdDecisionModules(string reason, bool forceLog = false)
    {
        int reconciledCount = ReconcileMemberListFromScene(forceLog);
        if (packAgentList == null || packAgentList.Count == 0)
        {
            LogDebug($"ReassertHerdDecisionModules failed: packAgentList is null or empty after scene reconciliation. reason='{reason}'", forceLog);
            return false;
        }

        RefreshMemberProfiles();

        int switchedCount = 0;
        int alreadyActiveCount = 0;
        int missingAgentModuleCount = 0;
        int nullMemberCount = 0;

        foreach (WorldObject member in packAgentList)
        {
            if (member == null)
            {
                nullMemberCount++;
                continue;
            }

            if (member.agentModule == null)
            {
                missingAgentModuleCount++;
                LogDebug($"Reassert skipped {DescribeWorldObject(member)}: missing AgentModule.", forceLog);
                continue;
            }

            AgentDecisionModuleBase currentDecision = member.agentModule.currentDecisionModule;
            if (currentDecision != null && currentDecision.DecisionType == AgentDecisionType.Herd)
            {
                alreadyActiveCount++;
                continue;
            }

            member.agentModule.SwitchDecisionModule(AgentDecisionType.Herd);
            switchedCount++;
            LogDebug(
                $"Reassert switched {DescribeWorldObject(member)} to Herd; " +
                $"previousDecision={currentDecision?.GetType().Name ?? "null"} " +
                $"currentDecision={member.agentModule.currentDecisionModule?.GetType().Name ?? "null"}",
                forceLog);
        }

        LogDebug(
            $"Reassert complete reason='{reason}': switched={switchedCount} alreadyHerd={alreadyActiveCount} " +
            $"missingAgentModule={missingAgentModuleCount} nullMembers={nullMemberCount} reconciled={reconciledCount} members={DescribeMemberCount()}",
            forceLog);
        return true;
    }

    public bool CanAcceptMoreSheep(bool allowBeyondSoftLimit = false)
    {
        if (packAgentList == null)
            return true;

        if (!allowBeyondSoftLimit && agentCount >= performanceSoftLimit)
            return false;

        return true;
    }

    public void SetShepherdHuman(WorldObject shepherd)
    {
        shepherdHuman = shepherd;
    }

    public void SetGuardianDog(WorldObject guardian)
    {
        guardianDog = guardian;
    }

    public void SetIndependentMotivationTarget(WorldObject member, Vector3 targetMap)
    {
        HerdMemberProfile profile = GetOrCreateProfile(member);
        if (profile == null)
            return;

        profile.hasIndependentMotivationTarget = true;
        profile.independentMotivationTargetMap = targetMap;
    }

    public void ClearIndependentMotivationTarget(WorldObject member)
    {
        HerdMemberProfile profile = FindProfile(member);
        if (profile == null)
            return;

        profile.hasIndependentMotivationTarget = false;
    }

    public float GetLeadershipRating01(WorldObject member)
    {
        HerdMemberProfile profile = GetOrCreateProfile(member);
        return profile != null ? profile.leadership01 : 0f;
    }

    public bool TryGetIndependentMotivationTarget(WorldObject member, out Vector3 targetMap)
    {
        targetMap = default;

        HerdMemberProfile profile = FindProfile(member);
        if (profile == null || !profile.hasIndependentMotivationTarget)
            return false;

        targetMap = profile.independentMotivationTargetMap;
        return true;
    }

    public HerdAgentClassification ClassifyAgent(WorldObject observer, WorldObject other)
    {
        if (other == null || other == observer)
            return HerdAgentClassification.Neutral;

        if (packAgentList != null && packAgentList.Contains(other))
            return HerdAgentClassification.HerdMember;

        if (other == shepherdHuman)
            return HerdAgentClassification.Shepherd;

        if (other == guardianDog)
            return HerdAgentClassification.GuardianDog;

        if (additionalDangerousAgents != null && additionalDangerousAgents.Contains(other))
            return HerdAgentClassification.Dangerous;

        if (additionalSafeAgents != null && additionalSafeAgents.Contains(other))
            return HerdAgentClassification.Safe;

        if (IsSheepAgent(other))
            return HerdAgentClassification.Safe;

        return other.species switch
        {
            Species.Canine => HerdAgentClassification.Dangerous,
            Species.BigAnimal => HerdAgentClassification.Dangerous,
            Species.Human => HerdAgentClassification.Neutral,
            _ => HerdAgentClassification.Neutral
        };
    }

    public int FindNearbyAgents(WorldObject observer, float radiusMeters, List<HerdSensedAgent> results)
    {
        results?.Clear();
        if (observer == null || results == null || radiusMeters <= 0f)
            return 0;

        float radiusSqr = radiusMeters * radiusMeters;
        Vector3 observerPos = observer.pos3d_map;

        foreach (WorldObject candidate in WorldObjectRegistry.Instance.GetAllObjects())
        {
            if (candidate == null || candidate == observer || candidate.Kind != WorldObjectKind.Agent)
                continue;

            Vector3 delta = candidate.pos3d_map - observerPos;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance > radiusSqr)
                continue;

            HerdAgentClassification classification = ClassifyAgent(observer, candidate);
            results.Add(new HerdSensedAgent(candidate, classification, Mathf.Sqrt(sqrDistance)));
        }

        return results.Count;
    }

    public bool TryComputeSteering(WorldObject sheep, out HerdSteeringResult result)
    {
        result = default;

        if (sheep == null || packAgentList == null || packAgentList.Count == 0)
        {
            LogDebugFrame($"TryComputeSteering failed: sheep={DescribeWorldObject(sheep)} members={DescribeMemberCount()}");
            return false;
        }

        Vector3 selfPos = sheep.pos3d_map;
        WorldObject leader = packLeader;
        if (leader != null && leader != sheep)
        {
            Vector3 toLeader = leader.pos3d_map - selfPos;
            toLeader.y = 0f;
            if (toLeader.sqrMagnitude > gatherToLeaderDistanceMeters * gatherToLeaderDistanceMeters)
            {
                result = new HerdSteeringResult(
                    leader.pos3d_map,
                    flockingSpeedFactor,
                    WalkMode.Walk,
                    dangerNearby: false,
                    usePathTarget: true);
                LogDebugFrame(
                    $"Steering {DescribeWorldObject(sheep)}: gather-to-leader target={leader.pos3d_map} " +
                    $"distance={Mathf.Sqrt(toLeader.sqrMagnitude):0.00} threshold={gatherToLeaderDistanceMeters:0.00}");
                return true;
            }
        }

        Vector3 cohesionCenter = Vector3.zero;
        Vector3 alignment = Vector3.zero;
        Vector3 separation = Vector3.zero;
        Vector3 dangerAvoidance = Vector3.zero;
        Vector3 safeInfluence = Vector3.zero;

        int cohesionCount = 0;
        int alignmentCount = 0;
        bool dangerNearby = false;

        float neighborRadiusSqr = neighborRadiusMeters * neighborRadiusMeters;
        float separationRadiusSqr = separationRadiusMeters * separationRadiusMeters;

        foreach (WorldObject member in packAgentList)
        {
            if (member == null || member == sheep)
                continue;

            Vector3 toMember = member.pos3d_map - selfPos;
            toMember.y = 0f;
            float sqrDistance = toMember.sqrMagnitude;
            if (sqrDistance > neighborRadiusSqr)
                continue;

            cohesionCenter += member.pos3d_map;
            cohesionCount++;

            Vector3 memberForward = member.transform.forward;
            memberForward.y = 0f;
            if (memberForward.sqrMagnitude > 0.0001f)
            {
                alignment += memberForward.normalized;
                alignmentCount++;
            }

            if (sqrDistance < separationRadiusSqr && sqrDistance > 0.0001f)
            {
                float distance = Mathf.Sqrt(sqrDistance);
                separation -= toMember.normalized / Mathf.Max(0.1f, distance);
            }
        }

        FindNearbyAgents(sheep, agentAwarenessRadiusMeters, sensedAgentScratch);
        foreach (HerdSensedAgent sensed in sensedAgentScratch)
        {
            if (sensed.agent == null)
                continue;

            Vector3 away = selfPos - sensed.agent.pos3d_map;
            away.y = 0f;
            if (away.sqrMagnitude <= 0.0001f)
                continue;

            if (sensed.classification == HerdAgentClassification.Dangerous)
            {
                dangerNearby = true;
                dangerAvoidance += away.normalized / Mathf.Max(0.1f, sensed.distanceMeters);
            }
            else if ((sensed.classification == HerdAgentClassification.Shepherd ||
                      sensed.classification == HerdAgentClassification.GuardianDog ||
                      sensed.classification == HerdAgentClassification.Safe) &&
                     sensed.distanceMeters > safeAgentComfortRadiusMeters)
            {
                safeInfluence -= away.normalized;
            }
        }

        Vector3 steering = Vector3.zero;

        if (cohesionCount > 0)
        {
            cohesionCenter /= cohesionCount;
            Vector3 toCenter = cohesionCenter - selfPos;
            toCenter.y = 0f;
            steering += NormalizeOrZero(toCenter) * cohesionWeight;
        }

        if (alignmentCount > 0)
            steering += NormalizeOrZero(alignment / alignmentCount) * alignmentWeight;

        steering += NormalizeOrZero(separation) * separationWeight;
        steering += NormalizeOrZero(dangerAvoidance) * dangerAvoidanceWeight;
        steering += NormalizeOrZero(safeInfluence) * safeAgentInfluenceWeight;

        float leadership01 = GetLeadershipRating01(sheep);
        if (leadership01 >= naturalLeaderCutoff01 &&
            TryGetIndependentMotivationTarget(sheep, out Vector3 motivationTarget))
        {
            Vector3 towardMotivation = motivationTarget - selfPos;
            towardMotivation.y = 0f;
            steering += NormalizeOrZero(towardMotivation) * independentLeaderMotivationWeight * leadership01;
        }

        if (steering.sqrMagnitude <= 0.0001f)
        {
            LogDebugFrame(
                $"Steering {DescribeWorldObject(sheep)} failed: steering zero. " +
                $"nearbyHerd={cohesionCount} aligned={alignmentCount} sensedAgents={sensedAgentScratch.Count} " +
                $"leader={DescribeWorldObject(leader)} pos={selfPos}");
            return false;
        }

        float speedFactor = dangerNearby
            ? dangerSpeedFactor
            : cohesionCount > 0
                ? flockingSpeedFactor
                : relaxedSpeedFactor;

        WalkMode walkMode = dangerNearby ? WalkMode.Run : WalkMode.Walk;
        result = new HerdSteeringResult(steering.normalized, speedFactor, walkMode, dangerNearby);
        LogDebugFrame(
            $"Steering {DescribeWorldObject(sheep)}: boids dir={result.directionMap} speed={speedFactor:0.00} " +
            $"nearbyHerd={cohesionCount} aligned={alignmentCount} sensedAgents={sensedAgentScratch.Count} danger={dangerNearby}");
        return true;
    }

    public static bool IsSheepAgent(WorldObject agent)
    {
        if (agent == null || agent.Kind != WorldObjectKind.Agent)
            return false;

        if (agent.species == Species.Sheep)
            return true;

        string breed = agent.breed ?? string.Empty;
        if (breed.IndexOf("sheep", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        string displayName = agent.DisplayName ?? string.Empty;
        return displayName.IndexOf("sheep", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void RefreshMemberProfiles()
    {
        memberProfiles.RemoveAll(profile => profile == null || profile.member == null || packAgentList == null || !packAgentList.Contains(profile.member));

        if (packAgentList == null)
            return;

        foreach (WorldObject member in packAgentList)
            GetOrCreateProfile(member);
    }

    private int ReconcileMemberListFromScene(bool forceLog)
    {
        if (packAgentList == null)
            packAgentList = new List<WorldObject>();

        int addedCount = 0;

        WorldObject[] childMembers = GetComponentsInChildren<WorldObject>(includeInactive: true);
        foreach (WorldObject childMember in childMembers)
        {
            if (TryReconcileMember(childMember, "child hierarchy", forceLog))
                addedCount++;
        }

        if (Application.isPlaying)
        {
            foreach (WorldObject registeredObject in WorldObjectRegistry.Instance.GetAllObjects())
            {
                if (registeredObject == null ||
                    registeredObject.Kind != WorldObjectKind.Agent ||
                    registeredObject.packMemberModule == null ||
                    registeredObject.packMemberModule.currentPack != this)
                {
                    continue;
                }

                if (TryReconcileMember(registeredObject, "currentPack reference", forceLog))
                    addedCount++;
            }
        }

        if (addedCount > 0)
            RefreshMemberProfiles();

        return addedCount;
    }

    private bool TryReconcileMember(WorldObject member, string source, bool forceLog)
    {
        if (member == null || member.Kind != WorldObjectKind.Agent)
            return false;

        if (member.packMemberModule == null)
        {
            LogDebug($"Reconcile skipped {DescribeWorldObject(member)} from {source}: missing PackMemberModule.", forceLog);
            return false;
        }

        if (packAgentList.Contains(member))
        {
            if (member.packMemberModule.currentPack == null)
                member.packMemberModule.currentPack = this;
            return false;
        }

        if (member.packMemberModule.currentPack != null && member.packMemberModule.currentPack != this)
            return false;

        member.packMemberModule.currentPack = this;
        packAgentList.Add(member);
        LogDebug($"Reconcile added {DescribeWorldObject(member)} from {source}.", forceLog);
        return true;
    }

    private HerdMemberProfile GetOrCreateProfile(WorldObject member)
    {
        if (member == null)
            return null;

        HerdMemberProfile profile = FindProfile(member);
        if (profile != null)
            return profile;

        profile = new HerdMemberProfile
        {
            member = member,
            leadership01 = autoAssignLeadershipRatings ? GenerateStableLeadershipRating(member) : 0f
        };
        memberProfiles.Add(profile);
        return profile;
    }

    private HerdMemberProfile FindProfile(WorldObject member)
    {
        if (member == null || memberProfiles == null)
            return null;

        for (int i = 0; i < memberProfiles.Count; i++)
        {
            HerdMemberProfile profile = memberProfiles[i];
            if (profile != null && profile.member == member)
                return profile;
        }

        return null;
    }

    private float GenerateStableLeadershipRating(WorldObject member)
    {
        int sourceHash = member.ObjectId != 0
            ? member.ObjectId
            : StringComparer.Ordinal.GetHashCode(member.DisplayName ?? member.name);

        uint hash = unchecked((uint)(sourceHash * 1103515245 + 12345));
        float random01 = (hash % 10000) / 9999f;
        if (random01 >= 0.82f)
            return Mathf.Lerp(0.65f, 1f, (random01 - 0.82f) / 0.18f);

        return Mathf.Lerp(0.05f, 0.45f, random01 / 0.82f);
    }

    private static Vector3 NormalizeOrZero(Vector3 value)
    {
        value.y = 0f;
        return value.sqrMagnitude > 0.0001f ? value.normalized : Vector3.zero;
    }

    public bool ShouldEmitDebugLogThisFrame()
    {
        return herdDebugLogging && Time.frameCount % DebugLogEveryFrames == 0;
    }

    public void LogDebug(string message)
    {
        LogDebug(message, force: false);
    }

    public void LogDebug(string message, bool force)
    {
        if (!force && !herdDebugLogging)
            return;

        Debug.Log($"[Herd {packName}] {message}", this);
    }

    public void LogDebugFrame(string message)
    {
        if (!ShouldEmitDebugLogThisFrame())
            return;

        LogDebug(message);
    }

    private string DescribeMemberCount()
    {
        return packAgentList != null ? packAgentList.Count.ToString() : "null";
    }

    private static string DescribeWorldObject(WorldObject value)
    {
        return value != null ? $"{value.DisplayName}#{value.ObjectId}" : "null";
    }
}
