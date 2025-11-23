using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global registry for all WorldObjects in the current scene / level.
/// Responsible for ID assignment and lookups.
/// </summary>
[DefaultExecutionOrder(-900)] // big negative = runs very early
public class WorldObjectRegistry : MonoBehaviour
{
    private static WorldObjectRegistry _instance;
    public static WorldObjectRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<WorldObjectRegistry>();
                if (_instance == null)
                {
                    Debug.LogError("WorldObjectRegistry: No instance found in scene. Please add one.");
                }
            }
            return _instance;
        }
    }

    public static bool HasInstance => Instance != null;

    [Tooltip("Starting ID value for auto-assigned objects.")]
    [SerializeField]
    private int startingId = 1;

    // Runtime maps
    private readonly Dictionary<int, WorldObject> objectsById = new();
    private readonly Dictionary<WorldObject, int> idByObject = new();

    private int nextId;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("Multiple WorldObjectRegistry instances found. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        nextId = startingId;
    }

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
            return requestedId;
        }

        // Case 2: assign a new ID
        int newId = AllocateId();
        objectsById[newId] = obj;
        idByObject[obj] = newId;
        obj.SetObjectId(newId);

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