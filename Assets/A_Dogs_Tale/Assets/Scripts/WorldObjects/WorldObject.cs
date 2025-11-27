using System.Collections.Generic;
using DogGame.AI;
using UnityEngine;

/// <summary>
/// Use Kind as:
///	  classification tag for world systems
///	  helper for editors / level tools
///	  query filter
///	  default prefab archetype
///	  save system hint
/// </summary>
public enum WorldObjectKind
{
    Unknown = 0,
    Agent   ,    // Brain, high level controller, join, control directly
    Scenery ,    // static objects
    Trigger ,    // environmental triggered location (trap, etc)
    Lever   ,    // usable fixed place item
    Movable ,    // can be pushed
    Obstacle,    // collider
    Item    ,    // pick up, use, food
    Portal  ,    // transport to other places or levels
    UI      ,    // if you ever want world-linked UI, optional
    Puzzle  ,    // World monitor/controller to tell stories
    // More...
}

/// <summary>
/// Root identity for anything that participates in the game world.
/// Attach this to Agents, Scenery, Traps, etc.
/// </summary>
[DisallowMultipleComponent]

[DefaultExecutionOrder(100)] // positive = runs late, after most other scripts
public class WorldObject : MonoBehaviour
{
    [Header("GameObject directory")]
    public ObjectDirectory dir;

    [Header("Identity")]
    [SerializeField] private int objectId = -1;
    [SerializeField] private string displayName;
    [SerializeField] private WorldObjectKind kind = WorldObjectKind.Unknown;
    [SerializeField] private bool autoRegister = true;

    // --------------------------
    // MODULE REFERENCES
    // Use Modules as:
	//   Behavior definition
	//   Interaction logic
	//   Ability providers
	//   Puzzle drivers
	//   Agent intelligence
    // --------------------------
    [Header("Modules (auto-populated)")]
    // Brain:
    public AgentController  AgentBrain { get; private set; }

    // Physical world controls:
    public LocationModule   Location   { get; private set; }
    public MotionModule     Motion     { get; private set; }

    // senses
    public VisualModule     Visual     { get; private set; }
    public ScentEmitterModule Scent      { get; private set; }
    public ActivatorModule  Activator  { get; private set; }

    // dynamic state data
    public AgentBlackboardView Blackboard { get; private set; }

    // generator instructions, not used after level generator
    public PlacementModule  Placement  { get; private set; }


    // Registration management functions
    public bool IsRegistered { get; private set; }

    public int ObjectId => objectId;
    public WorldObjectKind Kind => kind;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    private void Awake()
    {
        dir = FindFirstObjectByType<ObjectDirectory>();
        if (dir == null)
        {
            Debug.LogError($"WorldObject.Awake() was unable to find ObjectDirectory.");
        }
        // Auto-fill modules PER OBJECT
        AgentBrain= GetComponent<AgentController>();
        Location  = GetComponent<LocationModule>();
        Motion    = GetComponent<MotionModule>();
        Visual    = GetComponent<VisualModule>();
        Scent     = GetComponent<ScentEmitterModule>();
        Activator = GetComponent<ActivatorModule>();
        Placement = GetComponent<PlacementModule>();

        if (string.IsNullOrEmpty(displayName))
            displayName = gameObject.name;

        List<WorldModule> modules = new();
        // Find all attached modules
        modules.AddRange(GetComponents<WorldModule>());

        // Initialize each module
        foreach (var module in modules)
        {
            module.Initialize(this);    // save this pointer to me in all my modules
        }
    }

    public T GetModule<T>() where T : WorldModule
    {
        List<WorldModule> modules = new();
        // Find all attached modules
        modules.AddRange(GetComponents<WorldModule>());

        foreach (var module in modules)
        {
            if (module is T typed) return typed;
        }
        return null;
    }

    private void OnEnable()
    {
        if (!autoRegister) return;
        RegisterIfNeeded();
    }

    private void OnDisable()
    {
        // Don't bother with registry during teardown or in edit-time
        if (!Application.isPlaying)
            return;

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