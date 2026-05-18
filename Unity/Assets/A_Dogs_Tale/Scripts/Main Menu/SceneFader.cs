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

    [Header("Title Pull-Up")]
    [SerializeField] private RectTransform pullUpByLeash;
    [SerializeField] private GameObject settingsIcon;
    [SerializeField] private string settingsIconObjectName = "SettingsIcon";
    [SerializeField] private Image leashHangingImage;
    [SerializeField] private SpriteRenderer leashHangingSpriteRenderer;
    [SerializeField] private string leashHangingBeforeResourcePath = "Sprites/LeashHanging_A";
    [SerializeField] private string leashHangingAfterResourcePath = "Sprites/LeashHanging_B";
    [SerializeField] private Vector2 pullUpStartPosition = new(0f, -450f);
    [SerializeField] private Vector2 pullUpEndPosition = Vector2.zero;
    [SerializeField] private float pullUpDuration = 1f;
    [SerializeField] private float pullUpOvershootY = 24f;
    [SerializeField] private float pullUpSettleDuration = 0.18f;

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
            ReturnToTitleMenu();
    }

    public void ReturnToTitleMenu()
    {
        if (!hasGameStarted || isTransitioning || isTitleOverlayVisible)
            return;

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
        PrepareTitlePullUp();
        RefreshTitleMenuButtons();
        SetCanvasGroupState(menuCanvasGroup, 0f, false);

        audioPlayer.PlayClip("Opening Title");
        audioPlayer.PlayClip("Bark_GS_repeat");

        yield return StartCoroutine(Fade(menuCanvasGroup, 0f, 1f));
        yield return StartCoroutine(PlayTitlePullUp());

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
        PrepareTitlePullUp();
        RefreshTitleMenuButtons();

        yield return StartCoroutine(Fade(menuCanvasGroup, 0f, 1f));
        yield return StartCoroutine(PlayTitlePullUp());

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

    void PrepareTitlePullUp()
    {
        ResolveTitlePullUpReferences();

        if (pullUpByLeash != null)
            pullUpByLeash.anchoredPosition = pullUpStartPosition;

        Sprite beforeSprite = LoadLeashSprite(leashHangingBeforeResourcePath);
        SetLeashSprite(beforeSprite);
    }

    IEnumerator PlayTitlePullUp()
    {
        ResolveTitlePullUpReferences();
        EnableTitlePullUpObjects();

        if (pullUpByLeash == null)
            yield break;

        Vector2 overshootPosition = pullUpEndPosition + new Vector2(0f, Mathf.Max(0f, pullUpOvershootY));
        float riseDuration = Mathf.Max(0.01f, pullUpDuration);
        float settleDuration = Mathf.Max(0.01f, pullUpSettleDuration);

        yield return StartCoroutine(MovePullUpByLeash(pullUpStartPosition, overshootPosition, riseDuration, EaseOutCubic));
        yield return StartCoroutine(MovePullUpByLeash(overshootPosition, pullUpEndPosition, settleDuration, EaseOutBack));

        pullUpByLeash.anchoredPosition = pullUpEndPosition;

        Sprite afterSprite = LoadLeashSprite(leashHangingAfterResourcePath);
        SetLeashSprite(afterSprite);
    }

    IEnumerator MovePullUpByLeash(Vector2 from, Vector2 to, float duration, System.Func<float, float> ease)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            pullUpByLeash.anchoredPosition = Vector2.LerpUnclamped(from, to, ease(t));
            yield return null;
        }
    }

    void ResolveTitlePullUpReferences()
    {
        if (pullUpByLeash == null)
        {
            GameObject pullUpObject = DungeonGenerator.FindInActiveScene("PullUpByLeash");
            if (pullUpObject != null)
                pullUpByLeash = pullUpObject.GetComponent<RectTransform>();
        }

        if (settingsIcon == null && !string.IsNullOrWhiteSpace(settingsIconObjectName))
            settingsIcon = DungeonGenerator.FindInActiveScene(settingsIconObjectName);

        if (leashHangingImage == null || leashHangingSpriteRenderer == null)
        {
            GameObject leashObject = DungeonGenerator.FindInActiveScene("LeashHanging");
            if (leashObject != null)
            {
                if (leashHangingImage == null)
                    leashHangingImage = leashObject.GetComponent<Image>();
                if (leashHangingSpriteRenderer == null)
                    leashHangingSpriteRenderer = leashObject.GetComponent<SpriteRenderer>();
            }
        }
    }

    void EnableTitlePullUpObjects()
    {
        if (pullUpByLeash != null && !pullUpByLeash.gameObject.activeSelf)
            pullUpByLeash.gameObject.SetActive(true);

        if (settingsIcon != null && !settingsIcon.activeSelf)
            settingsIcon.SetActive(true);
    }

    void RefreshTitleMenuButtons()
    {
        MenuManager menuManager = dir != null ? dir.menuManager : null;
        if (menuManager == null)
            menuManager = FindFirstObjectByType<MenuManager>();

        menuManager?.RefreshMainMenuButtonVisibility();
    }

    Sprite LoadLeashSprite(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogWarning($"[SceneFader] Could not load leash image at Resources/{resourcePath}.", this);
            return null;
        }

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    void SetLeashSprite(Sprite sprite)
    {
        if (sprite == null)
            return;

        if (leashHangingImage != null)
            leashHangingImage.sprite = sprite;
        if (leashHangingSpriteRenderer != null)
            leashHangingSpriteRenderer.sprite = sprite;
    }

    static float EaseOutCubic(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
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
