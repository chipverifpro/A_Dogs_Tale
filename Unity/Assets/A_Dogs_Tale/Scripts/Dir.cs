using UnityEngine;
using Cinemachine;
using DogGame.Language;
using DogGame.LLM;

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

        if (gameInputRouter==null) gameInputRouter=FindFirstObjectByType<GameInputRouter>();
        if (pathfinding == null) pathfinding = FindFirstObjectByType<Pathfinding>();
        pass_num = 0;
        AllReady = false;
        ValidateDirectory();
    }

    private void OnEnable()
    {
        if (!TryRegisterSingletonInstance())
            return;

        if (gameInputRouter == null)
            gameInputRouter = FindFirstObjectByType<GameInputRouter>();
        if (pathfinding == null)
            pathfinding = FindFirstObjectByType<Pathfinding>();
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

        failures = 0;
        pass_num++;
        //Debug.Log($"[Dir{pass_num}] Begin InitializeConnections");

        // ===============================
        // World Builder Objects
        // ===============================
        if (!cfg)                     Debug.LogError($"[Dir{pass_num}] DungeonSettings (cfg) not assigned.");
        if (!gen)                     Debug.LogError($"[Dir{pass_num}] DungeonGenerator (gen) not assigned.");
        if (!dungeonGUISelector)      Debug.LogError($"[Dir{pass_num}] DungeonGUISelector not assigned.");
        if (!dungeonBuildSettingsUI)  Debug.LogError($"[Dir{pass_num}] DungeonBuildSettingsUI not assigned.");

        // ===============================
        // Audio Objects
        // ===============================
        if (!audioPlayer)             Debug.LogError($"[Dir{pass_num}] AudioPlayer not assigned.");
        if (!audioCatalog)            Debug.LogError($"[Dir{pass_num}] AudioCatalog not assigned.");
        if (!audioMixerGroups)        Debug.LogError($"[Dir{pass_num}] AudioMixerGroups not assigned.");

        // ===============================
        // Game Objects
        // ===============================
        if (!playerPack)              Debug.LogError($"[Dir{pass_num}] Player Pack not assigned.");
        if (!packManager)             Debug.LogError($"[Dir{pass_num}] PackManager not assigned.");
        //if (!player)                  Debug.LogError($"[Dir{pass_num}] Player not assigned.");
        if (!packFormations)          Debug.LogError($"[Dir{pass_num}] PackFormations not assigned.");
        if (!scents)                  Debug.LogError($"[Dir{pass_num}] ScentAirGround (scents) not assigned.");
        if (!scentRegistry)           Debug.LogError($"[Dir{pass_num}] ScentRegistry not assigned.");
        if (!convertScreenToWorld)    Debug.LogError($"[Dir{pass_num}] ConvertScreenToWorld not assigned.");
        if (!dogSpeechDictionary)     Debug.LogError($"[Dir{pass_num}] DogSpeechDictionary not assigned.");

        // ===============================
        // Game Cameras
        // ===============================
        if (!brain)                   Debug.LogError($"[Dir{pass_num}] CinemachineBrain (brain) not assigned.");
        if (!vcamFP)                  Debug.LogError($"[Dir{pass_num}] CinemachineVirtualCamera vcamFP not assigned.");
        if (!vcamPerspective)         Debug.LogError($"[Dir{pass_num}] CinemachineVirtualCamera vcamPerspective not assigned.");
        if (!vcamOverhead)            Debug.LogError($"[Dir{pass_num}] CinemachineVirtualCamera vcamOverhead not assigned.");
        if (!scentCam)                Debug.LogError($"[Dir{pass_num}] Scent Camera not assigned.");
        if (!cameraModeSwitcher)      Debug.LogError($"[Dir{pass_num}] CameraModeSwitcher not assigned.");

        // ===============================
        // Game User Interfaces
        // ===============================
        if (!bottomBanner)            Debug.LogError($"[Dir{pass_num}] BottomBanner not assigned.");

        // ===============================
        // Splash Screen Objects
        // ===============================
        if (!menuManager)             Debug.LogError($"[Dir{pass_num}] MenuManager not assigned.");
        if (!sceneFader)              Debug.LogError($"[Dir{pass_num}] SceneFader not assigned.");

        // ===============================
        // Rendering Objects
        // ===============================
        if (!elementStore)            Debug.LogError($"[Dir{pass_num}] ElementStore not assigned.");
        if (!warehouse)               Debug.LogError($"[Dir{pass_num}] WarehouseGO not assigned.");
        if (!manufactureGO)           Debug.LogError($"[Dir{pass_num}] ManufactureGO not assigned.");
        if (!scentAirGround)          Debug.LogError($"[Dir{pass_num}] ScentAirGround not assigned.");

        // ===============================
        // Communication
        // ===============================
        if (!demo_Speech)             Debug.LogError($"[Dir{pass_num}] Demo_Speech not assigned.");

        // ===============================
        // Statistics
        // ===============================
        if (!activityStats)           Debug.LogError($"[Dir{pass_num}] ActivityStats not assigned.");

        // ------------------ 
        if (failures == 0)
        {
            Debug.Log($"[Dir{pass_num}] Complete InitializeConnections. SUCCESS.");
            AllReady = true;
        }
        else
        {
            //Debug.Log($"[Dir{pass_num}] Complete InitializeConnections. {failures} failures");
            AllReady = false;
        }
        
    }
}
