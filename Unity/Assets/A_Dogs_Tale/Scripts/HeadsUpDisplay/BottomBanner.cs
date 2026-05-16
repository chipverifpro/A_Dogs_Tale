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
    public Sprite iconSprite;
    public string message;
    public string renderedText;
    public bool includesGameTime;
    public float gameTimeSeconds;
    public DateTime createdAtUtc;
}

public class BottomBanner : MonoBehaviour
{
    public static BottomBanner Instance { get; private set; }

    [Serializable]
    public sealed class SaveData
    {
        public float elapsedGameTimeSeconds;
        public bool canvasVisible;
        public List<MessageSaveData> messages = new List<MessageSaveData>();
    }

    [Serializable]
    public sealed class MessageSaveData
    {
        public int sense;
        public int level;
        public string iconSpriteName;
        public string message;
        public string renderedText;
        public bool includesGameTime;
        public float gameTimeSeconds;
        public string createdAtUtc;
    }

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
    [SerializeField] bool autoCollapseWhenMouseAway = true;
    [SerializeField] float collapsedHeightFraction = 0.33333334f;
    [SerializeField] float collapsedHeightExtraPixels = 3f;
    [SerializeField] float collapseSlideDuration = 0.18f;

    [Header("Message Log")]
    [SerializeField] int visibleLineCount = 3;
    [SerializeField] float rowMinHeight = 42f;
    [SerializeField] float rowSpacing = 4f;
    [SerializeField] float iconSize = 28f;
    [SerializeField] int maxMessageLines = 2;
    [SerializeField] bool defaultDisplayUsesRichText = true;
    [SerializeField] bool autoScrollToNewest = true;
    [SerializeField] int maxHistoryEntries;

    public Canvas BottomBannerCanvas;

    readonly List<BannerMessageEntry> messageHistory = new List<BannerMessageEntry>();
    readonly List<GameObject> rowObjects = new List<GameObject>();
    readonly List<TextMeshProUGUI> rowTextObjects = new List<TextMeshProUGUI>();

    GameObject panel;
    RectTransform panelRT;
    ScrollRect scrollRect;
    RectTransform viewportRT;
    RectTransform contentRT;
    Scrollbar verticalScrollbar;
    Coroutine hideRoutine;
    float elapsedGameTimeSeconds;
    float currentPanelHeight;
    float panelHeightVelocity;
    bool panelExpanded;
    bool legacyStyleMigrated;
    int lastPanelToggleFrame = -1;

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
            return false;
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
        UpdatePanelClickToggle();
        UpdateAutoCollapse();
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
        if (currentPanelHeight <= 0f)
            currentPanelHeight = GetTargetPanelHeight();
        panelRT.sizeDelta = new Vector2(0f, currentPanelHeight);

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

