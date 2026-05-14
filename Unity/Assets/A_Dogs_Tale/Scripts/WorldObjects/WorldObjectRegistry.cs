using System;
using System.Collections;
using System.Collections.Generic;
using DogGame.Modules;
using UnityEngine;

/// <summary>
/// Global registry for all WorldObjects in the current scene / level.
/// Responsible for ID assignment and lookups.
/// </summary>
[DefaultExecutionOrder(-900)] // big negative = runs very early
public class WorldObjectRegistry : MonoBehaviour
{
    [Header("Hierarchy")]
    [Tooltip("Optional explicit root for all WorldObjects. If null, one named 'WorldObjects' will be created.")]
    [SerializeField] private Transform worldObjectsRoot;

    [Header("Initial Agent Placement")]
    [Tooltip("When enabled, agents are moved to random generated floor cells after they are initially registered.")]
    [SerializeField] private bool randomizeInitialAgentPlacement = false;

    [Tooltip("If false, agents can be placed in corridor cells as well as rooms.")]
    [SerializeField] private bool excludeCorridorCells = true;

    [Tooltip("Randomize yaw after moving an agent to its initial cell.")]
    [SerializeField] private bool randomizeInitialAgentYaw = true;

    [Tooltip("Maximum random cell picks per agent before falling back to the first available cell.")]
    [SerializeField] private int maxRandomPlacementAttemptsPerAgent = 128;

    [Tooltip("Write a log entry for every agent moved by initial placement randomization.")]
    [SerializeField] private bool logInitialAgentPlacement = false;

    private readonly Dictionary<WorldObjectKind, Transform> _kindParents = new();
    private readonly HashSet<WorldObject> pendingInitialAgentPlacement = new();
    private readonly HashSet<WorldObject> randomizedInitialAgents = new();
    private Coroutine initialAgentPlacementCoroutine;

    private static WorldObjectRegistry _instance;
    private static bool _shuttingDown;

    internal static void ResetStaticStateForReload()
    {
        _instance = null;
        _shuttingDown = false;
    }

    // Begin a bunch of crap for starting up and shutting down safely.

    public static WorldObjectRegistry Instance
    {
        get
        {
            //Debug.Log($"WorldObjectRegistry Instance accessed.  _shuttingDown={_shuttingDown}, Application.isPlaying={Application.isPlaying}, _instance={_instance}");
            if (_shuttingDown || !Application.isPlaying)
                return _instance;

            if (_instance == null)
            {
                _instance = FindFirstObjectByType<WorldObjectRegistry>();
                if (_instance == null)
                {
                    // Auto-create instead of logging error
                    var go = new GameObject("WorldObjectRegistry");
                    _instance = go.AddComponent<WorldObjectRegistry>();
                    //DontDestroyOnLoad(go);
                    Debug.LogWarning("WorldObjectRegistry: No instance found, created one automatically.");
                }
            }

            return _instance;
        }
    }

//    public static bool HasInstance =>
//        _instance != null && Application.isPlaying && !_shuttingDown;

    private void Awake()
    {
        if (!TryRegisterSingletonInstance())
            return;

        nextId = startingId;

        EnsureHierarchyRoot();
    }

    private void OnEnable()
    {
        if (!TryRegisterSingletonInstance())
            return;

        if (nextId < startingId)
            nextId = startingId;

        EnsureHierarchyRoot();
    }

