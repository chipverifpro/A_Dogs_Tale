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
    public Directory dir;

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
    // Agent: (agentModule will add more Module types exclusively for agents)
    public AgentModule  agentModule { get; private set; }

    // Sensory:
    public EatModule eatModule { get; private set; }
    public HearingModule hearingModule { get; private set; }
    public SmellModule smellModule { get; private set; }
    public VisionModule visionModule { get; private set; }

    // Output:
    public AppearanceModule appearanceModule { get; private set; }
    public NoiseMakerModule noiseMakerModule { get; private set; }
    public ScentEmitterModule scentEmitterModule { get; private set; }

    // Ability:
    public ActivatorModule activatorModule { get; private set; }
    public ContainerModule containerModule { get; private set; }
    public InteractionModule interactionModule { get; private set; }
    public LocationModule locationModule { get; private set; }
    public MotionModule motionModule { get; private set; }
    
    // Data:
    public BlackboardModule blackboardModule { get; private set; }
    public PlacementModule placementModule { get; private set; }
    public StatusModule statusModule { get; private set; }

    // Quest:
    public QuestModuleBase questModuleBase { get; private set; }


    // Registration management functions
    public bool IsRegistered { get; private set; }

    public int ObjectId => objectId;
    public WorldObjectKind Kind => kind;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    private void Awake()
    {
        dir = FindFirstObjectByType<Directory>();
        if (dir == null)
        {
            Debug.LogError($"WorldObject.Awake() was unable to find ObjectDirectory.");
        }
        // Auto-fill modules PER OBJECT
        // --- Agent ---
        agentModule = GetComponent<AgentModule>();

        // --- Sensory ---
        eatModule     = GetComponent<EatModule>();
        hearingModule = GetComponent<HearingModule>();
        smellModule   = GetComponent<SmellModule>();
        visionModule  = GetComponent<VisionModule>();

        // --- Output ---
        appearanceModule  = GetComponent<AppearanceModule>();
        noiseMakerModule  = GetComponent<NoiseMakerModule>();
        scentEmitterModule= GetComponent<ScentEmitterModule>();

        // --- Ability ---
        activatorModule   = GetComponent<ActivatorModule>();
        containerModule   = GetComponent<ContainerModule>();
        interactionModule = GetComponent<InteractionModule>();
        locationModule    = GetComponent<LocationModule>();
        motionModule      = GetComponent<MotionModule>();

        // --- Data ---
        blackboardModule  = GetComponent<BlackboardModule>();
        placementModule   = GetComponent<PlacementModule>();
        statusModule      = GetComponent<StatusModule>();

        // --- Quest ---
        questModuleBase   = GetComponent<QuestModuleBase>();

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
        //if (!WorldObjectRegistry.HasInstance)
        //{
        //    Debug.LogWarning($"WorldObject '{DisplayName}' cannot register: no WorldObjectRegistry found.");
        //    return;
        //}

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