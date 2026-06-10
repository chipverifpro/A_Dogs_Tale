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
    [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] Color textColor = Color.white;
    [SerializeField] int fontSize = 34;
    [SerializeField] float height = 128f;
    [SerializeField] float sidePadding = 0f;
    //[SerializeField] bool useSafeArea = true;
    [SerializeField] float backgroundTopOffset = 0f;
    [SerializeField] float backgroundHeightMultiplier = 1f;
    [SerializeField] bool autoCollapseWhenMouseAway = true;
    [SerializeField] float collapseSlideDuration = 0.18f;
    [Tooltip("Debug mode: keep showing banner messages from agents outside the player's pack.")]
    [SerializeField] bool showNonPackAgentMessages = false;

    [Header("Message Log")]
    [SerializeField] int visibleLineCount = 3;
    [SerializeField] int collapsedVisibleEntryCount = 1;
    [SerializeField] float rowMinHeight = 84f;
    [SerializeField] float rowSpacing = 4f;
    [SerializeField] float iconSize = 76f;
    [SerializeField] float iconHitAreaInset = 0f;
//    [SerializeField] bool smoothIconSampling = true;
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
    RectTransform backgroundRT;
    ScrollRect scrollRect;
    RectTransform viewportRT;
    RectTransform contentRT;
    Scrollbar verticalScrollbar;
    Coroutine hideRoutine;
    float elapsedGameTimeSeconds;
    float currentPanelHeight;
    float panelHeightVelocity;
    float backgroundAuthoredHeight;
    float authoredPanelWidth;
    bool panelExpanded;
    bool legacyStyleMigrated;
    int lastPanelToggleFrame = -1;
    bool missingUiWarningLogged;
    bool requestedCanvasVisible = true;
    bool applyingResponsiveWidth;
    float lastResponsiveWidth = -1f;
    int lastResponsiveScreenWidth = -1;
    int lastResponsiveScreenHeight = -1;

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

        return IsScreenPointOverBannerToggleArea(screenPoint);
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
        ApplyBuildCompleteVisibility();
        RefreshResponsiveWidthForScreenChange();
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
            Transform existingCanvas = transform.Find("BottomBannerCanvas");
            if (existingCanvas != null)
            {
                BottomBannerCanvas = existingCanvas.GetComponent<Canvas>();
            }

            if (BottomBannerCanvas == null)
                BottomBannerCanvas = GetComponentInChildren<Canvas>(true);

            if (BottomBannerCanvas == null)
                BottomBannerCanvas = GetComponentInParent<Canvas>(true);
        }

        if (BottomBannerCanvas == null)
        {
            LogMissingSceneAuthoredUi("BottomBannerCanvas");
            return;
        }

        ApplyBuildCompleteVisibility();

        if (panel == null)
        {
            panel = FindDescendant(BottomBannerCanvas.transform, "BannerPanel");
            if (panel == null)
                panel = FindDescendant(transform, "BannerPanel");
            if (panel == null && GetComponentInChildren<ScrollRect>(true) != null)
                panel = gameObject;
        }

        if (panel == null)
        {
            LogMissingSceneAuthoredUi("BannerPanel");
            return;
        }

        panelRT = panelRT != null ? panelRT : panel.GetComponent<RectTransform>();
        if (panelRT != null && authoredPanelWidth <= 0f)
            authoredPanelWidth = Mathf.Max(panelRT.rect.width, panelRT.sizeDelta.x);

        if (backgroundRT == null)
        {
            GameObject backgroundGO = FindDescendant(panel.transform, "Background");
            backgroundRT = backgroundGO != null ? backgroundGO.GetComponent<RectTransform>() : null;
        }

        if (scrollRect == null)
        {
            GameObject scrollGO = FindDescendant(panel.transform, "MessageScrollView");
            scrollRect = scrollGO != null
                ? scrollGO.GetComponent<ScrollRect>()
                : panel.GetComponentInChildren<ScrollRect>(true);
        }

        if (scrollRect == null)
        {
            LogMissingSceneAuthoredUi("MessageScrollView ScrollRect");
            return;
        }

        viewportRT = viewportRT != null ? viewportRT : scrollRect.viewport;
        contentRT = contentRT != null ? contentRT : scrollRect.content;
        verticalScrollbar = verticalScrollbar != null ? verticalScrollbar : scrollRect.verticalScrollbar;

        if (viewportRT == null)
        {
            GameObject viewportGO = FindDescendant(scrollRect.transform, "Viewport");
            viewportRT = viewportGO != null ? viewportGO.GetComponent<RectTransform>() : null;
        }

        if (contentRT == null)
        {
            GameObject contentGO = viewportRT != null
                ? FindDescendant(viewportRT, "Content")
                : FindDescendant(scrollRect.transform, "Content");
            contentRT = contentGO != null ? contentGO.GetComponent<RectTransform>() : null;
        }

        if (verticalScrollbar == null)
            verticalScrollbar = scrollRect.GetComponentInChildren<Scrollbar>(true);

        if (contentRT == null)
            LogMissingSceneAuthoredUi("Content RectTransform");

        ApplyBannerStyle();

        ApplyBuildCompleteVisibility();
    }

    void ApplyBannerStyle()
    {
        if (panelRT != null)
        {
            panelRT.anchorMin = new Vector2(0.5f, 0f);
            panelRT.anchorMax = new Vector2(0.5f, 0f);
            panelRT.pivot = new Vector2(0.5f, 0f);
            panelRT.anchoredPosition = Vector2.zero;
            panelRT.localScale = Vector3.one;
        }

        if (backgroundRT != null)
        {
            backgroundRT.anchorMin = new Vector2(0f, 0f);
            backgroundRT.anchorMax = new Vector2(1f, 0f);
            backgroundRT.pivot = new Vector2(0.5f, 0f);
            backgroundRT.anchoredPosition = Vector2.zero;
            backgroundRT.offsetMin = Vector2.zero;
            backgroundRT.offsetMax = Vector2.zero;
            backgroundRT.localScale = Vector3.one;

            Image backgroundImage = backgroundRT.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.sprite = null;
                backgroundImage.type = Image.Type.Simple;
                backgroundImage.color = backgroundColor;
            }
        }

        RectTransform scrollRT = scrollRect != null ? scrollRect.GetComponent<RectTransform>() : null;
        if (scrollRT != null)
        {
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = Vector2.zero;
            scrollRT.localScale = Vector3.one;
        }

        if (viewportRT != null)
        {
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = new Vector2(8f, 0f);
            viewportRT.offsetMax = new Vector2(-8f, 0f);
        }

        if (verticalScrollbar != null)
            verticalScrollbar.gameObject.SetActive(panelExpanded);

        if (scrollRect != null)
            scrollRect.vertical = panelExpanded;
    }

    static GameObject FindDescendant(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;

        if (parent.name == childName)
            return parent.gameObject;

        for (int i = 0; i < parent.childCount; i++)
        {
            GameObject found = FindDescendant(parent.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    void ApplyBuildCompleteVisibility()
    {
        if (BottomBannerCanvas == null)
            return;

        BottomBannerCanvas.enabled = requestedCanvasVisible && IsBuildComplete();
    }

    static bool IsBuildComplete()
    {
        Dir dir = Dir.Instance;
        return dir != null && dir.gen != null && dir.gen.buildComplete;
    }

    void LogMissingSceneAuthoredUi(string elementName)
    {
        if (missingUiWarningLogged)
            return;

        missingUiWarningLogged = true;
        Debug.LogWarning($"[BottomBanner] Scene-authored UI is missing '{elementName}'. BottomBanner will not create or modify UI hierarchy.", this);
    }

    void OnRectTransformDimensionsChange()
    {
        if (applyingResponsiveWidth)
            return;

        if (panelRT != null)
        {
            currentPanelHeight = panelRT.rect.height;
            ApplyResponsiveWidth();
        }
    }

    void UpdateAutoCollapse()
    {
        if (!autoCollapseWhenMouseAway || panelRT == null || panel == null || !panel.activeInHierarchy)
            return;

        float targetHeight = GetTargetPanelHeight();
        if (currentPanelHeight <= 0f || float.IsNaN(currentPanelHeight))
            currentPanelHeight = panelRT.rect.height;

        if (collapseSlideDuration <= 0f)
        {
            currentPanelHeight = targetHeight;
        }
        else
        {
            currentPanelHeight = Mathf.SmoothDamp(
                currentPanelHeight,
                targetHeight,
                ref panelHeightVelocity,
                collapseSlideDuration,
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            if (Mathf.Abs(currentPanelHeight - targetHeight) < 0.5f)
            {
                currentPanelHeight = targetHeight;
                panelHeightVelocity = 0f;
            }
        }

        ApplyResponsiveWidth();
        panelRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, currentPanelHeight);
        UpdateBackgroundHeight(currentPanelHeight);

        if (!panelExpanded)
            ScrollToNewestMessage();
    }

    void ApplyResponsiveWidth()
    {
        if (panelRT == null)
            return;

        if (applyingResponsiveWidth)
            return;

        float targetWidth = GetAvailableBannerWidth() - Mathf.Max(0f, sidePadding) * 2f;
        if (targetWidth <= 0f)
            targetWidth = panelRT.rect.width;

        applyingResponsiveWidth = true;
        try
        {
            SetVector2IfChanged(panelRT.anchorMin, new Vector2(0.5f, 0f), value => panelRT.anchorMin = value);
            SetVector2IfChanged(panelRT.anchorMax, new Vector2(0.5f, 0f), value => panelRT.anchorMax = value);
            SetVector2IfChanged(panelRT.pivot, new Vector2(0.5f, 0f), value => panelRT.pivot = value);
            SetVector2IfChanged(panelRT.anchoredPosition, Vector2.zero, value => panelRT.anchoredPosition = value);
            SetVector3IfChanged(panelRT.localScale, Vector3.one, value => panelRT.localScale = value);

            if (!Mathf.Approximately(panelRT.rect.width, targetWidth) ||
                !Mathf.Approximately(lastResponsiveWidth, targetWidth))
            {
                panelRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
                lastResponsiveWidth = targetWidth;
            }

            if (backgroundRT != null)
                SetVector3IfChanged(backgroundRT.localScale, Vector3.one, value => backgroundRT.localScale = value);
            if (scrollRect != null)
                SetVector3IfChanged(scrollRect.transform.localScale, Vector3.one, value => scrollRect.transform.localScale = value);
        }
        finally
        {
            applyingResponsiveWidth = false;
        }
    }

    void RefreshResponsiveWidthForScreenChange()
    {
        if (panelRT == null)
            return;

        if (lastResponsiveScreenWidth == Screen.width &&
            lastResponsiveScreenHeight == Screen.height)
            return;

        lastResponsiveScreenWidth = Screen.width;
        lastResponsiveScreenHeight = Screen.height;
        ApplyResponsiveWidth();
    }

    float GetAvailableBannerWidth()
    {
        float availableWidth = float.PositiveInfinity;

        if (panelRT.parent is RectTransform parentRT && parentRT.rect.width > 0f)
            availableWidth = Mathf.Min(availableWidth, parentRT.rect.width);

        RectTransform canvasRT = BottomBannerCanvas != null
            ? BottomBannerCanvas.GetComponent<RectTransform>()
            : null;
        if (canvasRT != null && canvasRT.rect.width > 0f)
            availableWidth = Mathf.Min(availableWidth, canvasRT.rect.width);

        if (BottomBannerCanvas != null && BottomBannerCanvas.scaleFactor > 0f && Screen.width > 0)
            availableWidth = Mathf.Min(availableWidth, Screen.width / BottomBannerCanvas.scaleFactor);

        if (float.IsInfinity(availableWidth))
            availableWidth = authoredPanelWidth;

        return availableWidth;
    }

    void UpdateBackgroundHeight(float panelHeight)
    {
        if (backgroundRT == null)
            return;

        if (backgroundAuthoredHeight <= 0f)
        {
            backgroundAuthoredHeight = backgroundRT.rect.height;
            if (backgroundAuthoredHeight <= 0f)
                backgroundAuthoredHeight = backgroundRT.sizeDelta.y;
        }

        float backgroundHeight = panelHeight * Mathf.Max(1f, backgroundHeightMultiplier);
        backgroundRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, backgroundHeight);

        Vector2 anchoredPosition = backgroundRT.anchoredPosition;
        anchoredPosition.y = backgroundTopOffset;
        backgroundRT.anchoredPosition = anchoredPosition;
    }

    void UpdatePanelClickToggle()
    {
        if (!autoCollapseWhenMouseAway || BottomBannerCanvas == null || !BottomBannerCanvas.enabled)
            return;

        if (panelRT == null || panel == null || !panel.activeInHierarchy)
            return;

        if (!TryGetPrimaryPressScreenPoint(out Vector2 screenPoint))
            return;

        if (InteractionDialogUI.IsPointerBlockingBottomBanner(screenPoint))
            return;

        if (!IsScreenPointOverBannerToggleArea(screenPoint))
            return;

        if (lastPanelToggleFrame == Time.frameCount)
            return;

        lastPanelToggleFrame = Time.frameCount;
        panelExpanded = !panelExpanded;
        ApplyBannerStyle();
        if (panelExpanded)
            TopPulldown.CollapseOpenControls();
        else
            ScrollToNewestMessage();
    }

    bool IsScreenPointOverBannerToggleArea(Vector2 screenPoint)
    {
        RectTransform hitRoot = backgroundRT != null ? backgroundRT : panelRT;
        if (hitRoot == null)
            return false;

        if (!RectTransformUtility.RectangleContainsScreenPoint(hitRoot, screenPoint, null))
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(hitRoot, screenPoint, null, out Vector2 localPoint))
            return false;

        Rect rect = hitRoot.rect;
        float leftEdge = -hitRoot.pivot.x * rect.width;
        float hitWidth = GetToggleHitAreaWidth();
        return localPoint.x >= leftEdge && localPoint.x <= leftEdge + hitWidth;
    }

    float GetToggleHitAreaWidth()
    {
        return Mathf.Max(1f, GetEffectiveIconSize() + Mathf.Max(0f, iconHitAreaInset));
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
        return Mathf.Max(GetEntryPanelHeight(Mathf.Max(1, visibleLineCount)), GetMinimumPanelHeight());
    }

    float GetCollapsedPanelHeight(float expandedHeight)
    {
        return Mathf.Min(expandedHeight, GetEntryPanelHeight(Mathf.Max(1, collapsedVisibleEntryCount)));
    }

    float GetMinimumCollapsedPanelHeight()
    {
        return GetEntryPanelHeight(Mathf.Max(1, collapsedVisibleEntryCount));
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
        return GetEntryPanelHeight(Mathf.Max(1, visibleLineCount));
    }

    float GetEntryPanelHeight(int entryCount)
    {
        return 8f + entryCount * GetRowHeight() + Mathf.Max(0, entryCount - 1) * rowSpacing;
    }

    float GetRowHeight()
    {
        int lineCount = Mathf.Max(1, maxMessageLines);
        float estimatedTextHeight = fontSize * lineCount * 1.2f;
        float estimatedPadding = 12f;
        float estimatedIconHeight = GetEffectiveIconSize() + 4f;
        return Mathf.Max(rowMinHeight, estimatedTextHeight + estimatedPadding, estimatedIconHeight);
    }

    float GetEffectiveIconSize()
    {
        int lineCount = Mathf.Max(1, maxMessageLines);
        return Mathf.Min(iconSize, fontSize * lineCount * 1.2f);
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

        backgroundColor = new Color(0f, 0f, 0f, 0.75f);
        textColor = Color.white;
        fontSize = 34;
        height = GetMinimumPanelHeight();
        sidePadding = 0f;
    }

    Scrollbar CreateScrollbar(Transform parent)
    {
        return null;
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
        if (SpriteServer.TryGetEmojiSprite(emote, out Sprite sprite, out displayName))
            return sprite;

        return SpriteServer.TryGetHumanEmojiSprite(emote, out sprite, out displayName) ? sprite : null;
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
        if (!ShouldShowAgentMessage(agent))
            return;

        string actorName = agent != null ? agent.DisplayName : "Unknown agent";
        Sprite sprite = GetEmoteSprite(emote, out string emoteName);
        EmoteIconVisualFactory.ShowOverhead(agent, sprite);

        AddMessageInternal(
            BannerSense.None,
            BannerLevel.None,
            $"{actorName} did the {emoteName} emote.",
            includeGameTime,
            false,
            sprite);
    }

    void AddHumanEmoteInternal(WorldObject agent, int spriteIndex, bool includeGameTime)
    {
        AddEmoteInternal(agent, $"Human_{spriteIndex}", includeGameTime);
    }

    void AddAgentMessageInternal(WorldObject agent, BannerSense sense, BannerLevel level, string message, bool includeGameTime, bool isRichText, Sprite iconOverride = null)
    {
        if (!ShouldShowAgentMessage(agent))
            return;

        AddMessageInternal(sense, level, message, includeGameTime, isRichText, iconOverride);
    }

    void AddAgentMessageInternal(string agentIdentifier, BannerSense sense, BannerLevel level, string message, bool includeGameTime, bool isRichText, Sprite iconOverride = null)
    {
        if (!ShouldShowAgentMessage(agentIdentifier))
            return;

        AddMessageInternal(sense, level, message, includeGameTime, isRichText, iconOverride);
    }

    bool ShouldShowAgentMessage(WorldObject agent)
    {
        if (showNonPackAgentMessages)
            return true;

        return IsPlayerPackAgent(agent);
    }

    bool ShouldShowAgentMessage(string agentIdentifier)
    {
        if (showNonPackAgentMessages)
            return true;

        return TryResolveAgentIdentifier(agentIdentifier, out WorldObject agent) && IsPlayerPackAgent(agent);
    }

    static bool IsPlayerPackAgent(WorldObject agent)
    {
        if (agent == null)
            return false;

        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        return playerPack != null &&
               playerPack.packAgentList != null &&
               playerPack.packAgentList.Contains(agent);
    }

    static bool TryResolveAgentIdentifier(string agentIdentifier, out WorldObject agent)
    {
        agent = null;
        if (string.IsNullOrWhiteSpace(agentIdentifier))
            return false;

        string trimmed = agentIdentifier.Trim();

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (registry != null && int.TryParse(trimmed, out int objectId) && registry.TryGet(objectId, out agent))
            return agent != null;

        WorldObject[] candidates = FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            WorldObject candidate = candidates[i];
            if (candidate == null)
                continue;

            if (string.Equals(candidate.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.name, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                agent = candidate;
                return true;
            }
        }

        return false;
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
        ConfigureBannerIconSprite(sprite);
        iconImage.sprite = sprite;
        iconImage.preserveAspect = true;
        iconImage.useSpriteMesh = false;

        LayoutElement iconLayout = iconGO.GetComponent<LayoutElement>();
        float effectiveIconSize = GetEffectiveIconSize();
        iconLayout.minWidth = effectiveIconSize;
        iconLayout.preferredWidth = effectiveIconSize;
        iconLayout.minHeight = effectiveIconSize;
        iconLayout.preferredHeight = effectiveIconSize;

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

    void ConfigureBannerIconSprite(Sprite sprite)
    {
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
        ApplyBuildCompleteVisibility();
        if (contentRT == null)
            return;

        bool scrollToNewest = ShouldScrollToNewestForNewMessage();

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

        if (scrollToNewest)
            ScrollToNewestMessage();
    }

    bool ShouldScrollToNewestForNewMessage()
    {
        if (!autoScrollToNewest || scrollRect == null)
            return false;

        if (IsCollapsedToSingleLine())
            return true;

        return IsScrolledToBottom();
    }

    void ScrollToNewestMessage()
    {
        if (scrollRect == null || contentRT == null)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        Canvas.ForceUpdateCanvases();

        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = 0f;
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
    }

    SaveData CaptureSaveDataInternal()
    {
        SaveData data = new SaveData
        {
            elapsedGameTimeSeconds = elapsedGameTimeSeconds,
            canvasVisible = requestedCanvasVisible,
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
        requestedCanvasVisible = data.canvasVisible;
        BuildUIIfNeeded();
        ApplyBuildCompleteVisibility();

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

        ScrollToNewestMessage();
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
        }

        return Instance;
    }

    public static void Show(string message)
    {
        Show(BannerSense.None, BannerLevel.None, message, false);
    }

    public static void SetVisible(bool visible)
    {
        GetOrCreateInstance()?._SetVisible(visible);
    }

    public static void Collapse()
    {
        if (Instance == null)
            return;

        Instance.panelExpanded = false;
        Instance.ScrollToNewestMessage();
    }

    public static void Show(BannerSense sense, BannerLevel level, string message, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddMessageInternal(sense, level, message, includeGameTime, false);
    }

    public static void LogMessage(BannerSense sense, BannerLevel level, string message, bool includeGameTime = false)
    {
        Show(sense, level, message, includeGameTime);
    }

    public static void LogAgentMessage(WorldObject agent, BannerSense sense, BannerLevel level, string message, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddAgentMessageInternal(agent, sense, level, message, includeGameTime, false);
    }

    public static void LogAgentMessage(string agentIdentifier, BannerSense sense, BannerLevel level, string message, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddAgentMessageInternal(agentIdentifier, sense, level, message, includeGameTime, false);
    }

    public static void LogMessageWithIcon(BannerSense sense, BannerLevel level, string message, string iconSpriteName, bool includeGameTime = false)
    {
        Sprite icon = SpriteServer.SpriteLookup(iconSpriteName);
        GetOrCreateInstance()?.AddMessageInternal(sense, level, message, includeGameTime, false, icon);
    }

    public static void LogAgentMessageWithIcon(WorldObject agent, BannerSense sense, BannerLevel level, string message, string iconSpriteName, bool includeGameTime = false)
    {
        Sprite icon = SpriteServer.SpriteLookup(iconSpriteName);
        GetOrCreateInstance()?.AddAgentMessageInternal(agent, sense, level, message, includeGameTime, false, icon);
    }

    public static void LogAgentMessageWithIcon(string agentIdentifier, BannerSense sense, BannerLevel level, string message, string iconSpriteName, bool includeGameTime = false)
    {
        Sprite icon = SpriteServer.SpriteLookup(iconSpriteName);
        GetOrCreateInstance()?.AddAgentMessageInternal(agentIdentifier, sense, level, message, includeGameTime, false, icon);
    }

    public static void LogRichMessage(BannerSense sense, BannerLevel level, string richMessage, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddMessageInternal(sense, level, richMessage, includeGameTime, true);
    }

    public static void LogAgentRichMessage(WorldObject agent, BannerSense sense, BannerLevel level, string richMessage, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddAgentMessageInternal(agent, sense, level, richMessage, includeGameTime, true);
    }

    public static void LogAgentRichMessage(string agentIdentifier, BannerSense sense, BannerLevel level, string richMessage, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddAgentMessageInternal(agentIdentifier, sense, level, richMessage, includeGameTime, true);
    }

    public static void LogEmote(WorldObject agent, string emote, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddEmoteInternal(agent, emote, includeGameTime);
    }

    public static void LogHumanEmote(WorldObject agent, int spriteIndex, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddHumanEmoteInternal(agent, spriteIndex, includeGameTime);
    }

    public static void LogInventoryMessage(string message, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddInventoryInternal(message, includeGameTime);
    }

    public static void LogAgentInventoryMessage(WorldObject agent, string message, bool includeGameTime = false)
    {
        BottomBanner instance = GetOrCreateInstance();
        instance?.AddAgentMessageInternal(
            agent,
            BannerSense.None,
            BannerLevel.None,
            message,
            includeGameTime,
            false,
            instance.GetInventoryMessageSprite());
    }

    public static void LogBuildProgress(string message, bool includeGameTime = false)
    {
        GetOrCreateInstance()?.AddBuildProgressInternal(message, includeGameTime);
    }

    void _SetVisible(bool visible)
    {
        requestedCanvasVisible = visible;
        BuildUIIfNeeded();
        ApplyBuildCompleteVisibility();
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

    static void SetVector2IfChanged(Vector2 current, Vector2 next, Action<Vector2> setter)
    {
        if ((current - next).sqrMagnitude <= 0.0001f)
            return;

        setter(next);
    }

    static void SetVector3IfChanged(Vector3 current, Vector3 next, Action<Vector3> setter)
    {
        if ((current - next).sqrMagnitude <= 0.0001f)
            return;

        setter(next);
    }
}