    private bool TryRegisterSingletonInstance()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"Duplicate WorldObjectRegistry found. Destroying {gameObject.name}.", this);
            Destroy(gameObject);
            return false;
        }

        _instance = this;
        return true;
    }

    private void EnsureHierarchyRoot()
    {
        if (worldObjectsRoot != null)
            return;

        // Try to find an existing one first
        var existing = GameObject.Find("WorldObjects");
        if (existing != null)
        {
            worldObjectsRoot = existing.transform;
            return;
        }

        // Otherwise create a new root
        var rootGo = new GameObject("WorldObjects");
        worldObjectsRoot = rootGo.transform;
    }

    private void OnApplicationQuit()
    {
        _shuttingDown = true;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
    // End a bunch of crap for shutting down safely.

    [Tooltip("Starting ID value for auto-assigned objects.")]
    [SerializeField]
    private int startingId = 1;

    // Runtime maps
    private readonly Dictionary<int, WorldObject> objectsById = new();
    private readonly Dictionary<WorldObject, int> idByObject = new();

    [SerializeField]
    private int nextId;

    /// <summary>
    /// Register a WorldObject. If it already has a valid ID and that ID is free, we honor it.
    /// Otherwise we assign the next available ID.
    /// </summary>
    /// <returns>The ID assigned to this object, or -1 on failure.</returns>
    public int Register(WorldObject obj)
    {
        if (obj == null)
            return -1;

        // Already known?
        if (idByObject.TryGetValue(obj, out int existingId))
        {
            // ensure mapping consistency
            objectsById[existingId] = obj;
            // AssignParentForWorldObject(obj); // Don't move an existing object.
            return existingId;
        }

        int requestedId = obj.ObjectId;

        // Case 1: object has a positive ID and it's free → use it
        if (requestedId > 0 && !objectsById.ContainsKey(requestedId))
        {
            objectsById[requestedId] = obj;
            idByObject[obj] = requestedId;
            // Keep nextId ahead so we don't collide later
            if (requestedId >= nextId)
                nextId = requestedId + 1;
            AssignParentForWorldObject(obj);
            QueueInitialAgentPlacementIfNeeded(obj);
            return requestedId;
        }

        // Case 2: assign a new ID
        int newId = AllocateId();
        objectsById[newId] = obj;
        idByObject[obj] = newId;
        obj.SetObjectId(newId);

        AssignParentForWorldObject(obj);
        QueueInitialAgentPlacementIfNeeded(obj);
        return newId;
    }

    /// <summary>
    /// Remove a WorldObject from the registry.
    /// Safe to call even if the object was never registered.
    /// </summary>
    public void Unregister(WorldObject obj)
    {
        if (obj == null)
            return;

        if (idByObject.TryGetValue(obj, out int id))
        {
            idByObject.Remove(obj);
            pendingInitialAgentPlacement.Remove(obj);
            randomizedInitialAgents.Remove(obj);
            if (objectsById.TryGetValue(id, out WorldObject stored) && stored == obj)
            {
                objectsById.Remove(id);
            }
        }
    }

    /// <summary>
    /// Try to get a world object by ID.
    /// </summary>
    public bool TryGet(int id, out WorldObject obj) => objectsById.TryGetValue(id, out obj);

    /// <summary>
    /// Try to get an ID by world object.
    /// </summary>
    public bool TryGetId(WorldObject obj, out int id) => idByObject.TryGetValue(obj, out id);

    /// <summary>
    /// Try to get a world object by DisplayName, case-insensitively.
    /// </summary>
    public bool TryGetByDisplayName(string displayName, out WorldObject obj)
    {
        obj = null;

        if (string.IsNullOrWhiteSpace(displayName))
            return false;

        foreach (WorldObject candidate in objectsById.Values)
        {
            if (candidate == null)
                continue;

            if (string.Equals(candidate.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
            {
                obj = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Enumerate all currently registered objects.
    /// </summary>
    public IEnumerable<WorldObject> GetAllObjects()
    {
        return objectsById.Values;
    }

    private int AllocateId()
    {
        // Simple monotonic allocator. If you ever care about reuse, you can add a free list later.
        while (objectsById.ContainsKey(nextId))
        {
            nextId++;
        }
        return nextId++;
    }

    private Transform GetParentForKind(WorldObjectKind kind)
    {
        if (worldObjectsRoot == null)
            return null;

        if (_kindParents.TryGetValue(kind, out var parent) && parent != null)
            return parent;

        // Create a new child folder under WorldObjects
        string childName = kind.ToString(); // e.g. "Agent", "Scenery", etc.
        var childGo = new GameObject(childName);
        childGo.transform.SetParent(worldObjectsRoot, false);

        parent = childGo.transform;
        _kindParents[kind] = parent;
        return parent;
    }

    private void AssignParentForWorldObject(WorldObject wo)
    {
        // Only reparent during play, so we don't mess with edit-time layout unless you want that too.
        if (!Application.isPlaying)
            return;

        if (wo.transform == null)
            return;

        if (wo.agentModule!=null)
            return;     // Agents will get moved in the agent, so don't bother finding/creating a folder for it.

        var parent = GetParentForKind(wo.Kind);  // or wo.worldObjectKind / wo.kind – use your actual field
        if (parent == null)
            return;

        wo.transform.SetParent(parent, true); // keep world position
    }

    private void QueueInitialAgentPlacementIfNeeded(WorldObject obj)
    {
        if (!randomizeInitialAgentPlacement || !Application.isPlaying || obj == null || !IsAgent(obj))
            return;

        if (randomizedInitialAgents.Contains(obj))
            return;

        pendingInitialAgentPlacement.Add(obj);

        if (initialAgentPlacementCoroutine == null)
            initialAgentPlacementCoroutine = StartCoroutine(RandomizeInitialAgentPlacementWhenReady());
    }

    private IEnumerator RandomizeInitialAgentPlacementWhenReady()
    {
        while (!CanRandomizeInitialAgentPlacement())
            yield return null;

        RandomizePendingInitialAgentPlacements();
        initialAgentPlacementCoroutine = null;
    }

    private bool CanRandomizeInitialAgentPlacement()
    {
        if (pendingInitialAgentPlacement.Count == 0)
            return true;

        Dir dir = Dir.Instance;
        DungeonGenerator generator = dir != null ? dir.gen : null;
        return generator != null
            && generator.buildComplete
            && generator.rooms != null
            && generator.rooms.Count > 0;
    }

    private void RandomizePendingInitialAgentPlacements()
    {
        if (pendingInitialAgentPlacement.Count == 0)
            return;

        List<Cell> candidateCells = CollectInitialAgentPlacementCells();
        if (candidateCells.Count == 0)
        {
            Debug.LogWarning("WorldObjectRegistry: Initial agent placement randomization found no eligible generated cells.", this);
            pendingInitialAgentPlacement.Clear();
            return;
        }

        List<WorldObject> agents = new(pendingInitialAgentPlacement);
        pendingInitialAgentPlacement.Clear();

        HashSet<Vector3Int> occupiedCells = new();
        HashSet<WorldObject> pendingAgents = new(agents);
        foreach (WorldObject agent in agents)
        {
            RandomizeInitialAgentPlacementForAgent(agent, candidateCells, occupiedCells, pendingAgents);
        }
    }

    private bool RandomizeInitialAgentPlacementForAgent(
        WorldObject agent,
        List<Cell> candidateCells,
        HashSet<Vector3Int> occupiedCells,
        HashSet<WorldObject> pendingAgents)
    {
        if (agent == null)
            return false;

        if (randomizedInitialAgents.Contains(agent))
            return true;

        WorldObject leader = GetPackLeaderForFollower(agent);
        if (leader != null)
        {
            if (!randomizedInitialAgents.Contains(leader) && pendingAgents.Contains(leader))
            {
                RandomizeInitialAgentPlacementForAgent(leader, candidateCells, occupiedCells, pendingAgents);
            }

            ApplyInitialAgentPlacementFromLeader(agent, leader);
            randomizedInitialAgents.Add(agent);
            return true;
        }

        if (!TryPickRandomCell(candidateCells, occupiedCells, out Cell cell))
            return false;

        ApplyInitialAgentPlacement(agent, cell);
        occupiedCells.Add(cell.pos3d);
        randomizedInitialAgents.Add(agent);
        return true;
    }

    private List<Cell> CollectInitialAgentPlacementCells()
    {
        List<Cell> cells = new();
        Dir dir = Dir.Instance;
        DungeonGenerator generator = dir != null ? dir.gen : null;
        if (generator == null || generator.rooms == null)
            return cells;

        for (int roomIndex = 0; roomIndex < generator.rooms.Count; roomIndex++)
        {
            Room room = generator.rooms[roomIndex];
            if (room == null || room.cells == null)
                continue;

            if (excludeCorridorCells && room.isCorridor)
                continue;

            for (int cellIndex = 0; cellIndex < room.cells.Count; cellIndex++)
            {
                Cell cell = room.cells[cellIndex];
                if (cell == null)
                    continue;

                if (excludeCorridorCells && cell.isCorridor)
                    continue;

                cells.Add(cell);
            }
        }

        return cells;
    }

    private bool TryPickRandomCell(List<Cell> cells, HashSet<Vector3Int> occupiedCells, out Cell chosenCell)
    {
        chosenCell = null;
        if (cells == null || cells.Count == 0)
            return false;

        int attempts = Mathf.Max(1, maxRandomPlacementAttemptsPerAgent);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Cell candidate = cells[UnityEngine.Random.Range(0, cells.Count)];
            if (candidate == null || occupiedCells.Contains(candidate.pos3d))
                continue;

            chosenCell = candidate;
            return true;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            Cell candidate = cells[i];
            if (candidate == null || occupiedCells.Contains(candidate.pos3d))
                continue;

            chosenCell = candidate;
            return true;
        }

        chosenCell = cells[UnityEngine.Random.Range(0, cells.Count)];
        return chosenCell != null;
    }

    private void ApplyInitialAgentPlacement(WorldObject agent, Cell cell)
    {
        float unitHeight = Dir.Instance != null && Dir.Instance.cfg != null
            ? Mathf.Max(0.0001f, Dir.Instance.cfg.unitHeight)
            : 1f;
        Vector3 mapPosition = new(cell.x + 0.5f, cell.height * unitHeight, cell.y + 0.5f);
        Vector3 worldPosition = agent.MapToWorldPosition(mapPosition);
        agent.transform.position = worldPosition;

        if (randomizeInitialAgentYaw)
            agent.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

        agent.agentMovementModule?.ClearDesiredMovement();

        if (logInitialAgentPlacement)
            Debug.Log($"WorldObjectRegistry: Randomized initial placement for {agent.DisplayName} to cell {cell.pos}.", agent);
    }

    private void ApplyInitialAgentPlacementFromLeader(WorldObject follower, WorldObject leader)
    {
        follower.transform.SetPositionAndRotation(leader.transform.position, leader.transform.rotation);
        follower.agentMovementModule?.ClearDesiredMovement();

        if (logInitialAgentPlacement)
        {
            Debug.Log(
                $"WorldObjectRegistry: Moved pack follower {follower.DisplayName} to leader {leader.DisplayName}'s initial placement.",
                follower);
        }
    }

    private static WorldObject GetPackLeaderForFollower(WorldObject agent)
    {
        Pack pack = agent != null ? agent.packMemberModule?.currentPack : null;
        WorldObject leader = pack != null ? pack.packLeader : null;
        return leader != null && leader != agent ? leader : null;
    }

    private static bool IsAgent(WorldObject obj)
    {
        return obj.Kind == WorldObjectKind.Agent || obj.agentModule != null || obj.GetComponent<AgentModule>() != null;
    }

#if UNITY_EDITOR
    [ContextMenu("Randomize Registered Agent Placement Now")]
    private void RandomizeRegisteredAgentPlacementNow()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("WorldObjectRegistry: Randomize Registered Agent Placement Now only runs in Play Mode.", this);
            return;
        }

        foreach (WorldObject obj in objectsById.Values)
        {
            if (obj != null && IsAgent(obj))
                pendingInitialAgentPlacement.Add(obj);
        }

        randomizedInitialAgents.Clear();

        if (CanRandomizeInitialAgentPlacement())
            RandomizePendingInitialAgentPlacements();
        else if (initialAgentPlacementCoroutine == null)
            initialAgentPlacementCoroutine = StartCoroutine(RandomizeInitialAgentPlacementWhenReady());
    }
#endif

#if UNITY_EDITOR
    [ContextMenu("Log Registered Objects")]
    private void LogRegisteredObjects()
    {
        foreach (var kvp in objectsById)
        {
            Debug.Log($"ID {kvp.Key} -> {kvp.Value}", kvp.Value);
        }
    }
#endif
}
