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

[DefaultExecutionOrder(100)] // positive = runs late, after most other scripts
public class WorldObject : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private int objectId = -1;
    [SerializeField] private string displayName;
    [SerializeField] private WorldObjectKind kind = WorldObjectKind.Unknown;
    [SerializeField] private bool autoRegister = true;

    // --------------------------
    // MODULE REFERENCES
    // --------------------------
    [Header("Modules (auto-populated)")]
    public LocationModule   Location   { get; private set; }
    public MotionModule     Motion     { get; private set; }
    public VisualModule     Visual     { get; private set; }
    public NPCModule        NPC        { get; private set; }
    public ScentEmitter     Scent      { get; private set; }
    public ActivatorModule  Activator  { get; private set; }

    public bool IsRegistered { get; private set; }

    public int ObjectId => objectId;
    public WorldObjectKind Kind => kind;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    private void Awake()
    {
        // Auto-fill modules PER OBJECT
        Location  = GetComponent<LocationModule>();
        Motion    = GetComponent<MotionModule>();
        Visual    = GetComponent<VisualModule>();
        NPC       = GetComponent<NPCModule>();
        Scent     = GetComponent<ScentEmitter>();
        Activator = GetComponent<ActivatorModule>();

        if (string.IsNullOrEmpty(displayName))
            displayName = gameObject.name;
    }

    private void OnEnable()
    {
        if (!autoRegister) return;
        RegisterIfNeeded();
    }

    private void OnDisable()
    {
        if (IsRegistered && WorldObjectRegistry.HasInstance)
        {
            WorldObjectRegistry.Instance.Unregister(this);
            IsRegistered = false;
        }
    }

    public void RegisterIfNeeded()
    {
        if (IsRegistered || !autoRegister) return;
        if (!WorldObjectRegistry.HasInstance)
        {
            Debug.LogWarning($"WorldObject '{DisplayName}' cannot register: no WorldObjectRegistry found.");
            return;
        }

        int assigned = WorldObjectRegistry.Instance.Register(this);
        if (assigned >= 0)
        {
            objectId = assigned;
            IsRegistered = true;
        }
    }

    public override string ToString()
    {
        return $"WorldObject[{objectId}] {DisplayName} ({kind})";
    }
    public void SetObjectId(int newId) => objectId = newId;


#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(displayName))
            displayName = gameObject.name;
    }
#endif

}