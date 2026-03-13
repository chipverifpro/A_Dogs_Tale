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


    void Awake()
    {
        // If not assigned, try to find by name under the Canvas
        btnNewMap = btnNewMap ?? FindButton("NewMap");
        btnEditMap = btnEditMap ?? FindButton("EditMap");
        btnExplore = btnExplore ?? FindButton("Explore");
        btnFlyover = btnFlyover ?? FindButton("Flyover");
        btnSettings = btnSettings ?? FindButton("Settings");
        btnQuit = btnQuit ?? FindButton("Quit");

        // Clear any existing listeners and add ours
        Hook(btnNewMap, OnNewMap);
        Hook(btnEditMap, OnEditMap);
        Hook(btnExplore, OnExplore);
        Hook(btnFlyover, OnFlyover);
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
    }

    // === BUTTON HOOKS ===

    public void OnNewMap()
    {
        Debug.Log($"[MenuManager] OnNewMap invoked. fader={(fader ? fader.name : "null")} dir={(dir ? dir.name : "null")}", this);
        BottomBanner.Show("🐾 Digging a brand new hole...");
        dir.audioPlayer.PlayClip("Button-Click");
        StartCoroutine(fader.FadeToGame());
        //SceneManager.LoadScene("2D_Fargoal_Map");  // your map gen scene
        
        
        //generator.Start();
        // You can also call generator.NewMap() if you keep it same-scene
    }

    public void OnEditMap()
    {
        BottomBanner.Show("🐾 Burying bones... entering Edit Mode.");
        // TODO: load editor tools scene or toggle editor UI
    }

    public void OnExplore()
    {
        BottomBanner.Show("🐾 Sniff sniff... Dog Mode engaged!");
        // TODO: spawn player prefab in first-person
    }

    public void OnFlyover()
    {
        BottomBanner.Show("🐦 Flap flap... Birdy Mode overhead!");
        // TODO: switch to FlyoverCamera routine
    }

    public void OnSettings()
    {
        BottomBanner.Show("🎨 Adjusting imagination...");
        // TODO: open settings panel or scene
    }

    public void QuitGame()
    {
        BottomBanner.Show("💤 Curling up for a nap...");
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

    private IEnumerator SwitchScenes(string sceneName)
    {
        // Load new scene
        yield return SceneManager.LoadSceneAsync(sceneName);
    }

    [Header("Optional direct refs (drag from Canvas)")]
    public Button btnNewMap;
    public Button btnEditMap;
    public Button btnExplore;   // Dog Mode
    public Button btnFlyover;   // Birdy Mode
    public Button btnSettings;  // Imagination Adjustment
    public Button btnQuit;

    [Header("Optional game refs")]
    public DungeonGenerator generator;   // if New Map should generate immediately

    
    // ---------- Utilities ----------
    Button FindButton(string name)
    {
        var go = GameObject.Find(name);
        if (!go)
            go = GameObject.Find($"Button {name}");
        if (!go)
        {
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
