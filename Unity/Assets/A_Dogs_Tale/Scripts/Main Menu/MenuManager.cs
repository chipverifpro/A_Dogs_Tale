using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Object References")]
    public SceneFader fader;

    public AudioMixerGroups audioMixerGroups;           // mixer channels
    public AudioPlayer audioPlayer;                     // play controls
    public Dir dir;

    [Header("Bottom Banner")]
    public BottomBanner bottomBanner;  // assign your existing BottomBanner
    public MenuSettingsDialog settingsDialog;


    void Awake()
    {
        // If not assigned, try to find by name under the Canvas
        btnSimulation = btnSimulation ?? FindButton("Simulation");
        btnFlyover = btnFlyover ?? FindButton("Flyover");
        btnSave = btnSave ?? FindButton("Save", warnIfMissing: false);
        btnLoad = btnLoad ?? FindButton("Load");
        btnSave = btnSave ?? CreateSaveButtonFromLoadButton();
        btnDocumentation = btnDocumentation ?? FindButton("Documentation");
        btnSettings = btnSettings ?? FindButton("Settings");
        btnQuit = btnQuit ?? FindButton("Quit");

        // Clear any existing listeners and add ours
        Hook(btnSimulation, OnSimulation);
        Hook(btnFlyover, OnFlyover);
        Hook(btnSave, OnSave);
        Hook(btnLoad, OnLoad);
        Hook(btnDocumentation, OnDocumentation);
        Hook(btnSettings, OnSettings);
        Hook(btnQuit, QuitGame);

//        Debug.Log(
//            $"[MenuManager] Button refs after Awake: " +
//            $"NewMap={(btnNewMap ? btnNewMap.name : "null")}, " +
//            $"EditMap={(btnEditMap ? btnEditMap.name : "null")}, " +
//            $"Explore={(btnExplore ? btnExplore.name : "null")}, " +
//            $"Flyover={(btnFlyover ? btnFlyover.name : "null")}, " +
//            $"Settings={(btnSettings ? btnSettings.name : "null")}, " +
//            $"Quit={(btnQuit ? btnQuit.name : "null")}",
//            this);

        // Optional: auto-find common refs
        if (!bottomBanner) bottomBanner = FindFirstObjectByType<BottomBanner>();
        if (!generator) generator = FindFirstObjectByType<DungeonGenerator>();
        if (!settingsDialog) settingsDialog = GetComponent<MenuSettingsDialog>();
        if (!settingsDialog) settingsDialog = gameObject.AddComponent<MenuSettingsDialog>();
        settingsDialog.Initialize(this);
    }

    void Start()
    {
        // Create sound effects entries for the menu
        dir.audioCatalog.AddClipToCatalog(
            name: "Button-Click",
            filename: "Button-Click",
            subtitle: "[Button Click]",
            channel: "UI"
        );

        if (buttonsParent) buttonsParent.SetActive(true);
    }

    // === BUTTON HOOKS ===

    public void OnSimulation()
    {
        Debug.Log($"[MenuManager] OnSimulation invoked. fader={(fader ? fader.name : "null")} dir={(dir ? dir.name : "null")}", this);
        //BottomBanner.Show("🐾 Digging a brand new hole...");
        BottomBanner.Show("Digging a brand new hole...");
        dir.audioPlayer.PlayClip("Button-Click");
        StartCoroutine(fader.FadeToGame());
        //SceneManager.LoadScene("2D_Fargoal_Map");  // your map gen scene
        
        
        //generator.Start();
        // You can also call generator.NewMap() if you keep it same-scene
    }

    public void OnFlyover()
    {
        //BottomBanner.Show("🐦 Flap flap... Birdy Mode overhead!");
        BottomBanner.Show("Flap flap... Birdy Mode overhead!");
// TODO: switch to FlyoverCamera routine
    }

    public void OnLoad()
    {
        PlayButtonClick();
        if (!TryResolveGenerator(out DungeonGenerator mapGenerator))
            return;

        mapGenerator.LoadMapFromSingleSlot();
    }

    public void OnSave()
    {
        PlayButtonClick();
        if (!TryResolveGenerator(out DungeonGenerator mapGenerator))
            return;

        mapGenerator.SaveCurrentMapToSingleSlot();
    }

    public void OnDocumentation()
    {
        OpenDocs();
        //BottomBanner.Show("🐾 Sniff sniff... Doggy documentation engaged!");
        BottomBanner.Show("Sniff sniff... Doggy documentation engaged!");
    }

    public void OnSettings()
    {
        //BottomBanner.Show("🎨 Adjusting imagination...");
        BottomBanner.Show("Adjusting imagination...");
        settingsDialog?.Open();
    }

    public void QuitGame()
    {
        //BottomBanner.Show("💤 Curling up for a nap...");
        BottomBanner.Show("Curling up for a nap...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Backward-compatible alias for existing inspector bindings.
    public void OnQuit()
    {
        QuitGame();
    }

    public void OpenDocs()
    {
        Application.OpenURL("https://github.com/chipverifpro/A_Dogs_Tale/wiki/How-to-play");
    }

    private IEnumerator SwitchScenes(string sceneName)
    {
        // Load new scene
        yield return SceneManager.LoadSceneAsync(sceneName);
    }

    public GameObject buttonsParent;

    [Header("Optional direct refs (drag from Canvas)")]
    public Button btnSimulation;
    public Button btnFlyover;
    public Button btnSave;
    public Button btnLoad;
    public Button btnDocumentation;
    public Button btnSettings;  // Imagination Adjustment
    public Button btnQuit;

    [Header("Optional game refs")]
    public DungeonGenerator generator;   // if New Map should generate immediately

    
    // ---------- Utilities ----------
    Button FindButton(string name, bool warnIfMissing = true)
    {
        var go = FindIncludingInactive(name);
        if (!go)
            go = FindIncludingInactive($"Button {name}");
        if (!go)
        {
            if (warnIfMissing)
                Debug.LogWarning($"[MenuManager] Could not find button '{name}'.", this);
            return null;
        }

        var button = go.GetComponent<Button>();
        if (!button)
        {
            Debug.LogWarning($"[MenuManager] Object '{go.name}' was found, but it has no Button component.", go);
            return null;
        }

        Debug.Log($"[MenuManager] Resolved button '{name}' to scene object '{go.name}'.", button);
        return button;
    }

    Button CreateSaveButtonFromLoadButton()
    {
        if (!btnLoad)
            return null;

        Button saveButton = Instantiate(btnLoad, btnLoad.transform.parent);
        saveButton.name = "Save";
        saveButton.transform.SetSiblingIndex(btnLoad.transform.GetSiblingIndex());
        SetButtonLabel(saveButton, "Save");

        RectTransform saveRect = saveButton.GetComponent<RectTransform>();
        RectTransform loadRect = btnLoad.GetComponent<RectTransform>();
        if (saveRect != null && loadRect != null && saveButton.transform.parent == btnLoad.transform.parent)
        {
            float offsetY = loadRect.rect.height > 0f ? loadRect.rect.height + 8f : 48f;
            saveRect.anchoredPosition = loadRect.anchoredPosition + new Vector2(0f, offsetY);
        }

        Debug.Log("[MenuManager] Created Save button by cloning the Load button.", saveButton);
        return saveButton;
    }

    static void SetButtonLabel(Button button, string label)
    {
        if (!button)
            return;

        Text legacyText = button.GetComponentInChildren<Text>(includeInactive: true);
        if (legacyText != null)
            legacyText.text = label;

        TMPro.TMP_Text tmpText = button.GetComponentInChildren<TMPro.TMP_Text>(includeInactive: true);
        if (tmpText != null)
            tmpText.text = label;
    }

    bool TryResolveGenerator(out DungeonGenerator mapGenerator)
    {
        mapGenerator = generator;
        if (mapGenerator == null && dir != null)
            mapGenerator = dir.gen;
        if (mapGenerator == null)
            mapGenerator = FindFirstObjectByType<DungeonGenerator>();
        if (mapGenerator != null)
        {
            generator = mapGenerator;
            return true;
        }

        BottomBanner.Show("Save/load failed: map generator is not ready.");
        Debug.LogWarning("[MenuManager] Save/load ignored because no DungeonGenerator is available.", this);
        return false;
    }

    void PlayButtonClick()
    {
        AudioPlayer player = audioPlayer;
        if (player == null && dir != null)
            player = dir.audioPlayer;

        player?.PlayClip("Button-Click");
    }

    GameObject FindIncludingInactive(string name)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            // Skip prefabs (assets not in scene)
            if (obj.hideFlags == HideFlags.None && obj.name == name)
            {
                return obj;
            }
        }
        return null;
    }

    void Hook(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (!btn)
        {
            Debug.LogWarning($"[MenuManager] Skipping hook for action '{action.Method.Name}' because button is null.", this);
            return;
        }

        //Debug.Log($"[MenuManager] Hooking '{btn.name}' to '{action.Method.Name}'. Existing runtime listeners will be cleared.", btn);
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
        //Debug.Log($"[MenuManager] Hook complete for '{btn.name}' -> '{action.Method.Name}'.", btn);
    }
}
