using System.Collections.Generic;
using UnityEngine;
using DogGame.Modules;
using DogGame.AI;
using System;
using System.Linq;


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

[Flags]
public enum ModuleFlags : ulong
{
    none          = 0UL,
    // --- Sensory ---
    hearingModule = 1UL << 1,
    smellModule   = 1UL << 2,
    visionModule  = 1UL << 3,
    eatModule     = 1UL << 4,

    // --- Agent Decision Modules
    playerDecisionModule   = 1UL << 5,
    followerDecisionModule = 1UL << 6,
    wanderDecisionModule   = 1UL << 7,

    // --- Agent Interface Modules ---
    agentMovementModule   = 1UL << 8,
    packMemberModule = 1UL << 9,
    // OBSOLETE: agentSensesModule     = 1UL << 10,
    agentModule           = 1UL << 11,

    // --- Motivation ---
    motivationModule   = 1UL << 12,
        
    // --- Ability ---
    activatorModule   = 1UL << 13,
    containerModule   = 1UL << 14,
    interactionModule = 1UL << 15,
    locationModule    = 1UL << 16,
    motionModule      = 1UL << 17,

    // --- Output ---
    appearanceModule  = 1UL << 18,
    noiseMakerModule  = 1UL << 19,
    scentEmitterModule= 1UL << 20,


    // --- Data ---
    blackboardModule  = 1UL << 21,
    placementModule   = 1UL << 22,
    statusModule      = 1UL << 23,

    // --- Quest ---
    questModuleBase   = 1UL << 24,
}

// The following templates can be used for configuring new WorldModule instantiations...
public static class ModuleFlagsTemplates  // extension functions for the ModuleFlags enum
{
    public static ModuleFlags All { get {
            ModuleFlags all = 0;
            foreach (ModuleFlags flag in Enum.GetValues(typeof(ModuleFlags)))
                all |= flag;
            return all;
        }
    }
    // Some examples of handy configurations...
    public static readonly ModuleFlags FullAgent =  All
                                                     & ~ModuleFlags.questModuleBase
                                                     & ~ModuleFlags.placementModule;
    public static readonly ModuleFlags ScatterTerrain = ModuleFlags.placementModule
                                                       | ModuleFlags.scentEmitterModule
                                                       | ModuleFlags.appearanceModule;
    public static readonly ModuleFlags TreasureChest = ScatterTerrain
                                                      | ModuleFlags.containerModule;
}


/// <summary>
/// Root identity for anything that participates in the game world.
/// Attach this to Agents, Scenery, Traps, etc.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)] // positive = runs early, before modules try to access their worldObject
public class WorldObject : MonoBehaviour
{
    [Header("GameObject directory")]
    public Directory dir => Directory.Instance;

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

    // --- Agent Decision Modules
    public PlayerDecisionModule playerDecisionModule { get; private set; }
    public FollowerDecisionModule followerDecisionModule { get; private set; }
    public WandererDecisionModule wandererDecisionModule { get; private set; }

    // --- Agent Interface Modules ---
    public AgentMovementModule agentMovementModule { get; private set; }
    public PackMemberModule packMemberModule { get; private set; }

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

    // Motivation:
    public MotivationModule motivationModule { get; private set; }
    
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
        //dir = FindFirstObjectByType<Directory>();
        if (dir == null)
        {
            Debug.LogError($"WorldObject.Awake() was unable to find Directory.");
        }

        // Auto-fill modules PER OBJECT

        // --- Sensory ---
        hearingModule = GetComponent<HearingModule>();
        smellModule   = GetComponent<SmellModule>();
        visionModule  = GetComponent<VisionModule>();
        eatModule     = GetComponent<EatModule>();

        // --- Agent Decision Modules
        playerDecisionModule   = GetComponent<PlayerDecisionModule>();
        followerDecisionModule = GetComponent<FollowerDecisionModule>();
        wandererDecisionModule = GetComponent<WandererDecisionModule>();

        // --- Agent Interface Modules ---
        agentMovementModule   = GetComponent<AgentMovementModule>();
        packMemberModule = GetComponent<PackMemberModule>();
        agentModule           = GetComponent<AgentModule>();

        // --- Motivation ---
        motivationModule   = GetComponent<MotivationModule>();
            
