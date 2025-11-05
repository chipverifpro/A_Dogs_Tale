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
    public ObjectDirectory dir;

    [Header("Bottom Banner")]
    public BottomBanner bottomBanner;  // assign your existing BottomBanner


    void Awake()
    {
        // If not assigned, try to find by name under the Canvas
        btnNewMap = btnNewMap ?? FindButton("Button NewMap");
        btnEditMap = btnEditMap ?? FindButton("Button EditMap");
        btnExplore = btnExplore ?? FindButton("Button Explore");
        btnFlyover = btnFlyover ?? FindButton("Button Flyover");
        btnSettings = btnSettings ?? FindButton("Button Settings");
        btnQuit = btnQuit ?? FindButton("Button Quit");

        // Clear any existing listeners and add ours
        Hook(btnNewMap, OnNewMap);
        Hook(btnEditMap, OnEditMap);
        Hook(btnExplore, OnExplore);
        Hook(btnFlyover, OnFlyover);
        Hook(btnSettings, OnSettings);
        Hook(btnQuit, OnQuit);

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

    public void OnQuit()
    {
        BottomBanner.Show("💤 Curling up for a nap...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
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
        if (!go) return null;
        return go.GetComponent<Button>();
    }

    void Hook(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (!btn) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
    }
}