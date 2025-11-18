using UnityEngine;
using Cinemachine;

public class ObjectDirectory : MonoBehaviour
{
    // This is a catalog of all the objects and scripts in the game for all the
    // modules to share.  They only need a single reference to Directory to find
    // any other object.

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
    public Pack pack;
    public Player player;
    //public Scents scents;     // replaced with ScentAirGround.cs
    public ScentAirGround scents;
    public ScentRegistry scentRegistry;



    [Header("Game Camearas")]
    public CinemachineBrain brain;
    public CinemachineVirtualCamera vcamFP, vcamPerspective, vcamOverhead;
    public Camera scentCam;


    [Header("Game User Interfaces")]
    public BottomBanner bottomBanner;


    [Header("Splash Screen Objects")]
    public MenuManager menuManager;
    public SceneFader sceneFader;


    [Header("Rendering Objects")]
    public ElementStore elementStore;
    public WarehouseGO warehouse;
    public ManufactureGO manufactureGO;

    void Awake()
    {
        pass_num = 0;
        AllReady = false;
        InitializeDirectory();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // verify that all required objects have been created and configured.
        InitializeDirectory();
    }

    void InitializeDirectory()
    {
        failures = 0;
        pass_num++;
        Debug.Log($"[Directory{pass_num}] Begin InitializeConnections");

        // --- DungeonSettings (ScriptableObject) ---
        if (!cfg)
        {
            // Prefer a scene instance if you have one
            cfg = FindFirstObjectByType<DungeonSettings>(FindObjectsInactive.Include);
            //if (!cfg)
            //    cfg = Resources.Load<DungeonSettings>("DungeonSettings"); // put an asset in Resources/
            if (!cfg)
                Debug.LogWarning($"[Directory{pass_num}] cfg not found.");
            else
                Debug.Log($"[Directory{pass_num}] cfg = {cfg.name}");
            if (!cfg) failures++;
        }

        // --- DungeonGenerator (Monobehavior) ---
        if (!gen)
        {
            // Prefer a scene instance if you have one
            gen = FindFirstObjectByType<DungeonGenerator>(FindObjectsInactive.Include);
            if (!gen)
                Debug.LogWarning($"[Directory{pass_num}] gen not found.");
            else
                Debug.Log($"[Directory{pass_num}] gen = {gen.name}");
            if (!gen) failures++;
        }

        // --- BottomBanner (scene UI) ---
        if (!bottomBanner)
        {
            bottomBanner = FindFirstObjectByType<BottomBanner>(FindObjectsInactive.Include);
            if (!bottomBanner)
                Debug.LogWarning($"[Directory{pass_num}] bottomBanner not found.");
            else
                Debug.Log($"[Directory{pass_num}] bottomBanner = {bottomBanner.name}");
            if (!bottomBanner) failures++;
        }

        if (!pack) Debug.LogWarning($"[Directory{pass_num}] pack not assigned.");
        if (!player) Debug.LogWarning($"[Directory{pass_num}] player not assigned.");
        if (!brain) Debug.LogWarning($"[Directory{pass_num}] brain not assigned.");
        if (!vcamFP) Debug.LogWarning($"[Directory{pass_num}] vcamFP not assigned.");
        if (!vcamPerspective) Debug.LogWarning($"[Directory{pass_num}] vcamPerspective not assigned.");
        if (!vcamOverhead) Debug.LogWarning($"[Directory{pass_num}] vcamOverhead not assigned.");
        if (!menuManager) Debug.LogWarning($"[Directory{pass_num}] menuManager not assigned.");
        if (!sceneFader) Debug.LogWarning($"[Directory{pass_num}] sceneFader not assigned.");
        if (!audioPlayer) Debug.LogWarning($"[Directory{pass_num}] audioPlayer not assigned.");
        if (!audioCatalog) Debug.LogWarning($"[Directory{pass_num}] audioCatalog not assigned.");
        if (!audioMixerGroups) Debug.LogWarning($"[Directory{pass_num}] audioMixerGroups not assigned.");
        if (!dungeonGUISelector) Debug.LogWarning($"[Directory{pass_num}] dungeonGUISelector not assigned.");
        if (!dungeonBuildSettingsUI) Debug.LogWarning($"[Directory{pass_num}] dungeonBuildSettingsUI not assigned.");

        // ------------------ 
        if (failures == 0)
        {
            Debug.Log($"[Directory{pass_num}] Complete InitializeConnections. SUCCESS.");
            AllReady = true;
        }
        else
        {
            Debug.Log($"[Directory{pass_num}] Complete InitializeConnections. {failures} failures");
            AllReady = false;
        }
        
    }
}
