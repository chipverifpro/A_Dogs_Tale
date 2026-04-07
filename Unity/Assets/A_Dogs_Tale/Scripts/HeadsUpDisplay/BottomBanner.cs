using System;
using System.Collections;
using System.Collections.Generic;
using DogGame;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum BannerSense
{
    None,
    Alert,
    Hearing,
    Vision,
    Smell
}

public enum BannerLevel
{
    None,
    Low,
    Medium,
    High
}

[Serializable]
public sealed class BannerMessageEntry
{
    public BannerSense sense;
    public BannerLevel level;
    public string message;
    public string renderedText;
    public bool includesGameTime;
    public float gameTimeSeconds;
    public DateTime createdAtUtc;
}

public class BottomBanner : MonoBehaviour
{
    public static BottomBanner Instance { get; private set; }

    internal static void ResetStaticStateForReload()
    {
        Instance = null;
    }

    [Header("Style")]
    [SerializeField] Color backgroundColor = new Color(0.9f, 0.9f, 0.9f, 0.75f);
    [SerializeField] Color textColor = new Color(0.13f, 0.13f, 0.13f, 1f);
    [SerializeField] int fontSize = 22;
    [SerializeField] float height = 172f;
    [SerializeField] float sidePadding = 16f;
    [SerializeField] bool useSafeArea = true;

    [Header("Message Log")]
    [SerializeField] int visibleLineCount = 3;
    [SerializeField] float rowMinHeight = 42f;
    [SerializeField] float rowSpacing = 4f;
    [SerializeField] float iconSize = 28f;
    [SerializeField] int maxMessageLines = 2;
    [SerializeField] string spriteSheetResourcePath = "Sprites/SensesSymbolsColor_v4";
    [SerializeField] bool defaultDisplayUsesRichText = true;
    [SerializeField] bool autoScrollToNewest = true;
    [SerializeField] int maxHistoryEntries;

    public Canvas BottomBannerCanvas;

    readonly List<BannerMessageEntry> messageHistory = new List<BannerMessageEntry>();
    readonly Dictionary<string, Sprite> senseSpriteLookup = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    readonly List<GameObject> rowObjects = new List<GameObject>();

    GameObject panel;
    RectTransform panelRT;
    ScrollRect scrollRect;
    RectTransform viewportRT;
    RectTransform contentRT;
    Scrollbar verticalScrollbar;
    Coroutine hideRoutine;
    float elapsedGameTimeSeconds;
    bool legacyStyleMigrated;

    public IReadOnlyList<BannerMessageEntry> MessageHistory => messageHistory;

    public bool IsPointerOverPanel()
    {
        if (panelRT == null || panel == null || !panel.activeInHierarchy)
            return false;

        Vector2 screenPoint;
        if (Mouse.current != null)
        {
            screenPoint = Mouse.current.position.ReadValue();
        }
        else
        {
            screenPoint = Input.mousePosition;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(panelRT, screenPoint, null);
    }

    void Awake()
    {
        if (!TryRegisterSingletonInstance())
            return;

        BuildUIIfNeeded();
    }

    void OnEnable()
    {
        if (!TryRegisterSingletonInstance())
            return;

        BuildUIIfNeeded();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        elapsedGameTimeSeconds += GameTime.DeltaTime;
    }

    bool TryRegisterSingletonInstance()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return false;
        }