    void UpdateAutoCollapse()
    {
        if (panelRT == null || panel == null || !panel.activeInHierarchy)
            return;

        float targetHeight = GetTargetPanelHeight();
        bool isAutoCollapsed = autoCollapseWhenMouseAway && targetHeight < GetExpandedPanelHeight() - 0.01f;

        if (!autoCollapseWhenMouseAway)
        {
            currentPanelHeight = targetHeight;
            panelHeightVelocity = 0f;
        }
        else
        {
            currentPanelHeight = Mathf.SmoothDamp(
                currentPanelHeight,
                targetHeight,
                ref panelHeightVelocity,
                Mathf.Max(0.01f, collapseSlideDuration),
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            if (Mathf.Abs(currentPanelHeight - targetHeight) < 0.01f)
            {
                currentPanelHeight = targetHeight;
                panelHeightVelocity = 0f;
            }
        }

        panelRT.sizeDelta = new Vector2(panelRT.sizeDelta.x, currentPanelHeight);
        ApplyTextLineLimit();

        if (isAutoCollapsed && currentPanelHeight > targetHeight + 0.01f && scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    void UpdatePanelClickToggle()
    {
        if (!autoCollapseWhenMouseAway || BottomBannerCanvas == null || !BottomBannerCanvas.enabled)
            return;

        if (panelRT == null || panel == null || !panel.activeInHierarchy)
            return;

        if (!TryGetPrimaryPressScreenPoint(out Vector2 screenPoint))
            return;

        if (!RectTransformUtility.RectangleContainsScreenPoint(panelRT, screenPoint, null))
            return;

        if (lastPanelToggleFrame == Time.frameCount)
            return;

        lastPanelToggleFrame = Time.frameCount;
        panelExpanded = !panelExpanded;
    }

    static bool TryGetPrimaryPressScreenPoint(out Vector2 screenPoint)
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPoint = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPoint = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        screenPoint = Vector2.zero;
        return false;
    }

    void ApplyTextLineLimit()
    {
        int maxVisibleLines = Mathf.Max(1, maxMessageLines);
        for (int i = 0; i < rowTextObjects.Count; i++)
        {
            TextMeshProUGUI text = rowTextObjects[i];
            if (text != null)
                text.maxVisibleLines = maxVisibleLines;
        }
    }

    float GetTargetPanelHeight()
    {
        float expandedHeight = GetExpandedPanelHeight();
        float collapsedHeight = GetCollapsedPanelHeight(expandedHeight);
        if (!autoCollapseWhenMouseAway || panelExpanded)
            return expandedHeight;

        return collapsedHeight;
    }

    float GetExpandedPanelHeight()
    {
        return Mathf.Max(height, GetMinimumPanelHeight());
    }

    float GetCollapsedPanelHeight(float expandedHeight)
    {
        float fraction = Mathf.Clamp01(collapsedHeightFraction);
        if (fraction <= 0f)
            fraction = 0.33333334f;

        return Mathf.Max(GetMinimumCollapsedPanelHeight(), expandedHeight * fraction) + collapsedHeightExtraPixels;
    }

    float GetMinimumCollapsedPanelHeight()
    {
        return 20f + GetRowHeight();
    }

    float GetCanvasScaleFactor()
    {
        if (BottomBannerCanvas == null)
            return Screen.height > 0 ? Screen.height / 1080f : 1f;

        CanvasScaler scaler = BottomBannerCanvas.GetComponent<CanvasScaler>();
        if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            return BottomBannerCanvas.scaleFactor > 0f ? BottomBannerCanvas.scaleFactor : 1f;

        Vector2 reference = scaler.referenceResolution;
        if (reference.x <= 0f || reference.y <= 0f)
            return BottomBannerCanvas.scaleFactor > 0f ? BottomBannerCanvas.scaleFactor : 1f;

        float widthScale = Screen.width / reference.x;
        float heightScale = Screen.height / reference.y;
        return Mathf.Lerp(widthScale, heightScale, scaler.matchWidthOrHeight);
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
        string spriteName = GetSpriteName(sense, level);
        return SpriteServer.SpriteLookup(spriteName) ?? SpriteServer.SpriteLookup("Sense_Alert_None");
    }

    Sprite GetEmoteSprite(string emote, out string displayName)
    {
        return SpriteServer.TryGetEmojiSprite(emote, out Sprite sprite, out displayName) ? sprite : null;
    }

    Sprite GetInventoryMessageSprite()
    {
        return SpriteServer.SpriteLookup("Inventory");
    }

    Sprite GetBuildProgressSprite()
    {
        return SpriteServer.SpriteLookup("BuildProgress");
    }

    void AddEmoteInternal(WorldObject agent, string emote, bool includeGameTime)
    {
        string actorName = agent != null ? agent.DisplayName : "Unknown agent";
        Sprite sprite = GetEmoteSprite(emote, out string emoteName);
        EmoteIconVisualFactory.Show(agent, sprite);
        AddMessageInternal(
            BannerSense.None,
            BannerLevel.None,
            $"{actorName} did the {emoteName} emote.",
            includeGameTime,
            false,
            sprite);
    }

    void AddInventoryInternal(string message, bool includeGameTime)
    {
        AddMessageInternal(
            BannerSense.None,
            BannerLevel.None,
            message,
            includeGameTime,
            false,
            GetInventoryMessageSprite());
    }

    void AddBuildProgressInternal(string message, bool includeGameTime)
    {
        AddMessageInternal(
            BannerSense.None,
            BannerLevel.None,
            message,
            includeGameTime,
            false,
            GetBuildProgressSprite());
    }

    void ShowBuildProgressForInternal(string message, float seconds, bool includeGameTime)
    {
        _ShowFor(
            BannerSense.None,
            BannerLevel.None,
            message,
            seconds,
            includeGameTime,
            false,
            GetBuildProgressSprite());
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
        rowTextObjects.Add(text);

        return row;
    }

    void AddMessageInternal(BannerSense sense, BannerLevel level, string message, bool includeGameTime, bool isRichText)
    {
        AddMessageInternal(sense, level, message, includeGameTime, isRichText, null);
    }

    void AddMessageInternal(BannerSense sense, BannerLevel level, string message, bool includeGameTime, bool isRichText, Sprite iconOverride)
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        BuildUIIfNeeded();

        bool scrollToNewest = ShouldScrollToNewestForNewMessage();
        float previousContentY = contentRT != null ? contentRT.anchoredPosition.y : 0f;

        string renderedText = FormatMessageText(message, includeGameTime, isRichText);
        Sprite sprite = iconOverride != null ? iconOverride : GetSpriteFor(sense, level);

        BannerMessageEntry entry = new BannerMessageEntry
        {
            sense = sense,
            level = level,
            iconSprite = sprite,
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
                if (rowTextObjects.Count > 0)
                    rowTextObjects.RemoveAt(0);
                Destroy(oldestRow);
            }
        }

        panel.SetActive(true);

        if (!autoScrollToNewest)
            return;

        Canvas.ForceUpdateCanvases();
        if (scrollToNewest)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
        else if (contentRT != null)
        {
            scrollRect.StopMovement();
            Vector2 anchoredPosition = contentRT.anchoredPosition;
            anchoredPosition.y = previousContentY;
            contentRT.anchoredPosition = anchoredPosition;
        }
    }