        // --- Ability ---
        activatorModule   = GetComponent<ActivatorModule>();
        containerModule   = GetComponent<ContainerModule>();
        interactionModule = GetComponent<InteractionModule>();
        locationModule    = GetComponent<LocationModule>();
        motionModule      = GetComponent<MotionModule>();

        // --- Output ---
        appearanceModule  = GetComponent<AppearanceModule>();
        noiseMakerModule  = GetComponent<NoiseMakerModule>();
        scentEmitterModule= GetComponent<ScentEmitterModule>();


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

    private void Update()
    {
        TickCallerAllModules();
    }

    private void TickCallerAllModules()
    {
        float dt = Time.deltaTime;

        // SENSES

        //visionModule?.Tick(dt);
        //hearingModule?.Tick(dt);
        //smellModule?.Tick(dt);
        //eatModule?.Tick(dt);

        // AGENT DECISION
        agentModule?.Tick(dt);  // forwards to appropriate active DecisionModule...
        //playerDecisionModule?.Tick(dt);
        //wanderDecisionModule?.Tick(dt);
        //followerDecisionModule?.Tick(dt);

        // AGENT INTERFACE
        agentMovementModule?.Tick(dt);
        packMemberModule?.Tick(dt);

        // MOTIVATION
        motivationModule?.Tick(dt);

        // ABILITY
        motionModule?.Tick(dt);
        //if (locationModule != null)  locationModule.Tick(dt);
        //if (activatorModule != null)  activatorModule.Tick(dt);
        //if (containerModule != null)  containerModule.Tick(dt);
        //if (interactionModule != null)  interactionModule.Tick(dt);
        
        // DATA
        //if (blackboardModule != null)  blackboardModule.Tick(dt);
        //if (placementModule != null)  placementModule.Tick(dt);
        //if (statusModule != null)  statusModule.Tick(dt);

        // OUTPUT
        //if (appearanceModule != null)  appearanceModule.Tick(dt);
        noiseMakerModule?.Tick(dt);
        scentEmitterModule?.Tick(dt);
        
        // QUEST
        //if (fetchQuestModule != null)  fetchQuestModule.Tick(dt);
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



    // Allows for creating a template of enables for a particular
    // type of WorldObject, and all the appropriate modules get created.
    // Obviously, to run this, you already created the WorldObject we are in.
    public void CreateModulesIfNeeded(ModuleFlags enables)
    {
        // ===============================
        // Sensory
        // ===============================
        if (enables.HasFlag(ModuleFlags.hearingModule))
            hearingModule = EnsureComponent<HearingModule>();
        if (hearingModule == null) Debug.LogWarning($"hearingModule = null");

        if (enables.HasFlag(ModuleFlags.smellModule))
            smellModule = EnsureComponent<SmellModule>();
        if (smellModule == null) Debug.LogWarning($"smellModule = null");

        if (enables.HasFlag(ModuleFlags.visionModule))
            visionModule = EnsureComponent<VisionModule>();
        if (visionModule == null) Debug.LogWarning($"visionModule = null");

        if (enables.HasFlag(ModuleFlags.eatModule))
            eatModule = EnsureComponent<EatModule>();
        if (eatModule == null) Debug.LogWarning($"eatModule = null");


        // ===============================
        // Agent Decision Modules
        // ===============================
        if (enables.HasFlag(ModuleFlags.playerDecisionModule))
            playerDecisionModule = EnsureComponent<PlayerDecisionModule>();
        if (playerDecisionModule == null) Debug.LogWarning($"playerDecisionModule = null");

        if (enables.HasFlag(ModuleFlags.followerDecisionModule))
            followerDecisionModule = EnsureComponent<FollowerDecisionModule>();
        if (followerDecisionModule == null) Debug.LogWarning($"followerDecisionModule = null");

        if (enables.HasFlag(ModuleFlags.wanderDecisionModule))
            wandererDecisionModule = EnsureComponent<WandererDecisionModule>();
        if (wandererDecisionModule == null) Debug.LogWarning($"wandererDecisionModule = null");


        // ===============================
        // Agent Interface Modules
        // ===============================
        if (enables.HasFlag(ModuleFlags.agentMovementModule))
            agentMovementModule = EnsureComponent<AgentMovementModule>();
        if (agentMovementModule == null) Debug.LogWarning($"agentMovementModule = null");

        if (enables.HasFlag(ModuleFlags.packMemberModule))
            packMemberModule = EnsureComponent<PackMemberModule>();
        if (packMemberModule == null) Debug.LogWarning($"packMemberModule = null");

        if (enables.HasFlag(ModuleFlags.agentModule))
            agentModule = EnsureComponent<AgentModule>();
        if (agentModule == null) Debug.LogWarning($"agentModule = null");


        // ===============================
        // Motivation
        // ===============================
        if (enables.HasFlag(ModuleFlags.motivationModule))
            motivationModule = EnsureComponent<MotivationModule>();
        if (motivationModule == null) Debug.LogWarning($"motivationModule = null");


        // ===============================
        // Ability
        // ===============================
        if (enables.HasFlag(ModuleFlags.activatorModule))
            activatorModule = EnsureComponent<ActivatorModule>();
        if (activatorModule == null) Debug.LogWarning($"activatorModule = null");

        if (enables.HasFlag(ModuleFlags.containerModule))
            containerModule = EnsureComponent<ContainerModule>();
        if (containerModule == null) Debug.LogWarning($"containerModule = null");

        if (enables.HasFlag(ModuleFlags.interactionModule))
            interactionModule = EnsureComponent<InteractionModule>();
        if (interactionModule == null) Debug.LogWarning($"interactionModule = null");

        if (enables.HasFlag(ModuleFlags.locationModule))
            locationModule = EnsureComponent<LocationModule>();
        if (locationModule == null) Debug.LogWarning($"locationModule = null");

        if (enables.HasFlag(ModuleFlags.motionModule))
            motionModule = EnsureComponent<MotionModule>();
        if (motionModule == null) Debug.LogWarning($"motionModule = null");


        // ===============================
        // Output
        // ===============================
        if (enables.HasFlag(ModuleFlags.appearanceModule))
            appearanceModule = EnsureComponent<AppearanceModule>();
        if (appearanceModule == null) Debug.LogWarning($"appearanceModule = null");

        if (enables.HasFlag(ModuleFlags.noiseMakerModule))
            noiseMakerModule = EnsureComponent<NoiseMakerModule>();
        if (noiseMakerModule == null) Debug.LogWarning($"noiseMakerModule = null");

        if (enables.HasFlag(ModuleFlags.scentEmitterModule))
            scentEmitterModule = EnsureComponent<ScentEmitterModule>();
        if (scentEmitterModule == null) Debug.LogWarning($"scentEmitterModule = null");


        // ===============================
        // Data
        // ===============================
        if (enables.HasFlag(ModuleFlags.blackboardModule))
            blackboardModule = EnsureComponent<BlackboardModule>();
        if (blackboardModule == null) Debug.LogWarning($"blackboardModule = null");

        if (enables.HasFlag(ModuleFlags.placementModule))
            placementModule = EnsureComponent<PlacementModule>();
        if (placementModule == null) Debug.LogWarning($"placementModule = null");

        if (enables.HasFlag(ModuleFlags.statusModule))
            statusModule = EnsureComponent<StatusModule>();
        if (statusModule == null) Debug.LogWarning($"statusModule = null");


        // ===============================
        // Quest
        // ===============================
        //if (enables.HasFlag(ModuleFlags.questModuleBase))
        //    questModuleBase = EnsureComponent<QuestModuleBase>();
        //if (questModuleBase == null) Debug.LogWarning($"questModuleBase = null");
    }

    private T EnsureComponent<T>() where T : Component
    {
        GameObject go = this.gameObject;
        var component = go.GetComponent<T>();
        if (component != null) return component;
        
        Type componentType = typeof(T);
        
        // Prevent Unity from trying to add abstract behaviours
        if (typeof(MonoBehaviour).IsAssignableFrom(componentType) && componentType.IsAbstract)
        {
            Debug.Log(
                $"Cannot AddComponent for abstract MonoBehaviour type '{componentType.Name}'. " +
                $"You must add a concrete subclass instead.",
                go);
            return null;
        }
        return go.AddComponent<T>();
    }


#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(displayName))
            displayName = gameObject.name;
    }
#endif

}