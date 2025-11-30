using System.Collections.Generic;
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

    private readonly Dictionary<WorldObjectKind, Transform> _kindParents = new();

    private static WorldObjectRegistry _instance;
    private static bool _shuttingDown;

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

    public static bool HasInstance => Instance != null && _instance != null && Application.isPlaying && !_shuttingDown;
    
    public static bool HasInstance_new =>
        _instance != null && Application.isPlaying && !_shuttingDown;
        
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"Duplicate WorldObjectRegistry found. Destroying {gameObject.name}.", this);
            Destroy(gameObject);
            return;
        }

        _instance = this;
        nextId = startingId;

        EnsureHierarchyRoot();
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
            return requestedId;
        }

        // Case 2: assign a new ID
        int newId = AllocateId();
        objectsById[newId] = obj;
        idByObject[obj] = newId;
        obj.SetObjectId(newId);

        AssignParentForWorldObject(obj);
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
            if (objectsById.TryGetValue(id, out WorldObject stored) && stored == obj)
            {
                objectsById.Remove(id);
            }
        }
    }

    /// <summary>
    /// Try to get a world object by ID.
    /// </summary>
    public bool TryGet(int id, out WorldObject obj)
    {
        return objectsById.TryGetValue(id, out obj);
    }

    /// <summary>
    /// Get a world object by ID, or null if not found.
    /// </summary>
    public WorldObject Get(int id)
    {
        objectsById.TryGetValue(id, out var obj);
        return obj;
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

        var parent = GetParentForKind(wo.Kind);  // or wo.worldObjectKind / wo.kind – use your actual field
        if (parent == null)
            return;

        wo.transform.SetParent(parent, true); // keep world position
    }

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