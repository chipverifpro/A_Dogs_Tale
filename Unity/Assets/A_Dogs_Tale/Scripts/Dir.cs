using UnityEngine;
using Cinemachine;
using DogGame.Language;

[DefaultExecutionOrder(-1000)] // big negative = runs very early
public class Dir : MonoBehaviour
{
    // This is a catalog of all the objects and scripts in the game for all the
    // modules to share.  They only need a single reference to Dir to find
    // any other object.
    public static Dir Instance { get; private set; }    // singleton

    public bool AllReady = false;   // anyone should hold off their start until this is true.
    private int pass_num;           // debug message indicating if object was found first try or later.
    private int failures;           // tracks how many objects not found.

    [Header("World Builder Objects")]
    public DungeonSettings cfg;
    public DungeonGenerator gen;
    public DungeonGUISelector dungeonGUISelector;
    public DungeonBuildSettingsUI dungeonBuildSettingsUI;
    public Pathfinding pathfinding;


    [Header("Audio Objects")]
    public AudioPlayer audioPlayer;
    public AudioCatalog audioCatalog;
    public AudioMixerGroups audioMixerGroups;


    [Header("Game Objects")]
    public Pack playerPack;
    public PackManager packManager;
    //public Player player;
    public PackFormations packFormations;
    public ScentAirGround scents;
    public ScentRegistry scentRegistry;
    public ConvertScreenToWorld convertScreenToWorld;
    public DogSpeechDictionary dogSpeechDictionary;
    public WorldObjectRegistry worldObjectRegistry;
    public LeashSystem leashSystem;

    [Header("Game Camearas")]
    public CinemachineBrain brain;
    public CinemachineVirtualCamera vcamFP, vcamNose, vcamPerspective, vcamOverhead;
    public Camera scentCam;
    public CameraModeSwitcher cameraModeSwitcher;


    [Header("Game User Interfaces")]
    public BottomBanner bottomBanner;
    public GameInputRouter gameInputRouter;

    [Header("Splash Screen Objects")]
    public MenuManager menuManager;
    public SceneFader sceneFader;


    [Header("Rendering Objects")]
    public ElementStore elementStore;
    public WarehouseGO warehouse;
    public ManufactureGO manufactureGO;
    public ScentAirGround scentAirGround;


    [Header("Communication")]
    public Demo_Speech demo_Speech;


    [Header("Statistics")]
    public AcvtivityStats activityStats;


    [Header("LLM")]
    public LLMWorldScheduler llmWorldScheduler;
    public LLMDebugMonitor llmDebugMonitor;

    internal static void ResetStaticStateForReload()
    {
        Instance = null;
    }

    void Awake()
    {
        Debug.Log("Dir Awake");
        if (!TryRegisterSingletonInstance())
            return;

        InitializeRuntimeReferences();
        pass_num = 0;
        AllReady = false;
        ValidateDirectory();
    }

    private void OnEnable()
    {
        if (!TryRegisterSingletonInstance())
            return;

        InitializeRuntimeReferences();
    }

