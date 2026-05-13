using System.Collections;
using System.Collections.Generic;
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

    [Header("Menu Button Sprites")]
    [SerializeField] private string buttonSpriteResourcePath = "Sprites/BonesButtonsSprites_A";
    [SerializeField] private string settingsIconResourcePath = "Sprites/SettingsIcons_A";
    [SerializeField] private string settingsIconSpriteName = "SettingsIcons_A_7";
    [SerializeField] private Vector2 settingsIconButtonSize = new Vector2(64f, 64f);
    [SerializeField] private Vector2 settingsIconInset = new Vector2(20f, 20f);
    [SerializeField] private bool useNativeButtonSpriteSize = true;
    [SerializeField] private float buttonSpriteSizeScale = 0.667f;


    void Awake()
    {
        // If not assigned, try to find by name under the Canvas
        btnSimulation = btnSimulation ?? FindButton("Simulation");
        btnSave = btnSave ?? FindButton("Save", warnIfMissing: false);
        btnLoad = btnLoad ?? FindButton("Load");
        btnSave = btnSave ?? CreateSaveButtonFromLoadButton();
        btnDocumentation = btnDocumentation ?? FindButton("Documentation");
        btnSettings = btnSettings ?? FindButton("SettingsIcon");
        btnQuit = btnQuit ?? FindButton("Quit");

        // Clear any existing listeners and add ours
        Hook(btnSimulation, OnSimulation);
        Hook(btnSave, OnSave);
        Hook(btnLoad, OnLoad);
        Hook(btnDocumentation, OnDocumentation);
        Hook(btnSettings, OnSettings);
        Hook(btnQuit, QuitGame);

        ApplyMainMenuButtonSprites();
        ApplySettingsIconButton();

//        Debug.Log(
//            $"[MenuManager] Button refs after Awake: " +
//            $"NewMap={(btnNewMap ? btnNewMap.name : "null")}, " +
//            $"EditMap={(btnEditMap ? btnEditMap.name : "null")}, " +
//            $"Explore={(btnExplore ? btnExplore.name : "null")}, " +
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
        BottomBanner.Show("Digging a brand new hole...");
        PlayButtonClick();

        if (!TryResolveGenerator(out DungeonGenerator mapGenerator))
            return;

        if (fader == null && dir != null)
            fader = dir.sceneFader;
        if (fader == null)
            fader = FindFirstObjectByType<SceneFader>();

        if (fader != null)
        {
            StartCoroutine(fader.FadeToGameAfterMapBuild(mapGenerator));
            return;
        }

        Debug.LogWarning("[MenuManager] No SceneFader found; starting simulation without menu transition.", this);
        mapGenerator.BeginNewSimulation();
    }

    public void OnLoad()
    {
        PlayButtonClick();
        if (!TryResolveGenerator(out DungeonGenerator mapGenerator))
            return;

        if (fader == null && dir != null)
            fader = dir.sceneFader;
        if (fader == null)
            fader = FindFirstObjectByType<SceneFader>();

        if (fader != null)
        {
            StartCoroutine(fader.FadeToGameAfterMapLoad(mapGenerator));
            return;
        }

        Debug.LogWarning("[MenuManager] No SceneFader found; loading simulation without menu transition.", this);
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

    void ApplyMainMenuButtonSprites()
    {
        Sprite[] loadedSprites = Resources.LoadAll<Sprite>(buttonSpriteResourcePath);
        if (loadedSprites == null || loadedSprites.Length == 0)
        {
            Debug.LogWarning($"[MenuManager] Could not load menu button sprites at Resources/{buttonSpriteResourcePath}.", this);
            return;
        }

        Dictionary<string, Sprite> spritesByName = new Dictionary<string, Sprite>(loadedSprites.Length);
        for (int i = 0; i < loadedSprites.Length; i++)
        {
            Sprite sprite = loadedSprites[i];
            if (sprite != null)
                spritesByName[sprite.name] = sprite;
        }

        ApplyButtonBackground(btnSimulation, spritesByName, "BonesButtonsSprites_A_22");
        ApplyButtonBackground(btnSave, spritesByName, "BonesButtonsSprites_A_1");
        ApplyButtonBackground(btnLoad, spritesByName, "BonesButtonsSprites_A_7");
        ApplyButtonBackground(btnDocumentation, spritesByName, "BonesButtonsSprites_A_10");
        ApplyButtonBackground(btnQuit, spritesByName, "BonesButtonsSprites_A_16");
    }

    void ApplySettingsIconButton()
    {
        if (!btnSettings)
            return;

        Sprite settingsSprite = LoadSpriteByName(settingsIconResourcePath, settingsIconSpriteName);
        if (settingsSprite == null)
        {
            Debug.LogWarning($"[MenuManager] Could not find settings icon '{settingsIconSpriteName}' at Resources/{settingsIconResourcePath}.", this);
            return;
        }

        Canvas menuCanvas = btnSettings.GetComponentInParent<Canvas>();
        if (menuCanvas != null && btnSettings.transform.parent != menuCanvas.transform)
            btnSettings.transform.SetParent(menuCanvas.transform, worldPositionStays: false);

        RectTransform rect = btnSettings.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(settingsIconInset.x, -settingsIconInset.y);
            rect.sizeDelta = settingsIconButtonSize;
            rect.SetAsLastSibling();
        }

        LayoutElement layoutElement = btnSettings.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = btnSettings.gameObject.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;
        layoutElement.preferredWidth = settingsIconButtonSize.x;
        layoutElement.preferredHeight = settingsIconButtonSize.y;
        layoutElement.minWidth = settingsIconButtonSize.x;
        layoutElement.minHeight = settingsIconButtonSize.y;

        Image image = btnSettings.targetGraphic as Image;
        if (image == null)
            image = btnSettings.GetComponent<Image>();
        if (image == null)
        {
            Debug.LogWarning($"[MenuManager] Settings button '{btnSettings.name}' has no Image to style.", btnSettings);
            return;
        }

        image.sprite = settingsSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        btnSettings.targetGraphic = image;
        SetButtonLabel(btnSettings, string.Empty);
    }

    Sprite LoadSpriteByName(string resourcePath, string spriteName)
    {
        Sprite[] loadedSprites = Resources.LoadAll<Sprite>(resourcePath);
        if (loadedSprites == null || loadedSprites.Length == 0)
            return null;

        for (int i = 0; i < loadedSprites.Length; i++)
        {
            Sprite sprite = loadedSprites[i];
            if (sprite != null && sprite.name == spriteName)
                return sprite;
        }

        return null;
    }

    void ApplyButtonBackground(Button button, Dictionary<string, Sprite> spritesByName, string spriteName)
    {
        if (!button)
            return;

        if (!spritesByName.TryGetValue(spriteName, out Sprite sprite) || sprite == null)
        {
            Debug.LogWarning($"[MenuManager] Could not find sprite '{spriteName}' in {buttonSpriteResourcePath}.", this);
            return;
        }

        Image image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();
        if (image == null)
        {
            Debug.LogWarning($"[MenuManager] Button '{button.name}' has no Image background to style.", button);
            return;
        }

        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        button.targetGraphic = image;

        if (useNativeButtonSpriteSize)
            SetButtonNativeSpriteSize(button, sprite);
    }

    void SetButtonNativeSpriteSize(Button button, Sprite sprite)
    {
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        Vector2 scaledSize = sprite.rect.size * Mathf.Max(0.01f, buttonSpriteSizeScale);
        rect.sizeDelta = scaledSize;

        LayoutElement layoutElement = button.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = button.gameObject.AddComponent<LayoutElement>();

        layoutElement.preferredWidth = scaledSize.x;
        layoutElement.preferredHeight = scaledSize.y;
        layoutElement.minWidth = scaledSize.x;
        layoutElement.minHeight = scaledSize.y;
    }
}
