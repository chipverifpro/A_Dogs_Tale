using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class DungeonGUISelector : MonoBehaviour
{
    public ObjectDirectory dir;
    public DungeonSettings cfg; // Reference to the DungeonSettings ScriptableObject
    public TMP_Dropdown roomAlgorithmDropdown;
    public TMP_Dropdown tunnelsAlgorithmDropdown;
    public UnityEngine.UI.Button regenerateButton; // Button to trigger regeneration
    public DungeonGenerator generator;

    //public void OnRegenerateClicked()
    //{
    //    //generator.StopAllCoroutines();
    //    StartCoroutine(generator.RegenerateDungeon());
    //}

    /*
        public void OnRoomAlgorithmSelected(int index)
        {
            string selected = roomAlgorithmDropdown.options[index].text;
            Debug.Log("Room Algorithm selected: " + selected);

            cfg.RoomAlgorithm = (DungeonSettings.RoomAlgorithm_e)index;
            //generator.StopAllCoroutines();
            //ca.StopAllCoroutines();
            //Start();
            StartCoroutine(generator.RegenerateDungeon());
        }

        public void OnTunnelsAlgorithmSelected(int index)
        {
            string selected = tunnelsAlgorithmDropdown.options[index].text;
            Debug.Log("Tunnels Algorithm selected: " + selected);

            cfg.TunnelsAlgorithm = (DungeonSettings.TunnelsAlgorithm_e)index;
            //generator.StopAllCoroutines();
            //ca.StopAllCoroutines();
            //Start();
            StartCoroutine(generator.RegenerateDungeon());
        }
    */

    void Start()
    {
        roomAlgorithmDropdown.value = (int)cfg.RoomAlgorithm;
        roomAlgorithmDropdown.onValueChanged.AddListener(OnRoomAlgorithmChanged);

        tunnelsAlgorithmDropdown.value = (int)cfg.TunnelsAlgorithm;
        tunnelsAlgorithmDropdown.onValueChanged.AddListener(OnTunnelAlgorithmChanged);

        regenerateButton.onClick.AddListener(OnRegenerateClicked);
    }

    // Cache enum arrays for fast lookups
    private System.Array _roomAlgValues;
    private System.Array _tunnelAlgValues;

    void Awake()
    {
        InitializeConnections();
        InitializeUI();
        WireEvents();
    }

    void OnDestroy()
    {
        // Always unhook listeners you added (prevents leaks/multiple registrations on scene reload)
        if (roomAlgorithmDropdown != null)
            roomAlgorithmDropdown.onValueChanged.RemoveListener(OnRoomAlgorithmChanged);

        if (tunnelsAlgorithmDropdown != null)
            tunnelsAlgorithmDropdown.onValueChanged.RemoveListener(OnTunnelAlgorithmChanged);

        if (regenerateButton != null)
            regenerateButton.onClick.RemoveListener(OnRegenerateClicked);
    }

    // --- Setup ---

    void InitializeConnections()
    {
        if (!cfg)
        {
            cfg = FindFirstObjectByType<DungeonSettings>();
            if (!cfg) cfg = Resources.Load<DungeonSettings>("DungeonSettings"); // optional fallback
            if (!cfg) Debug.LogWarning("[DungeonGUISelector] DungeonSettings not found.");
        }

        if (!generator)
        {
            generator = FindFirstObjectByType<DungeonGenerator>();
            if (!generator) Debug.LogWarning("[DungeonGUISelector] DungeonGenerator not found.");
        }

        // Optional auto-find by GameObject names if you didn’t wire in Inspector
        if (!roomAlgorithmDropdown)
            roomAlgorithmDropdown = GameObject.Find("RoomAlgorithmDropdown")?.GetComponent<TMP_Dropdown>();
        if (!tunnelsAlgorithmDropdown)
            tunnelsAlgorithmDropdown = GameObject.Find("TunnelsAlgorithmDropdown")?.GetComponent<TMP_Dropdown>();
        if (!regenerateButton)
            regenerateButton = GameObject.Find("RegenerateButton")?.GetComponent<Button>();
    }

    void InitializeUI()
    {
        if (!cfg) return;

        // ----- ENUM PATH (recommended) -----
        // Assumes your DungeonSettings contains:
        //   public RoomAlgorithm roomAlgorithm;
        //   public TunnelAlgorithm tunnelsAlgorithm;
        // …and the enums:
        //   public enum RoomAlgorithm { BSP, Cellular, Drunkard, PrefabRooms, … }
        //   public enum TunnelAlgorithm { Straight, LSystem, AStarCarve, … }

        if (roomAlgorithmDropdown)
        {
            _roomAlgValues = System.Enum.GetValues(typeof(DungeonSettings.RoomAlgorithm_e));
            PopulateDropdownFromEnum(roomAlgorithmDropdown, _roomAlgValues);
            roomAlgorithmDropdown.value = IndexOfEnum(_roomAlgValues, cfg.RoomAlgorithm);
            roomAlgorithmDropdown.RefreshShownValue();
        }

        if (tunnelsAlgorithmDropdown)
        {
            _tunnelAlgValues = System.Enum.GetValues(typeof(DungeonSettings.TunnelsAlgorithm_e));
            PopulateDropdownFromEnum(tunnelsAlgorithmDropdown, _tunnelAlgValues);
            tunnelsAlgorithmDropdown.value = IndexOfEnum(_tunnelAlgValues, cfg.TunnelsAlgorithm);
            tunnelsAlgorithmDropdown.RefreshShownValue();
        }

        // ----- STRING PATH (if your settings store strings) -----
        // If you have string lists in cfg, do this instead:
        // if (roomAlgorithmDropdown && cfg.roomAlgorithmOptions != null)
        // {
        //     PopulateDropdownFromList(roomAlgorithmDropdown, cfg.roomAlgorithmOptions);
        //     roomAlgorithmDropdown.value = Mathf.Max(0, cfg.roomAlgorithmOptions.IndexOf(cfg.roomAlgorithmName));
        //     roomAlgorithmDropdown.RefreshShownValue();
        // }
        // if (tunnelsAlgorithmDropdown && cfg.tunnelAlgorithmOptions != null)
        // {
        //     PopulateDropdownFromList(tunnelsAlgorithmDropdown, cfg.tunnelAlgorithmOptions);
        //     tunnelsAlgorithmDropdown.value = Mathf.Max(0, cfg.tunnelAlgorithmOptions.IndexOf(cfg.tunnelAlgorithmName));
        //     tunnelsAlgorithmDropdown.RefreshShownValue();
        // }
    }

    void WireEvents()
    {
        if (roomAlgorithmDropdown)
        {
            roomAlgorithmDropdown.onValueChanged.RemoveListener(OnRoomAlgorithmChanged);
            roomAlgorithmDropdown.onValueChanged.AddListener(OnRoomAlgorithmChanged);
        }

        if (tunnelsAlgorithmDropdown)
        {
            tunnelsAlgorithmDropdown.onValueChanged.RemoveListener(OnTunnelAlgorithmChanged);
            tunnelsAlgorithmDropdown.onValueChanged.AddListener(OnTunnelAlgorithmChanged);
        }

        if (regenerateButton)
        {
            regenerateButton.onClick.RemoveListener(OnRegenerateClicked);
            regenerateButton.onClick.AddListener(OnRegenerateClicked);
        }
    }

    // --- Event handlers ---

    void OnRoomAlgorithmChanged(int index)
    {
        if (!cfg || _roomAlgValues == null) return;
        cfg.RoomAlgorithm = (DungeonSettings.RoomAlgorithm_e)_roomAlgValues.GetValue(index);
        // If using string path, set cfg.roomAlgorithmName = roomAlgorithmDropdown.options[index].text;
        // Optionally: mark settings dirty in editor with UnityEditor.EditorUtility.SetDirty(cfg);
        StartCoroutine(generator.RegenerateDungeon());
    }

    void OnTunnelAlgorithmChanged(int index)
    {
        if (!cfg || _tunnelAlgValues == null) return;
        cfg.TunnelsAlgorithm = (DungeonSettings.TunnelsAlgorithm_e)_tunnelAlgValues.GetValue(index);
        // If using string path, set cfg.tunnelAlgorithmName = tunnelsAlgorithmDropdown.options[index].text;
        StartCoroutine(generator.RegenerateDungeon());
    }

    void OnRegenerateClicked()
    {
        if (!generator)
        {
            Debug.LogWarning("[DungeonGUISelector] Regenerate clicked, but no DungeonGenerator assigned.");
            return;
        }

        // If your generator needs the settings object, make sure it already reads from cfg.
        // Otherwise, pass necessary params or call a dedicated API.
        try
        {
            generator.RegenerateDungeon(); // or generator.GenerateFromSettings(cfg);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DungeonGUISelector] Regenerate failed: {ex.Message}", generator);
        }
    }

    // --- Helpers ---

    static void PopulateDropdownFromEnum(TMP_Dropdown dd, System.Array enumValues)
    {
        dd.options.Clear();
        foreach (var v in enumValues)
            dd.options.Add(new TMP_Dropdown.OptionData(v.ToString()));
        dd.value = 0;
        dd.RefreshShownValue();
    }

    static int IndexOfEnum(System.Array values, System.Enum current)
    {
        int i = 0;
        foreach (var v in values)
        {
            if (Equals(v, current)) return i;
            i++;
        }
        return 0;
    }

    // If you store strings in cfg instead of enums:
    // static void PopulateDropdownFromList(TMP_Dropdown dd, IList<string> items)
    // {
    //     dd.options.Clear();
    //     for (int i = 0; i < items.Count; i++)
    //         dd.options.Add(new TMP_Dropdown.OptionData(items[i]));
    //     dd.value = 0;
    //     dd.RefreshShownValue();
    // }
}

// The following fuction is added to DungeonGenerator to enable or disable the
// GeneratorCanvas, which contains the buttons and pulldowns to change build settings.
// They need to be hidden during splash/main menu, because they stick out the top.
public partial class DungeonGenerator : MonoBehaviour
{
    public void EnableGeneratorCanvas(bool enable)
    {
        GameObject generatorCanvas = FindInActiveScene("GeneratorCanvas");
        if (generatorCanvas != null)
        {
            Debug.Log("Found GeneratorCanvas: " + generatorCanvas.name);
            generatorCanvas.SetActive(enable);
        }
        else
        {
            Debug.LogWarning("GeneratorCanvas not found in scene.");
        }
    }

    public static GameObject FindInActiveScene(string name)
    {
        // Get all root objects (active AND inactive)
        GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (var root in rootObjects)
        {
            GameObject found = FindInChildrenRecursive(root, name);
            if (found != null)
                return found;
        }

        return null; // Not found
    }

    private static GameObject FindInChildrenRecursive(GameObject obj, string name)
    {
        if (obj.name == name)
            return obj;

        foreach (Transform child in obj.transform)
        {
            GameObject result = FindInChildrenRecursive(child.gameObject, name);
            if (result != null)
                return result;
        }

        return null;
    }
}