    bool ShouldScrollToNewestForNewMessage()
    {
        if (!autoScrollToNewest || scrollRect == null)
            return false;

        if (IsCollapsedToSingleLine())
            return true;

        return IsScrolledToBottom();
    }

    bool IsCollapsedToSingleLine()
    {
        return autoCollapseWhenMouseAway &&
               GetTargetPanelHeight() < GetExpandedPanelHeight() - 0.01f;
    }

    bool IsScrolledToBottom()
    {
        if (scrollRect == null)
            return true;

        if (contentRT == null || viewportRT == null)
            return true;

        if (contentRT.rect.height <= viewportRT.rect.height + 0.5f)
            return true;

        return scrollRect.verticalNormalizedPosition <= 0.001f;
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

    SaveData CaptureSaveDataInternal()
    {
        SaveData data = new SaveData
        {
            elapsedGameTimeSeconds = elapsedGameTimeSeconds,
            canvasVisible = BottomBannerCanvas == null || BottomBannerCanvas.enabled,
            messages = new List<MessageSaveData>()
        };

        for (int i = 0; i < messageHistory.Count; i++)
        {
            BannerMessageEntry entry = messageHistory[i];
            if (entry == null)
                continue;

            data.messages.Add(new MessageSaveData
            {
                sense = (int)entry.sense,
                level = (int)entry.level,
                iconSpriteName = entry.iconSprite != null ? entry.iconSprite.name : "",
                message = entry.message,
                renderedText = entry.renderedText,
                includesGameTime = entry.includesGameTime,
                gameTimeSeconds = entry.gameTimeSeconds,
                createdAtUtc = entry.createdAtUtc.ToString("o")
            });
        }

        return data;
    }

    void RestoreSaveDataInternal(SaveData data)
    {
        ClearHistoryInternal();

        if (data == null)
            return;

        elapsedGameTimeSeconds = Mathf.Max(0f, data.elapsedGameTimeSeconds);
        BuildUIIfNeeded();

        if (data.messages != null)
        {
            foreach (MessageSaveData savedMessage in data.messages)
            {
                if (savedMessage == null)
                    continue;

                BannerSense sense = (BannerSense)savedMessage.sense;
                BannerLevel level = (BannerLevel)savedMessage.level;
                Sprite sprite = !string.IsNullOrWhiteSpace(savedMessage.iconSpriteName)
                    ? SpriteServer.SpriteLookup(savedMessage.iconSpriteName)
                    : null;
                if (sprite == null)
                    sprite = GetSpriteFor(sense, level);

                DateTime createdAtUtc = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(savedMessage.createdAtUtc) &&
                    DateTime.TryParse(
                        savedMessage.createdAtUtc,
                        null,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out DateTime parsedCreatedAtUtc))
                {
                    createdAtUtc = parsedCreatedAtUtc;
                }

                string renderedText = !string.IsNullOrEmpty(savedMessage.renderedText)
                    ? savedMessage.renderedText
                    : FormatMessageText(savedMessage.message, savedMessage.includesGameTime, false);

                BannerMessageEntry entry = new BannerMessageEntry
                {
                    sense = sense,
                    level = level,
                    iconSprite = sprite,
                    message = savedMessage.message ?? string.Empty,
                    renderedText = renderedText,
                    includesGameTime = savedMessage.includesGameTime,
                    gameTimeSeconds = savedMessage.gameTimeSeconds,
                    createdAtUtc = createdAtUtc
                };

                messageHistory.Add(entry);
                rowObjects.Add(CreateMessageRow(sprite, renderedText));
            }
        }

        if (panel != null)
            panel.SetActive(messageHistory.Count > 0);
        if (BottomBannerCanvas != null)
            BottomBannerCanvas.enabled = data.canvasVisible;

        if (autoScrollToNewest && scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    void ClearHistoryInternal()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        messageHistory.Clear();
        for (int i = 0; i < rowObjects.Count; i++)
        {
            if (rowObjects[i] != null)
                Destroy(rowObjects[i]);
        }

        rowObjects.Clear();
        rowTextObjects.Clear();
        _Clear();
    }

    void _ShowFor(BannerSense sense, BannerLevel level, string message, float seconds, bool includeGameTime, bool isRichText)
    {
        _ShowFor(sense, level, message, seconds, includeGameTime, isRichText, null);
    }

    void _ShowFor(BannerSense sense, BannerLevel level, string message, float seconds, bool includeGameTime, bool isRichText, Sprite iconOverride)
    {
        AddMessageInternal(sense, level, message, includeGameTime, isRichText, iconOverride);
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

    public static void SetVisible(bool visible)
    {
        GetOrCreateInstance()?._SetVisible(visible);
    }

    public static void Show(BannerSense sense, BannerLevel level, string message, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddMessageInternal(sense, level, message, includeGameTime, false);
    }

    public static void LogMessage(BannerSense sense, BannerLevel level, string message, bool includeGameTime = false)
    {
        Show(sense, level, message, includeGameTime);
    }

    public static void LogMessageWithIcon(BannerSense sense, BannerLevel level, string message, string iconSpriteName, bool includeGameTime = false)
    {
        Sprite icon = SpriteServer.SpriteLookup(iconSpriteName);
        GetOrCreateInstance()?.AddMessageInternal(sense, level, message, includeGameTime, false, icon);
    }

    public static void LogRichMessage(BannerSense sense, BannerLevel level, string richMessage, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddMessageInternal(sense, level, richMessage, includeGameTime, true);
    }

    public static void LogEmote(WorldObject agent, string emote, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddEmoteInternal(agent, emote, includeGameTime);
    }

    public static void LogInventoryMessage(string message, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddInventoryInternal(message, includeGameTime);
    }

    public static void LogBuildProgress(string message, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddBuildProgressInternal(message, includeGameTime);
    }

    void _SetVisible(bool visible)
    {
        BuildUIIfNeeded();

        if (BottomBannerCanvas != null)
            BottomBannerCanvas.enabled = visible;
    }

    public static void ShowFor(string message, float seconds)
    {
        ShowFor(BannerSense.None, BannerLevel.None, message, seconds, false);
    }

    public static void ShowFor(BannerSense sense, BannerLevel level, string message, float seconds, bool includeGameTime = false)
    {
        GetOrCreateInstance()?._ShowFor(sense, level, message, seconds, includeGameTime, false);
    }

    public static void ShowBuildProgressFor(string message, float seconds, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.ShowBuildProgressForInternal(message, seconds, includeGameTime);
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

        Instance.ClearHistoryInternal();
    }

    public static SaveData CaptureSaveData()
    {
        return GetOrCreateInstance()?.CaptureSaveDataInternal();
    }

    public static void RestoreSaveData(SaveData data)
    {
        GetOrCreateInstance()?.RestoreSaveDataInternal(data);
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
