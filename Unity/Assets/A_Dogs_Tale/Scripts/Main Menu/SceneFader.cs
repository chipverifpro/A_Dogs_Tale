using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
    public Dir dir;
    //public AudioPlayer sfx;
    public AudioPlayer audioPlayer;
    public AudioCatalog audioCatalog;
    public static SceneFader Instance;

    [Header("UI (optional)")]
    public CanvasGroup splashCanvasGroup;
    public CanvasGroup menuCanvasGroup;

    [Header("Timing")]
    public float minSplashSeconds = 1.5f;  // brief pause
    public float fadeDuration = 5f;       // cross fade duration

    [Header("Debug/UX")]
    public bool allowSkip = true;          // press any key / click to skip after min time
    [SerializeField] private KeyCode returnToTitleKey = KeyCode.Delete;

    bool isTitleOverlayVisible = true;
    bool isTransitioning;
    bool hasGameStarted;


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (audioPlayer == null)
        {
            audioPlayer = FindFirstObjectByType<AudioPlayer>();
            if (audioPlayer == null)
            {
                Debug.LogError($"SceneFader.Start: AudioPlayer not found");
            }
            else
            {
                audioCatalog = audioPlayer.audioCatalog;
            }
        }

        SetupTitleSFX();    // configure music and SFX

        StartCoroutine(ShowInitialMenu());
        
        //if (audioPlayer) audioPlayer.PlayClip("Bark_GermanShepherd");
        
        //if (!sfx) sfx = FindFirstObjectByType<AudioPlayer>();
        //if (sfx) sfx.RandomRepeatSFX("German_shepherd_bark",minVol:0.05f, maxVol:0.15f, MinTime:5f, MaxTime: 15f));
    }

    void Update()
    {
        if (!hasGameStarted || isTransitioning || isTitleOverlayVisible)
            return;

        if (WasReturnToTitlePressedThisFrame())
            StartCoroutine(FadeToTitleMenu());
    }

    void SetupTitleSFX()
    {
        audioCatalog.AddClipToCatalog(
            name: "Opening Title",
            filename: "Curious Whispers",
            subtitle: "[Music Playing: Curious Whispers]",
            channel: "Music",
            intervalRange: new(0, 0),     // continuous repeat
            preload: true
        );

        audioCatalog.AddClipToCatalog(
            name: "Mission Home Sweet Home",
            filename: "Through the Windowpane",
            subtitle: "[Music Playing: Through the Windowpane]",
            channel: "Music",
            intervalRange: new(0, 0),     // continuous repeat
            preload: false
        );

        audioCatalog.AddClipToCatalog(
            name: "Bark_GS_repeat",
            filename: "Bark_GermanShepherd",
            subtitle: "[Bark (German Shepherd)]",
            channel: "SFX",     // Ambient ?
            pitchRange: new(.95f, 1.05f),
            intervalRange: new(5f, 10f),
            startAfterInterval: true,
            preload: true
        );

        audioCatalog.AddClipToCatalog(
            name: "Bark_GS_once",
            filename: "Bark_GermanShepherd",
            subtitle: "[Bark (German Shepherd)]",
            channel: "SFX",     // Ambient ?
            pitchRange: new(.95f, 1.05f),
            intervalRange: null,
            startAfterInterval: false,
            preload: true
        );
    }

    [Header("Canvases")]
    //[SerializeField] private CanvasGroup splashCanvasGroup;
    //[SerializeField] private CanvasGroup menuCanvasGroup;

    [Header("Splash Image")]
    [SerializeField] private Image splashImage;              // drag the Image component from SplashCanvas here
    [SerializeField] private string splashResourceFolder = "Images";
    [SerializeField] private int splashCount = 19;

    // Call this right before you display the splash.
    private void SetRandomSplashSprite()
    {
        if (splashImage == null)
        {
            Debug.LogError("[TitleScreen] splashImage is not assigned.");
            return;
        }

        int chosenIndex = Random.Range(1, splashCount + 1); // inclusive 1..10
        string resourcePath = $"{splashResourceFolder}/SplashScreen{chosenIndex}";

        Sprite loadedSprite = Resources.Load<Sprite>(resourcePath);
        if (loadedSprite == null)
        {
            Debug.LogError($"[TitleScreen] Could not load splash sprite at Resources/{resourcePath}. " +
                           $"Make sure the file exists and Texture Type is 'Sprite (2D and UI)'.");
            return;
        }

        splashImage.sprite = loadedSprite;

        // Optional: if you want the Image to match the sprite’s native size
        // splashImage.SetNativeSize();
    }

    private IEnumerator ShowInitialMenu()
    {
        SetOverlayedGameplayUiVisible(false);

        // Pick a splash now so the loading transition has an image ready immediately.
        SetRandomSplashSprite();

        yield return null; // let things settle out before beginning this.
        BottomBanner.Show("Welcome, Pup! Sniffing out treasures...");

        SetSplashMenuCameraEnabled(true);
        SetCanvasGroupState(splashCanvasGroup, 0f, false);
        SetCanvasGroupState(menuCanvasGroup, 1f, true);

        audioPlayer.PlayClip("Opening Title");
        audioPlayer.PlayClip("Bark_GS_repeat");

        isTitleOverlayVisible = true;
    }

