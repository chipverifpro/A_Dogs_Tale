using UnityEngine;

public enum WorldObjectKind
{
    Unknown = 0,
    Agent   = 1,
    Scenery = 2,
    Trap    = 3,
    Gizmo   = 4,
    Item    = 5,
    Portal  = 6,
    UI      = 7, // if you ever want world-linked UI, optional
}

/// <summary>
/// Root identity for anything that participates in the game world.
/// Attach this to Agents, Scenery, Traps, etc.
/// </summary>
[DisallowMultipleComponent]
public class WorldObject : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Stable ID for this object within the game world / registry.")]
    [SerializeField]
    private int objectId = -1;

    [Tooltip("Human-readable name for debugging and UI.")]
    [SerializeField]
    private string displayName;

    [Tooltip("Rough category of this world object.")]
    [SerializeField]
    private WorldObjectKind kind = WorldObjectKind.Unknown;

    [Header("Registration")]
    [Tooltip("If true, this object registers itself with WorldObjectRegistry on enable.")]
    [SerializeField]
    private bool autoRegister = true;

    /// <summary>Unique ID within the WorldObjectRegistry.</summary>
    public int ObjectId => objectId;

    /// <summary>Category of this object (Agent, Scenery, etc.).</summary>
    public WorldObjectKind Kind => kind;

    /// <summary>Display name, defaults to GameObject name if not set.</summary>
    public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;

    /// <summary>True if this object is currently registered in the global registry.</summary>
    public bool IsRegistered { get; private set; }

    private void Awake()
    {
        // Ensure we at least have a display name.
        if (string.IsNullOrEmpty(displayName))
            displayName = gameObject.name;
    }

    private void OnEnable()
    {
        if (!autoRegister)
            return;

        RegisterIfNeeded();
    }

    private void OnDisable()
    {
        // Only unregister if we were previously registered.
        if (IsRegistered && WorldObjectRegistry.HasInstance)
        {
            WorldObjectRegistry.Instance.Unregister(this);
            IsRegistered = false;
        }
    }

    /// <summary>
    /// Registers this object with the global registry, assigning an ID if needed.
    /// Safe to call multiple times; it will do nothing if already registered.
    /// </summary>
    public void RegisterIfNeeded()
    {
        if (IsRegistered || !autoRegister)
            return;

        if (!WorldObjectRegistry.HasInstance)
        {
            Debug.LogWarning($"WorldObject '{DisplayName}' cannot register: no WorldObjectRegistry in scene.");
            return;
        }

        int assignedId = WorldObjectRegistry.Instance.Register(this);
        if (assignedId >= 0)
        {
            objectId = assignedId;
            IsRegistered = true;
        }
    }

    /// <summary>
    /// Allows the registry to set / update the ID.
    /// You generally shouldn’t call this directly.
    /// </summary>
    public void SetObjectId(int newId)
    {
        objectId = newId;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Keep displayName in sync for convenience in editor
        if (string.IsNullOrEmpty(displayName))
            displayName = gameObject.name;
    }
#endif

    public override string ToString()
    {
        return $"WorldObject[{objectId}] {DisplayName} ({kind})";
    }
}