    private bool TryRegisterSingletonInstance()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple ObjectDirectory instances found. Destroying duplicate.", this);
            Destroy(gameObject);
            return false;
        }

        Instance = this;    // set singleton instance
        return true;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // verify that all required objects have been created and configured.
        InitializeRuntimeReferences();
        ValidateDirectory();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    
    void ValidateDirectory()
    {
        if (AllReady) return;   // last time everything was good so don't go through it again.

        InitializeRuntimeReferences();
        failures = 0;
        pass_num++;
        //Debug.Log($"[Dir{pass_num}:{failures}] Begin InitializeConnections");

        // ===============================
        // World Builder Objects
        // ===============================
        if (!cfg)                     Debug.LogError($"[Dir{pass_num}:{++failures}] DungeonSettings (cfg) not assigned.");
        if (!gen)                     Debug.LogError($"[Dir{pass_num}:{++failures}] DungeonGenerator (gen) not assigned.");
        if (!dungeonGUISelector)      Debug.LogError($"[Dir{pass_num}:{++failures}] DungeonGUISelector not assigned.");
        if (!dungeonBuildSettingsUI)  Debug.LogError($"[Dir{pass_num}:{++failures}] DungeonBuildSettingsUI not assigned.");

        // ===============================
        // Audio Objects
        // ===============================
        if (!audioPlayer)             Debug.LogError($"[Dir{pass_num}:{++failures}] AudioPlayer not assigned.");
        if (!audioCatalog)            Debug.LogError($"[Dir{pass_num}:{++failures}] AudioCatalog not assigned.");
        if (!audioMixerGroups)        Debug.LogError($"[Dir{pass_num}:{++failures}] AudioMixerGroups not assigned.");

        // ===============================
        // Game Objects
        // ===============================
        if (!playerPack)              Debug.LogError($"[Dir{pass_num}:{++failures}] Player Pack not assigned.");
        if (!packManager)             Debug.LogError($"[Dir{pass_num}:{++failures}] PackManager not assigned.");
        //if (!player)                  Debug.LogError($"[Dir{pass_num}:{++failures}] Player not assigned.");
        if (!packFormations)          Debug.LogError($"[Dir{pass_num}:{++failures}] PackFormations not assigned.");
        if (!scents)                  Debug.LogError($"[Dir{pass_num}:{++failures}] ScentAirGround (scents) not assigned.");
        if (!scentRegistry)           Debug.LogError($"[Dir{pass_num}:{++failures}] ScentRegistry not assigned.");
        if (!convertScreenToWorld)    Debug.LogError($"[Dir{pass_num}:{++failures}] ConvertScreenToWorld not assigned.");
        if (!dogSpeechDictionary)     Debug.LogError($"[Dir{pass_num}:{++failures}] DogSpeechDictionary not assigned.");

        // ===============================
        // Game Cameras
        // ===============================
        if (!brain)                   Debug.LogError($"[Dir{pass_num}:{++failures}] CinemachineBrain (brain) not assigned.");
        if (!vcamFP)                  Debug.LogError($"[Dir{pass_num}:{++failures}] CinemachineVirtualCamera vcamFP not assigned.");
        if (!vcamPerspective)         Debug.LogError($"[Dir{pass_num}:{++failures}] CinemachineVirtualCamera vcamPerspective not assigned.");
        if (!vcamOverhead)            Debug.LogError($"[Dir{pass_num}:{++failures}] CinemachineVirtualCamera vcamOverhead not assigned.");
        if (!scentCam)                Debug.LogError($"[Dir{pass_num}:{++failures}] Scent Camera not assigned.");
        if (!cameraModeSwitcher)      Debug.LogError($"[Dir{pass_num}:{++failures}] CameraModeSwitcher not assigned.");

        // ===============================
        // Game User Interfaces
        // ===============================
        if (!bottomBanner)            Debug.LogError($"[Dir{pass_num}:{++failures}] BottomBanner not assigned.");

        // ===============================
        // Splash Screen Objects
        // ===============================
        if (!menuManager)             Debug.LogError($"[Dir{pass_num}:{++failures}] MenuManager not assigned.");
        if (!sceneFader)              Debug.LogError($"[Dir{pass_num}:{++failures}] SceneFader not assigned.");

        // ===============================
        // Rendering Objects
        // ===============================
        if (!elementStore)            Debug.LogError($"[Dir{pass_num}:{++failures}] ElementStore not assigned.");
        if (!warehouse)               Debug.LogError($"[Dir{pass_num}:{++failures}] WarehouseGO not assigned.");
        if (!manufactureGO)           Debug.LogError($"[Dir{pass_num}:{++failures}] ManufactureGO not assigned.");
        if (!scentAirGround)          Debug.LogError($"[Dir{pass_num}:{++failures}] ScentAirGround not assigned.");

        // ===============================
        // Communication
        // ===============================
        if (!demo_Speech)             Debug.LogError($"[Dir{pass_num}:{++failures}] Demo_Speech not assigned.");

        // ===============================
        // Statistics
        // ===============================
        if (!activityStats)           Debug.LogError($"[Dir{pass_num}:{++failures}] ActivityStats not assigned.");

        // ------------------ 
        if (failures == 0)
        {
            Debug.Log($"[Dir{pass_num}:{failures}] Complete InitializeConnections. SUCCESS.");
            AllReady = true;
        }
        else
        {
            //Debug.Log($"[Dir{pass_num}:{failures}] Complete InitializeConnections. {failures} failures");
            AllReady = false;
        }
        
    }

    private void InitializeRuntimeReferences()
    {
        if (gameInputRouter == null)
            gameInputRouter = FindFirstObjectByType<GameInputRouter>();
        if (pathfinding == null)
            pathfinding = FindFirstObjectByType<Pathfinding>();
        if (packManager == null)
            packManager = FindFirstObjectByType<PackManager>();
        if (packManager != null && packManager.dir == null)
            packManager.dir = this;
        if (packManager != null)
            packManager.InitializeRuntimeReferences();
        if (playerPack == null)
            playerPack = packManager != null && packManager.playerPack != null
                ? packManager.playerPack
                : FindPackByName("Player Pack");
        if (packManager != null && packManager.playerPack == null)
            packManager.playerPack = playerPack;
        if (packFormations == null)
            packFormations = FindFirstObjectByType<PackFormations>();
    }

    private static Pack FindPackByName(string packName)
    {
        if (string.IsNullOrWhiteSpace(packName))
            return null;

        Pack[] packs = FindObjectsByType<Pack>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Pack pack in packs)
        {
            if (pack != null && pack.packName == packName)
                return pack;
        }

        return null;
    }
}