/*
    private IEnumerator CrossFade_OLD()
    {
        yield return null;      // let things settle out before beginning this.
        BottomBanner.Show("🐾 Welcome, Pup! Sniffing out treasures...");

        // Display just the splash screen.
        splashCanvasGroup.alpha = 1;
        menuCanvasGroup.alpha = 0;

        // Start background Music and SFX
        audioPlayer.PlayClip("Opening Title");
        audioPlayer.PlayClip("Bark_GS_repeat");

        // display splash screen for a bit.  Press any key/mouse button to skip.
        yield return StartCoroutine(WaitAllowSkip(minSplashSeconds));

        // Fade out splash
        StartCoroutine(Fade(splashCanvasGroup, 1f, 0f));
        // Simultaneously fade in menu...
        yield return StartCoroutine(Fade(menuCanvasGroup, 0f, 1f));
    }
*/

    public IEnumerator FadeToGame()
    {
        if (isTransitioning)
            yield break;

        isTransitioning = true;

        //BottomBanner.Show("🐾 Welcome, Pup! On the way to Adventure...");
        BottomBanner.Show("Welcome, Pup! On the way to Adventure...");

        // LEGACY:
        //MusicPlayer musicPlayer = FindFirstObjectByType<MusicPlayer>();
        //if (musicPlayer != null)
        //    musicPlayer.StartMusic(musicPlayer.exploreAudioFileName, fadeOut:true, fadeIn:false);

        // Display just the menu screen.
        //splashCanvasGroup.alpha = 0;
        SetCanvasGroupState(menuCanvasGroup, 1f, true);

        // display splash screen for a bit.  Press any key to skip.
        //yield return StartCoroutine(WaitAllowSkip(minSplashSeconds));

        // Fade out title music and SFX
        audioPlayer.StopClips(trackName: "Opening Title", fadeOut: 1f);
        audioPlayer.StopClips(trackName: "Bark_GS_repeat", fadeOut: -1f); // fadeOut: -1 means finish clip

        // Fade out Main Menu
        yield return StartCoroutine(Fade(menuCanvasGroup, 1f, 0f));
        SetCanvasGroupState(splashCanvasGroup, 0f, false);
        SetCanvasGroupState(menuCanvasGroup, 0f, false);
        SetSplashMenuCameraEnabled(false);
        SetOverlayedGameplayUiVisible(true);
        isTitleOverlayVisible = false;

        if (!hasGameStarted)
        {
            // Let generator know the main menu closed. It will start its music, among other things.
            if (dir?.gen != null)
                dir.gen.MainMenuClosed();
            hasGameStarted = true;
        }

        isTransitioning = false;
    }

    public IEnumerator FadeToGameAfterMapBuild(DungeonGenerator generator)
    {
        if (isTransitioning)
            yield break;

        if (generator == null)
        {
            Debug.LogWarning("[SceneFader] Cannot start simulation because no DungeonGenerator was provided.", this);
            yield break;
        }

        yield return StartCoroutine(FadeToGameAfterMapOperation(generator, generator.BeginNewSimulation));
    }

    public IEnumerator FadeToGameAfterMapLoad(DungeonGenerator generator)
    {
        if (isTransitioning)
            yield break;

        if (generator == null)
        {
            Debug.LogWarning("[SceneFader] Cannot load simulation because no DungeonGenerator was provided.", this);
            yield break;
        }

        if (!DungeonGenerator.SingleMapSaveExists)
        {
            string savePath = DungeonGenerator.SingleMapSavePath;
            BottomBanner.Show($"No map save found at {savePath}");
            Debug.LogWarning($"[SceneFader] Load skipped because no save exists at {savePath}", this);
            yield break;
        }

        yield return StartCoroutine(FadeToGameAfterMapOperation(generator, generator.LoadMapFromSingleSlot));
    }

    private IEnumerator FadeToGameAfterMapOperation(DungeonGenerator generator, System.Action startMapOperation)
    {
        if (isTransitioning)
            yield break;

        isTransitioning = true;

        BottomBanner.Show("Welcome, Pup! On the way to Adventure...");
        SetOverlayedGameplayUiVisible(false);
        SetSplashMenuCameraEnabled(true);
        SetRandomSplashSprite();

        SetCanvasGroupState(menuCanvasGroup, 1f, true);
        SetCanvasGroupState(splashCanvasGroup, 0f, false);

        audioPlayer.StopClips(trackName: "Opening Title", fadeOut: 1f);
        audioPlayer.StopClips(trackName: "Bark_GS_repeat", fadeOut: -1f);

        Coroutine splashFadeIn = StartCoroutine(Fade(splashCanvasGroup, 0f, 1f));
        yield return StartCoroutine(Fade(menuCanvasGroup, 1f, 0f));
        if (splashFadeIn != null)
            yield return splashFadeIn;

        SetCanvasGroupState(menuCanvasGroup, 0f, false);
        SetCanvasGroupState(splashCanvasGroup, 1f, true);

        startMapOperation?.Invoke();
        while (generator.regenerateCoroutine != null || !generator.buildComplete)
            yield return null;

        yield return StartCoroutine(Fade(splashCanvasGroup, 1f, 0f));
        SetCanvasGroupState(splashCanvasGroup, 0f, false);
        SetCanvasGroupState(menuCanvasGroup, 0f, false);
        SetSplashMenuCameraEnabled(false);
        SetOverlayedGameplayUiVisible(true);
        isTitleOverlayVisible = false;

        if (!hasGameStarted)
        {
            if (dir?.gen != null)
                dir.gen.MainMenuClosed();
            hasGameStarted = true;
        }

        isTransitioning = false;
    }

    public IEnumerator WaitAllowSkip(float minSplashSeconds)
    {
        float t = 0f;
        while (t < minSplashSeconds)
        {
            t += Time.deltaTime;
            // Optional: allow skip after min time
 //           if (allowSkip && t >= minSplashSeconds && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
 //           {
 //               break;  // skip remaining initial title time and begin crossfade
 //           }
            yield return null;
        }
        yield return null;
    }

    private IEnumerator Fade(CanvasGroup canvasGroup, float startAlpha, float targetAlpha)
    {
        Debug.Log($"Fading canvas {canvasGroup.name} from {startAlpha} to {targetAlpha}");
        canvasGroup.blocksRaycasts = true; // prevent clicks during fade
        canvasGroup.interactable = true;

        float fadePct = 0f;

        while (fadePct < 1f)
        {
            fadePct += Time.deltaTime / fadeDuration;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, fadePct);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;    // done, make sure fade is complete.
        canvasGroup.blocksRaycasts = (targetAlpha != 0f);
        canvasGroup.interactable = (targetAlpha != 0f);
        yield break;
    }

    IEnumerator FadeToTitleMenu()
    {
        if (isTransitioning)
            yield break;

        isTransitioning = true;
        SetOverlayedGameplayUiVisible(false);
        SetCanvasGroupState(splashCanvasGroup, 0f, false);
        SetCanvasGroupState(menuCanvasGroup, 0f, false);

        yield return StartCoroutine(Fade(menuCanvasGroup, 0f, 1f));

        isTitleOverlayVisible = true;
        isTransitioning = false;
    }

    void SetOverlayedGameplayUiVisible(bool visible)
    {
        SetSceneObjectActive("GeneratorCanvas", visible);
        SetSceneObjectActive("ScentTargetCanvas", visible);
        BottomBanner.SetVisible(visible);
    }

    void SetSceneObjectActive(string objectName, bool visible)
    {
        GameObject target = DungeonGenerator.FindInActiveScene(objectName);
        if (target != null)
            target.SetActive(visible);
    }

    void SetSplashMenuCameraEnabled(bool enabled)
    {
        GameObject splashMenuCamera = DungeonGenerator.FindInActiveScene("CameraSplashMenu");
        if (splashMenuCamera != null)
            splashMenuCamera.SetActive(enabled);
    }

    void SetCanvasGroupState(CanvasGroup canvasGroup, float alpha, bool interactive)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = alpha;
        canvasGroup.blocksRaycasts = interactive;
        canvasGroup.interactable = interactive;
    }

    bool WasReturnToTitlePressedThisFrame()
    {
        return WasKeyCodePressedThisFrame(returnToTitleKey)
            || WasKeyboardKeyPressedThisFrame(Key.Backspace);
    }

    static bool WasKeyCodePressedThisFrame(KeyCode keyCode)
    {
        return keyCode switch
        {
            KeyCode.Backspace => WasKeyboardKeyPressedThisFrame(Key.Backspace),
            KeyCode.Delete => WasKeyboardKeyPressedThisFrame(Key.Delete),
            KeyCode.Escape => WasKeyboardKeyPressedThisFrame(Key.Escape),
            KeyCode.Return => WasKeyboardKeyPressedThisFrame(Key.Enter),
            KeyCode.KeypadEnter => WasKeyboardKeyPressedThisFrame(Key.NumpadEnter),
            KeyCode.Space => WasKeyboardKeyPressedThisFrame(Key.Space),
            _ => false
        };
    }

    static bool WasKeyboardKeyPressedThisFrame(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
    }
}
