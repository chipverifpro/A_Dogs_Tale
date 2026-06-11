using System.Collections;
using System.Collections.Generic;
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

    [Header("Gameplay Intro")]
    [SerializeField] private bool showPlayerIntroEmote = true;
    [SerializeField] private string playerIntroEmoteId = "A_0";

    [Header("Debug/UX")]
    public bool allowSkip = true;          // press any key / click to skip after min time
    [SerializeField] private KeyCode returnToTitleKey = KeyCode.Delete;

    bool isTitleOverlayVisible = true;
    bool isTransitioning;
    bool hasGameStarted;
    bool isReviewingSplashScreens;
    int currentSplashIndex = 1;
    List<SplashAdjust> splashAdjustments;
    RectTransform splashTitleLargeRect;
    RectTransform splashTitleSmallRect;
    readonly List<RectTransform> splashTitleLargeChildRects = new();
    readonly List<RectTransform> splashTitleSmallChildRects = new();
    RectTransform buildingMessageRect;
    RectTransform buildingMessageTextRect;
    RectTransform buildingMessageOutlineRect;
    bool hasCapturedSplashAdjustmentDefaults;
    RectTransformPositionState defaultSplashTitleLargeState;
    RectTransformPositionState defaultSplashTitleSmallState;
    readonly List<RectTransformPositionState> defaultSplashTitleLargeChildStates = new();
    readonly List<RectTransformPositionState> defaultSplashTitleSmallChildStates = new();
    RectTransformPositionState defaultBuildingMessageState;
    RectTransformPositionState defaultBuildingMessageTextState;
    RectTransformPositionState defaultBuildingMessageOutlineState;
    int currentLoadingMessageIndex = -1;

    public bool IsTitleOverlayVisible => isTitleOverlayVisible;
    public bool IsReviewingSplashScreens => isReviewingSplashScreens;

    private enum SplashOrientation
    {
        Horizontal,
        Vertical,
        Square
    }

    private sealed class SplashAdjust
    {
        public int imageNum;
        public Vector2 titlePos = new(0.5f, 0.2f);
        public Vector2 messagePos = new(0.5f, 0.85f);

        public SplashAdjust(int imageNum)
        {
            this.imageNum = imageNum;
        }

        public SplashAdjust(int imageNum, Vector2 titlePos, Vector2 messagePos)
        {
            this.imageNum = imageNum;
            this.titlePos = titlePos;
            this.messagePos = messagePos;
        }
    }

    private struct RectTransformPositionState
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 offsetMin;
        public Vector2 offsetMax;
    }

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
            return;
        }

        PrimeStartupBlackScreen();
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

        splashAdjustments = new List<SplashAdjust>
        {
            // Example: duplicate this line and edit imageNum/titlePos/messagePos for each picture.
            //new SplashAdjust(imageNum: 1, titlePos: new Vector2(0.5f, 0.25f), messagePos: new Vector2(0.5f, 0.85f)),
            new SplashAdjust(imageNum: 66, titlePos: new Vector2(0.5f, 0.5f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum: 65, titlePos: new Vector2(0.5f, 0.25f), messagePos: new Vector2(0.8f, 0.8f)),
            new SplashAdjust(imageNum: 64, titlePos: new Vector2(0.5f, 0.0f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum: 63, titlePos: new Vector2(0.1f, 0.05f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum: 62, titlePos: new Vector2(0.0f, 0.05f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum: 60, titlePos: new Vector2(0.1f, 0.35f), messagePos: new Vector2(0.5f, 0.9f)),
            // split
            new SplashAdjust(imageNum: 59, titlePos: new Vector2(0.5f, 0.5f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum: 58, titlePos: new Vector2(0.5f, 0.0f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum: 56, titlePos: new Vector2(0.0f, 0.25f), messagePos: new Vector2(0.75f, 0.75f)),
            new SplashAdjust(imageNum: 55, titlePos: new Vector2(0.25f, 0.25f), messagePos: new Vector2(0.75f, 0.75f)),
            new SplashAdjust(imageNum: 54, titlePos: new Vector2(0.0f, 0.0f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum: 53, titlePos: new Vector2(0.3f, 0.25f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum: 52, titlePos: new Vector2(0.4f, 0.5f), messagePos: new Vector2(0.75f, 0.75f)),
            new SplashAdjust(imageNum: 51, titlePos: new Vector2(0.9f, 0.4f), messagePos: new Vector2(0.75f, 0.85f)),
            new SplashAdjust(imageNum: 50, titlePos: new Vector2(0.5f, 0.0f), messagePos: new Vector2(0.55f, 0.75f)),
            new SplashAdjust(imageNum: 49, titlePos: new Vector2(0.25f, 0.25f), messagePos: new Vector2(0.75f, 0.75f)),
            new SplashAdjust(imageNum: 48, titlePos: new Vector2(0.5f, 0.5f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum: 47, titlePos: new Vector2(0.05f, 0.05f), messagePos: new Vector2(0.75f, 0.75f)),
            new SplashAdjust(imageNum: 46, titlePos: new Vector2(0.1f, 0.25f), messagePos: new Vector2(0.75f, 0.75f)),
            //split
            new SplashAdjust(imageNum: 44, titlePos: new Vector2(0.5f, 0.1f), messagePos: new Vector2(0.5f, 0.75f)),
            // split
            new SplashAdjust(imageNum: 43, titlePos: new Vector2(0.1f, 0.0f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum: 42, titlePos: new Vector2(0.5f, 0.0f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum: 41, titlePos: new Vector2(0.5f, 0.5f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum: 40, titlePos: new Vector2(0.5f, 0.25f), messagePos: new Vector2(0.75f, 0.8f)),
            new SplashAdjust(imageNum: 39, titlePos: new Vector2(0.5f, 0.05f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum: 35, titlePos: new Vector2(0.5f, 0.0f), messagePos: new Vector2(0.4f, 0.75f)),
            new SplashAdjust(imageNum: 34, titlePos: new Vector2(0.5f, 0.25f), messagePos: new Vector2(0.9f, 0.8f)),

            new SplashAdjust(imageNum: 31, titlePos: new Vector2(0.5f, 0.0f), messagePos: new Vector2(0.25f, 0.9f)),
            new SplashAdjust(imageNum: 30, titlePos: new Vector2(0.75f, 0.25f), messagePos: new Vector2(0.25f, 0.75f)),
            new SplashAdjust(imageNum: 28, titlePos: new Vector2(0.5f, 0.1f), messagePos: new Vector2(0.75f, 0.25f)),
            
            new SplashAdjust(imageNum: 27, titlePos: new Vector2(0.5f, 0.1f), messagePos: new Vector2(0.75f, 0.8f)),
            new SplashAdjust(imageNum: 25, titlePos: new Vector2(0.5f, 0.2f), messagePos: new Vector2(0.5f, 0.4f)),
            new SplashAdjust(imageNum: 24, titlePos: new Vector2(0.25f, 0.25f), messagePos: new Vector2(0.15f, 0.75f)),
            // wrong rotate order
            new SplashAdjust(imageNum: 23, titlePos: new Vector2(0.95f, 0.05f), messagePos: new Vector2(0.8f, 0.7f)),
            new SplashAdjust(imageNum: 22, titlePos: new Vector2(0.5f, 0.6f), messagePos: new Vector2(0.5f, 0.8f)),
            new SplashAdjust(imageNum: 21, titlePos: new Vector2(0.2f, 0.2f), messagePos: new Vector2(0.4f, 0.9f)),

            new SplashAdjust(imageNum: 20, titlePos: new Vector2(0.1f, 0.1f), messagePos: new Vector2(0.1f, 0.3f)),
            new SplashAdjust(imageNum: 19, titlePos: new Vector2(0.5f, 0.5f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum: 17, titlePos: new Vector2(0.5f, 0.6f), messagePos: new Vector2(0.5f, 0.8f)),
            new SplashAdjust(imageNum: 16, titlePos: new Vector2(0.5f, 0.2f), messagePos: new Vector2(0.75f, 0.95f)),
            // not split
            new SplashAdjust(imageNum: 15, titlePos: new Vector2(0.5f, 0.5f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum: 11, titlePos: new Vector2(0.5f, 0.05f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum:  8, titlePos: new Vector2(0.5f, 0.0f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum:  7, titlePos: new Vector2(0.5f, 0.5f), messagePos: new Vector2(0.5f, 0.75f)),
            new SplashAdjust(imageNum:  6, titlePos: new Vector2(0.5f, 0.15f), messagePos: new Vector2(0.5f, 0.9f)),
            new SplashAdjust(imageNum:  5, titlePos: new Vector2(0.5f, 0.25f), messagePos: new Vector2(0.75f, 0.75f)),
            new SplashAdjust(imageNum:  4, titlePos: new Vector2(1.0f, 0.1f), messagePos: new Vector2(0.5f, 0.9f)),
            new SplashAdjust(imageNum:  3, titlePos: new Vector2(0.0f, 0.0f), messagePos: new Vector2(1.0f, 0.95f)),
            new SplashAdjust(imageNum:  2, titlePos: new Vector2(0.0f, 0.0f), messagePos: new Vector2(0.4f, 0.9f)),
            new SplashAdjust(imageNum:  1, titlePos: new Vector2(0.5f, 0.05f), messagePos: new Vector2(0.5f, 0.85f)),
            
        };

        SetupTitleSFX();    // configure music and SFX

        StartCoroutine(ShowInitialMenu());
        
        //if (audioPlayer) audioPlayer.PlayClip("Bark_GermanShepherd");
        
        //if (!sfx) sfx = FindFirstObjectByType<AudioPlayer>();
        //if (sfx) sfx.RandomRepeatSFX("German_shepherd_bark",minVol:0.05f, maxVol:0.15f, MinTime:5f, MaxTime: 15f));
    }

    void Update()
    {
        if (isReviewingSplashScreens)
        {
            UpdateSplashScreenReviewInput();
            return;
        }

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
    [SerializeField] private GameObject buildingMessage;
    [SerializeField] private TMPro.TMP_Text buildingMessageText;
    [SerializeField] private TMPro.TMP_Text buildingMessageOutlineText;
    [SerializeField] private string splashResourceFolder = "Images";
    [SerializeField] private int splashCount = 40;
    [Tooltip("Screen aspect ratios above this are horizontal, below its inverse are vertical, and between them are treated as square.")]
    [SerializeField] private float splashOrientationAspectThreshold = 1.2f;
    [SerializeField] private Vector2 splashReviewIndexLabelOffset = new Vector2(16f, 12f);
    [SerializeField] private int splashReviewIndexLabelFontSize = 18;
    [SerializeField] private float splashReviewSwipeMinPixels = 80f;
    [SerializeField] private float splashReviewSwipeMaxVerticalRatio = 0.75f;

    private TMPro.TMP_Text splashReviewIndexLabel;
    private const int NoSplashReviewSwipePointer = int.MinValue;
    private int splashReviewSwipePointerId = NoSplashReviewSwipePointer;
    private Vector2 splashReviewSwipeStartScreen;

    // Call this right before you display the splash.
    private void SetRandomSplashSprite()
    {
        int chosenIndex = Random.Range(1, splashCount + 1); // inclusive 1..10
        SetSplashSprite(chosenIndex);
    }

    private void SetSplashSprite(int index)
    {
        if (splashImage == null)
        {
            Debug.LogError("[TitleScreen] splashImage is not assigned.");
            return;
        }

        currentSplashIndex = WrapSplashIndex(index);
        string resourcePath = $"{splashResourceFolder}/SplashScreen{currentSplashIndex}";

        Sprite loadedSprite = LoadSplashSpriteForCurrentScreen(resourcePath);
        if (loadedSprite == null)
        {
            Debug.LogError($"[TitleScreen] Could not load splash sprite at Resources/{resourcePath}. " +
                           $"Make sure the file exists and Texture Type is 'Sprite (2D and UI)'.");
            return;
        }

        splashImage.sprite = loadedSprite;
        ApplySplashCoverLayout(loadedSprite);
        ApplySplashAdjustmentForCurrentImage();
        if (isReviewingSplashScreens)
            SetRandomBuildingMessage(avoidImmediateRepeat: true);
        UpdateSplashReviewIndexLabel();

        // Optional: if you want the Image to match the sprite’s native size
        // splashImage.SetNativeSize();
    }

    private int WrapSplashIndex(int index)
    {
        int count = Mathf.Max(1, splashCount);
        while (index < 1)
            index += count;
        while (index > count)
            index -= count;
        return index;
    }

    private Sprite LoadSplashSpriteForCurrentScreen(string resourcePath)
    {
        Sprite[] sprites = SpriteServer.GetSpriteSheetSprites(resourcePath);
        if (sprites == null || sprites.Length == 0)
            return SpriteServer.SpriteLookup(resourcePath)
                ?? SpriteServer.SpriteResourceLookup(resourcePath);

        if (sprites.Length == 1)
            return sprites[0];

        SplashOrientation orientation = GetSplashOrientation();
        Sprite selectedSprite = orientation switch
        {
            SplashOrientation.Horizontal => FindSplashVariant(sprites, "_0"),
            SplashOrientation.Vertical => FindSplashVariant(sprites, "_1"),
            _ => FindSplashVariant(sprites, "_2")
        };

        if (selectedSprite != null)
            return selectedSprite;

        if (orientation == SplashOrientation.Square)
            return FindUnsuffixedSplashVariant(sprites) ?? FindSplashVariant(sprites, "_0") ?? sprites[0];

        return FindSplashVariant(sprites, "_2") ?? FindUnsuffixedSplashVariant(sprites) ?? sprites[0];
    }

    private SplashOrientation GetSplashOrientation()
    {
        float threshold = Mathf.Max(1f, splashOrientationAspectThreshold);
        float screenAspect = GetSplashScreenAspect();
        if (screenAspect >= threshold)
            return SplashOrientation.Horizontal;

        if (screenAspect <= 1f / threshold)
            return SplashOrientation.Vertical;

        return SplashOrientation.Square;
    }

    private float GetSplashScreenAspect()
    {
        RectTransform parentRect = splashImage != null ? splashImage.rectTransform.parent as RectTransform : null;
        float width = parentRect != null && parentRect.rect.width > 0f ? parentRect.rect.width : Screen.width;
        float height = parentRect != null && parentRect.rect.height > 0f ? parentRect.rect.height : Screen.height;
        return height > 0f ? width / height : 1f;
    }

    private static Sprite FindSplashVariant(Sprite[] sprites, string suffix)
    {
        foreach (Sprite sprite in sprites)
        {
            if (sprite != null && sprite.name.EndsWith(suffix, System.StringComparison.Ordinal))
                return sprite;
        }

        return null;
    }

    private static Sprite FindUnsuffixedSplashVariant(Sprite[] sprites)
    {
        foreach (Sprite sprite in sprites)
        {
            if (sprite == null)
                continue;

            int underscoreIndex = sprite.name.LastIndexOf('_');
            bool hasNumericSuffix = underscoreIndex >= 0
                && underscoreIndex < sprite.name.Length - 1
                && int.TryParse(sprite.name.Substring(underscoreIndex + 1), out _);
            if (!hasNumericSuffix)
                return sprite;
        }

        return null;
    }

    private void ApplySplashCoverLayout(Sprite sprite)
    {
        if (sprite == null || splashImage == null || Mathf.Approximately(sprite.rect.height, 0f))
            return;

        splashImage.type = Image.Type.Simple;
        splashImage.preserveAspect = false;

        RectTransform imageRect = splashImage.rectTransform;
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = Vector2.zero;

        float spriteAspect = sprite.rect.width / sprite.rect.height;
        AspectRatioFitter aspectFitter = splashImage.GetComponent<AspectRatioFitter>();
        if (aspectFitter != null)
        {
            aspectFitter.enabled = true;
            aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspectFitter.aspectRatio = spriteAspect;
            return;
        }

        RectTransform parentRect = imageRect.parent as RectTransform;
        float parentWidth = parentRect != null && parentRect.rect.width > 0f ? parentRect.rect.width : Screen.width;
        float parentHeight = parentRect != null && parentRect.rect.height > 0f ? parentRect.rect.height : Screen.height;
        if (parentWidth <= 0f || parentHeight <= 0f)
            return;

        float parentAspect = parentWidth / parentHeight;
        imageRect.sizeDelta = spriteAspect > parentAspect
            ? new Vector2(parentHeight * spriteAspect, parentHeight)
            : new Vector2(parentWidth, parentWidth / spriteAspect);
    }

    void ApplySplashAdjustmentForCurrentImage()
    {
        ResolveSplashAdjustmentTargets();
        CaptureSplashAdjustmentDefaultsIfNeeded();

        if (!TryGetSplashAdjustment(currentSplashIndex, out SplashAdjust adjustment))
        {
            RestoreSplashAdjustmentDefaults();
            return;
        }

        ApplyTitleScreenPercentPosition(splashTitleLargeRect, splashTitleLargeChildRects, adjustment.titlePos);
        ApplyTitleScreenPercentPosition(splashTitleSmallRect, splashTitleSmallChildRects, adjustment.titlePos);
        ApplyMessageScreenPercentPosition(adjustment.messagePos);
    }

    bool TryGetSplashAdjustment(int imageNum, out SplashAdjust adjustment)
    {
        if (splashAdjustments != null)
        {
            for (int i = 0; i < splashAdjustments.Count; i++)
            {
                SplashAdjust candidate = splashAdjustments[i];
                if (candidate != null && candidate.imageNum == imageNum)
                {
                    adjustment = candidate;
                    return true;
                }
            }
        }

        adjustment = null;
        return false;
    }

    void ResolveSplashAdjustmentTargets()
    {
        if (splashTitleLargeRect == null)
        {
            Transform titleTransform = splashCanvasGroup != null
                ? splashCanvasGroup.transform.Find("GameTitle Large")
                : null;
            if (titleTransform != null)
                splashTitleLargeRect = titleTransform.GetComponent<RectTransform>();
        }

        if (splashTitleSmallRect == null)
        {
            Transform titleTransform = splashCanvasGroup != null
                ? splashCanvasGroup.transform.Find("GameTitle Small")
                : null;
            if (titleTransform != null)
                splashTitleSmallRect = titleTransform.GetComponent<RectTransform>();
        }
        CacheDirectChildRectTransforms(splashTitleLargeRect, splashTitleLargeChildRects);
        CacheDirectChildRectTransforms(splashTitleSmallRect, splashTitleSmallChildRects);

        ResolveBuildingMessage();
        if (buildingMessageRect == null && buildingMessage != null)
            buildingMessageRect = buildingMessage.GetComponent<RectTransform>();
        if (buildingMessageTextRect == null && buildingMessageText != null)
            buildingMessageTextRect = buildingMessageText.rectTransform;
        if (buildingMessageOutlineRect == null && buildingMessageOutlineText != null)
            buildingMessageOutlineRect = buildingMessageOutlineText.rectTransform;
    }

    void CaptureSplashAdjustmentDefaultsIfNeeded()
    {
        if (hasCapturedSplashAdjustmentDefaults)
            return;

        defaultSplashTitleLargeState = CaptureRectTransformPositionState(splashTitleLargeRect);
        defaultSplashTitleSmallState = CaptureRectTransformPositionState(splashTitleSmallRect);
        CaptureRectTransformPositionStates(splashTitleLargeChildRects, defaultSplashTitleLargeChildStates);
        CaptureRectTransformPositionStates(splashTitleSmallChildRects, defaultSplashTitleSmallChildStates);
        defaultBuildingMessageState = CaptureRectTransformPositionState(buildingMessageRect);
        defaultBuildingMessageTextState = CaptureRectTransformPositionState(buildingMessageTextRect);
        defaultBuildingMessageOutlineState = CaptureRectTransformPositionState(buildingMessageOutlineRect);
        hasCapturedSplashAdjustmentDefaults = true;
    }

    void RestoreSplashAdjustmentDefaults()
    {
        RestoreRectTransformPositionState(splashTitleLargeRect, defaultSplashTitleLargeState);
        RestoreRectTransformPositionState(splashTitleSmallRect, defaultSplashTitleSmallState);
        RestoreRectTransformPositionStates(splashTitleLargeChildRects, defaultSplashTitleLargeChildStates);
        RestoreRectTransformPositionStates(splashTitleSmallChildRects, defaultSplashTitleSmallChildStates);
        RestoreRectTransformPositionState(buildingMessageRect, defaultBuildingMessageState);
        RestoreRectTransformPositionState(buildingMessageTextRect, defaultBuildingMessageTextState);
        RestoreRectTransformPositionState(buildingMessageOutlineRect, defaultBuildingMessageOutlineState);
    }

    static RectTransformPositionState CaptureRectTransformPositionState(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return default;

        return new RectTransformPositionState
        {
            anchorMin = rectTransform.anchorMin,
            anchorMax = rectTransform.anchorMax,
            pivot = rectTransform.pivot,
            anchoredPosition = rectTransform.anchoredPosition,
            sizeDelta = rectTransform.sizeDelta,
            offsetMin = rectTransform.offsetMin,
            offsetMax = rectTransform.offsetMax
        };
    }

    static void RestoreRectTransformPositionState(RectTransform rectTransform, RectTransformPositionState state)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = state.anchorMin;
        rectTransform.anchorMax = state.anchorMax;
        rectTransform.pivot = state.pivot;
        rectTransform.anchoredPosition = state.anchoredPosition;
        rectTransform.sizeDelta = state.sizeDelta;
        rectTransform.offsetMin = state.offsetMin;
        rectTransform.offsetMax = state.offsetMax;
    }

    static void CaptureRectTransformPositionStates(List<RectTransform> rectTransforms, List<RectTransformPositionState> states)
    {
        states.Clear();
        for (int i = 0; i < rectTransforms.Count; i++)
            states.Add(CaptureRectTransformPositionState(rectTransforms[i]));
    }

    static void RestoreRectTransformPositionStates(List<RectTransform> rectTransforms, List<RectTransformPositionState> states)
    {
        int count = Mathf.Min(rectTransforms.Count, states.Count);
        for (int i = 0; i < count; i++)
            RestoreRectTransformPositionState(rectTransforms[i], states[i]);
    }

    static void ApplyScreenPercentPosition(RectTransform rectTransform, Vector2 position)
    {
        if (rectTransform == null)
            return;

        Vector2 normalizedAnchor = new Vector2(
            Mathf.Clamp01(position.x),
            1f - Mathf.Clamp01(position.y));

        rectTransform.anchorMin = normalizedAnchor;
        rectTransform.anchorMax = normalizedAnchor;
        rectTransform.pivot = normalizedAnchor;
        rectTransform.anchoredPosition = Vector2.zero;
    }

    void ApplyMessageScreenPercentPosition(Vector2 position)
    {
        StretchRectToParent(buildingMessageRect);

        ApplyScreenPercentPosition(buildingMessageTextRect, position);
        ApplyScreenPercentPosition(buildingMessageOutlineRect, position);
    }

    static void ApplyTitleScreenPercentPosition(RectTransform titleRect, List<RectTransform> childRects, Vector2 position)
    {
        StretchRectToParent(titleRect);

        if (childRects.Count == 0)
        {
            ApplyScreenPercentPosition(titleRect, position);
            return;
        }

        for (int i = 0; i < childRects.Count; i++)
            ApplyScreenPercentPosition(childRects[i], position);
    }

    static void StretchRectToParent(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    static void CacheDirectChildRectTransforms(RectTransform parent, List<RectTransform> childRects)
    {
        if (parent == null || childRects.Count > 0)
            return;

        for (int i = 0; i < parent.childCount; i++)
        {
            if (parent.GetChild(i).TryGetComponent(out RectTransform childRect))
                childRects.Add(childRect);
        }
    }

    public void StartSplashScreenReview()
    {
        if (isTransitioning)
            return;

        ResolveBuildingMessage();
        EnsureSplashReviewIndexLabel();
        SetSplashMenuCameraEnabled(true);
        SetBuildingMessageVisible(true);
        SetCanvasGroupState(menuCanvasGroup, 0f, false);
        SetCanvasGroupState(splashCanvasGroup, 1f, true);

        if (splashReviewIndexLabel != null)
            splashReviewIndexLabel.gameObject.SetActive(true);

        ClearSplashReviewSwipeTracking();
        isReviewingSplashScreens = true;
        SetSplashSprite(currentSplashIndex);
    }

    void StopSplashScreenReview()
    {
        if (!isReviewingSplashScreens)
            return;

        isReviewingSplashScreens = false;
        ClearSplashReviewSwipeTracking();
        if (splashReviewIndexLabel != null)
            splashReviewIndexLabel.gameObject.SetActive(false);

        SetBuildingMessageVisible(false);
        SetCanvasGroupState(splashCanvasGroup, 0f, false);
        SetCanvasGroupState(menuCanvasGroup, 1f, true);
        RefreshTitleMenuButtons();
    }

    void UpdateSplashScreenReviewInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.leftArrowKey.wasPressedThisFrame)
        {
            SetSplashSprite(currentSplashIndex - 1);
            return;
        }

        if (keyboard != null && keyboard.rightArrowKey.wasPressedThisFrame)
        {
            SetSplashSprite(currentSplashIndex + 1);
            return;
        }

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            StopSplashScreenReview();
            return;
        }

        UpdateSplashScreenReviewSwipeInput();
    }

    void UpdateSplashScreenReviewSwipeInput()
    {
        if (Application.platform != RuntimePlatform.Android)
            return;

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null)
            return;

        for (int i = 0; i < touchscreen.touches.Count; i++)
        {
            var touch = touchscreen.touches[i];
            if (touch == null)
                continue;

            int touchId = touch.touchId.ReadValue();
            Vector2 position = touch.position.ReadValue();

            if (splashReviewSwipePointerId == NoSplashReviewSwipePointer)
            {
                if (!touch.press.wasPressedThisFrame)
                    continue;

                splashReviewSwipePointerId = touchId;
                splashReviewSwipeStartScreen = position;
                return;
            }

            if (splashReviewSwipePointerId != touchId)
                continue;

            if (!touch.press.wasReleasedThisFrame)
                return;

            Vector2 delta = position - splashReviewSwipeStartScreen;
            ClearSplashReviewSwipeTracking();

            float minSwipePixels = Mathf.Max(1f, splashReviewSwipeMinPixels);
            if (Mathf.Abs(delta.x) < minSwipePixels)
                return;

            float maxVerticalPixels = Mathf.Abs(delta.x) * Mathf.Max(0f, splashReviewSwipeMaxVerticalRatio);
            if (Mathf.Abs(delta.y) > maxVerticalPixels)
                return;

            SetSplashSprite(delta.x < 0f ? currentSplashIndex + 1 : currentSplashIndex - 1);
            return;
        }
    }

    void ClearSplashReviewSwipeTracking()
    {
        splashReviewSwipePointerId = NoSplashReviewSwipePointer;
        splashReviewSwipeStartScreen = Vector2.zero;
    }

    void EnsureSplashReviewIndexLabel()
    {
        if (splashReviewIndexLabel != null)
            return;

        Transform parent = splashCanvasGroup != null ? splashCanvasGroup.transform : transform;
        GameObject labelObject = new GameObject("SplashReviewIndexLabel", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = splashReviewIndexLabelOffset;
        rect.sizeDelta = new Vector2(160f, 36f);

        splashReviewIndexLabel = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
        splashReviewIndexLabel.fontSize = splashReviewIndexLabelFontSize;
        splashReviewIndexLabel.alignment = TMPro.TextAlignmentOptions.BottomLeft;
        splashReviewIndexLabel.color = new Color(1f, 1f, 1f, 0.9f);
        splashReviewIndexLabel.raycastTarget = false;
        splashReviewIndexLabel.gameObject.SetActive(false);
        splashReviewIndexLabel.transform.SetAsLastSibling();
    }

    void UpdateSplashReviewIndexLabel()
    {
        if (splashReviewIndexLabel == null)
            return;

        string quoteIndexText = currentLoadingMessageIndex >= 0 && loadingMessages != null && loadingMessages.Length > 0
            ? $"  Quote {currentLoadingMessageIndex + 1}/{loadingMessages.Length}"
            : string.Empty;
        splashReviewIndexLabel.text = $"{currentSplashIndex}/{Mathf.Max(1, splashCount)}{quoteIndexText}";
        splashReviewIndexLabel.transform.SetAsLastSibling();
    }

    private IEnumerator ShowInitialMenu()
    {
        PrimeStartupBlackScreen();
        SetOverlayedGameplayUiVisible(false);
        SetBuildingMessageVisible(false);
        RefreshTitleMenuButtons();

        // Pick a splash now so the loading transition has an image ready immediately.
        SetRandomSplashSprite();

        BottomBanner.Show("Welcome, Pup! Sniffing out treasures...");

        audioPlayer.PlayClip("Opening Title");
        audioPlayer.PlayClip("Bark_GS_repeat");

        yield return StartCoroutine(Fade(menuCanvasGroup, 0f, 1f));
        yield return StartCoroutine(PlayTitlePullUp());

        isTitleOverlayVisible = true;
    }

    private void PrimeStartupBlackScreen()
    {
        SetSplashMenuCameraEnabled(true);
        SetCanvasGroupState(splashCanvasGroup, 0f, false);
        SetCanvasGroupState(menuCanvasGroup, 0f, false);
        PrepareTitlePullUp();
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
        TriggerPlayerIntroEmote();

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
        SetRandomBuildingMessage();
        SetSplashMenuCameraEnabled(true);
        SetBuildingMessageVisible(true);
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
        SetBuildingMessageVisible(false);
        SetSplashMenuCameraEnabled(false);
        SetOverlayedGameplayUiVisible(true);
        isTitleOverlayVisible = false;
        TriggerPlayerIntroEmote();

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

    void TriggerPlayerIntroEmote()
    {
        if (!showPlayerIntroEmote || string.IsNullOrWhiteSpace(playerIntroEmoteId))
            return;

        WorldObject controlledObject = null;
        if (dir != null)
        {
            controlledObject = dir.gameInputRouter != null
                ? dir.gameInputRouter.currentControlledWorldObject
                : dir.playerPack != null ? dir.playerPack.packLeader : null;
        }

        if (controlledObject == null)
            controlledObject = GameInputRouter.Instance != null
                ? GameInputRouter.Instance.currentControlledWorldObject
                : null;

        if (controlledObject == null)
            return;

        BottomBanner.LogEmote(controlledObject, playerIntroEmoteId);
    }

    void SetSceneObjectActive(string objectName, bool visible)
    {
        GameObject target = DungeonGenerator.FindInActiveScene(objectName);
        if (target != null)
            target.SetActive(visible);
    }

    void SetBuildingMessageVisible(bool visible)
    {
        ResolveBuildingMessage();

        if (buildingMessage != null && buildingMessage.activeSelf != visible)
            buildingMessage.SetActive(visible);
    }

    void SetRandomBuildingMessage(bool avoidImmediateRepeat = false)
    {
        if (loadingMessages == null || loadingMessages.Length == 0)
            return;

        ResolveBuildingMessage();

        int messageIndex = Random.Range(0, loadingMessages.Length);
        if (avoidImmediateRepeat && loadingMessages.Length > 1 && messageIndex == currentLoadingMessageIndex)
            messageIndex = (messageIndex + Random.Range(1, loadingMessages.Length)) % loadingMessages.Length;

        currentLoadingMessageIndex = messageIndex;
        string message = FormatSplashQuoteForDisplay(loadingMessages[messageIndex]);
        if (buildingMessageText != null)
            buildingMessageText.text = message;
        if (buildingMessageOutlineText != null)
            buildingMessageOutlineText.text = message;
    }

    static string FormatSplashQuoteForDisplay(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message ?? string.Empty;

        int middleIndex = message.Length / 2;
        int bestSpaceIndex = -1;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < message.Length; i++)
        {
            if (message[i] != ' ')
                continue;

            int distance = Mathf.Abs(i - middleIndex);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestSpaceIndex = i;
        }

        return bestSpaceIndex >= 0
            ? message.Substring(0, bestSpaceIndex) + "\n" + message.Substring(bestSpaceIndex + 1)
            : message;
    }

    void ResolveBuildingMessage()
    {
        if (buildingMessage == null)
            buildingMessage = DungeonGenerator.FindInActiveScene("BuildingMessage");

        if (buildingMessageText == null && buildingMessage != null)
            buildingMessageText = FindTMPTextChild(buildingMessage.transform, "BuildingMessage Text");
        if (buildingMessageOutlineText == null && buildingMessage != null)
            buildingMessageOutlineText = FindTMPTextChild(buildingMessage.transform, "BuildingMessage Outline");
    }

    TMPro.TMP_Text FindTMPTextChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName && child.TryGetComponent(out TMPro.TMP_Text text))
                return text;
        }

        return null;
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

        Sprite sprite = SpriteServer.SpriteLookup(resourcePath)
            ?? SpriteServer.SpriteResourceLookup(resourcePath);
        if (sprite != null)
            return sprite;

        Debug.LogWarning($"[SceneFader] Could not load leash image at Resources/{resourcePath}.", this);
        return null;
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


    [SerializeField] private string[] loadingMessages =
    {
        "Building the world...",
        "Digging up a map...",
        "Searching for a home...",
        "Raising the fences...",
        "Planting the trees...",
        "Laying the trails...",
        "Marking the paths...",
        "Setting the pawprints...",
        "Hiding the treasures...",
        "Burying the bones...",
        "Painting the doghouse...",
        "Assembling the park...",
        "Growing the grass...",
        "Filling the water bowls...",
        "Opening the gates...",
        "Placing the landmarks...",
        "Packing the forest...",
        "Building the castle...",
        "Sniff-proofing the yard...",
        "Unrolling the adventure...",

        "Following a scent trail...",
        "Sniffing out clues...",
        "Wagging into position...",
        "Picking the best stick...",
        "Rounding up the pack...",
        "Finding something interesting...",
        "Checking every corner...",
        "Listening for trouble...",
        "Tracking fresh footprints...",
        "Looking for the ball...",
        "Preparing zoomies...",
        "Practicing good dog manners...",
        "Choosing who gets the stick...",
        "Deciding where to dig...",
        "Testing the nose-cam...",
        "Perking up the ears...",
        "Focusing on the target...",
        "Picking a favorite route...",
        "Getting ready to explore...",
        "Waiting for the signal...",

        "Gathering the pack...",
        "Choosing a leader...",
        "Forming up the team...",
        "Getting into formation...",
        "Counting noses...",
        "Making room in the line...",
        "Lining up the scouts...",
        "Teaching everyone the route...",
        "Deciding who follows who...",
        "Organizing the adventure party...",

        "Preparing today’s mission...",
        "Hiding the quest markers...",
        "Looking for lost things...",
        "Setting the scene...",
        "Getting the adventure ready...",
        "Placing mysterious clues...",
        "Checking the old map...",
        "Opening the next chapter...",
        "Waiting for the heroes...",
        "Making something worth sniffing...",

        "Stirring the scent fog...",
        "Mixing the smells...",
        "Freshening the trail...",
        "Blowing scent through the wind...",
        "Updating the nose report...",
        "Tuning the sniff sensors...",
        "Sharpening the trail markers...",
        "Making everything smell important...",
        "Leaving suspicious scents around...",
        "Preparing premium smells...",

        "Convincing the sheep to cooperate...",
        "Asking the ducks to hold still...",
        "Translating barks...",
        "Re-hiding the bone...",
        "Pretending this was all planned...",
        "Untangling the leashes...",
        "Calibrating tail wag speed...",
        "Making the grass slightly chewable...",
        "Negotiating with squirrels...",
        "Dog-proofing the interface...",
        "Reticulating pawprints...",
        "Rendering extra enthusiasm...",
        "Politely ignoring the mailman...",
        "Importing sticks...",
        "Fluffing the clouds...",
        "Making the world 37% sniffier..."
    };
}