        Instance = this;
        return true;
    }

    void BuildUIIfNeeded()
    {
        MigrateLegacyStyleIfNeeded();

        if (BottomBannerCanvas == null)
        {
            GameObject canvasGO = new GameObject("BottomBannerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            BottomBannerCanvas = canvasGO.GetComponent<Canvas>();
        }

        EnsureRootOverlayCanvas();

        BottomBannerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        BottomBannerCanvas.overrideSorting = true;
        BottomBannerCanvas.sortingOrder = 5000;

        CanvasScaler scaler = BottomBannerCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        if (panel == null)
        {
            panel = new GameObject("BannerPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(BottomBannerCanvas.transform, false);
            panelRT = panel.GetComponent<RectTransform>();
        }
        else if (panelRT == null)
        {
            panelRT = panel.GetComponent<RectTransform>();
        }

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = backgroundColor;

        panelRT.anchorMin = new Vector2(0f, 0f);
        panelRT.anchorMax = new Vector2(1f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta = new Vector2(0f, Mathf.Max(height, GetMinimumPanelHeight()));

        if (scrollRect == null)
        {
            GameObject scrollGO = new GameObject("MessageScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGO.transform.SetParent(panel.transform, false);

            RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(sidePadding, 10f);
            scrollRT.offsetMax = new Vector2(-sidePadding, -10f);

            Image scrollImage = scrollGO.GetComponent<Image>();
            scrollImage.color = new Color(1f, 1f, 1f, 0f);
            scrollRect = scrollGO.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            viewportRT = viewportGO.GetComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = new Vector2(-18f, 0f);
            viewportGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.025f);

            GameObject contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGO.transform.SetParent(viewportGO.transform, false);
            contentRT = contentGO.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.offsetMin = Vector2.zero;
            contentRT.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = contentGO.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = rowSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = contentGO.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            verticalScrollbar = CreateScrollbar(scrollGO.transform);
            scrollRect.viewport = viewportRT;
            scrollRect.content = contentRT;
            scrollRect.verticalScrollbar = verticalScrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scrollRect.verticalScrollbarSpacing = 4f;
        }

        ApplySafeArea();
    }

    void EnsureRootOverlayCanvas()
    {
        if (transform.parent != null)
            transform.SetParent(null, false);

        RectTransform canvasRT = BottomBannerCanvas.GetComponent<RectTransform>();
        if (canvasRT == null)
            return;

        canvasRT.anchorMin = Vector2.zero;
        canvasRT.anchorMax = Vector2.one;
        canvasRT.pivot = new Vector2(0.5f, 0.5f);
        canvasRT.anchoredPosition = Vector2.zero;
        canvasRT.sizeDelta = Vector2.zero;
        canvasRT.offsetMin = Vector2.zero;
        canvasRT.offsetMax = Vector2.zero;
        canvasRT.localScale = Vector3.one;
        canvasRT.localRotation = Quaternion.identity;
    }

    void OnRectTransformDimensionsChange()
    {
        if (useSafeArea && panelRT != null)
            ApplySafeArea();
    }

    void ApplySafeArea()
    {
        if (panelRT == null)
            return;

        Rect safeArea;
#if UNITY_EDITOR || UNITY_STANDALONE
        safeArea = new Rect(0f, 0f, Screen.width, Screen.height);
#else
        safeArea = Screen.safeArea;
#endif

        float bottomInset = useSafeArea ? Mathf.Max(0f, safeArea.y) : 0f;
        panelRT.anchoredPosition = new Vector2(0f, bottomInset);
    }

    float GetMinimumPanelHeight()
    {
        return 20f + visibleLineCount * GetRowHeight() + Mathf.Max(0, visibleLineCount - 1) * rowSpacing;
    }

    float GetRowHeight()
    {
        int lineCount = Mathf.Max(1, maxMessageLines);
        float estimatedTextHeight = fontSize * lineCount * 1.2f;
        float estimatedPadding = 12f;
        float estimatedIconHeight = iconSize + 4f;
        return Mathf.Max(rowMinHeight, estimatedTextHeight + estimatedPadding, estimatedIconHeight);
    }

    void MigrateLegacyStyleIfNeeded()
    {
        if (legacyStyleMigrated)
            return;

        legacyStyleMigrated = true;

        bool looksLikeLegacyBanner =
            Mathf.Approximately(backgroundColor.r, 0f) &&
            Mathf.Approximately(backgroundColor.g, 0f) &&
            Mathf.Approximately(backgroundColor.b, 0f) &&
            Mathf.Approximately(backgroundColor.a, 0.5f) &&
            Mathf.Approximately(textColor.r, 1f) &&
            Mathf.Approximately(textColor.g, 1f) &&
            Mathf.Approximately(textColor.b, 1f) &&
            fontSize >= 48 &&
            height <= 48f;

        if (!looksLikeLegacyBanner)
        {
            height = Mathf.Max(height, GetMinimumPanelHeight());
            return;
        }

        backgroundColor = new Color(0.9f, 0.9f, 0.9f, 0.75f);
        textColor = new Color(0.13f, 0.13f, 0.13f, 1f);
        fontSize = 22;
        height = GetMinimumPanelHeight();
        sidePadding = 16f;
    }

    Scrollbar CreateScrollbar(Transform parent)
    {
        GameObject scrollbarGO = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarGO.transform.SetParent(parent, false);

        RectTransform scrollbarRT = scrollbarGO.GetComponent<RectTransform>();
        scrollbarRT.anchorMin = new Vector2(1f, 0f);
        scrollbarRT.anchorMax = new Vector2(1f, 1f);
        scrollbarRT.pivot = new Vector2(1f, 1f);
        scrollbarRT.offsetMin = new Vector2(-14f, 2f);
        scrollbarRT.offsetMax = Vector2.zero;
        scrollbarRT.sizeDelta = new Vector2(14f, 0f);

        Image trackImage = scrollbarGO.GetComponent<Image>();
        trackImage.color = new Color(0.65f, 0.65f, 0.65f, 0.45f);

        GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
        slidingArea.transform.SetParent(scrollbarGO.transform, false);
        RectTransform slidingRT = slidingArea.GetComponent<RectTransform>();
        slidingRT.anchorMin = Vector2.zero;
        slidingRT.anchorMax = Vector2.one;
        slidingRT.offsetMin = new Vector2(2f, 2f);
        slidingRT.offsetMax = new Vector2(-2f, -2f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(slidingArea.transform, false);
        RectTransform handleRT = handle.GetComponent<RectTransform>();
        handleRT.anchorMin = new Vector2(0f, 1f);
        handleRT.anchorMax = new Vector2(1f, 1f);
        handleRT.pivot = new Vector2(0.5f, 1f);
        handleRT.sizeDelta = new Vector2(0f, 40f);

        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = new Color(0.35f, 0.35f, 0.35f, 0.8f);

        Scrollbar scrollbar = scrollbarGO.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRT;
        scrollbar.targetGraphic = handleImage;
        scrollbar.size = 1f;
        scrollbar.value = 0f;

        return scrollbar;
    }

    void LoadSenseSpritesIfNeeded()
    {
        if (senseSpriteLookup.Count > 0)
            return;

        Sprite[] sprites = Resources.LoadAll<Sprite>(spriteSheetResourcePath);
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null || string.IsNullOrEmpty(sprite.name))
                continue;

            senseSpriteLookup[sprite.name] = sprite;
        }
    }

    static string EscapeTMP(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input.Replace("<", "&lt;").Replace(">", "&gt;");
    }

    string FormatMessageText(string message, bool includeGameTime, bool isRichText)
    {
        string safeMessage = message ?? string.Empty;
        string body = isRichText ? safeMessage : EscapeTMP(safeMessage);

        if (!includeGameTime)
            return body;

        return $"<color=#5A5A5A>[{FormatGameTime(elapsedGameTimeSeconds)}]</color> {body}";
    }

    static string FormatGameTime(float totalSeconds)
    {
        if (totalSeconds < 0f)
            totalSeconds = 0f;

        TimeSpan span = TimeSpan.FromSeconds(totalSeconds);
        int hours = (int)span.TotalHours;
        return $"{hours:00}:{span.Minutes:00}:{span.Seconds:00}";
    }

    string GetSpriteName(BannerSense sense, BannerLevel level)
    {
        if (sense == BannerSense.None)
            return "Sense_Alert_None";

        string levelName = level.ToString();
        if (level == BannerLevel.None)
            levelName = "None";

        return $"Sense_{sense}_{levelName}";
    }

    Sprite GetSpriteFor(BannerSense sense, BannerLevel level)
    {
        LoadSenseSpritesIfNeeded();

        string spriteName = GetSpriteName(sense, level);
        if (senseSpriteLookup.TryGetValue(spriteName, out Sprite sprite))
            return sprite;

        if (senseSpriteLookup.TryGetValue("Sense_Alert_None", out sprite))
            return sprite;

        return null;
    }

    GameObject CreateMessageRow(Sprite sprite, string renderedText)
    {
        GameObject row = new GameObject("MessageRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        row.transform.SetParent(contentRT, false);

        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        float rowHeight = GetRowHeight();
        rowLayout.minHeight = rowHeight;
        rowLayout.preferredHeight = rowHeight;

        HorizontalLayoutGroup rowGroup = row.GetComponent<HorizontalLayoutGroup>();
        rowGroup.spacing = 8f;
        rowGroup.padding = new RectOffset(4, 4, 2, 2);
        rowGroup.childAlignment = TextAnchor.UpperLeft;
        rowGroup.childControlHeight = true;
        rowGroup.childControlWidth = true;
        rowGroup.childForceExpandHeight = false;
        rowGroup.childForceExpandWidth = false;

        ContentSizeFitter fitter = row.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconGO.transform.SetParent(row.transform, false);
        Image iconImage = iconGO.GetComponent<Image>();
        iconImage.sprite = sprite;
        iconImage.preserveAspect = true;

        LayoutElement iconLayout = iconGO.GetComponent<LayoutElement>();
        iconLayout.minWidth = iconSize;
        iconLayout.preferredWidth = iconSize;
        iconLayout.minHeight = iconSize;
        iconLayout.preferredHeight = iconSize;

        GameObject textGO = new GameObject("MessageText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textGO.transform.SetParent(row.transform, false);

        LayoutElement textLayout = textGO.GetComponent<LayoutElement>();
        textLayout.minWidth = 0f;
        textLayout.flexibleWidth = 1f;

        TextMeshProUGUI text = textGO.GetComponent<TextMeshProUGUI>();
        text.text = renderedText;
        text.fontSize = fontSize;
        text.color = textColor;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.maxVisibleLines = Mathf.Max(1, maxMessageLines);
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.margin = Vector4.zero;

        return row;
    }

    void AddMessageInternal(BannerSense sense, BannerLevel level, string message, bool includeGameTime, bool isRichText)
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        BuildUIIfNeeded();

        string renderedText = FormatMessageText(message, includeGameTime, isRichText);
        Sprite sprite = GetSpriteFor(sense, level);

        BannerMessageEntry entry = new BannerMessageEntry
        {
            sense = sense,
            level = level,
            message = message ?? string.Empty,
            renderedText = renderedText,
            includesGameTime = includeGameTime,
            gameTimeSeconds = elapsedGameTimeSeconds,
            createdAtUtc = DateTime.UtcNow
        };

        messageHistory.Add(entry);
        rowObjects.Add(CreateMessageRow(sprite, renderedText));

        if (maxHistoryEntries > 0)
        {
            while (messageHistory.Count > maxHistoryEntries && rowObjects.Count > 0)
            {
                messageHistory.RemoveAt(0);
                GameObject oldestRow = rowObjects[0];
                rowObjects.RemoveAt(0);
                Destroy(oldestRow);
            }
        }

        panel.SetActive(true);

        if (!autoScrollToNewest)
            return;

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    void _Clear()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (panel != null)
            panel.SetActive(false);
    }

    void _ShowFor(BannerSense sense, BannerLevel level, string message, float seconds, bool includeGameTime, bool isRichText)
    {
        AddMessageInternal(sense, level, message, includeGameTime, isRichText);
        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        // Disable Hide mode for now
        //hideRoutine = StartCoroutine(HideAfter(seconds));
    }

    IEnumerator HideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _Clear();
    }

    static BottomBanner GetOrCreateInstance()
    {
        if (Instance == null)
        {
#if UNITY_2023_1_OR_NEWER
            Instance = FindFirstObjectByType<BottomBanner>(FindObjectsInactive.Include);
#else
            Instance = FindFirstObjectByType<BottomBanner>();
#endif
            if (Instance == null)
                CreateSingleton();
        }

        return Instance;
    }

    static void CreateSingleton()
    {
        GameObject go = new GameObject("BottomBanner");
        go.AddComponent<BottomBanner>();
    }

    public static void Show(string message)
    {
        Show(BannerSense.None, BannerLevel.None, message, false);
    }

    public static void Show(BannerSense sense, BannerLevel level, string message, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddMessageInternal(sense, level, message, includeGameTime, false);
    }

    public static void LogMessage(BannerSense sense, BannerLevel level, string message, bool includeGameTime = false)
    {
        Show(sense, level, message, includeGameTime);
    }

    public static void LogRichMessage(BannerSense sense, BannerLevel level, string richMessage, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddMessageInternal(sense, level, richMessage, includeGameTime, true);
    }

    public static void ShowFor(string message, float seconds)
    {
        ShowFor(BannerSense.None, BannerLevel.None, message, seconds, false);
    }

    public static void ShowFor(BannerSense sense, BannerLevel level, string message, float seconds, bool includeGameTime = false)
    {
        GetOrCreateInstance()?._ShowFor(sense, level, message, seconds, includeGameTime, false);
    }

    public static void Clear()
    {
        if (Instance == null)
            return;

        Instance._Clear();
    }

    public static void ClearHistory()
    {
        if (Instance == null)
            return;

        Instance.messageHistory.Clear();
        for (int i = 0; i < Instance.rowObjects.Count; i++)
        {
            if (Instance.rowObjects[i] != null)
                Destroy(Instance.rowObjects[i]);
        }

        Instance.rowObjects.Clear();
        Instance._Clear();
    }

    public void Display(string message)
    {
        BottomBanner liveInstance = GetOrCreateInstance();
        if (liveInstance != null && liveInstance != this)
        {
            liveInstance.Display(message);
            return;
        }

        if (defaultDisplayUsesRichText)
            DisplayRich(message);
        else
            DisplayPlain(message);
    }

    public void DisplayPlain(string message)
    {
        BottomBanner liveInstance = GetOrCreateInstance();
        if (liveInstance != null && liveInstance != this)
        {
            liveInstance.DisplayPlain(message);
            return;
        }

        AddMessageInternal(BannerSense.None, BannerLevel.None, message, false, false);
    }

    public void DisplayRich(string richMessage)
    {
        BottomBanner liveInstance = GetOrCreateInstance();
        if (liveInstance != null && liveInstance != this)
        {
            liveInstance.DisplayRich(richMessage);
            return;
        }

        AddMessageInternal(BannerSense.None, BannerLevel.None, richMessage, false, true);
    }

    public void AddMessage(BannerSense sense, BannerLevel level, string message, bool includeGameTime = false)
    {
        BottomBanner liveInstance = GetOrCreateInstance();
        if (liveInstance != null && liveInstance != this)
        {
            liveInstance.AddMessage(sense, level, message, includeGameTime);
            return;
        }

        AddMessageInternal(sense, level, message, includeGameTime, false);
    }

    public void AddRichMessage(BannerSense sense, BannerLevel level, string richMessage, bool includeGameTime = false)
    {
        BottomBanner liveInstance = GetOrCreateInstance();
        if (liveInstance != null && liveInstance != this)
        {
            liveInstance.AddRichMessage(sense, level, richMessage, includeGameTime);
            return;
        }

        AddMessageInternal(sense, level, richMessage, includeGameTime, true);
    }
}
