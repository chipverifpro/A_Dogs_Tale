using System.Collections.Generic;
using UnityEngine;
using DogGame.Modules;
using DogGame.AI;
using System;
using DogGame.LLM;
using DogGame.Tasks;
using DogGame.LLM.Agent;
using DogGame;
using DogGame.Noise;
using DogGame.UI.InteractionWheel;


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
    Player  ,    // Dummy WorldObject representing the player input
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
    none                    = 0UL,
    // --- Sensory ---
    locationModule          = 1UL << 1,
    hearingModule           = 1UL << 2,
    scentPerceptionModule   = 1UL << 3,
    visionPerceptionModule            = 1UL << 4,
    tasteModule             = 1UL << 5,

    // --- Agent Decision Modules
    playerDecisionModule        = 1UL << 11,
    followerDecisionModule      = 1UL << 12,
    wanderDecisionModule        = 1UL << 13,
    immobileDecisionModule      = 1UL << 14,
    taskFollowerDecisionModule  = 1UL << 15,

    // --- Agent Interface Modules ---
    agentModule           = 1UL << 21,
    agentMovementModule   = 1UL << 22,
    packMemberModule      = 1UL << 23,
    llmRequestResponseModule = 1UL << 24,
    reactionModule        = 1UL << 25,
    motivationModule      = 1UL << 26,
        
    // --- Ability ---
    activatorModule     = 1UL << 31,
    interactionModule   = 1UL << 32,
    
    
    // --- Output ---
    motionModule        = 1UL << 41,
    appearanceModule    = 1UL << 42,
    noiseMakerModule    = 1UL << 43,
    scentEmitterModule  = 1UL << 44,

    // --- Data ---
    blackboardModule    = 1UL << 51,
    placementModule     = 1UL << 52,
    statusModule        = 1UL << 53,
    taskListModule      = 1UL << 54,
    containerModule     = 1UL << 55,

    // --- Quest ---
    fetchQuestModule    = 1UL << 61,
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
                                                     & ~QuestModules
                                                     & ~ModuleFlags.placementModule;
    public static readonly ModuleFlags QuestModules = ModuleFlags.fetchQuestModule;
    public static readonly ModuleFlags DecisionModules = ModuleFlags.playerDecisionModule
                                                       | ModuleFlags.followerDecisionModule
                                                       | ModuleFlags.wanderDecisionModule
                                                       | ModuleFlags.immobileDecisionModule
                                                       | ModuleFlags.taskFollowerDecisionModule;
    public static readonly ModuleFlags ScatterTerrain = ModuleFlags.placementModule
                                                       | ModuleFlags.locationModule
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

    // MotionAdapter implements IAgentMovementAdapter interface 
    //     connecting LLMAsyncPlanDriver to agentMovementModule
    public MotionAdapter motionAdapter = null;

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
    public ImmobileDecisionModule immobileDecisionModule { get; private set; }
    public TaskFollowerDecisionModule taskFollowerDecisionModule { get; private set; }
    
    // --- Agent Interface Modules ---
    public AgentModule  agentModule { get; private set; }
    public AgentMovementModule agentMovementModule { get; private set; }
    public PackMemberModule packMemberModule { get; private set; }
    public MotivationModule motivationModule { get; private set; }

    // LLM Planning Modules
    public LLMThinkModule llmThinkModule { get; private set; }    
    public ReactionModule reactionModule { get; private set; }
    public LLMConfigModule llmConfigModule { get; private set; }
    public LLMWorldStateModule llmWorldStateModule { get; private set; }
    
    //public TaskExecutor  taskExecutor { get; private set; }
    public TaskController taskController { get; private set; }
    public LLMWorldScheduler llmWorldScheduler { get; private set; }

    // Sensory:
    public TasteModule TasteModule { get; private set; }
    public HearingModule hearingModule { get; private set; }
    public ScentPerceptionModule scentPerceptionModule { get; private set; }
    public VisionPerceptionModule visionPerceptionModule { get; private set; }

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
    public TaskListModule taskListModule { get; private set; }

    // Quest:
    public QuestModuleBase fetchQuestModule { get; private set; }


    // Registration management functions
    public bool IsRegistered { get; private set; }
    public bool HasValidId => objectId >= 0;
    
    public int ObjectId => objectId;
    public WorldObjectKind Kind => kind;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;


    // This is a copy of the most basic commands in LocationModule.
    // If you don't need all the fancy features there and just
    //   want current location, it can be grabbed here without a
    //   LocationModule.
    public Vector3 pos3d_world => this.transform.position;
    public Vector3 pos3d_f => new(pos3d_world.x, pos3d_world.z, pos3d_world.y);


    private void Awake()
    {
        //dir = FindFirstObjectByType<Directory>();
        if (dir == null)
        {
            Debug.LogError($"WorldObject.Awake() was unable to find Directory.");
        }

        motionAdapter = new(this);  // create the adapter

        // grab from Directory:
        llmWorldScheduler = dir.llmWorldScheduler;
        taskController = GetComponent<TaskController>();
        //taskExecutor = GetComponent<TaskExecutor>();

        // Auto-fill module pointers, if they are attached to the same GameObject as this.


        // --- Sensory ---
        locationModule    = GetComponent<LocationModule>();
        hearingModule          = GetComponent<HearingModule>();
        scentPerceptionModule  = GetComponent<ScentPerceptionModule>();
        visionPerceptionModule           = GetComponent<VisionPerceptionModule>();
        TasteModule              = GetComponent<TasteModule>();

        // --- Agent Decision Modules
        playerDecisionModule   = GetComponent<PlayerDecisionModule>();
        followerDecisionModule = GetComponent<FollowerDecisionModule>();
        wandererDecisionModule = GetComponent<WandererDecisionModule>();
        immobileDecisionModule = GetComponent<ImmobileDecisionModule>();
        taskFollowerDecisionModule = GetComponent<TaskFollowerDecisionModule>();
        
        // --- Agent Interface Modules ---
        agentModule           = GetComponent<AgentModule>();
        agentMovementModule   = GetComponent<AgentMovementModule>();
        packMemberModule      = GetComponent<PackMemberModule>();
        motivationModule      = GetComponent<MotivationModule>();
        llmThinkModule        = GetComponent<LLMThinkModule>();    
        reactionModule        = GetComponent<ReactionModule>();

        llmConfigModule       = GetComponent<LLMConfigModule>();
        llmWorldStateModule   = GetComponent<LLMWorldStateModule>();

        // --- Ability ---
        activatorModule   = GetComponent<ActivatorModule>();
        interactionModule = GetComponent<InteractionModule>();
        
        // --- Output ---
        motionModule      = GetComponent<MotionModule>();
        appearanceModule  = GetComponent<AppearanceModule>();
        noiseMakerModule  = GetComponent<NoiseMakerModule>();
        scentEmitterModule= GetComponent<ScentEmitterModule>();

        // --- Data ---
        blackboardModule  = GetComponent<BlackboardModule>();
        placementModule   = GetComponent<PlacementModule>();
        statusModule      = GetComponent<StatusModule>();
        taskListModule    = GetComponent<TaskListModule>();
        containerModule   = GetComponent<ContainerModule>();

        // --- Quest ---
        fetchQuestModule  = GetComponent<FetchQuestModule>();

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

        // if we are an agent, but not in a pack, we are a FreeAgent.
        // Note that if we do have a packMemberModule but it doesn't identify a pack, that module will do this move to FreeAgents.
        if (agentModule!=null && packMemberModule==null)
        {
            Debug.LogWarning($"[WorldObject.Awake {gameObject.name}] setting parent of {name} to FreeAgents");
            this.gameObject.transform.SetParent(dir.packManager.FreeAgentsParent.transform);
        }
    }

    private void Update()
    {
        TickCallerAllModules();
    }

    private int debugDoubleTick = -1;
    private void TickCallerAllModules()
    {
        //Debug.Log($"[{ToString()}] TickCallerAllModules");
        // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: TickCallerAllModules run more than once per frame");
            debugDoubleTick = Time.frameCount;

        //float dt = Time.deltaTime;
        float dt = GameTime.DeltaTime;
        if (dt <= 0f)
            return;
            
        // SENSES
        //visionPerceptionModule?.Tick(dt);
        hearingModule?.Tick(dt);
        //scentPerceptionModule?.Tick(dt);
        //TasteModule?.Tick(dt);

        // PLANNING             // Enqueue tasks
        // reactionModule       
        // llmPlanningModule

        // AGENT DECISION       // Enqueue tasks
        agentModule?.Tick(dt);  // forwards to appropriate active DecisionModule...
            //playerDecisionModule?.Tick(dt);
            //wanderDecisionModule?.Tick(dt);
            //followerDecisionModule?.Tick(dt);
            //immobileDecisionModule?.Tick(dt);
            //...

        // AGENT EXECUTION
        //taskExecutor?.Tick(context, dt); // needs context

        // AGENT INTERFACE
        visionPerceptionModule?.Tick(dt);
        scentPerceptionModule?.Tick(dt);
        agentMovementModule?.Tick(dt);
        packMemberModule?.Tick(dt);
        llmThinkModule?.Tick(dt);

        // MOTIVATION
        motivationModule?.Tick(dt);

        // ABILITY
        //motionModule?.Tick(dt);
        //locationModule?.Tick(dt);
        //activatorModule?.Tick(dt);
        //containerModule?.Tick(dt);
        //interactionModule?.Tick(dt);
        
        // DATA                             // No need to tick
        //blackboardModule?.Tick(dt);
        //placementModule?.Tick(dt);
        //statusModule?.Tick(dt);

        // OUTPUT
        //appearanceModule?.Tick(dt);
        noiseMakerModule?.Tick(dt);
        scentEmitterModule?.Tick(dt);
        
        reactionModule?.Tick(dt);
        // QUEST
        //fetchQuestModule?.Tick(dt);
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

        if (IsRegistered)// && WorldObjectRegistry.HasInstance)
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

    // Pass interaction event to the activatorModule, but create it first if it doesn't exist.
    public ActivateResult Activate(ActivateContext context, ActivateRequest request)
    {
        if (activatorModule==null && context.promoteTarget)
        {
            //CreateModulesIfNeeded(ModuleFlags.activatorModule);
            CreateModulesIfNeeded(ModuleFlagsTemplates.FullAgent);
        }
        if (activatorModule==null)
            return new(ActivateResultKind.Failed, message: $"Target {DisplayName} has no activatorModule");
        return activatorModule.HandleActivate(context, request);
    }

    // Allows for creating a template of enables for a particular
    // type of WorldObject, and all the appropriate modules get created.
    // Obviously, to run this, you already created the WorldObject we are in.
    public void CreateModulesIfNeeded(ModuleFlags enables)
    {
        //Debug.Log($"[{displayName} worldObject.CreateModulesIfNeeded] verifying these modules: {enables}");

        // ===============================
        // Sensory
        // ===============================
        if (enables.HasFlag(ModuleFlags.hearingModule))
        {
            hearingModule = EnsureComponent<HearingModule>();
            if (hearingModule == null) Debug.LogWarning($"hearingModule = null");
        }

        if (enables.HasFlag(ModuleFlags.scentPerceptionModule))
        {
            scentPerceptionModule = EnsureComponent<ScentPerceptionModule>();
            if (scentPerceptionModule == null) Debug.LogWarning($"scentPerceptionModule = null");
        }

        if (enables.HasFlag(ModuleFlags.visionPerceptionModule))
        {   
            visionPerceptionModule = EnsureComponent<VisionPerceptionModule>();
            if (visionPerceptionModule == null) Debug.LogWarning($"visionPerceptionModule = null");
        }

        if (enables.HasFlag(ModuleFlags.tasteModule))
        {
            TasteModule = EnsureComponent<TasteModule>();
            if (TasteModule == null) Debug.LogWarning($"TasteModule = null");
        }


        // ===============================
        // Agent Decision Modules
        // ===============================
        if (enables.HasFlag(ModuleFlags.playerDecisionModule))
        {
            playerDecisionModule = EnsureComponent<PlayerDecisionModule>();
            if (playerDecisionModule == null) Debug.LogWarning($"playerDecisionModule = null");
        }

        if (enables.HasFlag(ModuleFlags.followerDecisionModule))
        {
            followerDecisionModule = EnsureComponent<FollowerDecisionModule>();
            if (followerDecisionModule == null) Debug.LogWarning($"followerDecisionModule = null");
        }

        if (enables.HasFlag(ModuleFlags.wanderDecisionModule))
        {
            wandererDecisionModule = EnsureComponent<WandererDecisionModule>();
            if (wandererDecisionModule == null) Debug.LogWarning($"wandererDecisionModule = null");
        }

        if (enables.HasFlag(ModuleFlags.immobileDecisionModule))
        {
            immobileDecisionModule = EnsureComponent<ImmobileDecisionModule>();
            if (immobileDecisionModule == null) Debug.LogWarning($"immobileDecisionModule = null");
        }

        // ===============================
        // Agent Interface Modules
        // ===============================
        if (enables.HasFlag(ModuleFlags.agentMovementModule))
        {
            agentMovementModule = EnsureComponent<AgentMovementModule>();
            if (agentMovementModule == null) Debug.LogWarning($"agentMovementModule = null");
        }

        if (enables.HasFlag(ModuleFlags.packMemberModule))
        {
            packMemberModule = EnsureComponent<PackMemberModule>();
            if (packMemberModule == null) Debug.LogWarning($"packMemberModule = null");
        }

        if (enables.HasFlag(ModuleFlags.agentModule))
        {
            agentModule = EnsureComponent<AgentModule>();
            if (agentModule == null) Debug.LogWarning($"agentModule = null");
        }


        // ===============================
        // Motivation
        // ===============================
        if (enables.HasFlag(ModuleFlags.motivationModule))
        {
            motivationModule = EnsureComponent<MotivationModule>();
            if (motivationModule == null) Debug.LogWarning($"motivationModule = null");
        }


        // ===============================
        // Ability
        // ===============================
        if (enables.HasFlag(ModuleFlags.activatorModule))
        {
            activatorModule = EnsureComponent<ActivatorModule>();
            if (activatorModule == null) Debug.LogWarning($"activatorModule = null");
        }

        if (enables.HasFlag(ModuleFlags.containerModule))
        {
            containerModule = EnsureComponent<ContainerModule>();
            if (containerModule == null) Debug.LogWarning($"containerModule = null");
        }

        if (enables.HasFlag(ModuleFlags.interactionModule))
        {
            interactionModule = EnsureComponent<InteractionModule>();
            if (interactionModule == null) Debug.LogWarning($"interactionModule = null");
        }

        if (enables.HasFlag(ModuleFlags.locationModule))
        {
            locationModule = EnsureComponent<LocationModule>();
            if (locationModule == null) Debug.LogWarning($"locationModule = null");
        }

        if (enables.HasFlag(ModuleFlags.motionModule))
        {
            motionModule = EnsureComponent<MotionModule>();
            if (motionModule == null) Debug.LogWarning($"motionModule = null");
        }


        // ===============================
        // Output
        // ===============================
        if (enables.HasFlag(ModuleFlags.appearanceModule))
        {
            appearanceModule = EnsureComponent<AppearanceModule>();
            if (appearanceModule == null) Debug.LogWarning($"appearanceModule = null");
        }

        if (enables.HasFlag(ModuleFlags.noiseMakerModule))
        {
            noiseMakerModule = EnsureComponent<NoiseMakerModule>();
            if (noiseMakerModule == null) Debug.LogWarning($"noiseMakerModule = null");
        }

        if (enables.HasFlag(ModuleFlags.scentEmitterModule))
        {
            scentEmitterModule = EnsureComponent<ScentEmitterModule>();
            if (scentEmitterModule == null) Debug.LogWarning($"scentEmitterModule = null");
        }


        // ===============================
        // Data
        // ===============================
        if (enables.HasFlag(ModuleFlags.blackboardModule))
        {
            blackboardModule = EnsureComponent<BlackboardModule>();
            if (blackboardModule == null) Debug.LogWarning($"blackboardModule = null");
        }


        if (enables.HasFlag(ModuleFlags.placementModule))
        {
            placementModule = EnsureComponent<PlacementModule>();
            if (placementModule == null) Debug.LogWarning($"placementModule = null");
        }


        if (enables.HasFlag(ModuleFlags.statusModule))
        {
            statusModule = EnsureComponent<StatusModule>();
            if (statusModule == null) Debug.LogWarning($"statusModule = null");
        }



        // ===============================
        // Quest
        // ===============================
        //if (enables.HasFlag(ModuleFlags.questModuleBase))
        //    questModuleBase = EnsureComponent<QuestModuleBase>();
        //if (questModuleBase == null) Debug.LogWarning($"questModuleBase = null");
    }

    public void ApplyFollowerDefaults()
    {
        Debug.Log("Applying follower defaults");
        // Safe defaults: avoid null refs / weird behavior.
        agentModule.enabled = true;

        // Decision module selection should NOT use "new" if it's a MonoBehaviour.
        // Make decision modules Components or ScriptableObjects (your call).
        //packMemberModule.JoinPack(dir.playerPack, false);

        motionModule.motionControlMode = DogGame.Modules.MotionControlMode.Autopilot;
        motionModule.facingMode = DogGame.Modules.FacingMode.FaceMovementDirection;

        // Motivation defaults: mild pack pull, high distraction tolerance
        motivationModule.trainingProfile.obedience = 0.35f;
        motivationModule.trainingProfile.focus     = 0.35f;

        // packMemberModule.role = PackRole.Follower;
        //wo.packMemberModule.currentPack = dir.playerPack;

        // Mark debug agents clearly
        agentModule.agentName = $"{agentModule.agentName} (Follower Defaults)";
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

    // Ensure that the BlackboardModule and it's included Blackboard are both valid.
    // Return a pointer to the Blackboard (of type SimpleBlackboard, implementing IBlackboard interface).
    public IBlackboard EnsureBlackboard()
    {
        // if everything is good, use it.
        if (blackboardModule!=null && blackboardModule.Blackboard!=null)
            return blackboardModule.Blackboard;

        // If the pointer to an existing Module is missing, get it.
        if (blackboardModule == null)
            blackboardModule = GetComponent<BlackboardModule>();

        // Create the module if missing
        if (blackboardModule == null)
            blackboardModule = gameObject.AddComponent<BlackboardModule>();

        // now create the Blackboard (SimpleBlackboard) itself
        blackboardModule.ForceInitialize();
        return blackboardModule.Blackboard;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(displayName))
            displayName = gameObject.name;
    }
#endif

}