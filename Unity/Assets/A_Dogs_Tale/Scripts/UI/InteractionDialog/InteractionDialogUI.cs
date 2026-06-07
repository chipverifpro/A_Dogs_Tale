using System.Collections.Generic;
using DogGame;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class InteractionDialogUI : MonoBehaviour
{
    private static InteractionDialogUI activeInstance;
    private const int PreviewRenderLayer = 31;
    private const string EmoteIconVisualInstanceName = "EmoteIconVisual";
    private const string QuestIconVisualInstanceName = "QuestRequestIconVisual";
    private static readonly Vector3 PlayerPreviewAnchorPosition = new(62000f, 60000f, 60000f);
    private static readonly Vector3 PlayerItemPreviewAnchorPosition = new(63000f, 60000f, 60000f);
    private static readonly Vector3 TargetPreviewAnchorPosition = new(64000f, 60000f, 60000f);
    private static readonly Vector3 TargetItemPreviewAnchorPosition = new(65000f, 60000f, 60000f);

    [Header("Resources")]
    [SerializeField] private string interactionFrameSpriteResourcePath = "Sprites/Frames/Interaction_5_Frame_B";
    [SerializeField] private string circleSpriteResourcePath = "Sprites/Frames/Circle_540_540";
    [SerializeField] private string circleWithArrowsSpriteResourcePath = "Sprites/Frames/CircleWithArrows_921_540";
    [SerializeField] private string tradeArrowsSpriteResourcePath = "Sprites/Frames/TradeArrows_A";
    [SerializeField] private string titleFontResourcePath = "TMP_Fonts/LuckiestGuy-Regular SDF";

    [Header("Layout")]
    [SerializeField] private int uiSortOrder = 5310;
    [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);
    [SerializeField] private Vector2 dialogSize = new(1536f, 1024f);
    [SerializeField, Range(0f, 75f)] private float dialogScaleReductionPercent = 25f;
    [SerializeField] private Vector2 closeButtonAnchoredPosition = new(-250f, -120f);
    [SerializeField] private Vector2 closeButtonSize = new(120f, 120f);
    [SerializeField] private float actionButtonHeight = 112f;
    [SerializeField] private float previewSpinDegreesPerSecond = 24f;
    [SerializeField, Range(0f, 85f)] private float previewViewAngleDegrees = 30f;
    [SerializeField, Min(0f)] private float tradePartnerSearchRadiusTiles = 2f;
    [SerializeField, Min(0f)] private float socialNearbyRadiusMultiplier = 2f;
    [SerializeField, Min(0f)] private float throwForwardImpulse = 7f;
    [SerializeField, Min(0f)] private float throwUpwardImpulse = 2f;
    [SerializeField, Min(0f)] private float throwReleaseHeight = 0.5f;
    [SerializeField] private Vector2 tooltipScreenOffset = new(18f, -18f);
    [SerializeField] private Vector2 tooltipPadding = new(12f, 8f);
    [SerializeField, Min(80f)] private float tooltipMaxWidth = 300f;
    [SerializeField, Min(8f)] private float tooltipFontSize = 20f;

    private Sprite interactionFrameSprite;
    private Canvas overlayCanvas;
    private CanvasScaler overlayCanvasScaler;
    private RectTransform dialogRect;
    private RectTransform tooltipRect;
    private GameObject dialogRoot;
    private TextMeshProUGUI tooltipLabel;
    private InteractionDialogTooltipTrigger activeTooltipTrigger;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI playerNameLabel;
    private TextMeshProUGUI playerHeldItemLabel;
    private TextMeshProUGUI targetNameLabel;
    private TextMeshProUGUI targetHeldItemLabel;
    private TextMeshProUGUI playerSelectionTypeLabel;
    private TextMeshProUGUI targetSelectionTypeLabel;
    private TextMeshProUGUI playerItemSelectionTypeLabel;
    private TextMeshProUGUI targetItemSelectionTypeLabel;
    private Image socialTabHighlight;
    private Image questsTabHighlight;
    private Image packTabHighlight;
    private Image itemsTabHighlight;
    private Image scentTabHighlight;
    private Button previousPlayerAgentButton;
    private Button nextPlayerAgentButton;
    private Button previousPlayerItemButton;
    private Button nextPlayerItemButton;
    private Button previousTargetAgentButton;
    private Button nextTargetAgentButton;
    private Button previousTargetItemButton;
    private Button nextTargetItemButton;
    private Button playerPackIndicatorButton;
    private Button targetPackIndicatorButton;
    private Button socialTabButton;
    private Button questsTabButton;
    private Button packTabButton;
    private Button itemsTabButton;
    private Button scentTabButton;
    private GameObject tradeArrowsObject;
    private Button giveHotspotButton;
    private Button exchangeHotspotButton;
    private Button takeHotspotButton;
    private Button setLeaderButton;
    private Button joinPackButton;
    private Button leavePackButton;
    private GameObject actionPanelObject;
    private GameObject packActionPanelObject;
    private GameObject packMemberListObject;
    private GameObject questListObject;
    private GameObject scentSourceListObject;
    private GameObject socialEmoteGridObject;
    private GameObject packHeldItemListObject;
    private RectTransform packMemberListViewportRect;
    private RectTransform packMemberListContentRect;
    private RectTransform questListContentRect;
    private RectTransform scentSourceListRect;
    private RectTransform scentSourceListContentRect;
    private RectTransform socialEmoteGridContentRect;
    private RectTransform packHeldItemListContentRect;
    private ScrollRect packMemberScrollRect;
    private ScrollRect questListScrollRect;
    private ScrollRect scentSourceListScrollRect;
    private ScrollRect socialEmoteGridScrollRect;
    private ScrollRect packHeldItemListScrollRect;
    private TextMeshProUGUI questListEmptyLabel;
    private TextMeshProUGUI scentSourceListEmptyLabel;
    private TextMeshProUGUI packHeldItemListEmptyLabel;
    private float packMemberListDragStartLocalY;
    private float packMemberListDragStartContentY;
    private float scentSourceListDragStartLocalY;
    private float scentSourceListDragStartContentY;
    private bool scentSourceListPointerDown;
    private bool scentSourceListPointerDragged;
    private Vector2 scentSourceListPointerDownPosition;
    private int displayedScentSourceListSelectedIndex = -1;
    private int displayedScentSourceListOptionCount = -1;
    private WorldObject displayedScentSourceListFirst;
    private WorldObject displayedScentSourceListLast;
    private PreviewSlot playerPreviewSlot;
    private PreviewSlot playerItemPreviewSlot;
    private PreviewSlot targetPreviewSlot;
    private PreviewSlot targetItemPreviewSlot;
    private readonly List<WorldObject> playerAgentOptions = new();
    private readonly List<WorldObject> playerItemOptions = new();
    private readonly List<WorldObject> targetAgentOptions = new();
    private readonly List<WorldObject> targetItemOptions = new();
    private readonly List<WorldObject> packMemberOptions = new();
    private readonly List<WorldObject> packRightOptions = new();
    private readonly List<Image> packMemberListBackgrounds = new();
    private readonly List<Image> scentSourceListBackgrounds = new();
    private readonly List<GameObject> socialEmoteGridTiles = new();
    private readonly List<PackHeldItemOption> packHeldItemOptions = new();
    private readonly List<Image> packHeldItemListBackgrounds = new();
    private readonly List<WorldObject> socialTargetOptions = new();
    private readonly List<WorldObject> questTargetOptions = new();
    private readonly List<WorldObject> scentTargetOptions = new();
    private readonly List<QuestModuleBase> interactionQuestModules = new();
    private readonly Dictionary<QuestModuleBase, TextMeshProUGUI> interactionQuestStatusLabels = new();
    private readonly HashSet<QuestModuleBase> expandedInteractionQuestModules = new();
    private const float PackMemberListPadding = 4f;
    private const float PackMemberListRowHeight = 42f;
    private const float PackMemberListRowSpacing = 5f;
    private const int HumanEmojiCount = 32;
    private const int SocialEmoteGridColumns = 5;
    private const float SocialEmoteTileSize = 72f;
    private int selectedPlayerAgentIndex;
    private int selectedPlayerItemIndex;
    private int selectedTargetAgentIndex;
    private int selectedTargetItemIndex;
    private int selectedPackLeftIndex;
    private int selectedPackRightIndex = 1;
    private int selectedSocialTargetIndex;
    private int selectedQuestTargetIndex;
    private int selectedScentTargetIndex;
    private WorldObject displayedPlayer;
    private WorldObject displayedPlayerItem;
    private WorldObject displayedTarget;
    private WorldObject displayedTargetItem;
    private WorldObject displayedPackLeft;
    private WorldObject displayedPackRight;
    private WorldObject displayedSocialLeft;
    private WorldObject displayedSocialRight;
    private WorldObject displayedQuestLeft;
    private WorldObject displayedQuestRight;
    private WorldObject displayedScentLeft;
    private WorldObject displayedScentRight;
    private WorldObject pendingLeftAgentSelection;
    private WorldObject pendingRightAgentSelection;
    private InteractionTab currentTab = InteractionTab.Items;
    private bool isOpen;
    private bool pausedGameForDialog;
    private bool interactionQuestListDirty = true;
    private bool displayedSocialEmoteGridUsesHuman;
    private bool displayedSocialEmoteGridInitialized;
    private bool packHeldItemListDirty = true;
    private WorldObject displayedPackHeldItemSelectedAgent;
    private WorldObject displayedPackHeldItemSelectedItem;
    private Sprite circleSprite;
    private Sprite circleWithArrowsSprite;
    private Sprite tradeArrowsSprite;
    private TMP_FontAsset titleFont;

    private sealed class PreviewSlot
    {
        public Image CircleImage;
        public RawImage Image;
        public RenderTexture Texture;
        public GameObject WorldRoot;
        public GameObject Clone;
        public Camera Camera;
        public Light Light;
        public Vector3 AnchorPosition;
        public float FramingRadius = 1f;
        public float OrthographicPadding = 2.15f;
        public Vector2 CircleSize;
        public Vector2 CircleWithArrowsSize;
        public WorldObject DisplayedObject;
    }

    private sealed class PackHeldItemOption
    {
        public PackHeldItemOption(WorldObject agent, WorldObject item)
        {
            Agent = agent;
            Item = item;
        }

        public WorldObject Agent { get; }
        public WorldObject Item { get; }
    }

    private enum InventoryAction
    {
        Use = 0,
        Eat = 1,
        Drop = 4,
        PickUp = 5
    }

    private enum InteractionTab
    {
        Social,
        Quests,
        Items,
        Pack,
        Scent
    }

    private enum PackButtonKind
    {
        Behavior,
        Membership,
        Formation
    }

    private void Awake()
    {
        activeInstance = this;
        BuildUI();
        Hide();
    }

    private void OnValidate()
    {
        ApplyDialogScaleAndPosition();
    }

    private void Update()
    {
        if (WasInteractionTogglePressedThisFrame())
            Toggle();

        if (!isOpen)
            return;

        ApplyDialogScaleAndPosition();
        RefreshInteractionView();
        HandleScentSourceListPointerInput();
        SpinPreview(playerPreviewSlot);
        SpinPreview(playerItemPreviewSlot);
        SpinPreview(targetPreviewSlot);
        SpinPreview(targetItemPreviewSlot);
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;

        RestorePauseStateForDialog();
        ReleasePreviewSlot(playerPreviewSlot);
        ReleasePreviewSlot(playerItemPreviewSlot);
        ReleasePreviewSlot(targetPreviewSlot);
        ReleasePreviewSlot(targetItemPreviewSlot);
    }

    public void Toggle()
    {
        if (isOpen)
            Hide();
        else
            Show();
    }

    public void Show()
    {
        EnsureEventSystem();
        if (!isOpen)
            ApplyPauseStateForDialog();

        isOpen = true;

        if (dialogRoot != null)
            dialogRoot.SetActive(true);

        interactionQuestListDirty = true;
        packHeldItemListDirty = true;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    public static bool IsPointerBlockingBottomBanner(Vector2 screenPoint)
    {
        InteractionDialogUI instance = activeInstance;
        if (instance == null ||
            !instance.isOpen ||
            instance.dialogRoot == null ||
            !instance.dialogRoot.activeInHierarchy ||
            instance.dialogRect == null)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(instance.dialogRect, screenPoint, null);
    }

    public void Hide()
    {
        bool wasOpen = isOpen;

        isOpen = false;

        if (dialogRoot != null)
            dialogRoot.SetActive(false);

        HideTooltip();

        if (wasOpen)
            RestorePauseStateForDialog();
    }

    private void ApplyPauseStateForDialog()
    {
        if (GamePause.IsPaused)
        {
            pausedGameForDialog = false;
            return;
        }

        GamePause.Pause();
        pausedGameForDialog = true;
    }

    private void RestorePauseStateForDialog()
    {
        if (!pausedGameForDialog)
            return;

        pausedGameForDialog = false;
        GamePause.Resume();
    }

    private bool WasInteractionTogglePressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.jKey.wasPressedThisFrame)
            return false;

        GameObject selectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selectedObject != null &&
            (selectedObject.GetComponent<TMP_InputField>() != null || selectedObject.GetComponent<InputField>() != null))
        {
            return false;
        }

        return true;
    }

    private void BuildUI()
    {
        interactionFrameSprite = Resources.Load<Sprite>(interactionFrameSpriteResourcePath);
        circleSprite = Resources.Load<Sprite>(circleSpriteResourcePath);
        circleWithArrowsSprite = Resources.Load<Sprite>(circleWithArrowsSpriteResourcePath);
        tradeArrowsSprite = Resources.Load<Sprite>(tradeArrowsSpriteResourcePath);
        titleFont = Resources.Load<TMP_FontAsset>(titleFontResourcePath);
        EnsureEventSystem();

        GameObject canvasObject = new("InteractionDialogCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = uiSortOrder;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        overlayCanvasScaler = canvasObject.GetComponent<CanvasScaler>();
        overlayCanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        overlayCanvasScaler.referenceResolution = referenceResolution;
        overlayCanvasScaler.matchWidthOrHeight = 1f;

        dialogRoot = CreateUIObject("InteractionDialog", canvasObject.transform);
        dialogRect = dialogRoot.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 1f);
        dialogRect.sizeDelta = dialogSize;
        ApplyDialogScaleAndPosition();

        Image dialogImage = dialogRoot.AddComponent<Image>();
        if (interactionFrameSprite != null)
        {
            dialogImage.sprite = interactionFrameSprite;
            dialogImage.type = Image.Type.Sliced;
            dialogImage.preserveAspect = true;
            dialogImage.color = Color.white;
        }
        else
        {
            dialogImage.color = new Color(0.08f, 0.075f, 0.055f, 0.94f);
        }
        dialogRoot.AddComponent<InteractionDialogInputBlocker>();

        BuildPreviewSlots(dialogRoot.transform);
        BuildPackIndicatorButtons(dialogRoot.transform);
        BuildHeader(dialogRoot.transform);
        BuildTopInfo(dialogRoot.transform);
        BuildTradeArrows(dialogRoot.transform);
        BuildTabLabels(dialogRoot.transform);
        BuildSelectionArrows(dialogRoot.transform);
        BuildActionButtons(dialogRoot.transform);
        BuildPackHeldItemList(dialogRoot.transform);
        BuildPackMemberList(dialogRoot.transform);
        BuildInteractionQuestList(dialogRoot.transform);
        BuildScentSourceList(dialogRoot.transform);
        BuildSocialEmoteGrid(dialogRoot.transform);
        BuildPackActionButtons(dialogRoot.transform);
        BuildCloseHotspot(dialogRoot.transform);
        BuildTooltip(canvasObject.transform);
    }

    private void ApplyDialogScaleAndPosition()
    {
        if (dialogRect == null)
            return;

        float reduction01 = Mathf.Clamp01(dialogScaleReductionPercent / 100f);
        float requestedScale = Mathf.Max(0.01f, 1f - reduction01);
        float scale = Mathf.Min(requestedScale, GetMaxDialogScaleForScreenWidth());
        dialogRect.localScale = new Vector3(scale, scale, 1f);

        // The frame pivot/origin is top-center. Offset by half the scaled height so
        // the visible dialog remains centered on the screen.
        dialogRect.anchoredPosition = new Vector2(0f, dialogSize.y * scale * 0.5f);
    }

    private float GetMaxDialogScaleForScreenWidth()
    {
        if (dialogSize.x <= 0f || Screen.width <= 0)
            return 1f;

        float canvasScale = GetCanvasScaleFactorForScreen();
        if (canvasScale <= 0f)
            return 1f;

        return Mathf.Max(0.01f, Screen.width / (dialogSize.x * canvasScale));
    }

    private float GetCanvasScaleFactorForScreen()
    {
        Vector2 resolution = overlayCanvasScaler != null
            ? overlayCanvasScaler.referenceResolution
            : referenceResolution;
        if (resolution.x <= 0f || resolution.y <= 0f || Screen.width <= 0 || Screen.height <= 0)
            return 1f;

        float widthScale = Screen.width / resolution.x;
        float heightScale = Screen.height / resolution.y;
        float match = overlayCanvasScaler != null
            ? overlayCanvasScaler.matchWidthOrHeight
            : 1f;

        return Mathf.Pow(widthScale, 1f - match) * Mathf.Pow(heightScale, match);
    }

    private void BuildHeader(Transform parent)
    {
        GameObject titleObject = CreateUIObject("Title", parent);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, -118f);
        titleRect.sizeDelta = new Vector2(720f, 120f);

        titleLabel = titleObject.AddComponent<TextMeshProUGUI>();
        titleLabel.text = "INTERACTION";
        if (titleFont != null)
            titleLabel.font = titleFont;
        titleLabel.fontSize = 72f;
        titleLabel.fontStyle = FontStyles.Bold;
        titleLabel.color = new Color(0.29f, 0.18f, 0.09f, 1f);
        titleLabel.alignment = TextAlignmentOptions.Center;
        titleLabel.raycastTarget = false;

    }

    private void BuildCloseHotspot(Transform parent)
    {
        Button closeButton = CreateInvisibleButton("CloseButton", parent, Hide);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(0.5f, 0.5f);
        closeRect.anchoredPosition = closeButtonAnchoredPosition;
        closeRect.sizeDelta = closeButtonSize;
    }

    private void BuildTooltip(Transform parent)
    {
        GameObject tooltipObject = CreateUIObject("InteractionDialogTooltip", parent);
        tooltipRect = tooltipObject.GetComponent<RectTransform>();
        tooltipRect.anchorMin = new Vector2(0f, 1f);
        tooltipRect.anchorMax = new Vector2(0f, 1f);
        tooltipRect.pivot = new Vector2(0f, 1f);
        tooltipRect.sizeDelta = new Vector2(160f, 44f);

        Image background = tooltipObject.AddComponent<Image>();
        background.color = new Color(0.08f, 0.06f, 0.035f, 0.92f);
        background.raycastTarget = false;

        GameObject labelObject = CreateUIObject("Label", tooltipObject.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = tooltipPadding;
        labelRect.offsetMax = -tooltipPadding;

        tooltipLabel = labelObject.AddComponent<TextMeshProUGUI>();
        tooltipLabel.fontSize = tooltipFontSize;
        tooltipLabel.color = new Color(1f, 0.86f, 0.54f, 1f);
        tooltipLabel.alignment = TextAlignmentOptions.Center;
        tooltipLabel.textWrappingMode = TextWrappingModes.NoWrap;
        tooltipLabel.overflowMode = TextOverflowModes.Overflow;
        tooltipLabel.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            tooltipLabel.font = TMP_Settings.defaultFontAsset;

        tooltipObject.SetActive(false);
        BringTooltipToFront();
    }

    private void ConfigureTooltip(GameObject target, string tooltipText)
    {
        if (target == null || string.IsNullOrWhiteSpace(tooltipText))
            return;

        InteractionDialogTooltipTrigger trigger = target.GetComponent<InteractionDialogTooltipTrigger>();
        if (trigger == null)
            trigger = target.AddComponent<InteractionDialogTooltipTrigger>();

        trigger.Initialize(this, tooltipText);
    }

    internal void ShowTooltip(InteractionDialogTooltipTrigger trigger, Vector2 screenPosition)
    {
        if (trigger == null || tooltipRect == null || tooltipLabel == null)
            return;

        string text = trigger.TooltipText;
        if (string.IsNullOrWhiteSpace(text))
            return;

        activeTooltipTrigger = trigger;
        tooltipRect.gameObject.SetActive(true);
        UpdateTooltipText(text);
        PositionTooltip(screenPosition);
        BringTooltipToFront();
    }

    internal void MoveTooltip(InteractionDialogTooltipTrigger trigger, Vector2 screenPosition)
    {
        if (trigger == null || trigger != activeTooltipTrigger || tooltipRect == null || !tooltipRect.gameObject.activeSelf)
            return;

        PositionTooltip(screenPosition);
    }

    internal void HideTooltip(InteractionDialogTooltipTrigger trigger)
    {
        if (trigger != null && trigger != activeTooltipTrigger)
            return;

        HideTooltip();
    }

    private void HideTooltip()
    {
        activeTooltipTrigger = null;
        if (tooltipRect != null)
            tooltipRect.gameObject.SetActive(false);
    }

    private void UpdateTooltipText(string text)
    {
        tooltipLabel.text = text;
        Vector2 preferred = tooltipLabel.GetPreferredValues(text, tooltipMaxWidth, 0f);
        float width = Mathf.Min(tooltipMaxWidth, preferred.x) + tooltipPadding.x * 2f;
        float height = preferred.y + tooltipPadding.y * 2f;
        tooltipRect.sizeDelta = new Vector2(Mathf.Max(80f, width), Mathf.Max(38f, height));
    }

    private void PositionTooltip(Vector2 screenPosition)
    {
        RectTransform canvasRect = overlayCanvas != null ? overlayCanvas.transform as RectTransform : null;
        if (canvasRect == null)
            return;

        Camera eventCamera = overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : overlayCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out Vector2 localPoint))
            return;

        float canvasScale = overlayCanvas.scaleFactor > 0f ? overlayCanvas.scaleFactor : 1f;
        Vector2 scaledOffset = tooltipScreenOffset / canvasScale;
        Vector2 anchoredPosition = new(
            localPoint.x + canvasRect.rect.width * 0.5f,
            localPoint.y - canvasRect.rect.height * 0.5f);
        anchoredPosition += scaledOffset;

        float minX = 12f;
        float maxX = canvasRect.rect.width - tooltipRect.sizeDelta.x - 12f;
        float minY = -(canvasRect.rect.height - tooltipRect.sizeDelta.y - 12f);
        float maxY = -12f;

        anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, Mathf.Max(minX, maxX));
        anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, Mathf.Min(minY, maxY), Mathf.Max(minY, maxY));
        tooltipRect.anchoredPosition = anchoredPosition;
    }

    private void BringTooltipToFront()
    {
        if (tooltipRect != null)
            tooltipRect.SetAsLastSibling();
    }

    private void BuildTabLabels(Transform parent)
    {
        Vector2 tabSize = new(240f, 70f);
        Vector2 labelSize = new(190f, 74f);
        Vector2 labelOffset = new(28f, 0f);
        socialTabHighlight = CreateTabHighlight(parent, "SocialTabHighlight", new Vector2(-480f, -428f), tabSize);
        CreateTabLabel(parent, "SocialTabLabel", "Social", new Vector2(-480f, -428f) + labelOffset, labelSize);
        packTabHighlight = CreateTabHighlight(parent, "PackTabHighlight", new Vector2(-240f, -428f), tabSize);
        CreateTabLabel(parent, "PackTabLabel", "Pack", new Vector2(-240f, -428f) + labelOffset, labelSize);
        itemsTabHighlight = CreateTabHighlight(parent, "ItemsTabHighlight", new Vector2(0f, -428f), tabSize);
        CreateTabLabel(parent, "ItemsTabLabel", "Items", new Vector2(0f, -428f) + labelOffset, labelSize);
        questsTabHighlight = CreateTabHighlight(parent, "QuestsTabHighlight", new Vector2(240f, -428f), tabSize);
        CreateTabLabel(parent, "QuestsTabLabel", "Quests", new Vector2(240f, -428f) + labelOffset, labelSize);
        scentTabHighlight = CreateTabHighlight(parent, "ScentTabHighlight", new Vector2(480f, -428f), tabSize);
        CreateTabLabel(parent, "ScentTabLabel", "Scent", new Vector2(480f, -428f) + labelOffset, labelSize);
        socialTabButton = CreateTabHotspot(parent, "SocialTabButton", new Vector2(-480f, -428f), tabSize, OnSocialTabClicked);
        packTabButton = CreateTabHotspot(parent, "PackTabButton", new Vector2(-240f, -428f), tabSize, OnPackTabClicked);
        itemsTabButton = CreateTabHotspot(parent, "ItemsTabButton", new Vector2(0f, -428f), tabSize, OnItemsTabClicked);
        questsTabButton = CreateTabHotspot(parent, "QuestsTabButton", new Vector2(240f, -428f), tabSize, OnQuestsTabClicked);
        scentTabButton = CreateTabHotspot(parent, "ScentTabButton", new Vector2(480f, -428f), tabSize, OnScentTabClicked);
        RefreshTabHighlights();
    }

    private static Image CreateTabHighlight(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject highlightObject = CreateUIObject(objectName, parent);
        RectTransform highlightRect = highlightObject.GetComponent<RectTransform>();
        highlightRect.anchorMin = new Vector2(0.5f, 1f);
        highlightRect.anchorMax = new Vector2(0.5f, 1f);
        highlightRect.pivot = new Vector2(0.5f, 0.5f);
        highlightRect.anchoredPosition = anchoredPosition;
        highlightRect.sizeDelta = size;

        Image highlight = highlightObject.AddComponent<Image>();
        highlight.color = new Color(1f, 0.62f, 0.08f, 0.24f);
        highlight.raycastTarget = false;
        return highlight;
    }

    private Button CreateTabHotspot(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        UnityEngine.Events.UnityAction clickHandler)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(clickHandler);
        ConfigureTooltip(buttonObject, GetInteractionButtonTooltipText(objectName));
        return button;
    }

    private void BuildTopInfo(Transform parent)
    {
        playerNameLabel = CreateInfoLabel(parent, "PlayerName", new Vector2(-190f, -232f), new Vector2(360f, 70f), 44f, TextAlignmentOptions.Left);
        playerHeldItemLabel = CreateInfoLabel(parent, "PlayerHeldItem", new Vector2(-110f, -334f), new Vector2(300f, 58f), 38f, TextAlignmentOptions.Left);
        targetNameLabel = CreateInfoLabel(parent, "TargetName", new Vector2(190f, -232f), new Vector2(360f, 70f), 44f, TextAlignmentOptions.Right);
        targetHeldItemLabel = CreateInfoLabel(parent, "TargetHeldItem", new Vector2(110f, -334f), new Vector2(300f, 58f), 38f, TextAlignmentOptions.Right);
        playerSelectionTypeLabel = CreateInfoLabel(parent, "PlayerSelectionType", new Vector2(-497f, -371f), new Vector2(220f, 34f), 22f, TextAlignmentOptions.Center);
        targetSelectionTypeLabel = CreateInfoLabel(parent, "TargetSelectionType", new Vector2(480f, -371f), new Vector2(220f, 34f), 22f, TextAlignmentOptions.Center);
        playerItemSelectionTypeLabel = CreateInfoLabel(parent, "PlayerItemSelectionType", new Vector2(-334f, -371f), new Vector2(160f, 34f), 22f, TextAlignmentOptions.Center);
        targetItemSelectionTypeLabel = CreateInfoLabel(parent, "TargetItemSelectionType", new Vector2(320f, -371f), new Vector2(160f, 34f), 22f, TextAlignmentOptions.Center);
        SetLabelText(playerSelectionTypeLabel, "Pack Member");
        SetLabelText(playerItemSelectionTypeLabel, "Held Item");
        SetLabelText(targetItemSelectionTypeLabel, "Held Item");
    }

    private void BuildTradeArrows(Transform parent)
    {
        tradeArrowsObject = CreateUIObject("TradeArrows", parent);
        RectTransform arrowsRect = tradeArrowsObject.GetComponent<RectTransform>();
        arrowsRect.anchorMin = new Vector2(0.5f, 1f);
        arrowsRect.anchorMax = new Vector2(0.5f, 1f);
        arrowsRect.pivot = new Vector2(0.5f, 0.5f);
        arrowsRect.anchoredPosition = new Vector2(0f, -292f);
        arrowsRect.sizeDelta = new Vector2(86f, 186f);

        Image arrowsImage = tradeArrowsObject.AddComponent<Image>();
        arrowsImage.sprite = tradeArrowsSprite;
        arrowsImage.preserveAspect = true;
        arrowsImage.color = Color.white;
        arrowsImage.raycastTarget = false;

        giveHotspotButton = CreateTradeHotspot(parent, "GiveHotspot", new Vector2(0f, -244f), OnGiveClicked);
        exchangeHotspotButton = CreateTradeHotspot(parent, "ExchangeHotspot", new Vector2(0f, -292f), OnTradeClicked);
        takeHotspotButton = CreateTradeHotspot(parent, "TakeHotspot", new Vector2(0f, -340f), OnTakeItemClicked);
    }

    private Button CreateTradeHotspot(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        UnityEngine.Events.UnityAction clickHandler)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(94f, 44f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(clickHandler);
        ConfigureTooltip(buttonObject, GetInteractionButtonTooltipText(objectName));
        return button;
    }

    private void BuildActionButtons(Transform parent)
    {
        actionPanelObject = CreateUIObject("ActionPanel", parent);
        RectTransform actionPanelRect = actionPanelObject.GetComponent<RectTransform>();
        actionPanelRect.anchorMin = new Vector2(0.5f, 1f);
        actionPanelRect.anchorMax = new Vector2(0.5f, 1f);
        actionPanelRect.pivot = new Vector2(0.5f, 0.5f);
        actionPanelRect.anchoredPosition = new Vector2(270f, -690f);
        actionPanelRect.sizeDelta = new Vector2(690f, 300f);

        VerticalLayoutGroup layout = actionPanelObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 8f;
        layout.padding = new RectOffset(0, 0, 0, 0);

        Transform topRow = CreateActionButtonRow("HeldItemActionRowTop", actionPanelObject.transform, actionButtonHeight);
        Transform bottomRow = CreateActionButtonRow("HeldItemActionRowBottom", actionPanelObject.transform, actionButtonHeight * 0.86f);

        CreateActionButton(topRow, InventoryAction.Use, OnUseClicked);
        CreateActionButton(topRow, InventoryAction.Eat, OnEatClicked);
        CreateActionButton(bottomRow, InventoryAction.Drop, OnDropClicked, 0.86f);
        CreateThrowActionButton(bottomRow, 0.86f);
        CreateActionButton(bottomRow, InventoryAction.PickUp, OnPickUpClicked, 0.86f);
    }

    private void BuildPackActionButtons(Transform parent)
    {
        packActionPanelObject = CreateUIObject("PackActionPanel", parent);
        RectTransform actionPanelRect = packActionPanelObject.GetComponent<RectTransform>();
        actionPanelRect.anchorMin = new Vector2(0.5f, 1f);
        actionPanelRect.anchorMax = new Vector2(0.5f, 1f);
        actionPanelRect.pivot = new Vector2(0.5f, 0.5f);
        actionPanelRect.anchoredPosition = new Vector2(270f, -690f);
        actionPanelRect.sizeDelta = new Vector2(690f, 300f);

        VerticalLayoutGroup layout = packActionPanelObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 8f;
        layout.padding = new RectOffset(0, 0, 0, 0);

        Transform behaviorRow = CreateActionButtonRow("PackBehaviorActionRow", packActionPanelObject.transform, 86f);
        Transform membershipRow = CreateActionButtonRow("PackMembershipActionRow", packActionPanelObject.transform, 86f);
        Transform formationRow = CreateActionButtonRow("PackFormationActionRow", packActionPanelObject.transform, 86f);

        CreatePackActionButton(behaviorRow, "TakeControlButton", PackButtonKind.Behavior, 0, "TAKE CONTROL", () => OnPackBehaviorClicked(AgentDecisionType.Player));
        CreatePackActionButton(behaviorRow, "RegroupButton", PackButtonKind.Behavior, 1, "REGROUP", () => OnPackBehaviorClicked(AgentDecisionType.Follower));
        CreatePackActionButton(behaviorRow, "WaitHereButton", PackButtonKind.Behavior, 3, "WAIT HERE", () => OnPackBehaviorClicked(AgentDecisionType.Immobile));
        CreatePackActionButton(behaviorRow, "PatrolRoomButton", PackButtonKind.Behavior, 4, "PATROL ROOM", () => OnPackBehaviorClicked(AgentDecisionType.Wanderer));
        CreatePackActionButton(behaviorRow, "ExploreButton", PackButtonKind.Behavior, 2, "EXPLORE", () => OnPackBehaviorClicked(AgentDecisionType.Explorer));
        CreatePackActionButton(behaviorRow, "AIButton", PackButtonKind.Behavior, 5, "AI", () => OnPackBehaviorClicked(AgentDecisionType.TaskFollower));

        setLeaderButton = CreatePackActionButton(membershipRow, "SetLeaderButton", PackButtonKind.Membership, 4, "SET LEADER", OnSetPackLeaderClicked);
        joinPackButton = CreatePackActionButton(membershipRow, "JoinPackButton", PackButtonKind.Membership, 0, "JOIN", OnJoinPackClicked, false);
        leavePackButton = CreatePackActionButton(membershipRow, "LeavePackButton", PackButtonKind.Membership, 2, "LEAVE", OnLeavePackClicked);

        CreatePackActionButton(formationRow, "AbreastFormationButton", PackButtonKind.Formation, 6, "ABREAST", () => OnPackFormationClicked(FormationsEnum.LineAbreast));
        CreatePackActionButton(formationRow, "TwoColumnsFormationButton", PackButtonKind.Formation, 10, "TWO COLUMNS", () => OnPackFormationClicked(FormationsEnum.TwoColums));
        CreatePackActionButton(formationRow, "WedgeFormationButton", PackButtonKind.Formation, 12, "WEDGE", () => OnPackFormationClicked(FormationsEnum.Wedge));
        CreatePackActionButton(formationRow, "CircleFormationButton", PackButtonKind.Formation, 14, "CIRCLE", () => OnPackFormationClicked(FormationsEnum.Circle));
        CreatePackActionButton(formationRow, "FollowFormationButton", PackButtonKind.Formation, 16, "FOLLOW", () => OnPackFormationClicked(FormationsEnum.SingleFile));
        CreatePackActionButton(formationRow, "ClusterFormationButton", PackButtonKind.Formation, -1, "CLUSTER", null, false);

        packActionPanelObject.SetActive(false);
    }

    private void BuildPackHeldItemList(Transform parent)
    {
        packHeldItemListObject = CreateUIObject("PackHeldItemList", parent);
        RectTransform listRect = packHeldItemListObject.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.5f, 1f);
        listRect.anchorMax = new Vector2(0.5f, 1f);
        listRect.pivot = new Vector2(0.5f, 0.5f);
        listRect.anchoredPosition = new Vector2(-425f, -690f);
        listRect.sizeDelta = new Vector2(470f, 300f);

        Image listBackground = packHeldItemListObject.AddComponent<Image>();
        listBackground.color = new Color(0.09f, 0.065f, 0.035f, 0.58f);
        listBackground.raycastTarget = true;

        packHeldItemListScrollRect = packHeldItemListObject.AddComponent<ScrollRect>();
        packHeldItemListScrollRect.horizontal = false;
        packHeldItemListScrollRect.vertical = true;
        packHeldItemListScrollRect.movementType = ScrollRect.MovementType.Clamped;
        packHeldItemListScrollRect.scrollSensitivity = 24f;

        GameObject viewportObject = CreateUIObject("Viewport", packHeldItemListObject.transform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(10f, 10f);
        viewportRect.offsetMax = new Vector2(-10f, -10f);

        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(0.055f, 0.047f, 0.036f, 0.45f);
        viewportImage.raycastTarget = true;
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUIObject("Content", viewportObject.transform);
        packHeldItemListContentRect = contentObject.GetComponent<RectTransform>();
        packHeldItemListContentRect.anchorMin = new Vector2(0f, 1f);
        packHeldItemListContentRect.anchorMax = new Vector2(1f, 1f);
        packHeldItemListContentRect.pivot = new Vector2(0.5f, 1f);
        packHeldItemListContentRect.anchoredPosition = Vector2.zero;
        packHeldItemListContentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 5f;
        layout.padding = new RectOffset(4, 4, 4, 4);

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        packHeldItemListScrollRect.viewport = viewportRect;
        packHeldItemListScrollRect.content = packHeldItemListContentRect;

        GameObject emptyObject = CreateUIObject("EmptyLabel", packHeldItemListObject.transform);
        RectTransform emptyRect = emptyObject.GetComponent<RectTransform>();
        emptyRect.anchorMin = Vector2.zero;
        emptyRect.anchorMax = Vector2.one;
        emptyRect.offsetMin = new Vector2(24f, 24f);
        emptyRect.offsetMax = new Vector2(-24f, -24f);

        packHeldItemListEmptyLabel = emptyObject.AddComponent<TextMeshProUGUI>();
        packHeldItemListEmptyLabel.text = "No pack held items";
        packHeldItemListEmptyLabel.fontSize = 24f;
        packHeldItemListEmptyLabel.color = new Color(0.86f, 0.8f, 0.66f, 0.78f);
        packHeldItemListEmptyLabel.alignment = TextAlignmentOptions.Center;
        packHeldItemListEmptyLabel.raycastTarget = false;

        packHeldItemListObject.SetActive(false);
    }

    private void BuildPackMemberList(Transform parent)
    {
        packMemberListObject = CreateUIObject("PackMemberList", parent);
        RectTransform listRect = packMemberListObject.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.5f, 1f);
        listRect.anchorMax = new Vector2(0.5f, 1f);
        listRect.pivot = new Vector2(0.5f, 0.5f);
        listRect.anchoredPosition = new Vector2(-425f, -690f);
        listRect.sizeDelta = new Vector2(470f, 300f);

        Image listBackground = packMemberListObject.AddComponent<Image>();
        listBackground.color = new Color(0.09f, 0.065f, 0.035f, 0.58f);
        listBackground.raycastTarget = true;

        GameObject viewportObject = CreateUIObject("Viewport", packMemberListObject.transform);
        packMemberListViewportRect = viewportObject.GetComponent<RectTransform>();
        packMemberListViewportRect.anchorMin = Vector2.zero;
        packMemberListViewportRect.anchorMax = Vector2.one;
        packMemberListViewportRect.offsetMin = new Vector2(10f, 10f);
        packMemberListViewportRect.offsetMax = new Vector2(-10f, -10f);

        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUIObject("Content", viewportObject.transform);
        packMemberListContentRect = contentObject.GetComponent<RectTransform>();
        packMemberListContentRect.anchorMin = new Vector2(0f, 1f);
        packMemberListContentRect.anchorMax = new Vector2(1f, 1f);
        packMemberListContentRect.pivot = new Vector2(0.5f, 1f);
        packMemberListContentRect.anchoredPosition = Vector2.zero;
        packMemberListContentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = PackMemberListRowSpacing;
        int padding = Mathf.RoundToInt(PackMemberListPadding);
        layout.padding = new RectOffset(padding, padding, padding, padding);

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        packMemberScrollRect = packMemberListObject.AddComponent<ScrollRect>();
        packMemberScrollRect.content = packMemberListContentRect;
        packMemberScrollRect.viewport = packMemberListViewportRect;
        packMemberScrollRect.horizontal = false;
        packMemberScrollRect.vertical = true;
        packMemberScrollRect.movementType = ScrollRect.MovementType.Clamped;
        packMemberScrollRect.scrollSensitivity = 24f;

        GameObject hitAreaObject = CreateUIObject("HitArea", packMemberListObject.transform);
        RectTransform hitAreaRect = hitAreaObject.GetComponent<RectTransform>();
        hitAreaRect.anchorMin = Vector2.zero;
        hitAreaRect.anchorMax = Vector2.one;
        hitAreaRect.offsetMin = Vector2.zero;
        hitAreaRect.offsetMax = Vector2.zero;

        Image hitAreaImage = hitAreaObject.AddComponent<Image>();
        hitAreaImage.color = new Color(1f, 1f, 1f, 0.001f);
        hitAreaImage.raycastTarget = true;

        InteractionDialogPackMemberListHitArea hitArea = hitAreaObject.AddComponent<InteractionDialogPackMemberListHitArea>();
        hitArea.Initialize(this);

        packMemberListObject.SetActive(false);
    }

    private void BuildInteractionQuestList(Transform parent)
    {
        questListObject = CreateUIObject("InteractionQuestList", parent);
        RectTransform listRect = questListObject.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.5f, 1f);
        listRect.anchorMax = new Vector2(0.5f, 1f);
        listRect.pivot = new Vector2(0.5f, 0.5f);
        listRect.anchoredPosition = new Vector2(-425f, -690f);
        listRect.sizeDelta = new Vector2(470f, 300f);

        Image listBackground = questListObject.AddComponent<Image>();
        listBackground.color = new Color(0.09f, 0.065f, 0.035f, 0.58f);
        listBackground.raycastTarget = true;

        questListScrollRect = questListObject.AddComponent<ScrollRect>();
        questListScrollRect.horizontal = false;
        questListScrollRect.vertical = true;
        questListScrollRect.movementType = ScrollRect.MovementType.Clamped;
        questListScrollRect.scrollSensitivity = 24f;

        GameObject viewportObject = CreateUIObject("Viewport", questListObject.transform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(10f, 10f);
        viewportRect.offsetMax = new Vector2(-10f, -10f);

        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(0.055f, 0.047f, 0.036f, 0.45f);
        viewportImage.raycastTarget = true;
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUIObject("Content", viewportObject.transform);
        questListContentRect = contentObject.GetComponent<RectTransform>();
        questListContentRect.anchorMin = new Vector2(0f, 1f);
        questListContentRect.anchorMax = new Vector2(1f, 1f);
        questListContentRect.pivot = new Vector2(0.5f, 1f);
        questListContentRect.anchoredPosition = Vector2.zero;
        questListContentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 8f;
        layout.padding = new RectOffset(4, 4, 4, 4);

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        questListScrollRect.viewport = viewportRect;
        questListScrollRect.content = questListContentRect;

        GameObject emptyObject = CreateUIObject("EmptyLabel", questListObject.transform);
        RectTransform emptyRect = emptyObject.GetComponent<RectTransform>();
        emptyRect.anchorMin = Vector2.zero;
        emptyRect.anchorMax = Vector2.one;
        emptyRect.offsetMin = new Vector2(24f, 24f);
        emptyRect.offsetMax = new Vector2(-24f, -24f);

        questListEmptyLabel = emptyObject.AddComponent<TextMeshProUGUI>();
        questListEmptyLabel.text = "No quests";
        questListEmptyLabel.fontSize = 24f;
        questListEmptyLabel.color = new Color(0.86f, 0.8f, 0.66f, 0.78f);
        questListEmptyLabel.alignment = TextAlignmentOptions.Center;
        questListEmptyLabel.raycastTarget = false;

        questListObject.SetActive(false);
    }

    private void BuildScentSourceList(Transform parent)
    {
        scentSourceListObject = CreateUIObject("ScentSourceList", parent);
        scentSourceListRect = scentSourceListObject.GetComponent<RectTransform>();
        scentSourceListRect.anchorMin = new Vector2(0.5f, 1f);
        scentSourceListRect.anchorMax = new Vector2(0.5f, 1f);
        scentSourceListRect.pivot = new Vector2(0.5f, 0.5f);
        scentSourceListRect.anchoredPosition = new Vector2(425f, -690f);
        scentSourceListRect.sizeDelta = new Vector2(470f, 300f);

        Image listBackground = scentSourceListObject.AddComponent<Image>();
        listBackground.color = new Color(0.09f, 0.065f, 0.035f, 0.58f);
        listBackground.raycastTarget = true;

        scentSourceListScrollRect = scentSourceListObject.AddComponent<ScrollRect>();
        scentSourceListScrollRect.horizontal = false;
        scentSourceListScrollRect.vertical = true;
        scentSourceListScrollRect.movementType = ScrollRect.MovementType.Clamped;
        scentSourceListScrollRect.scrollSensitivity = 24f;

        GameObject viewportObject = CreateUIObject("Viewport", scentSourceListObject.transform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(10f, 10f);
        viewportRect.offsetMax = new Vector2(-10f, -10f);

        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(0.055f, 0.047f, 0.036f, 0.45f);
        viewportImage.raycastTarget = true;
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUIObject("Content", viewportObject.transform);
        scentSourceListContentRect = contentObject.GetComponent<RectTransform>();
        scentSourceListContentRect.anchorMin = new Vector2(0f, 1f);
        scentSourceListContentRect.anchorMax = new Vector2(1f, 1f);
        scentSourceListContentRect.pivot = new Vector2(0.5f, 1f);
        scentSourceListContentRect.anchoredPosition = Vector2.zero;
        scentSourceListContentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 5f;
        layout.padding = new RectOffset(4, 4, 4, 4);

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scentSourceListScrollRect.viewport = viewportRect;
        scentSourceListScrollRect.content = scentSourceListContentRect;

        GameObject emptyObject = CreateUIObject("EmptyLabel", scentSourceListObject.transform);
        RectTransform emptyRect = emptyObject.GetComponent<RectTransform>();
        emptyRect.anchorMin = Vector2.zero;
        emptyRect.anchorMax = Vector2.one;
        emptyRect.offsetMin = new Vector2(24f, 24f);
        emptyRect.offsetMax = new Vector2(-24f, -24f);

        scentSourceListEmptyLabel = emptyObject.AddComponent<TextMeshProUGUI>();
        scentSourceListEmptyLabel.text = "No scent sources";
        scentSourceListEmptyLabel.fontSize = 24f;
        scentSourceListEmptyLabel.color = new Color(0.86f, 0.8f, 0.66f, 0.78f);
        scentSourceListEmptyLabel.alignment = TextAlignmentOptions.Center;
        scentSourceListEmptyLabel.raycastTarget = false;

        GameObject hitAreaObject = CreateUIObject("HitArea", scentSourceListObject.transform);
        RectTransform hitAreaRect = hitAreaObject.GetComponent<RectTransform>();
        hitAreaRect.anchorMin = Vector2.zero;
        hitAreaRect.anchorMax = Vector2.one;
        hitAreaRect.offsetMin = Vector2.zero;
        hitAreaRect.offsetMax = Vector2.zero;

        Image hitAreaImage = hitAreaObject.AddComponent<Image>();
        hitAreaImage.color = new Color(1f, 1f, 1f, 0.001f);
        hitAreaImage.raycastTarget = true;

        InteractionDialogScentSourceListHitArea hitArea = hitAreaObject.AddComponent<InteractionDialogScentSourceListHitArea>();
        hitArea.Initialize(this);

        scentSourceListObject.SetActive(false);
    }

    private void BuildSocialEmoteGrid(Transform parent)
    {
        socialEmoteGridObject = CreateUIObject("SocialEmoteGrid", parent);
        RectTransform gridRect = socialEmoteGridObject.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 1f);
        gridRect.anchorMax = new Vector2(0.5f, 1f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.anchoredPosition = new Vector2(-425f, -690f);
        gridRect.sizeDelta = new Vector2(470f, 300f);

        Image gridBackground = socialEmoteGridObject.AddComponent<Image>();
        gridBackground.color = new Color(0.09f, 0.065f, 0.035f, 0.58f);
        gridBackground.raycastTarget = true;

        socialEmoteGridScrollRect = socialEmoteGridObject.AddComponent<ScrollRect>();
        socialEmoteGridScrollRect.horizontal = false;
        socialEmoteGridScrollRect.vertical = true;
        socialEmoteGridScrollRect.movementType = ScrollRect.MovementType.Clamped;
        socialEmoteGridScrollRect.scrollSensitivity = 24f;

        GameObject viewportObject = CreateUIObject("Viewport", socialEmoteGridObject.transform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(10f, 10f);
        viewportRect.offsetMax = new Vector2(-10f, -10f);

        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(0.055f, 0.047f, 0.036f, 0.45f);
        viewportImage.raycastTarget = true;
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUIObject("Content", viewportObject.transform);
        socialEmoteGridContentRect = contentObject.GetComponent<RectTransform>();
        socialEmoteGridContentRect.anchorMin = new Vector2(0f, 1f);
        socialEmoteGridContentRect.anchorMax = new Vector2(1f, 1f);
        socialEmoteGridContentRect.pivot = new Vector2(0.5f, 1f);
        socialEmoteGridContentRect.anchoredPosition = Vector2.zero;
        socialEmoteGridContentRect.sizeDelta = Vector2.zero;

        GridLayoutGroup layout = contentObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(SocialEmoteTileSize, SocialEmoteTileSize);
        layout.spacing = new Vector2(8f, 8f);
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = SocialEmoteGridColumns;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        socialEmoteGridScrollRect.viewport = viewportRect;
        socialEmoteGridScrollRect.content = socialEmoteGridContentRect;
        socialEmoteGridObject.SetActive(false);
    }

    private Button CreatePackActionButton(
        Transform parent,
        string objectName,
        PackButtonKind kind,
        int spriteIndex,
        string fallbackText,
        UnityEngine.Events.UnityAction clickHandler,
        bool implemented = true)
    {
        Sprite sprite = GetPackActionSprite(kind, spriteIndex);
        Button button = CreateSpriteButton(objectName, parent, sprite, fallbackText, clickHandler ?? OnUnimplementedPackActionClicked);
        ConfigureActionButtonSize(button, sprite, 78f);
        button.interactable = implemented;

        Image image = button.targetGraphic as Image;
        if (image != null && !implemented)
            image.color = new Color(image.color.r, image.color.g, image.color.b, 0.36f);

        return button;
    }

    private static Sprite GetPackActionSprite(PackButtonKind kind, int spriteIndex)
    {
        string spriteSheet = kind == PackButtonKind.Behavior
            ? "Sprites/MoveModes_B"
            : "Sprites/PackFormationsSprites_C";

        return SpriteServer.SpriteSheetLookup(spriteSheet, spriteIndex);
    }

    private Transform CreateActionButtonRow(string rowName, Transform parent, float rowHeight)
    {
        GameObject rowObject = CreateUIObject(rowName, parent);

        HorizontalLayoutGroup rowLayout = rowObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = false;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.spacing = 8f;

        LayoutElement layoutElement = rowObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = rowHeight;
        layoutElement.minHeight = rowHeight;

        return rowObject.transform;
    }

    private void CreateActionButton(Transform parent, InventoryAction action, UnityEngine.Events.UnityAction clickHandler, float heightScale = 1f)
    {
        Sprite sprite = GetInventoryActionSprite(action);
        string fallbackText = GetActionFallbackText(action);
        Button button = CreateSpriteButton($"{action}Button", parent, sprite, fallbackText, clickHandler);
        ConfigureActionButtonSize(button, sprite, actionButtonHeight * Mathf.Max(0.01f, heightScale));
    }

    private void CreateThrowActionButton(Transform parent, float heightScale = 1f)
    {
        Sprite sprite = SpriteServer.SpriteLookup("Throw_Item")
            ?? SpriteServer.SpriteSheetLookup("Sprites/DogActions_B", 0);
        Button button = CreateSpriteButton("ThrowButton", parent, sprite, "THROW", OnThrowClicked);
        ConfigureActionButtonSize(button, sprite, actionButtonHeight * Mathf.Max(0.01f, heightScale));
    }

    private static void ConfigureActionButtonSize(Button button, Sprite sprite, float buttonHeight)
    {
        float width = buttonHeight;
        if (sprite != null && sprite.rect.height > 0f)
            width = buttonHeight * (sprite.rect.width / sprite.rect.height);

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, buttonHeight);

        LayoutElement layoutElement = button.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = buttonHeight;
        layoutElement.minWidth = width;
        layoutElement.minHeight = buttonHeight;
    }

    private static Sprite GetInventoryActionSprite(InventoryAction action)
    {
        string spriteName = action switch
        {
            InventoryAction.Use => "UseItem",
            InventoryAction.Eat => "EatItem",
            InventoryAction.Drop => "DropItem",
            InventoryAction.PickUp => "PickUpItem",
            _ => string.Empty
        };

        return SpriteServer.SpriteLookup(spriteName);
    }

    private static string GetActionFallbackText(InventoryAction action)
    {
        return action switch
        {
            InventoryAction.Use => "USE",
            InventoryAction.Eat => "EAT",
            InventoryAction.Drop => "DROP",
            InventoryAction.PickUp => "PICK UP",
            _ => action.ToString()
        };
    }

    private Button CreateSpriteButton(
        string objectName,
        Transform parent,
        Sprite sprite,
        string fallbackText,
        UnityEngine.Events.UnityAction clickHandler)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = sprite != null
            ? Color.white
            : new Color(0.88f, 0.78f, 0.5f, 0.86f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(clickHandler);

        if (sprite == null)
            AddFallbackButtonText(buttonObject.transform, fallbackText);

        ConfigureTooltip(buttonObject, FormatTooltipText(fallbackText));
        return button;
    }

    private static void AddFallbackButtonText(Transform parent, string text)
    {
        GameObject labelObject = CreateUIObject("FallbackLabel", parent);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 20f;
        label.color = Color.black;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private static TextMeshProUGUI CreateInfoLabel(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject labelObject = CreateUIObject(objectName, parent);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 1f);
        labelRect.anchorMax = new Vector2(0.5f, 1f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = anchoredPosition;
        labelRect.sizeDelta = size;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.color = new Color(0.29f, 0.18f, 0.09f, 1f);
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private void BuildSelectionArrows(Transform parent)
    {
        previousPlayerAgentButton = CreateArrowButton(parent, "PreviousPlayerAgentButton", new Vector2(-593f, -290f), OnPreviousPlayerAgentClicked, 48f);
        nextPlayerAgentButton = CreateArrowButton(parent, "NextPlayerAgentButton", new Vector2(-400f, -290f), OnNextPlayerAgentClicked, 48f);
        previousPlayerItemButton = CreateArrowButton(parent, "PreviousPlayerItemButton", new Vector2(-380f, -320f), OnPreviousPlayerItemClicked, 32f);
        nextPlayerItemButton = CreateArrowButton(parent, "NextPlayerItemButton", new Vector2(-288f, -320f), OnNextPlayerItemClicked, 32f);

        previousTargetItemButton = CreateArrowButton(parent, "PreviousTargetItemButton", new Vector2(275f, -320f), OnPreviousTargetItemClicked, 32f);
        nextTargetItemButton = CreateArrowButton(parent, "NextTargetItemButton", new Vector2(365f, -320f), OnNextTargetItemClicked, 32f);
        previousTargetAgentButton = CreateArrowButton(parent, "PreviousTargetAgentButton", new Vector2(383f, -290f), OnPreviousTargetAgentClicked, 48f);
        nextTargetAgentButton = CreateArrowButton(parent, "NextTargetAgentButton", new Vector2(576f, -290f), OnNextTargetAgentClicked, 48f);
    }

    private Button CreateArrowButton(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        UnityEngine.Events.UnityAction clickHandler,
        float size = 41f)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(size, size);

        Image image = buttonObject.AddComponent<Image>();
        image.sprite = null;
        image.color = Color.clear;
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(clickHandler);
        ConfigureTooltip(buttonObject, GetInteractionButtonTooltipText(objectName));

        return button;
    }

    private void BuildPreviewSlots(Transform parent)
    {
        playerPreviewSlot = CreatePreviewSlot(parent, "PlayerPreview", PlayerPreviewAnchorPosition, new Vector2(-497f, -290f), new Vector2(150f, 150f), new Vector2(140f, 140f), new Vector2(239f, 140f), 1.325f);
        playerItemPreviewSlot = CreatePreviewSlot(parent, "PlayerItemPreview", PlayerItemPreviewAnchorPosition, new Vector2(-334f, -320f), new Vector2(78f, 78f), new Vector2(80f, 80f), new Vector2(137f, 80f), 1.2f);
        targetItemPreviewSlot = CreatePreviewSlot(parent, "TargetItemPreview", TargetItemPreviewAnchorPosition, new Vector2(320f, -320f), new Vector2(78f, 78f), new Vector2(80f, 80f), new Vector2(137f, 80f), 1.2f);
        targetPreviewSlot = CreatePreviewSlot(parent, "TargetPreview", TargetPreviewAnchorPosition, new Vector2(480f, -290f), new Vector2(150f, 150f), new Vector2(140f, 140f), new Vector2(239f, 140f), 1.325f);
    }

    private void BuildPackIndicatorButtons(Transform parent)
    {
        playerPackIndicatorButton = CreatePackIndicatorButton(parent, "PlayerPackIndicatorButton", new Vector2(-334f, -320f), OnPlayerPackIndicatorClicked);
        targetPackIndicatorButton = CreatePackIndicatorButton(parent, "TargetPackIndicatorButton", new Vector2(320f, -320f), OnTargetPackIndicatorClicked);
        SetPackIndicatorButtonsActive(false);
    }

    private Button CreatePackIndicatorButton(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        UnityEngine.Events.UnityAction clickHandler)
    {
        Button button = CreateInvisibleButton(objectName, parent, clickHandler);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(84f, 84f);
        return button;
    }

    private PreviewSlot CreatePreviewSlot(
        Transform parent,
        string objectName,
        Vector3 anchorPosition,
        Vector2 anchoredPosition,
        Vector2 size,
        Vector2 circleSize,
        Vector2 circleWithArrowsSize,
        float orthographicPadding)
    {
        GameObject circleObject = CreateUIObject($"{objectName}Circle", parent);
        RectTransform circleRect = circleObject.GetComponent<RectTransform>();
        circleRect.anchorMin = new Vector2(0.5f, 1f);
        circleRect.anchorMax = new Vector2(0.5f, 1f);
        circleRect.pivot = new Vector2(0.5f, 0.5f);
        circleRect.anchoredPosition = anchoredPosition;
        circleRect.sizeDelta = circleSize;

        Image circleImage = circleObject.AddComponent<Image>();
        circleImage.sprite = circleSprite;
        circleImage.preserveAspect = true;
        circleImage.color = Color.white;
        circleImage.raycastTarget = false;

        GameObject previewObject = CreateUIObject(objectName, parent);
        RectTransform previewRect = previewObject.GetComponent<RectTransform>();
        previewRect.anchorMin = new Vector2(0.5f, 1f);
        previewRect.anchorMax = new Vector2(0.5f, 1f);
        previewRect.pivot = new Vector2(0.5f, 0.5f);
        previewRect.anchoredPosition = anchoredPosition;
        previewRect.sizeDelta = size;

        RawImage image = previewObject.AddComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = false;

        return new PreviewSlot
        {
            CircleImage = circleImage,
            Image = image,
            AnchorPosition = anchorPosition,
            CircleSize = circleSize,
            CircleWithArrowsSize = circleWithArrowsSize,
            OrthographicPadding = orthographicPadding
        };
    }

    private void RefreshInteractionView(bool forcePreviewRefresh = false)
    {
        RefreshTabHighlights();
        if (currentTab == InteractionTab.Pack)
        {
            RefreshPackView(forcePreviewRefresh);
            return;
        }

        if (currentTab == InteractionTab.Social)
        {
            RefreshSocialView(forcePreviewRefresh);
            return;
        }

        if (currentTab == InteractionTab.Quests)
        {
            RefreshQuestsView(forcePreviewRefresh);
            return;
        }

        if (currentTab == InteractionTab.Scent)
        {
            RefreshScentView(forcePreviewRefresh);
            return;
        }

        SetPackControlsActive(false);
        SetQuestControlsActive(false);
        SetScentControlsActive(false);
        SetSocialControlsActive(false);
        SetPackIndicatorButtonsActive(false);
        SetItemsControlsActive(true);
        SetPreviewSlotActive(playerItemPreviewSlot, true);
        SetPreviewSlotActive(targetItemPreviewSlot, true);
        SetItemSelectionTypeLabelsActive(true);

        BuildPlayerAgentOptions();
        ApplyPendingSelection(playerAgentOptions, pendingLeftAgentSelection, ref selectedPlayerAgentIndex);
        WorldObject player = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
        WorldObject previousPlayerItem = GetSelectedFromList(playerItemOptions, ref selectedPlayerItemIndex);
        BuildItemOptions(player, playerItemOptions);
        KeepSelectedObject(playerItemOptions, previousPlayerItem, ref selectedPlayerItemIndex);
        WorldObject playerItem = GetSelectedFromList(playerItemOptions, ref selectedPlayerItemIndex);
        RefreshPackHeldItemList(player, playerItem);

        BuildTargetAgentOptions(player);
        ApplyPendingSelection(targetAgentOptions, pendingRightAgentSelection, ref selectedTargetAgentIndex);
        WorldObject target = GetSelectedFromList(targetAgentOptions, ref selectedTargetAgentIndex);
        WorldObject previousTargetItem = GetSelectedFromList(targetItemOptions, ref selectedTargetItemIndex);
        BuildItemOptions(target, targetItemOptions);
        KeepSelectedObject(targetItemOptions, previousTargetItem, ref selectedTargetItemIndex);
        WorldObject targetItem = GetSelectedFromList(targetItemOptions, ref selectedTargetItemIndex);

        RefreshCircleAndHotspot(playerPreviewSlot, playerAgentOptions.Count, previousPlayerAgentButton, nextPlayerAgentButton);
        RefreshCircleAndHotspot(playerItemPreviewSlot, playerItemOptions.Count, previousPlayerItemButton, nextPlayerItemButton);
        RefreshCircleAndHotspot(targetPreviewSlot, targetAgentOptions.Count, previousTargetAgentButton, nextTargetAgentButton);
        RefreshCircleAndHotspot(targetItemPreviewSlot, targetItemOptions.Count, previousTargetItemButton, nextTargetItemButton);

        SetLabelText(playerNameLabel, player != null ? player.DisplayName : string.Empty);
        SetLabelText(playerHeldItemLabel, playerItem != null ? playerItem.DisplayName : string.Empty);
        SetLabelText(targetNameLabel, target != null ? target.DisplayName : string.Empty);
        SetLabelText(targetHeldItemLabel, targetItem != null ? targetItem.DisplayName : string.Empty);

        if (forcePreviewRefresh || player != displayedPlayer)
            BuildPreviewClone(playerPreviewSlot, player, "Player");
        if (forcePreviewRefresh || playerItem != displayedPlayerItem)
            BuildPreviewClone(playerItemPreviewSlot, playerItem, "PlayerItem");
        if (forcePreviewRefresh || target != displayedTarget)
            BuildPreviewClone(targetPreviewSlot, target, "Target");
        if (forcePreviewRefresh || targetItem != displayedTargetItem)
            BuildPreviewClone(targetItemPreviewSlot, targetItem, "TargetItem");

        displayedPlayer = player;
        displayedPlayerItem = playerItem;
        displayedTarget = target;
        displayedTargetItem = targetItem;
        ClearPendingSelections();
    }

    private void RefreshScentView(bool forcePreviewRefresh = false)
    {
        SetPackControlsActive(false);
        SetQuestControlsActive(false);
        SetScentControlsActive(true);
        SetSocialControlsActive(false);
        SetPackIndicatorButtonsActive(false);
        SetItemsControlsActive(false);
        SetPreviewSlotActive(playerItemPreviewSlot, false);
        SetPreviewSlotActive(targetItemPreviewSlot, false);
        SetItemSelectionTypeLabelsActive(false);

        BuildPlayerAgentOptions();
        ApplyPendingSelection(playerAgentOptions, pendingLeftAgentSelection, ref selectedPlayerAgentIndex);
        WorldObject leftMember = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
        BuildScentTargetOptions(leftMember);
        ApplyPendingSelection(scentTargetOptions, pendingRightAgentSelection, ref selectedScentTargetIndex);
        WorldObject rightMember = GetSelectedFromList(scentTargetOptions, ref selectedScentTargetIndex);
        RefreshScentSourceList();

        RefreshCircleAndHotspot(playerPreviewSlot, playerAgentOptions.Count, previousPlayerAgentButton, nextPlayerAgentButton);
        RefreshCircleAndHotspot(targetPreviewSlot, scentTargetOptions.Count, previousTargetAgentButton, nextTargetAgentButton);
        RefreshCircleAndHotspot(playerItemPreviewSlot, 0, previousPlayerItemButton, nextPlayerItemButton);
        RefreshCircleAndHotspot(targetItemPreviewSlot, 0, previousTargetItemButton, nextTargetItemButton);

        SetLabelText(playerNameLabel, leftMember != null ? leftMember.DisplayName : string.Empty);
        SetLabelText(playerHeldItemLabel, string.Empty);
        SetLabelText(targetNameLabel, rightMember != null ? rightMember.DisplayName : string.Empty);
        SetLabelText(targetHeldItemLabel, string.Empty);

        if (forcePreviewRefresh || leftMember != displayedScentLeft)
            BuildPreviewClone(playerPreviewSlot, leftMember, "ScentLeft");
        if (forcePreviewRefresh || rightMember != displayedScentRight)
            BuildPreviewClone(targetPreviewSlot, rightMember, "ScentRight");

        BuildPreviewClone(playerItemPreviewSlot, null, "ScentLeftItem");
        BuildPreviewClone(targetItemPreviewSlot, null, "ScentRightItem");

        displayedScentLeft = leftMember;
        displayedScentRight = rightMember;
        displayedPlayer = leftMember;
        displayedPlayerItem = null;
        displayedTarget = rightMember;
        displayedTargetItem = null;
        ClearPendingSelections();
    }

    private void RefreshQuestsView(bool forcePreviewRefresh = false)
    {
        SetPackControlsActive(false);
        SetQuestControlsActive(true);
        SetScentControlsActive(false);
        SetSocialControlsActive(false);
        SetPackIndicatorButtonsActive(false);
        SetItemsControlsActive(false);
        SetPreviewSlotActive(playerItemPreviewSlot, false);
        SetPreviewSlotActive(targetItemPreviewSlot, false);
        SetItemSelectionTypeLabelsActive(false);

        BuildPlayerAgentOptions();
        ApplyPendingSelection(playerAgentOptions, pendingLeftAgentSelection, ref selectedPlayerAgentIndex);
        WorldObject leftMember = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
        RefreshInteractionQuestList();
        BuildQuestTargetOptions(leftMember);
        ApplyPendingSelection(questTargetOptions, pendingRightAgentSelection, ref selectedQuestTargetIndex);
        WorldObject rightMember = GetSelectedFromList(questTargetOptions, ref selectedQuestTargetIndex);

        RefreshCircleAndHotspot(playerPreviewSlot, playerAgentOptions.Count, previousPlayerAgentButton, nextPlayerAgentButton);
        RefreshCircleAndHotspot(targetPreviewSlot, questTargetOptions.Count, previousTargetAgentButton, nextTargetAgentButton);
        RefreshCircleAndHotspot(playerItemPreviewSlot, 0, previousPlayerItemButton, nextPlayerItemButton);
        RefreshCircleAndHotspot(targetItemPreviewSlot, 0, previousTargetItemButton, nextTargetItemButton);

        SetLabelText(playerNameLabel, leftMember != null ? leftMember.DisplayName : string.Empty);
        SetLabelText(playerHeldItemLabel, string.Empty);
        SetLabelText(targetNameLabel, rightMember != null ? rightMember.DisplayName : string.Empty);
        SetLabelText(targetHeldItemLabel, string.Empty);

        if (forcePreviewRefresh || leftMember != displayedQuestLeft)
            BuildPreviewClone(playerPreviewSlot, leftMember, "QuestLeft");
        if (forcePreviewRefresh || rightMember != displayedQuestRight)
            BuildPreviewClone(targetPreviewSlot, rightMember, "QuestRight");

        BuildPreviewClone(playerItemPreviewSlot, null, "QuestLeftItem");
        BuildPreviewClone(targetItemPreviewSlot, null, "QuestRightItem");

        displayedQuestLeft = leftMember;
        displayedQuestRight = rightMember;
        displayedPlayer = leftMember;
        displayedPlayerItem = null;
        displayedTarget = rightMember;
        displayedTargetItem = null;
        ClearPendingSelections();
    }

    private void RefreshSocialView(bool forcePreviewRefresh = false)
    {
        SetPackControlsActive(false);
        SetQuestControlsActive(false);
        SetScentControlsActive(false);
        SetSocialControlsActive(true);
        SetPackIndicatorButtonsActive(false);
        SetItemsControlsActive(false);
        SetPreviewSlotActive(playerItemPreviewSlot, false);
        SetPreviewSlotActive(targetItemPreviewSlot, false);
        SetItemSelectionTypeLabelsActive(false);

        BuildPlayerAgentOptions();
        ApplyPendingSelection(playerAgentOptions, pendingLeftAgentSelection, ref selectedPlayerAgentIndex);
        WorldObject leftMember = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
        RefreshSocialEmoteGrid(leftMember);
        BuildSocialTargetOptions(leftMember);
        ApplyPendingSelection(socialTargetOptions, pendingRightAgentSelection, ref selectedSocialTargetIndex);
        WorldObject rightMember = GetSelectedFromList(socialTargetOptions, ref selectedSocialTargetIndex);

        RefreshCircleAndHotspot(playerPreviewSlot, playerAgentOptions.Count, previousPlayerAgentButton, nextPlayerAgentButton);
        RefreshCircleAndHotspot(targetPreviewSlot, socialTargetOptions.Count, previousTargetAgentButton, nextTargetAgentButton);
        RefreshCircleAndHotspot(playerItemPreviewSlot, 0, previousPlayerItemButton, nextPlayerItemButton);
        RefreshCircleAndHotspot(targetItemPreviewSlot, 0, previousTargetItemButton, nextTargetItemButton);

        SetLabelText(playerNameLabel, leftMember != null ? leftMember.DisplayName : string.Empty);
        SetLabelText(playerHeldItemLabel, string.Empty);
        SetLabelText(targetNameLabel, rightMember != null ? rightMember.DisplayName : string.Empty);
        SetLabelText(targetHeldItemLabel, string.Empty);

        if (forcePreviewRefresh || leftMember != displayedSocialLeft)
            BuildPreviewClone(playerPreviewSlot, leftMember, "SocialLeft");
        if (forcePreviewRefresh || rightMember != displayedSocialRight)
            BuildPreviewClone(targetPreviewSlot, rightMember, "SocialRight");

        BuildPreviewClone(playerItemPreviewSlot, null, "SocialLeftItem");
        BuildPreviewClone(targetItemPreviewSlot, null, "SocialRightItem");

        displayedSocialLeft = leftMember;
        displayedSocialRight = rightMember;
        displayedPlayer = leftMember;
        displayedPlayerItem = null;
        displayedTarget = rightMember;
        displayedTargetItem = null;
        ClearPendingSelections();
    }

    private void RefreshPackView(bool forcePreviewRefresh = false)
    {
        SetItemsControlsActive(false);
        SetQuestControlsActive(false);
        SetScentControlsActive(false);
        SetSocialControlsActive(false);
        SetPackControlsActive(true);
        SetPackIndicatorButtonsActive(true);
        SetItemSelectionTypeLabelsActive(false);

        BuildPackMemberOptions();
        ApplyPendingSelection(packMemberOptions, pendingLeftAgentSelection, ref selectedPackLeftIndex);
        WorldObject leftMember = GetSelectedFromList(packMemberOptions, ref selectedPackLeftIndex);
        RefreshPackMemberList();
        BuildPackRightOptions(leftMember);
        ApplyPendingSelection(packRightOptions, pendingRightAgentSelection, ref selectedPackRightIndex);
        WorldObject rightMember = GetSelectedFromList(packRightOptions, ref selectedPackRightIndex);

        RefreshCircleAndHotspot(playerPreviewSlot, packMemberOptions.Count, previousPlayerAgentButton, nextPlayerAgentButton);
        RefreshCircleAndHotspot(targetPreviewSlot, packRightOptions.Count, previousTargetAgentButton, nextTargetAgentButton);
        RefreshCircleAndHotspot(playerItemPreviewSlot, 0, previousPlayerItemButton, nextPlayerItemButton);
        RefreshCircleAndHotspot(targetItemPreviewSlot, 0, previousTargetItemButton, nextTargetItemButton);
        RefreshPackIndicatorSlot(playerItemPreviewSlot, leftMember);
        RefreshPackIndicatorSlot(targetItemPreviewSlot, rightMember);
        RefreshPackIndicatorButton(playerPackIndicatorButton, leftMember);
        RefreshPackIndicatorButton(targetPackIndicatorButton, rightMember);
        RefreshPackMembershipButtons(rightMember);

        SetLabelText(playerNameLabel, leftMember != null ? leftMember.DisplayName : string.Empty);
        SetLabelText(playerHeldItemLabel, string.Empty);
        SetLabelText(targetNameLabel, rightMember != null ? rightMember.DisplayName : string.Empty);
        SetLabelText(targetHeldItemLabel, string.Empty);

        if (forcePreviewRefresh || leftMember != displayedPackLeft)
            BuildPreviewClone(playerPreviewSlot, leftMember, "PackLeft");
        if (forcePreviewRefresh || rightMember != displayedPackRight)
            BuildPreviewClone(targetPreviewSlot, rightMember, "PackRight");

        BuildPreviewClone(playerItemPreviewSlot, null, "PackLeftItem");
        BuildPreviewClone(targetItemPreviewSlot, null, "PackRightItem");

        displayedPackLeft = leftMember;
        displayedPackRight = rightMember;
        displayedPlayer = leftMember;
        displayedPlayerItem = null;
        displayedTarget = rightMember;
        displayedTargetItem = null;
        ClearPendingSelections();
    }

    private void SetItemsControlsActive(bool active)
    {
        if (tradeArrowsObject != null)
            tradeArrowsObject.SetActive(active);
        if (giveHotspotButton != null)
            giveHotspotButton.gameObject.SetActive(active);
        if (exchangeHotspotButton != null)
            exchangeHotspotButton.gameObject.SetActive(active);
        if (takeHotspotButton != null)
            takeHotspotButton.gameObject.SetActive(active);
        if (actionPanelObject != null)
            actionPanelObject.SetActive(active);
        if (packHeldItemListObject != null)
        {
            packHeldItemListObject.SetActive(active);
            if (active)
                packHeldItemListObject.transform.SetAsLastSibling();
        }
    }

    private void SetPackControlsActive(bool active)
    {
        if (packActionPanelObject != null)
            packActionPanelObject.SetActive(active);
        if (packMemberListObject != null)
        {
            packMemberListObject.SetActive(active);
            if (active)
                packMemberListObject.transform.SetAsLastSibling();
        }
    }

    private void SetQuestControlsActive(bool active)
    {
        if (questListObject != null)
        {
            questListObject.SetActive(active);
            if (active)
                questListObject.transform.SetAsLastSibling();
        }
    }

    private void SetScentControlsActive(bool active)
    {
        if (scentSourceListObject != null)
        {
            scentSourceListObject.SetActive(active);
            if (active)
                scentSourceListObject.transform.SetAsLastSibling();
        }
    }

    private void SetSocialControlsActive(bool active)
    {
        if (socialEmoteGridObject != null)
        {
            socialEmoteGridObject.SetActive(active);
            if (active)
                socialEmoteGridObject.transform.SetAsLastSibling();
        }
    }

    private void RefreshSocialEmoteGrid(WorldObject leftMember)
    {
        if (socialEmoteGridContentRect == null)
            return;

        bool useHumanSet = leftMember != null && leftMember.species == Species.Human;
        if (displayedSocialEmoteGridInitialized && displayedSocialEmoteGridUsesHuman == useHumanSet)
            return;

        ClearSocialEmoteGridTiles();
        displayedSocialEmoteGridUsesHuman = useHumanSet;
        displayedSocialEmoteGridInitialized = true;

        if (useHumanSet)
        {
            for (int i = 0; i < HumanEmojiCount; i++)
            {
                Sprite sprite = GetHumanEmoteSprite(i);
                if (sprite != null)
                    CreateSocialHumanEmoteTile(i, sprite);
            }
        }
        else
        {
            for (int i = 0; i < DogEmojiCatalog.Entries.Length; i++)
            {
                DogEmojiEntry entry = DogEmojiCatalog.Entries[i];
                Sprite sprite = GetDogEmoteSprite(entry);
                if (sprite != null)
                    CreateSocialDogEmoteTile(entry, sprite);
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(socialEmoteGridContentRect);
        if (socialEmoteGridScrollRect != null)
            socialEmoteGridScrollRect.verticalNormalizedPosition = 1f;
    }

    private void ClearSocialEmoteGridTiles()
    {
        socialEmoteGridTiles.Clear();

        if (socialEmoteGridContentRect == null)
            return;

        for (int i = socialEmoteGridContentRect.childCount - 1; i >= 0; i--)
        {
            Transform child = socialEmoteGridContentRect.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    private void CreateSocialDogEmoteTile(DogEmojiEntry entry, Sprite sprite)
    {
        GameObject tileObject = CreateSocialEmoteTile($"SocialDogEmote_{entry.EntryId}", sprite, entry.Name);
        Button button = tileObject.GetComponent<Button>();
        button.onClick.AddListener(() => HandleSocialDogEmoteClicked(entry));
    }

    private void CreateSocialHumanEmoteTile(int spriteIndex, Sprite sprite)
    {
        GameObject tileObject = CreateSocialEmoteTile($"SocialHumanEmote_{spriteIndex}", sprite, $"Human Emote {spriteIndex + 1}");
        Button button = tileObject.GetComponent<Button>();
        int capturedIndex = spriteIndex;
        button.onClick.AddListener(() => HandleSocialHumanEmoteClicked(capturedIndex));
    }

    private GameObject CreateSocialEmoteTile(string objectName, Sprite sprite, string tooltipText)
    {
        GameObject tileObject = CreateUIObject(objectName, socialEmoteGridContentRect);
        LayoutElement layout = tileObject.AddComponent<LayoutElement>();
        layout.preferredWidth = SocialEmoteTileSize;
        layout.preferredHeight = SocialEmoteTileSize;
        layout.minWidth = SocialEmoteTileSize;
        layout.minHeight = SocialEmoteTileSize;

        Image background = tileObject.AddComponent<Image>();
        background.color = new Color(0.2f, 0.15f, 0.08f, 0.9f);
        background.raycastTarget = true;

        Button button = tileObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);

        GameObject iconObject = CreateUIObject("Icon", tileObject.transform);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = Vector2.one * (SocialEmoteTileSize - 16f);

        Image iconImage = iconObject.AddComponent<Image>();
        iconImage.sprite = sprite;
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;
        iconImage.raycastTarget = false;

        ConfigureTooltip(tileObject, tooltipText);
        socialEmoteGridTiles.Add(tileObject);
        return tileObject;
    }

    private void HandleSocialDogEmoteClicked(DogEmojiEntry entry)
    {
        WorldObject actor = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
        if (actor == null)
            return;

        BottomBanner.LogEmote(actor, entry.EntryId);
    }

    private void HandleSocialHumanEmoteClicked(int spriteIndex)
    {
        WorldObject actor = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
        if (actor == null)
            return;

        Sprite sprite = GetHumanEmoteSprite(spriteIndex);
        if (sprite != null)
            EmoteIconVisualFactory.Show(actor, sprite);
    }

    private static Sprite GetDogEmoteSprite(DogEmojiEntry entry)
    {
        return SpriteServer.SpriteLookup(entry.EntryId)
            ?? SpriteServer.SpriteSheetLookup($"DogEmojiSheet{entry.SheetId}", entry.SpriteIndex);
    }

    private static Sprite GetHumanEmoteSprite(int spriteIndex)
    {
        return SpriteServer.SpriteSheetLookup("Sprites/Emotes/Human_Emoji_A", spriteIndex)
            ?? SpriteServer.SpriteSheetLookup("Human_Emoji_A", spriteIndex);
    }

    private void RefreshPackHeldItemList(WorldObject selectedAgent, WorldObject selectedItem)
    {
        if (packHeldItemListContentRect == null)
            return;

        List<PackHeldItemOption> currentOptions = BuildPackHeldItemOptions();
        bool optionsChanged = packHeldItemListDirty || HasPackHeldItemOptionsChanged(currentOptions);
        bool selectionChanged = displayedPackHeldItemSelectedAgent != selectedAgent ||
                                displayedPackHeldItemSelectedItem != selectedItem;

        if (optionsChanged)
        {
            packHeldItemOptions.Clear();
            packHeldItemOptions.AddRange(currentOptions);
            RebuildPackHeldItemListRows();
            packHeldItemListDirty = false;
        }

        if (packHeldItemListEmptyLabel != null)
            packHeldItemListEmptyLabel.gameObject.SetActive(packHeldItemOptions.Count <= 0);

        RefreshPackHeldItemListHighlights(selectedAgent, selectedItem);
        if (optionsChanged || selectionChanged)
            ScrollPackHeldItemListToSelection(selectedAgent, selectedItem);

        displayedPackHeldItemSelectedAgent = selectedAgent;
        displayedPackHeldItemSelectedItem = selectedItem;
    }

    private List<PackHeldItemOption> BuildPackHeldItemOptions()
    {
        List<PackHeldItemOption> options = new();
        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        if (playerPack == null || playerPack.packAgentList == null)
            return options;

        for (int i = 0; i < playerPack.packAgentList.Count; i++)
            AddPackHeldItemOptionsForAgent(options, playerPack.packAgentList[i]);

        return options;
    }

    private static void AddPackHeldItemOptionsForAgent(List<PackHeldItemOption> options, WorldObject agent)
    {
        if (agent == null || !agent.gameObject.activeInHierarchy)
            return;

        ContainerModule container = GetOrCreateContainer(agent);
        if (container == null || container.HeldItemCount <= 0)
            return;

        for (int i = 0; i < container.HeldItemCount; i++)
        {
            WorldObject item = container.HeldItems[i];
            if (item != null)
                options.Add(new PackHeldItemOption(agent, item));
        }
    }

    private bool HasPackHeldItemOptionsChanged(List<PackHeldItemOption> currentOptions)
    {
        if (currentOptions.Count != packHeldItemOptions.Count)
            return true;

        for (int i = 0; i < currentOptions.Count; i++)
        {
            if (currentOptions[i].Agent != packHeldItemOptions[i].Agent ||
                currentOptions[i].Item != packHeldItemOptions[i].Item)
            {
                return true;
            }
        }

        return false;
    }

    private void RebuildPackHeldItemListRows()
    {
        ClearPackHeldItemListRows();

        for (int i = 0; i < packHeldItemOptions.Count; i++)
            CreatePackHeldItemListRow(packHeldItemOptions[i], i);

        LayoutRebuilder.ForceRebuildLayoutImmediate(packHeldItemListContentRect);
    }

    private void ClearPackHeldItemListRows()
    {
        packHeldItemListBackgrounds.Clear();

        if (packHeldItemListContentRect == null)
            return;

        for (int i = packHeldItemListContentRect.childCount - 1; i >= 0; i--)
        {
            Transform child = packHeldItemListContentRect.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    private void CreatePackHeldItemListRow(PackHeldItemOption option, int index)
    {
        GameObject rowObject = CreateUIObject($"PackHeldItemRow_{index}", packHeldItemListContentRect);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, PackMemberListRowHeight);

        LayoutElement layoutElement = rowObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = PackMemberListRowHeight;
        layoutElement.minHeight = PackMemberListRowHeight;

        Image background = rowObject.AddComponent<Image>();
        background.color = GetPackHeldItemListRowColor(false);
        background.raycastTarget = true;

        Button button = rowObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(() => SelectPackHeldItem(option));

        GameObject labelObject = CreateUIObject("Label", rowObject.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 2f);
        labelRect.offsetMax = new Vector2(-12f, -2f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = FormatPackHeldItemListLabel(option);
        label.fontSize = 22f;
        label.color = new Color(1f, 0.88f, 0.58f, 1f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        packHeldItemListBackgrounds.Add(background);
        ConfigureTooltip(rowObject, $"Select {FormatPackHeldItemListLabel(option)}");
    }

    private void RefreshPackHeldItemListHighlights(WorldObject selectedAgent, WorldObject selectedItem)
    {
        for (int i = 0; i < packHeldItemListBackgrounds.Count; i++)
        {
            bool selected = i < packHeldItemOptions.Count &&
                            packHeldItemOptions[i].Agent == selectedAgent &&
                            packHeldItemOptions[i].Item == selectedItem;
            packHeldItemListBackgrounds[i].color = GetPackHeldItemListRowColor(selected);
        }
    }

    private void ScrollPackHeldItemListToSelection(WorldObject selectedAgent, WorldObject selectedItem)
    {
        if (packHeldItemListScrollRect == null || packHeldItemOptions.Count <= 0)
            return;

        int selectedIndex = FindPackHeldItemOptionIndex(selectedAgent, selectedItem);
        if (selectedIndex < 0)
            return;

        int rowCount = packHeldItemOptions.Count;
        int visibleRows = Mathf.Max(1, Mathf.FloorToInt(300f / (PackMemberListRowHeight + PackMemberListRowSpacing)));
        if (rowCount <= visibleRows)
        {
            packHeldItemListScrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        float denominator = Mathf.Max(1, rowCount - visibleRows);
        float normalized = 1f - Mathf.Clamp01(selectedIndex / denominator);
        packHeldItemListScrollRect.verticalNormalizedPosition = normalized;
    }

    private int FindPackHeldItemOptionIndex(WorldObject agent, WorldObject item)
    {
        for (int i = 0; i < packHeldItemOptions.Count; i++)
        {
            if (packHeldItemOptions[i].Agent == agent && packHeldItemOptions[i].Item == item)
                return i;
        }

        return -1;
    }

    private void SelectPackHeldItem(PackHeldItemOption option)
    {
        if (option == null || option.Agent == null || option.Item == null)
            return;

        BuildPlayerAgentOptions();
        int agentIndex = playerAgentOptions.IndexOf(option.Agent);
        if (agentIndex < 0)
            return;

        selectedPlayerAgentIndex = agentIndex;
        BuildItemOptions(option.Agent, playerItemOptions);
        int itemIndex = playerItemOptions.IndexOf(option.Item);
        if (itemIndex < 0)
            return;

        selectedPlayerItemIndex = itemIndex;
        packHeldItemListDirty = true;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private static string FormatPackHeldItemListLabel(PackHeldItemOption option)
    {
        string itemName = option != null && option.Item != null ? option.Item.DisplayName : "Item";
        string agentName = option != null && option.Agent != null ? option.Agent.DisplayName : "Agent";
        return $"{itemName} - {agentName}";
    }

    private static Color GetPackHeldItemListRowColor(bool selected)
    {
        return selected
            ? new Color(0.28f, 0.2f, 0.07f, 0.96f)
            : new Color(0.12f, 0.095f, 0.055f, 0.86f);
    }

    private void RefreshScentSourceList()
    {
        if (scentSourceListContentRect == null)
            return;

        bool listChanged = HasScentSourceListChanged();
        bool selectionChanged = displayedScentSourceListSelectedIndex != selectedScentTargetIndex;
        if (!listChanged)
        {
            RefreshScentSourceListHighlights();
            if (selectionChanged)
                ScrollScentSourceListToSelection();
            displayedScentSourceListSelectedIndex = selectedScentTargetIndex;
            return;
        }

        ClearScentSourceListRows();

        if (scentSourceListEmptyLabel != null)
            scentSourceListEmptyLabel.gameObject.SetActive(scentTargetOptions.Count <= 0);

        if (scentTargetOptions.Count <= 0)
        {
            RememberDisplayedScentSourceListState();
            return;
        }

        for (int i = 0; i < scentTargetOptions.Count; i++)
            CreateScentSourceListRow(scentTargetOptions[i], i);

        RefreshScentSourceListHighlights();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scentSourceListContentRect);
        ScrollScentSourceListToSelection();
        RememberDisplayedScentSourceListState();
    }

    private bool HasScentSourceListChanged()
    {
        WorldObject first = scentTargetOptions.Count > 0 ? scentTargetOptions[0] : null;
        WorldObject last = scentTargetOptions.Count > 0 ? scentTargetOptions[^1] : null;
        return displayedScentSourceListOptionCount != scentTargetOptions.Count ||
               displayedScentSourceListFirst != first ||
               displayedScentSourceListLast != last ||
               scentSourceListBackgrounds.Count != scentTargetOptions.Count;
    }

    private void RememberDisplayedScentSourceListState()
    {
        displayedScentSourceListSelectedIndex = selectedScentTargetIndex;
        displayedScentSourceListOptionCount = scentTargetOptions.Count;
        displayedScentSourceListFirst = scentTargetOptions.Count > 0 ? scentTargetOptions[0] : null;
        displayedScentSourceListLast = scentTargetOptions.Count > 0 ? scentTargetOptions[^1] : null;
    }

    private void ClearScentSourceListRows()
    {
        scentSourceListBackgrounds.Clear();

        if (scentSourceListContentRect == null)
            return;

        for (int i = scentSourceListContentRect.childCount - 1; i >= 0; i--)
        {
            Transform child = scentSourceListContentRect.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    private void CreateScentSourceListRow(WorldObject source, int index)
    {
        GameObject rowObject = CreateUIObject($"ScentSourceRow_{index}", scentSourceListContentRect);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, PackMemberListRowHeight);

        LayoutElement layoutElement = rowObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = PackMemberListRowHeight;
        layoutElement.minHeight = PackMemberListRowHeight;

        Image background = rowObject.AddComponent<Image>();
        background.color = GetScentSourceListRowColor(index == selectedScentTargetIndex);
        background.raycastTarget = true;

        GameObject labelObject = CreateUIObject("Label", rowObject.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 2f);
        labelRect.offsetMax = new Vector2(-12f, -2f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = source != null ? source.DisplayName : string.Empty;
        label.fontSize = 22f;
        label.color = new Color(1f, 0.88f, 0.58f, 1f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        scentSourceListBackgrounds.Add(background);
        if (source != null)
            ConfigureTooltip(rowObject, $"Select {source.DisplayName}");
    }

    private void RefreshScentSourceListHighlights()
    {
        for (int i = 0; i < scentSourceListBackgrounds.Count; i++)
            scentSourceListBackgrounds[i].color = GetScentSourceListRowColor(i == selectedScentTargetIndex);
    }

    private static Color GetScentSourceListRowColor(bool selected)
    {
        return selected
            ? new Color(0.95f, 0.54f, 0.12f, 0.86f)
            : new Color(0.20f, 0.13f, 0.065f, 0.78f);
    }

    private void ScrollScentSourceListToSelection()
    {
        if (scentSourceListScrollRect == null || scentSourceListContentRect == null || scentTargetOptions.Count <= 1)
        {
            if (scentSourceListScrollRect != null)
                scentSourceListScrollRect.verticalNormalizedPosition = 1f;
            if (scentSourceListContentRect != null)
                scentSourceListContentRect.anchoredPosition = Vector2.zero;
            return;
        }

        float stride = PackMemberListRowHeight + PackMemberListRowSpacing;
        float targetOffset = selectedScentTargetIndex * stride;
        SetScentSourceListScrollOffset(targetOffset);
    }

    internal void SelectScentSourceListRowAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        int rowIndex = GetScentSourceListRowIndexAtScreenPosition(screenPosition, eventCamera);
        if (rowIndex >= 0)
            OnScentSourceListRowClicked(rowIndex);
    }

    internal void BeginScentSourceListDrag(Vector2 screenPosition, Camera eventCamera)
    {
        if (!TryGetScentSourceListLocalPoint(screenPosition, eventCamera, out Vector2 localPoint))
            return;

        scentSourceListDragStartLocalY = localPoint.y;
        scentSourceListDragStartContentY = scentSourceListContentRect != null
            ? scentSourceListContentRect.anchoredPosition.y
            : 0f;
    }

    internal void DragScentSourceList(Vector2 screenPosition, Camera eventCamera)
    {
        if (!TryGetScentSourceListLocalPoint(screenPosition, eventCamera, out Vector2 localPoint))
            return;

        float dragDeltaY = localPoint.y - scentSourceListDragStartLocalY;
        SetScentSourceListScrollOffset(scentSourceListDragStartContentY + dragDeltaY);
    }

    internal void ScrollScentSourceList(Vector2 scrollDelta)
    {
        if (scentSourceListContentRect == null || scentSourceListScrollRect == null)
            return;

        float currentOffset = scentSourceListContentRect.anchoredPosition.y;
        SetScentSourceListScrollOffset(currentOffset - scrollDelta.y * scentSourceListScrollRect.scrollSensitivity);
    }

    private int GetScentSourceListRowIndexAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        RectTransform viewportRect = scentSourceListScrollRect != null ? scentSourceListScrollRect.viewport : null;
        if (scentTargetOptions.Count <= 0 ||
            viewportRect == null ||
            scentSourceListContentRect == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(viewportRect, screenPosition, eventCamera) ||
            !TryGetScentSourceListLocalPoint(screenPosition, eventCamera, out Vector2 localPoint))
        {
            return -1;
        }

        float visibleDistanceFromTop = viewportRect.rect.height * 0.5f - localPoint.y;
        float contentDistanceFromTop = visibleDistanceFromTop + scentSourceListContentRect.anchoredPosition.y;
        float rowOffset = contentDistanceFromTop - PackMemberListPadding;
        if (rowOffset < 0f)
            return -1;

        float stride = PackMemberListRowHeight + PackMemberListRowSpacing;
        int rowIndex = Mathf.FloorToInt(rowOffset / stride);
        float rowLocalY = rowOffset - rowIndex * stride;
        if (rowIndex < 0 || rowIndex >= scentTargetOptions.Count || rowLocalY > PackMemberListRowHeight)
            return -1;

        return rowIndex;
    }

    private bool TryGetScentSourceListLocalPoint(Vector2 screenPosition, Camera eventCamera, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        RectTransform viewportRect = scentSourceListScrollRect != null ? scentSourceListScrollRect.viewport : null;
        return viewportRect != null &&
               RectTransformUtility.ScreenPointToLocalPointInRectangle(
                   viewportRect,
                   screenPosition,
                   eventCamera,
                   out localPoint);
    }

    private void SetScentSourceListScrollOffset(float offsetY)
    {
        if (scentSourceListContentRect == null)
            return;

        float maxOffset = GetScentSourceListMaxScrollOffset();
        Vector2 anchoredPosition = scentSourceListContentRect.anchoredPosition;
        anchoredPosition.y = Mathf.Clamp(offsetY, 0f, maxOffset);
        scentSourceListContentRect.anchoredPosition = anchoredPosition;

        if (scentSourceListScrollRect != null)
            scentSourceListScrollRect.StopMovement();
    }

    private float GetScentSourceListMaxScrollOffset()
    {
        RectTransform viewportRect = scentSourceListScrollRect != null ? scentSourceListScrollRect.viewport : null;
        if (viewportRect == null || scentTargetOptions.Count <= 0)
            return 0f;

        float contentHeight =
            PackMemberListPadding * 2f +
            scentTargetOptions.Count * PackMemberListRowHeight +
            Mathf.Max(0, scentTargetOptions.Count - 1) * PackMemberListRowSpacing;
        return Mathf.Max(0f, contentHeight - viewportRect.rect.height);
    }

    internal void OnScentSourceListRowClicked(int index)
    {
        if (index < 0 || index >= scentTargetOptions.Count)
            return;

        AudioPlayer.PlayUiButtonClick();
        pendingRightAgentSelection = scentTargetOptions[index];
        selectedScentTargetIndex = index;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void HandleScentSourceListPointerInput()
    {
        if (currentTab != InteractionTab.Scent ||
            scentSourceListRect == null ||
            scentSourceListContentRect == null ||
            Mouse.current == null)
        {
            scentSourceListPointerDown = false;
            return;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        bool pointerOverList = RectTransformUtility.RectangleContainsScreenPoint(scentSourceListRect, screenPosition, null);
        Vector2 scrollDelta = Mouse.current.scroll.ReadValue();
        if (pointerOverList && Mathf.Abs(scrollDelta.y) > 0.01f)
            ScrollScentSourceList(scrollDelta);

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            scentSourceListPointerDown = pointerOverList;
            scentSourceListPointerDragged = false;
            scentSourceListPointerDownPosition = screenPosition;
            if (scentSourceListPointerDown)
                BeginScentSourceListDrag(screenPosition, null);
        }

        if (scentSourceListPointerDown && Mouse.current.leftButton.isPressed)
        {
            if ((screenPosition - scentSourceListPointerDownPosition).sqrMagnitude > 9f)
                scentSourceListPointerDragged = true;

            if (scentSourceListPointerDragged)
                DragScentSourceList(screenPosition, null);
        }

        if (scentSourceListPointerDown && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (!scentSourceListPointerDragged && pointerOverList)
                SelectScentSourceListRowAtScreenPosition(screenPosition, null);

            scentSourceListPointerDown = false;
            scentSourceListPointerDragged = false;
        }
    }

    private void RefreshInteractionQuestList()
    {
        if (questListContentRect == null)
            return;

        if (!interactionQuestListDirty)
        {
            UpdateInteractionQuestHeaderLabels();
            return;
        }

        QuestManager.RefreshActiveQuestModules();
        interactionQuestStatusLabels.Clear();

        for (int i = questListContentRect.childCount - 1; i >= 0; i--)
        {
            Transform child = questListContentRect.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        CollectInteractionQuestModules();
        int renderedQuestCount = 0;
        foreach (QuestModuleBase quest in interactionQuestModules)
        {
            BuildInteractionQuestRow(quest, questListContentRect);
            renderedQuestCount++;
        }

        if (questListEmptyLabel != null)
            questListEmptyLabel.gameObject.SetActive(renderedQuestCount == 0);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(questListContentRect);

        if (questListScrollRect != null)
            questListScrollRect.verticalNormalizedPosition = 1f;

        interactionQuestListDirty = false;
    }

    private void CollectInteractionQuestModules()
    {
        interactionQuestModules.Clear();

        AddInteractionQuestModules(QuestModuleBase.KnownQuestModules);
        AddInteractionQuestModules(QuestManager.ActiveQuestModules);

        QuestModuleBase[] sceneQuestModules = FindObjectsByType<QuestModuleBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        AddInteractionQuestModules(sceneQuestModules);

        FetchQuestModule[] fetchQuestModules = FindObjectsByType<FetchQuestModule>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        AddInteractionQuestModules(fetchQuestModules);

        WorldObject[] worldObjects = FindObjectsByType<WorldObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (WorldObject worldObject in worldObjects)
        {
            if (worldObject != null)
                AddInteractionQuestModule(worldObject.fetchQuestModule);
        }
    }

    private void AddInteractionQuestModules(IEnumerable<QuestModuleBase> questModules)
    {
        if (questModules == null)
            return;

        foreach (QuestModuleBase questModule in questModules)
            AddInteractionQuestModule(questModule);
    }

    private void AddInteractionQuestModule(QuestModuleBase questModule)
    {
        if (questModule == null || interactionQuestModules.Contains(questModule))
            return;

        interactionQuestModules.Add(questModule);
    }

    private void BuildInteractionQuestRow(QuestModuleBase quest, Transform parent)
    {
        if (quest == null)
            return;

        GameObject rowObject = CreateUIObject($"{quest.QuestTitle}Row", parent);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        bool expanded = expandedInteractionQuestModules.Contains(quest);
        float rowHeight = expanded ? 270f : 70f;
        if (expanded && CanShowInteractionQuestAcceptButton(quest))
            rowHeight += 42f;
        rowRect.sizeDelta = new Vector2(0f, rowHeight);

        LayoutElement rowLayoutElement = rowObject.AddComponent<LayoutElement>();
        rowLayoutElement.preferredHeight = rowHeight;
        rowLayoutElement.minHeight = rowHeight;
        rowLayoutElement.flexibleHeight = 0f;

        Image rowImage = rowObject.AddComponent<Image>();
        rowImage.color = GetInteractionQuestRowColor(quest.Status);
        rowImage.raycastTarget = true;

        VerticalLayoutGroup rowLayout = rowObject.AddComponent<VerticalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.UpperLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.padding = new RectOffset(10, 10, 8, 8);
        rowLayout.spacing = 6f;

        BuildInteractionQuestHeader(quest, rowObject.transform);

        if (expanded)
            BuildInteractionQuestObjectiveList(quest, rowObject.transform);
    }

    private void BuildInteractionQuestHeader(QuestModuleBase quest, Transform parent)
    {
        Button headerButton = CreateInteractionQuestButton("QuestHeader", parent, string.Empty, () => ToggleInteractionQuestExpanded(quest));
        RectTransform headerRect = headerButton.GetComponent<RectTransform>();
        headerRect.sizeDelta = new Vector2(0f, 52f);
        SetInteractionQuestPreferredHeight(headerButton.gameObject, 52f);

        HorizontalLayoutGroup headerLayout = headerButton.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = false;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;
        headerLayout.spacing = 8f;

        TextMeshProUGUI expandLabel = CreateInteractionQuestLabel("ExpandIcon", headerButton.transform, 24f, new Color(0.98f, 0.93f, 0.78f, 1f), TextAlignmentOptions.Center);
        expandLabel.text = expandedInteractionQuestModules.Contains(quest) ? "v" : ">";
        expandLabel.rectTransform.sizeDelta = new Vector2(26f, 0f);

        TextMeshProUGUI titleLabel = CreateInteractionQuestLabel("QuestTitle", headerButton.transform, 19f, new Color(0.98f, 0.93f, 0.78f, 1f), TextAlignmentOptions.MidlineLeft);
        titleLabel.text = quest.QuestTitle;
        titleLabel.rectTransform.sizeDelta = new Vector2(255f, 0f);
        titleLabel.overflowMode = TextOverflowModes.Ellipsis;

        TextMeshProUGUI timerLabel = CreateInteractionQuestLabel("QuestTimer", headerButton.transform, 17f, new Color(0.84f, 0.95f, 1f, 1f), TextAlignmentOptions.MidlineRight);
        timerLabel.text = quest.HasCountdown ? FormatInteractionQuestCountdown(quest) : FormatInteractionQuestStatus(quest.Status);
        timerLabel.rectTransform.sizeDelta = new Vector2(110f, 0f);
        interactionQuestStatusLabels[quest] = timerLabel;
    }

    private void BuildInteractionQuestObjectiveList(QuestModuleBase quest, Transform parent)
    {
        if (!string.IsNullOrWhiteSpace(quest.QuestSummary))
        {
            TextMeshProUGUI summaryLabel = CreateInteractionQuestLabel("QuestSummary", parent, 16f, new Color(0.9f, 0.85f, 0.72f, 0.86f), TextAlignmentOptions.Left);
            summaryLabel.text = quest.QuestSummary;
            summaryLabel.textWrappingMode = TextWrappingModes.Normal;
            summaryLabel.rectTransform.sizeDelta = new Vector2(0f, 42f);
            SetInteractionQuestPreferredHeight(summaryLabel.gameObject, 42f);
        }

        foreach (QuestObjectiveSnapshot objective in quest.ObjectiveSnapshots)
        {
            TextMeshProUGUI objectiveLabel = CreateInteractionQuestLabel("Objective", parent, 16f, new Color(0.94f, 0.91f, 0.82f, 1f), TextAlignmentOptions.Left);
            string marker = objective.IsCompleted ? "[x]" : "[ ]";
            string prefix = objective.IsCurrent && !objective.IsCompleted ? "> " : "  ";
            objectiveLabel.text = $"{prefix}{marker} {objective.Description}";
            objectiveLabel.textWrappingMode = TextWrappingModes.NoWrap;
            objectiveLabel.overflowMode = TextOverflowModes.Ellipsis;
            objectiveLabel.rectTransform.sizeDelta = new Vector2(0f, 24f);
            SetInteractionQuestPreferredHeight(objectiveLabel.gameObject, 24f);
        }

        if (CanShowInteractionQuestAcceptButton(quest))
            BuildInteractionQuestAcceptButton(quest, parent);
    }

    private void BuildInteractionQuestAcceptButton(QuestModuleBase quest, Transform parent)
    {
        Button acceptButton = CreateInteractionQuestButton("AcceptQuestButton", parent, "Accept Quest", () => AcceptInteractionQuestFromDialog(quest));
        RectTransform acceptRect = acceptButton.GetComponent<RectTransform>();
        acceptRect.sizeDelta = new Vector2(0f, 36f);
        SetInteractionQuestPreferredHeight(acceptButton.gameObject, 36f);
    }

    private void ToggleInteractionQuestExpanded(QuestModuleBase quest)
    {
        if (expandedInteractionQuestModules.Contains(quest))
            expandedInteractionQuestModules.Remove(quest);
        else
            expandedInteractionQuestModules.Add(quest);

        interactionQuestListDirty = true;
        RefreshInteractionQuestList();
    }

    private void UpdateInteractionQuestHeaderLabels()
    {
        foreach (KeyValuePair<QuestModuleBase, TextMeshProUGUI> row in interactionQuestStatusLabels)
        {
            if (row.Key == null || row.Value == null)
                continue;

            row.Value.text = row.Key.HasCountdown ? FormatInteractionQuestCountdown(row.Key) : FormatInteractionQuestStatus(row.Key.Status);
        }
    }

    private void AcceptInteractionQuestFromDialog(QuestModuleBase quest)
    {
        if (quest == null)
            return;

        if (!TryGetInteractionQuestActorAndTarget(quest, out WorldObject actor, out WorldObject target))
            return;

        if (!IsInteractionQuestGiverNearby(actor, target))
        {
            BottomBanner.Show($"{target.DisplayName} is too far away.");
            interactionQuestListDirty = true;
            RefreshInteractionQuestList();
            return;
        }

        GameInputRouter router = GameInputRouter.Instance;
        GameMode gameMode = router != null ? router.currentGameMode : GameMode.Explore;
        Vector3 hitPoint = target.transform.position;

        var activateContext = new ActivateContext(
            userIsInstigator: true,
            instigator: actor,
            target: target,
            gameMode: gameMode,
            hitPoint: hitPoint,
            promoteTarget: true);

        ActivateResult result = target.Activate(activateContext, new ActivateRequest(ActivateKind.StartQuest));
        if (!string.IsNullOrWhiteSpace(result.message))
        {
            if (result.kind == ActivateResultKind.Accepted)
                BottomBanner.Show(result.message);
            else
                BottomBanner.Show($"Quest not accepted: {result.message}");
        }

        interactionQuestListDirty = true;
        RefreshInteractionQuestList();
    }

    private bool CanShowInteractionQuestAcceptButton(QuestModuleBase quest)
    {
        if (quest == null || !quest.CanStartFromQuestDialog)
            return false;

        return TryGetInteractionQuestActorAndTarget(quest, out WorldObject actor, out WorldObject target) &&
               IsInteractionQuestGiverNearby(actor, target);
    }

    private bool TryGetInteractionQuestActorAndTarget(QuestModuleBase quest, out WorldObject actor, out WorldObject target)
    {
        actor = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex) ?? GetCurrentControlledWorldObjectForInteractionQuest();
        target = quest != null ? quest.QuestInteractionTarget : null;
        return actor != null && target != null;
    }

    private bool IsInteractionQuestGiverNearby(WorldObject actor, WorldObject target)
    {
        if (actor == null || target == null)
            return false;

        Vector3 delta = actor.transform.position - target.transform.position;
        return delta.sqrMagnitude <= tradePartnerSearchRadiusTiles * tradePartnerSearchRadiusTiles;
    }

    private static WorldObject GetCurrentControlledWorldObjectForInteractionQuest()
    {
        GameInputRouter router = GameInputRouter.Instance;
        if (router != null && router.currentControlledWorldObject != null)
            return router.currentControlledWorldObject;

        Dir dir = Dir.Instance;
        return dir != null && dir.playerPack != null ? dir.playerPack.packLeader : null;
    }

    private static string FormatInteractionQuestCountdown(QuestModuleBase quest)
    {
        int totalSeconds = Mathf.CeilToInt(quest.CountdownRemainingSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        string label = string.IsNullOrWhiteSpace(quest.CountdownLabel) ? "Time" : quest.CountdownLabel;
        return $"{label} {minutes:00}:{seconds:00}";
    }

    private static string FormatInteractionQuestStatus(QuestRunStatus status)
    {
        return status switch
        {
            QuestRunStatus.Inactive => "Not started",
            QuestRunStatus.Running => "Active",
            QuestRunStatus.Succeeded => "Completed",
            QuestRunStatus.Failed => "Failed",
            QuestRunStatus.Cancelled => "Cancelled",
            _ => status.ToString()
        };
    }

    private static Color GetInteractionQuestRowColor(QuestRunStatus status)
    {
        return status switch
        {
            QuestRunStatus.Succeeded => new Color(0.095f, 0.15f, 0.105f, 0.92f),
            QuestRunStatus.Failed => new Color(0.17f, 0.08f, 0.065f, 0.92f),
            QuestRunStatus.Cancelled => new Color(0.12f, 0.105f, 0.105f, 0.92f),
            QuestRunStatus.Inactive => new Color(0.105f, 0.095f, 0.08f, 0.82f),
            _ => new Color(0.12f, 0.105f, 0.08f, 0.92f)
        };
    }

    private Button CreateInteractionQuestButton(string objectName, Transform parent, string text, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        Button button = buttonObject.AddComponent<Button>();
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.15f, 0.105f, 0.88f);
        button.targetGraphic = image;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.28f, 0.24f, 0.17f, 0.95f);
        colors.pressedColor = new Color(0.36f, 0.3f, 0.2f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        if (!string.IsNullOrEmpty(text))
        {
            TextMeshProUGUI label = CreateInteractionQuestLabel("Label", buttonObject.transform, 20f, new Color(0.98f, 0.93f, 0.78f, 1f), TextAlignmentOptions.Center);
            label.text = text;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
        }

        ConfigureTooltip(buttonObject, FormatTooltipText(string.IsNullOrEmpty(text) ? objectName : text));
        return button;
    }

    private static TextMeshProUGUI CreateInteractionQuestLabel(string objectName, Transform parent, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject labelObject = CreateUIObject(objectName, parent);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    private static void SetInteractionQuestPreferredHeight(GameObject uiObject, float height)
    {
        LayoutElement layoutElement = uiObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = uiObject.AddComponent<LayoutElement>();

        layoutElement.preferredHeight = height;
        layoutElement.minHeight = height;
        layoutElement.flexibleHeight = 0f;
    }

    private void RefreshPackMemberList()
    {
        if (packMemberListContentRect == null)
            return;

        ClearPackMemberListRows();

        if (packMemberOptions.Count <= 0)
        {
            CreatePackMemberListPlaceholder("No pack members");
            return;
        }

        for (int i = 0; i < packMemberOptions.Count; i++)
            CreatePackMemberListRow(packMemberOptions[i], i);

        RefreshPackMemberListHighlights();
        LayoutRebuilder.ForceRebuildLayoutImmediate(packMemberListContentRect);
        ScrollPackMemberListToSelection();
    }

    private void ClearPackMemberListRows()
    {
        packMemberListBackgrounds.Clear();

        if (packMemberListContentRect == null)
            return;

        for (int i = packMemberListContentRect.childCount - 1; i >= 0; i--)
        {
            Transform child = packMemberListContentRect.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    private void CreatePackMemberListRow(WorldObject member, int index)
    {
        GameObject rowObject = CreateUIObject($"PackMemberRow_{index}", packMemberListContentRect);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, PackMemberListRowHeight);

        LayoutElement layoutElement = rowObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = PackMemberListRowHeight;
        layoutElement.minHeight = PackMemberListRowHeight;

        Image background = rowObject.AddComponent<Image>();
        background.color = GetPackMemberListRowColor(index == selectedPackLeftIndex);
        background.raycastTarget = true;

        int capturedIndex = index;
        InteractionDialogPackMemberRowClickTrigger clickTrigger = rowObject.AddComponent<InteractionDialogPackMemberRowClickTrigger>();
        clickTrigger.Initialize(this, capturedIndex);

        GameObject labelObject = CreateUIObject("Label", rowObject.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 2f);
        labelRect.offsetMax = new Vector2(-12f, -2f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = GetPackMemberListLabelText(member);
        label.fontSize = 22f;
        label.color = new Color(1f, 0.88f, 0.58f, 1f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        packMemberListBackgrounds.Add(background);
        ConfigureTooltip(rowObject, $"Select {member.DisplayName}");
    }

    private void CreatePackMemberListPlaceholder(string text)
    {
        GameObject rowObject = CreateUIObject("PackMemberListPlaceholder", packMemberListContentRect);
        LayoutElement layoutElement = rowObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = PackMemberListRowHeight;
        layoutElement.minHeight = PackMemberListRowHeight;

        TextMeshProUGUI label = rowObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 22f;
        label.color = new Color(1f, 0.88f, 0.58f, 0.68f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
    }

    private void RefreshPackMemberListHighlights()
    {
        for (int i = 0; i < packMemberListBackgrounds.Count; i++)
            packMemberListBackgrounds[i].color = GetPackMemberListRowColor(i == selectedPackLeftIndex);
    }

    private static Color GetPackMemberListRowColor(bool selected)
    {
        return selected
            ? new Color(0.95f, 0.54f, 0.12f, 0.86f)
            : new Color(0.20f, 0.13f, 0.065f, 0.78f);
    }

    private static string GetPackMemberListLabelText(WorldObject member)
    {
        if (member == null)
            return string.Empty;

        return IsPlayerPackLeader(member)
            ? $"{member.DisplayName}  Leader"
            : member.DisplayName;
    }

    private void ScrollPackMemberListToSelection()
    {
        if (packMemberScrollRect == null)
            return;

        if (packMemberOptions.Count <= 1)
        {
            packMemberScrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        Canvas.ForceUpdateCanvases();
        float normalized = 1f - Mathf.Clamp01(selectedPackLeftIndex / (float)(packMemberOptions.Count - 1));
        packMemberScrollRect.verticalNormalizedPosition = normalized;
    }

    internal void OnPackMemberListRowClicked(int index)
    {
        if (index < 0 || index >= packMemberOptions.Count)
            return;

        AudioPlayer.PlayUiButtonClick();
        pendingLeftAgentSelection = packMemberOptions[index];
        selectedPackLeftIndex = index;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    internal void SelectPackMemberListRowAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        int rowIndex = GetPackMemberListRowIndexAtScreenPosition(screenPosition, eventCamera);
        if (rowIndex >= 0)
            OnPackMemberListRowClicked(rowIndex);
    }

    internal void BeginPackMemberListDrag(Vector2 screenPosition, Camera eventCamera)
    {
        if (!TryGetPackMemberListLocalPoint(screenPosition, eventCamera, out Vector2 localPoint))
            return;

        packMemberListDragStartLocalY = localPoint.y;
        packMemberListDragStartContentY = packMemberListContentRect != null
            ? packMemberListContentRect.anchoredPosition.y
            : 0f;
    }

    internal void DragPackMemberList(Vector2 screenPosition, Camera eventCamera)
    {
        if (!TryGetPackMemberListLocalPoint(screenPosition, eventCamera, out Vector2 localPoint))
            return;

        float dragDeltaY = localPoint.y - packMemberListDragStartLocalY;
        SetPackMemberListScrollOffset(packMemberListDragStartContentY + dragDeltaY);
    }

    internal void ScrollPackMemberList(Vector2 scrollDelta)
    {
        if (packMemberListContentRect == null || packMemberScrollRect == null)
            return;

        float currentOffset = packMemberListContentRect.anchoredPosition.y;
        SetPackMemberListScrollOffset(currentOffset - scrollDelta.y * packMemberScrollRect.scrollSensitivity);
    }

    private int GetPackMemberListRowIndexAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        if (packMemberOptions.Count <= 0 ||
            packMemberListViewportRect == null ||
            packMemberListContentRect == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(packMemberListViewportRect, screenPosition, eventCamera) ||
            !TryGetPackMemberListLocalPoint(screenPosition, eventCamera, out Vector2 localPoint))
        {
            return -1;
        }

        float visibleDistanceFromTop = packMemberListViewportRect.rect.height * 0.5f - localPoint.y;
        float contentDistanceFromTop = visibleDistanceFromTop + packMemberListContentRect.anchoredPosition.y;
        float rowOffset = contentDistanceFromTop - PackMemberListPadding;
        if (rowOffset < 0f)
            return -1;

        float stride = PackMemberListRowHeight + PackMemberListRowSpacing;
        int rowIndex = Mathf.FloorToInt(rowOffset / stride);
        float rowLocalY = rowOffset - rowIndex * stride;
        if (rowIndex < 0 || rowIndex >= packMemberOptions.Count || rowLocalY > PackMemberListRowHeight)
            return -1;

        return rowIndex;
    }

    private bool TryGetPackMemberListLocalPoint(Vector2 screenPosition, Camera eventCamera, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        return packMemberListViewportRect != null &&
               RectTransformUtility.ScreenPointToLocalPointInRectangle(
                   packMemberListViewportRect,
                   screenPosition,
                   eventCamera,
                   out localPoint);
    }

    private void SetPackMemberListScrollOffset(float offsetY)
    {
        if (packMemberListContentRect == null || packMemberListViewportRect == null)
            return;

        float maxOffset = GetPackMemberListMaxScrollOffset();
        Vector2 anchoredPosition = packMemberListContentRect.anchoredPosition;
        anchoredPosition.y = Mathf.Clamp(offsetY, 0f, maxOffset);
        packMemberListContentRect.anchoredPosition = anchoredPosition;

        if (packMemberScrollRect != null)
            packMemberScrollRect.StopMovement();
    }

    private float GetPackMemberListMaxScrollOffset()
    {
        if (packMemberListViewportRect == null || packMemberOptions.Count <= 0)
            return 0f;

        float contentHeight =
            PackMemberListPadding * 2f +
            packMemberOptions.Count * PackMemberListRowHeight +
            Mathf.Max(0, packMemberOptions.Count - 1) * PackMemberListRowSpacing;
        return Mathf.Max(0f, contentHeight - packMemberListViewportRect.rect.height);
    }

    private void SetPackIndicatorButtonsActive(bool active)
    {
        if (playerPackIndicatorButton != null)
            playerPackIndicatorButton.gameObject.SetActive(active);
        if (targetPackIndicatorButton != null)
            targetPackIndicatorButton.gameObject.SetActive(active);
    }

    private void RefreshPackIndicatorSlot(PreviewSlot slot, WorldObject member)
    {
        if (slot == null)
            return;

        if (slot.Image != null)
            slot.Image.gameObject.SetActive(false);

        if (slot.CircleImage == null)
            return;

        bool hasMember = member != null;
        slot.CircleImage.gameObject.SetActive(hasMember);
        if (!hasMember)
            return;

        slot.CircleImage.sprite = GetPackIndicatorSprite(member);
        slot.CircleImage.preserveAspect = true;
        slot.CircleImage.color = Color.white;
        slot.CircleImage.rectTransform.sizeDelta = slot.CircleSize;
    }

    private static void RefreshPackIndicatorButton(Button button, WorldObject member)
    {
        if (button != null)
            button.interactable = member != null && !IsPlayerPackLeader(member);
    }

    private void RefreshPackMembershipButtons(WorldObject member)
    {
        bool inPlayerPack = IsInPlayerPack(member);
        bool isLeader = IsPlayerPackLeader(member);

        SetPackActionButtonInteractable(setLeaderButton, inPlayerPack && !isLeader);
        SetPackActionButtonInteractable(joinPackButton, member != null && !inPlayerPack);
        SetPackActionButtonInteractable(leavePackButton, inPlayerPack);
    }

    private static void SetPackActionButtonInteractable(Button button, bool interactable)
    {
        if (button == null)
            return;

        button.interactable = interactable;

        Image image = button.targetGraphic as Image;
        if (image != null)
            image.color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.36f);
    }

    private static Sprite GetPackIndicatorSprite(WorldObject member)
    {
        int spriteIndex;
        if (IsPlayerPackLeader(member))
            spriteIndex = 18;
        else if (IsInPlayerPack(member))
            spriteIndex = 19;
        else
            spriteIndex = 20;

        return SpriteServer.SpriteSheetLookup("Sprites/PackFormationsSprites_C", spriteIndex);
    }

    private void SetItemSelectionTypeLabelsActive(bool active)
    {
        if (playerItemSelectionTypeLabel != null)
            playerItemSelectionTypeLabel.gameObject.SetActive(active);
        if (targetItemSelectionTypeLabel != null)
            targetItemSelectionTypeLabel.gameObject.SetActive(active);
    }

    private static void SetPreviewSlotActive(PreviewSlot slot, bool active)
    {
        if (slot == null)
            return;

        if (slot.CircleImage != null)
            slot.CircleImage.gameObject.SetActive(active);
        if (slot.Image != null)
            slot.Image.gameObject.SetActive(active);
    }

    private void RefreshTabHighlights()
    {
        if (socialTabHighlight != null)
            socialTabHighlight.gameObject.SetActive(currentTab == InteractionTab.Social);
        if (questsTabHighlight != null)
            questsTabHighlight.gameObject.SetActive(currentTab == InteractionTab.Quests);
        if (packTabHighlight != null)
            packTabHighlight.gameObject.SetActive(currentTab == InteractionTab.Pack);
        if (itemsTabHighlight != null)
            itemsTabHighlight.gameObject.SetActive(currentTab == InteractionTab.Items);
        if (scentTabHighlight != null)
            scentTabHighlight.gameObject.SetActive(currentTab == InteractionTab.Scent);
        if (titleLabel != null)
            titleLabel.text = currentTab.ToString().ToUpperInvariant();
        if (targetSelectionTypeLabel != null)
            targetSelectionTypeLabel.text = GetTargetSelectionTypeLabel();
    }

    private string GetTargetSelectionTypeLabel()
    {
        return currentTab switch
        {
            InteractionTab.Social => "Nearby Agent",
            InteractionTab.Pack => "Pack/Nearby",
            InteractionTab.Items => "Nearby Agent",
            InteractionTab.Quests => "Quest Giver",
            InteractionTab.Scent => "Scent Source",
            _ => string.Empty
        };
    }

    private void OnSocialTabClicked()
    {
        if (currentTab == InteractionTab.Social)
            return;

        PreserveCurrentAgentsForTabSwitch();
        currentTab = InteractionTab.Social;
        displayedSocialLeft = null;
        displayedSocialRight = null;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnQuestsTabClicked()
    {
        if (currentTab == InteractionTab.Quests)
            return;

        PreserveCurrentAgentsForTabSwitch();
        currentTab = InteractionTab.Quests;
        displayedQuestLeft = null;
        displayedQuestRight = null;
        interactionQuestListDirty = true;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnPackTabClicked()
    {
        if (currentTab == InteractionTab.Pack)
            return;

        PreserveCurrentAgentsForTabSwitch();
        currentTab = InteractionTab.Pack;
        displayedPackLeft = null;
        displayedPackRight = null;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnItemsTabClicked()
    {
        if (currentTab == InteractionTab.Items)
            return;

        PreserveCurrentAgentsForTabSwitch();
        currentTab = InteractionTab.Items;
        displayedPlayer = null;
        displayedPlayerItem = null;
        displayedTarget = null;
        displayedTargetItem = null;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnScentTabClicked()
    {
        if (currentTab == InteractionTab.Scent)
            return;

        PreserveCurrentAgentsForTabSwitch();
        currentTab = InteractionTab.Scent;
        displayedScentLeft = null;
        displayedScentRight = null;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void PreserveCurrentAgentsForTabSwitch()
    {
        WorldObject leftAgent = displayedPlayer ?? displayedPackLeft ?? displayedSocialLeft ?? displayedQuestLeft ?? displayedScentLeft;
        WorldObject rightAgent = displayedTarget ?? displayedPackRight ?? displayedSocialRight ?? displayedQuestRight ?? displayedScentRight;

        pendingLeftAgentSelection = leftAgent;
        pendingRightAgentSelection = rightAgent;

        RememberSelection(playerAgentOptions, leftAgent, ref selectedPlayerAgentIndex);
        RememberSelection(packMemberOptions, leftAgent, ref selectedPackLeftIndex);
        RememberSelection(targetAgentOptions, rightAgent, ref selectedTargetAgentIndex);
        RememberSelection(socialTargetOptions, rightAgent, ref selectedSocialTargetIndex);
        RememberSelection(questTargetOptions, rightAgent, ref selectedQuestTargetIndex);
        RememberSelection(scentTargetOptions, rightAgent, ref selectedScentTargetIndex);
        RememberSelection(packRightOptions, rightAgent, ref selectedPackRightIndex);
    }

    private void RefreshCircleAndHotspot(PreviewSlot slot, int optionCount, Button previousButton, Button nextButton)
    {
        bool hasArrows = optionCount > 1;
        if (slot != null && slot.CircleImage != null)
        {
            RectTransform circleRect = slot.CircleImage.rectTransform;
            slot.CircleImage.sprite = hasArrows && circleWithArrowsSprite != null
                ? circleWithArrowsSprite
                : circleSprite;
            slot.CircleImage.preserveAspect = true;
            circleRect.sizeDelta = hasArrows ? slot.CircleWithArrowsSize : slot.CircleSize;
        }

        if (previousButton != null)
            previousButton.gameObject.SetActive(hasArrows);
        if (nextButton != null)
            nextButton.gameObject.SetActive(hasArrows);
    }

    private static WorldObject GetSelectedFromList(List<WorldObject> options, ref int selectedIndex)
    {
        if (options.Count <= 0)
        {
            selectedIndex = 0;
            return null;
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, options.Count - 1);
        return options[selectedIndex];
    }

    private static void SetLabelText(TextMeshProUGUI label, string text)
    {
        if (label != null)
            label.text = text ?? string.Empty;
    }

    private void BuildPlayerAgentOptions()
    {
        WorldObject previousSelection = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
        playerAgentOptions.Clear();

        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        if (playerPack != null && playerPack.packAgentList != null)
        {
            for (int i = 0; i < playerPack.packAgentList.Count; i++)
            {
                WorldObject agent = playerPack.packAgentList[i];
                if (agent != null && agent.gameObject.activeInHierarchy)
                    playerAgentOptions.Add(agent);
            }
        }

        Dir dir = Dir.Instance;
        WorldObject packLeader = dir != null && dir.playerPack != null ? dir.playerPack.packLeader : null;
        if (playerAgentOptions.Count == 0 && packLeader != null)
            playerAgentOptions.Add(packLeader);

        KeepSelectedObject(playerAgentOptions, previousSelection, ref selectedPlayerAgentIndex);
    }

    private void BuildPackMemberOptions()
    {
        WorldObject previousLeft = GetSelectedFromList(packMemberOptions, ref selectedPackLeftIndex);
        packMemberOptions.Clear();

        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        if (playerPack != null && playerPack.packAgentList != null)
        {
            for (int i = 0; i < playerPack.packAgentList.Count; i++)
            {
                WorldObject agent = playerPack.packAgentList[i];
                if (agent != null && agent.gameObject.activeInHierarchy)
                    packMemberOptions.Add(agent);
            }
        }

        Dir dir = Dir.Instance;
        WorldObject packLeader = dir != null && dir.playerPack != null ? dir.playerPack.packLeader : null;
        if (packMemberOptions.Count == 0 && packLeader != null)
            packMemberOptions.Add(packLeader);

        KeepSelectedObject(packMemberOptions, previousLeft, ref selectedPackLeftIndex);
    }

    private void BuildPackRightOptions(WorldObject leftMember)
    {
        WorldObject previousRight = GetSelectedFromList(packRightOptions, ref selectedPackRightIndex);
        packRightOptions.Clear();

        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        if (playerPack != null && playerPack.packAgentList != null)
        {
            for (int i = 0; i < playerPack.packAgentList.Count; i++)
            {
                WorldObject agent = playerPack.packAgentList[i];
                if (agent != null && agent != leftMember && agent.gameObject.activeInHierarchy)
                    packRightOptions.Add(agent);
            }
        }

        AddNearbyPackRightAgents(leftMember);
        KeepSelectedObject(packRightOptions, previousRight, ref selectedPackRightIndex);
    }

    private void AddNearbyPackRightAgents(WorldObject leftMember)
    {
        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (leftMember == null || registry == null)
            return;

        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        float radius = tradePartnerSearchRadiusTiles * socialNearbyRadiusMultiplier;
        float radiusSqr = radius * radius;
        Vector3 leftPosition = leftMember.pos3d_map;

        foreach (WorldObject candidate in registry.GetAllObjects())
        {
            if (candidate == null || candidate == leftMember || !candidate.gameObject.activeInHierarchy)
                continue;

            if (!CanUseAsSocialTarget(candidate))
                continue;

            if (candidate.packMemberModule != null && candidate.packMemberModule.currentPack == playerPack)
                continue;

            Vector3 delta = candidate.pos3d_map - leftPosition;
            delta.y = 0f;
            if (delta.sqrMagnitude > radiusSqr)
                continue;

            if (!packRightOptions.Contains(candidate))
                packRightOptions.Add(candidate);
        }

        packRightOptions.Sort((a, b) =>
        {
            bool aInPlayerPack = IsInPlayerPack(a);
            bool bInPlayerPack = IsInPlayerPack(b);
            if (aInPlayerPack != bInPlayerPack)
                return aInPlayerPack ? -1 : 1;

            float aDistanceSqr = GetPlanarDistanceSqr(leftPosition, a.pos3d_map);
            float bDistanceSqr = GetPlanarDistanceSqr(leftPosition, b.pos3d_map);
            int distanceComparison = aDistanceSqr.CompareTo(bDistanceSqr);
            if (distanceComparison != 0)
                return distanceComparison;

            return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
        });
    }

    private void EnsurePackRightSelection(WorldObject leftMember)
    {
        if (packMemberOptions.Count <= 1)
        {
            selectedPackRightIndex = 0;
            return;
        }

        selectedPackLeftIndex = Mathf.Clamp(selectedPackLeftIndex, 0, packMemberOptions.Count - 1);
        selectedPackRightIndex = Mathf.Clamp(selectedPackRightIndex, 0, packMemberOptions.Count - 1);
        if (GetSelectedFromList(packMemberOptions, ref selectedPackRightIndex) != leftMember)
            return;

        selectedPackRightIndex = FindNextPackMemberIndex(selectedPackRightIndex, 1, selectedPackLeftIndex);
    }

    private int FindNextPackMemberIndex(int currentIndex, int direction, int skipIndex)
    {
        if (packMemberOptions.Count <= 0)
            return 0;

        if (packMemberOptions.Count == 1)
            return 0;

        int nextIndex = currentIndex;
        for (int i = 0; i < packMemberOptions.Count; i++)
        {
            nextIndex = (nextIndex + direction + packMemberOptions.Count) % packMemberOptions.Count;
            if (nextIndex != skipIndex)
                return nextIndex;
        }

        return currentIndex;
    }

    private static void BuildItemOptions(WorldObject carrier, List<WorldObject> options)
    {
        options.Clear();

        ContainerModule container = GetOrCreateContainer(carrier);
        if (container == null || container.HeldItemCount <= 0)
            return;

        for (int i = 0; i < container.HeldItemCount; i++)
        {
            WorldObject item = container.HeldItems[i];
            if (item != null)
                options.Add(item);
        }
    }

    private void BuildTargetAgentOptions(WorldObject player)
    {
        WorldObject previousSelection = GetSelectedFromList(targetAgentOptions, ref selectedTargetAgentIndex);
        targetAgentOptions.Clear();

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (player == null || registry == null)
        {
            selectedTargetAgentIndex = 0;
            return;
        }

        float radiusSqr = tradePartnerSearchRadiusTiles * tradePartnerSearchRadiusTiles;
        Vector3 playerPosition = player.pos3d_map;

        foreach (WorldObject candidate in registry.GetAllObjects())
        {
            if (candidate == null || candidate == player || !candidate.gameObject.activeInHierarchy)
                continue;

            if (!CanUseAsTradeTarget(candidate))
                continue;

            Vector3 delta = candidate.pos3d_map - playerPosition;
            delta.y = 0f;
            float distanceSqr = delta.sqrMagnitude;
            if (distanceSqr > radiusSqr)
                continue;

            targetAgentOptions.Add(candidate);
        }

        targetAgentOptions.Sort((a, b) =>
        {
            float aDistanceSqr = GetPlanarDistanceSqr(playerPosition, a.pos3d_map);
            float bDistanceSqr = GetPlanarDistanceSqr(playerPosition, b.pos3d_map);
            int distanceComparison = aDistanceSqr.CompareTo(bDistanceSqr);
            if (distanceComparison != 0)
                return distanceComparison;

            return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
        });

        KeepSelectedObject(targetAgentOptions, previousSelection, ref selectedTargetAgentIndex);
    }

    private void BuildSocialTargetOptions(WorldObject player)
    {
        WorldObject previousSelection = GetSelectedFromList(socialTargetOptions, ref selectedSocialTargetIndex);
        socialTargetOptions.Clear();

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (player == null || registry == null)
        {
            selectedSocialTargetIndex = 0;
            return;
        }

        float radius = tradePartnerSearchRadiusTiles * socialNearbyRadiusMultiplier;
        float radiusSqr = radius * radius;
        Vector3 playerPosition = player.pos3d_map;

        foreach (WorldObject candidate in registry.GetAllObjects())
        {
            if (candidate == null || candidate == player || !candidate.gameObject.activeInHierarchy)
                continue;

            if (!CanUseAsSocialTarget(candidate))
                continue;

            Vector3 delta = candidate.pos3d_map - playerPosition;
            delta.y = 0f;
            float distanceSqr = delta.sqrMagnitude;
            if (distanceSqr > radiusSqr)
                continue;

            socialTargetOptions.Add(candidate);
        }

        socialTargetOptions.Sort((a, b) =>
        {
            float aDistanceSqr = GetPlanarDistanceSqr(playerPosition, a.pos3d_map);
            float bDistanceSqr = GetPlanarDistanceSqr(playerPosition, b.pos3d_map);
            int distanceComparison = aDistanceSqr.CompareTo(bDistanceSqr);
            if (distanceComparison != 0)
                return distanceComparison;

            return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
        });

        KeepSelectedObject(socialTargetOptions, previousSelection, ref selectedSocialTargetIndex);
    }

    private void BuildQuestTargetOptions(WorldObject player)
    {
        WorldObject previousSelection = GetSelectedFromList(questTargetOptions, ref selectedQuestTargetIndex);
        questTargetOptions.Clear();

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (player == null || registry == null)
        {
            selectedQuestTargetIndex = 0;
            return;
        }

        float radiusSqr = tradePartnerSearchRadiusTiles * tradePartnerSearchRadiusTiles;
        Vector3 playerPosition = player.pos3d_map;

        foreach (WorldObject candidate in registry.GetAllObjects())
        {
            if (candidate == null || candidate == player || !candidate.gameObject.activeInHierarchy)
                continue;

            if (!CanUseAsQuestTarget(candidate))
                continue;

            Vector3 delta = candidate.pos3d_map - playerPosition;
            delta.y = 0f;
            float distanceSqr = delta.sqrMagnitude;
            if (distanceSqr > radiusSqr)
                continue;

            questTargetOptions.Add(candidate);
        }

        questTargetOptions.Sort((a, b) =>
        {
            float aDistanceSqr = GetPlanarDistanceSqr(playerPosition, a.pos3d_map);
            float bDistanceSqr = GetPlanarDistanceSqr(playerPosition, b.pos3d_map);
            int distanceComparison = aDistanceSqr.CompareTo(bDistanceSqr);
            if (distanceComparison != 0)
                return distanceComparison;

            return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
        });

        KeepSelectedObject(questTargetOptions, previousSelection, ref selectedQuestTargetIndex);
    }

    private void BuildScentTargetOptions(WorldObject player)
    {
        WorldObject previousSelection = GetSelectedFromList(scentTargetOptions, ref selectedScentTargetIndex);
        scentTargetOptions.Clear();

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (player == null || registry == null)
        {
            selectedScentTargetIndex = 0;
            return;
        }

        Vector3 playerPosition = player.pos3d_map;

        foreach (WorldObject candidate in registry.GetAllObjects())
        {
            if (candidate == null || candidate == player || !candidate.gameObject.activeInHierarchy)
                continue;

            if (!CanUseAsScentTarget(candidate))
                continue;

            scentTargetOptions.Add(candidate);
        }

        scentTargetOptions.Sort((a, b) =>
        {
            float aDistanceSqr = GetPlanarDistanceSqr(playerPosition, a.pos3d_map);
            float bDistanceSqr = GetPlanarDistanceSqr(playerPosition, b.pos3d_map);
            int distanceComparison = aDistanceSqr.CompareTo(bDistanceSqr);
            if (distanceComparison != 0)
                return distanceComparison;

            return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
        });

        KeepSelectedObject(scentTargetOptions, previousSelection, ref selectedScentTargetIndex);
    }

    private static float GetPlanarDistanceSqr(Vector3 first, Vector3 second)
    {
        Vector3 delta = second - first;
        delta.y = 0f;
        return delta.sqrMagnitude;
    }

    private static void KeepSelectedObject(List<WorldObject> options, WorldObject previousSelection, ref int selectedIndex)
    {
        if (previousSelection == null)
        {
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, options.Count - 1));
            return;
        }

        int foundIndex = options.IndexOf(previousSelection);
        selectedIndex = foundIndex >= 0
            ? foundIndex
            : Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, options.Count - 1));
    }

    private static void RememberSelection(List<WorldObject> options, WorldObject selection, ref int selectedIndex)
    {
        if (selection == null || options.Count <= 0)
            return;

        int foundIndex = options.IndexOf(selection);
        if (foundIndex >= 0)
            selectedIndex = foundIndex;
    }

    private static void ApplyPendingSelection(List<WorldObject> options, WorldObject selection, ref int selectedIndex)
    {
        if (selection == null)
            return;

        RememberSelection(options, selection, ref selectedIndex);
    }

    private void ClearPendingSelections()
    {
        pendingLeftAgentSelection = null;
        pendingRightAgentSelection = null;
    }

    private static bool CanUseAsTradeTarget(WorldObject candidate)
    {
        if (candidate == null)
            return false;

        return candidate.containerModule != null &&
               candidate.containerModule.itemCapacity > 0;
    }

    private static bool CanUseAsSocialTarget(WorldObject candidate)
    {
        if (candidate == null)
            return false;

        return candidate.Kind == WorldObjectKind.Agent ||
               candidate.agentModule != null ||
               candidate.GetComponent<AgentModule>() != null;
    }

    private static bool CanUseAsQuestTarget(WorldObject candidate)
    {
        if (candidate == null || !CanUseAsSocialTarget(candidate))
            return false;

        return candidate.hasAnyQuestModule();
    }

    private static bool CanUseAsScentTarget(WorldObject candidate)
    {
        if (candidate == null)
            return false;

        return candidate.scentEmitterModule != null;
    }

    private static bool IsInPlayerPack(WorldObject candidate)
    {
        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        return candidate != null &&
               candidate.packMemberModule != null &&
               candidate.packMemberModule.currentPack == playerPack;
    }

    private static bool IsPlayerPackLeader(WorldObject candidate)
    {
        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        return candidate != null && playerPack != null && playerPack.packLeader == candidate;
    }

    private void OnPreviousPlayerAgentClicked()
    {
        if (currentTab == InteractionTab.Pack)
        {
            CyclePackLeftSelection(-1);
            return;
        }

        if (currentTab == InteractionTab.Social)
        {
            CycleSocialLeftSelection(-1);
            return;
        }

        if (currentTab == InteractionTab.Quests)
        {
            CycleQuestLeftSelection(-1);
            return;
        }

        if (currentTab == InteractionTab.Scent)
        {
            CycleScentLeftSelection(-1);
            return;
        }

        CycleSelection(playerAgentOptions, ref selectedPlayerAgentIndex, -1);
        selectedPlayerItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnNextPlayerAgentClicked()
    {
        if (currentTab == InteractionTab.Pack)
        {
            CyclePackLeftSelection(1);
            return;
        }

        if (currentTab == InteractionTab.Social)
        {
            CycleSocialLeftSelection(1);
            return;
        }

        if (currentTab == InteractionTab.Quests)
        {
            CycleQuestLeftSelection(1);
            return;
        }

        if (currentTab == InteractionTab.Scent)
        {
            CycleScentLeftSelection(1);
            return;
        }

        CycleSelection(playerAgentOptions, ref selectedPlayerAgentIndex, 1);
        selectedPlayerItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnPreviousPlayerItemClicked()
    {
        CycleSelection(playerItemOptions, ref selectedPlayerItemIndex, -1);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnNextPlayerItemClicked()
    {
        CycleSelection(playerItemOptions, ref selectedPlayerItemIndex, 1);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnPreviousTargetAgentClicked()
    {
        if (currentTab == InteractionTab.Pack)
        {
            CyclePackRightSelection(-1);
            return;
        }

        if (currentTab == InteractionTab.Social)
        {
            CycleSocialRightSelection(-1);
            return;
        }

        if (currentTab == InteractionTab.Quests)
        {
            CycleQuestRightSelection(-1);
            return;
        }

        if (currentTab == InteractionTab.Scent)
        {
            CycleScentRightSelection(-1);
            return;
        }

        CycleSelection(targetAgentOptions, ref selectedTargetAgentIndex, -1);
        selectedTargetItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnNextTargetAgentClicked()
    {
        if (currentTab == InteractionTab.Pack)
        {
            CyclePackRightSelection(1);
            return;
        }

        if (currentTab == InteractionTab.Social)
        {
            CycleSocialRightSelection(1);
            return;
        }

        if (currentTab == InteractionTab.Quests)
        {
            CycleQuestRightSelection(1);
            return;
        }

        if (currentTab == InteractionTab.Scent)
        {
            CycleScentRightSelection(1);
            return;
        }

        CycleSelection(targetAgentOptions, ref selectedTargetAgentIndex, 1);
        selectedTargetItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnPreviousTargetItemClicked()
    {
        CycleSelection(targetItemOptions, ref selectedTargetItemIndex, -1);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnNextTargetItemClicked()
    {
        CycleSelection(targetItemOptions, ref selectedTargetItemIndex, 1);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void CyclePackLeftSelection(int direction)
    {
        BuildPackMemberOptions();
        if (packMemberOptions.Count <= 1)
            return;

        int previousLeftIndex = Mathf.Clamp(selectedPackLeftIndex, 0, packMemberOptions.Count - 1);
        selectedPackLeftIndex = FindNextPackMemberIndex(selectedPackLeftIndex, direction, -1);
        selectedPackRightIndex = previousLeftIndex;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void CyclePackRightSelection(int direction)
    {
        BuildPackMemberOptions();
        WorldObject leftMember = GetSelectedFromList(packMemberOptions, ref selectedPackLeftIndex);
        BuildPackRightOptions(leftMember);
        if (packRightOptions.Count <= 1)
            return;

        CycleSelection(packRightOptions, ref selectedPackRightIndex, direction);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void CycleSocialLeftSelection(int direction)
    {
        BuildPlayerAgentOptions();
        if (playerAgentOptions.Count <= 1)
            return;

        CycleSelection(playerAgentOptions, ref selectedPlayerAgentIndex, direction);
        selectedSocialTargetIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void CycleSocialRightSelection(int direction)
    {
        BuildPlayerAgentOptions();
        WorldObject player = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
        BuildSocialTargetOptions(player);
        if (socialTargetOptions.Count <= 1)
            return;

        CycleSelection(socialTargetOptions, ref selectedSocialTargetIndex, direction);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void CycleQuestLeftSelection(int direction)
    {
        BuildPlayerAgentOptions();
        if (playerAgentOptions.Count <= 1)
            return;

        CycleSelection(playerAgentOptions, ref selectedPlayerAgentIndex, direction);
        selectedQuestTargetIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void CycleQuestRightSelection(int direction)
    {
        BuildPlayerAgentOptions();
        WorldObject player = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
        BuildQuestTargetOptions(player);
        if (questTargetOptions.Count <= 1)
            return;

        CycleSelection(questTargetOptions, ref selectedQuestTargetIndex, direction);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void CycleScentLeftSelection(int direction)
    {
        BuildPlayerAgentOptions();
        if (playerAgentOptions.Count <= 1)
            return;

        CycleSelection(playerAgentOptions, ref selectedPlayerAgentIndex, direction);
        selectedScentTargetIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void CycleScentRightSelection(int direction)
    {
        BuildPlayerAgentOptions();
        WorldObject player = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
        BuildScentTargetOptions(player);
        if (scentTargetOptions.Count <= 1)
            return;

        CycleSelection(scentTargetOptions, ref selectedScentTargetIndex, direction);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnUseClicked()
    {
        WorldObject user = displayedPlayer;
        WorldObject item = displayedPlayerItem;
        ContainerModule container = GetOrCreateContainer(user);
        if (user == null)
            return;

        if (item == null)
        {
            ShowInteractionMessage($"{user.DisplayName} has no item to use");
            return;
        }

        if (item.activatorModule == null)
        {
            ShowInteractionMessage($"{item.DisplayName} cannot be used");
            return;
        }

        ActivatorModule activator = item.activatorModule;
        string itemName = item.DisplayName;
        bool success = activator.TryUseItem(user, displayedTarget);
        if (success && activator.parameterDestruct)
        {
            if (container != null && !container.ReleaseItem(item, out string reason))
            {
                ShowInteractionMessage(reason);
                Debug.LogWarning($"InteractionDialogUI: failed to destroy used item {itemName}: {reason}", this);
                RefreshInteractionView(forcePreviewRefresh: true);
                return;
            }

            Destroy(item.gameObject);
        }

        ShowInteractionMessage(success
            ? $"{user.DisplayName} used {itemName}"
            : $"{user.DisplayName} could not use {itemName}");

        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnEatClicked()
    {
        WorldObject eater = displayedPlayer;
        ContainerModule container = GetOrCreateContainer(eater);
        if (eater == null || container == null)
            return;

        WorldObject item = displayedPlayerItem;
        if (item == null)
        {
            ShowInteractionMessage($"{eater.DisplayName} has no item to eat");
            return;
        }

        string itemName = item.DisplayName;
        if (!container.ReleaseItem(item, out string reason))
        {
            ShowInteractionMessage(reason);
            Debug.LogWarning($"InteractionDialogUI: failed to eat {itemName}: {reason}", this);
            return;
        }

        Destroy(item.gameObject);
        ShowInteractionMessage($"{eater.DisplayName} ate {itemName}");
        selectedPlayerItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnDropClicked()
    {
        WorldObject carrier = displayedPlayer;
        WorldObject item = displayedPlayerItem;
        ContainerModule container = GetOrCreateContainer(carrier);
        if (carrier == null || item == null || container == null)
            return;

        if (!TryDropItemNearCarrier(container, carrier, item, out string reason))
        {
            ShowInteractionMessage(reason);
            Debug.LogWarning($"InteractionDialogUI: failed to drop {item.DisplayName}: {reason}", this);
            return;
        }

        ShowInteractionMessage($"{carrier.DisplayName} dropped {item.DisplayName}");
        selectedPlayerItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnThrowClicked()
    {
        WorldObject carrier = displayedPlayer;
        WorldObject item = displayedPlayerItem;
        ContainerModule container = GetOrCreateContainer(carrier);
        if (carrier == null || item == null || container == null)
            return;

        if (!TryThrowItemFromCarrier(container, carrier, item, out string reason))
        {
            ShowInteractionMessage(reason);
            Debug.LogWarning($"InteractionDialogUI: failed to throw {item.DisplayName}: {reason}", this);
            return;
        }

        ShowInteractionMessage($"{carrier.DisplayName} threw {item.DisplayName}");
        selectedPlayerItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnPickUpClicked()
    {
        WorldObject carrier = displayedPlayer;
        ContainerModule container = GetOrCreateContainer(carrier);
        if (carrier == null || container == null)
            return;

        if (!container.TryPickupNearestItem(out WorldObject pickedUpItem, out string reason))
        {
            ShowInteractionMessage(reason);
            return;
        }

        ShowInteractionMessage($"{carrier.DisplayName} picked up {pickedUpItem.DisplayName}");
        selectedPlayerItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnGiveClicked()
    {
        WorldObject giver = displayedPlayer;
        WorldObject item = displayedPlayerItem;
        WorldObject recipient = displayedTarget;
        ContainerModule giverContainer = GetOrCreateContainer(giver);
        ContainerModule recipientContainer = GetOrCreateContainer(recipient);

        if (giver == null || giverContainer == null)
            return;

        if (item == null)
        {
            ShowInteractionMessage($"{giver.DisplayName} has no item to give");
            return;
        }

        if (recipient == null || recipientContainer == null)
        {
            ShowInteractionMessage("No one nearby to give an item to");
            return;
        }

        if (TransferItem(giverContainer, recipientContainer, item, out string reason))
        {
            ShowInteractionMessage($"{giver.DisplayName} gave {item.DisplayName} to {recipient.DisplayName}");
            selectedPlayerItemIndex = 0;
            selectedTargetItemIndex = 0;
            RefreshInteractionView(forcePreviewRefresh: true);
            return;
        }

        ShowInteractionMessage(reason);
        Debug.LogWarning($"InteractionDialogUI: failed to give {item.DisplayName}: {reason}", this);
    }

    private void OnTakeItemClicked()
    {
        WorldObject taker = displayedPlayer;
        WorldObject giver = displayedTarget;
        WorldObject item = displayedTargetItem;
        ContainerModule takerContainer = GetOrCreateContainer(taker);
        ContainerModule giverContainer = GetOrCreateContainer(giver);

        if (taker == null || takerContainer == null)
            return;

        if (giver == null || giverContainer == null)
        {
            ShowInteractionMessage("No one nearby to take an item from");
            return;
        }

        if (item == null)
        {
            ShowInteractionMessage($"{giver.DisplayName} has no selected item to take");
            return;
        }

        if (TransferItem(giverContainer, takerContainer, item, out string reason))
        {
            ShowInteractionMessage($"{taker.DisplayName} took {item.DisplayName} from {giver.DisplayName}");
            selectedTargetItemIndex = 0;
            selectedPlayerItemIndex = 0;
            RefreshInteractionView(forcePreviewRefresh: true);
            return;
        }

        ShowInteractionMessage(reason);
        Debug.LogWarning($"InteractionDialogUI: failed to take {item.DisplayName}: {reason}", this);
    }

    private void OnTradeClicked()
    {
        WorldObject trader = displayedPlayer;
        WorldObject partner = displayedTarget;
        WorldObject traderItem = displayedPlayerItem;
        WorldObject partnerItem = displayedTargetItem;
        ContainerModule traderContainer = GetOrCreateContainer(trader);
        ContainerModule partnerContainer = GetOrCreateContainer(partner);

        if (trader == null || traderContainer == null)
            return;

        if (partner == null || partnerContainer == null)
        {
            ShowInteractionMessage("No one nearby to trade with");
            return;
        }

        if (traderItem == null)
        {
            if (partnerItem != null)
            {
                OnTakeItemClicked();
                return;
            }

            ShowInteractionMessage($"{trader.DisplayName} has no item to trade");
            return;
        }

        if (partnerItem == null)
        {
            OnGiveClicked();
            return;
        }

        if (SwapItems(traderContainer, partnerContainer, traderItem, partnerItem, out string reason))
        {
            ShowInteractionMessage($"{trader.DisplayName} traded {traderItem.DisplayName} to {partner.DisplayName} for {partnerItem.DisplayName}");
            RefreshInteractionView(forcePreviewRefresh: true);
            return;
        }

        ShowInteractionMessage(reason);
        Debug.LogWarning($"InteractionDialogUI: failed to trade {traderItem.DisplayName} for {partnerItem.DisplayName}: {reason}", this);
    }

    private void OnPackBehaviorClicked(AgentDecisionType decisionType)
    {
        WorldObject member = GetSelectedPackRightMember();
        if (member == null)
        {
            ShowInteractionMessage("No agent selected");
            return;
        }

        if (decisionType == AgentDecisionType.Player && !TrySelectPackMemberForPlayerControl(member))
        {
            ShowInteractionMessage($"{member.DisplayName} could not be controlled");
            return;
        }

        if (member.agentModule == null)
            member.CreateModulesIfNeeded(ModuleFlags.agentModule);

        if (member.agentModule == null)
        {
            ShowInteractionMessage($"{member.DisplayName} cannot change behavior");
            Debug.LogWarning($"InteractionDialogUI: {member.DisplayName} has no AgentModule for pack behavior {decisionType}.", member);
            return;
        }

        member.agentModule.SwitchDecisionModule(decisionType);
        ShowInteractionMessage($"{member.DisplayName} behavior set to {GetPackBehaviorDisplayName(decisionType)}");
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private bool TrySelectPackMemberForPlayerControl(WorldObject member)
    {
        GameInputRouter router = Dir.Instance != null ? Dir.Instance.gameInputRouter : null;
        if (router == null)
            router = GameInputRouter.Instance;

        if (router == null)
            return false;

        return router.TrySelectControlledAgent(member);
    }

    private void OnSetPackLeaderClicked()
    {
        WorldObject member = GetSelectedPackRightMember();
        PromotePackIndicatorSelection(member);
    }

    private void OnPlayerPackIndicatorClicked()
    {
        PromotePackIndicatorSelection(GetSelectedPackLeftMember());
    }

    private void OnTargetPackIndicatorClicked()
    {
        WorldObject member = GetSelectedPackRightMember();
        if (member != null && !IsInPlayerPack(member))
        {
            JoinPackFromIndicatorSelection(member);
            return;
        }

        PromotePackIndicatorSelection(member);
    }

    private void OnJoinPackClicked()
    {
        JoinPackFromIndicatorSelection(GetSelectedPackRightMember());
    }

    private void JoinPackFromIndicatorSelection(WorldObject member)
    {
        if (member == null)
        {
            ShowInteractionMessage("No agent selected");
            return;
        }

        if (!TryJoinPlayerPackTail(member, out string reason))
        {
            ShowInteractionMessage(reason);
            Debug.LogWarning($"InteractionDialogUI: failed to add {member.DisplayName} to player pack: {reason}", member);
            return;
        }

        ShowInteractionMessage($"{member.DisplayName} joined the pack");
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void PromotePackIndicatorSelection(WorldObject member)
    {
        if (member == null)
        {
            ShowInteractionMessage("No agent selected");
            return;
        }

        if (IsPlayerPackLeader(member))
        {
            ShowInteractionMessage($"{member.DisplayName} is already pack leader");
            return;
        }

        if (!TryPromoteToPlayerPackLeader(member, out string reason))
        {
            ShowInteractionMessage(reason);
            Debug.LogWarning($"InteractionDialogUI: failed to promote {member.DisplayName} to player pack leader: {reason}", member);
            return;
        }

        selectedPackLeftIndex = 0;
        selectedPackRightIndex = 0;
        ShowInteractionMessage($"{member.DisplayName} is pack leader");
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private static bool TryPromoteToPlayerPackLeader(WorldObject member, out string reason)
    {
        reason = string.Empty;
        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        if (member == null)
        {
            reason = "No agent selected.";
            return false;
        }

        if (playerPack == null)
        {
            reason = "No player pack available.";
            return false;
        }

        if (member.agentModule == null || member.packMemberModule == null)
            member.CreateModulesIfNeeded(ModuleFlagsTemplates.FullAgent);

        if (member.packMemberModule == null)
        {
            reason = $"{member.DisplayName} cannot join a pack.";
            return false;
        }

        Pack currentPack = member.packMemberModule.currentPack;
        if (currentPack != null && currentPack != playerPack && !member.packMemberModule.LeaveCurrentPack())
        {
            reason = $"{member.DisplayName} could not leave {currentPack.packName}.";
            return false;
        }

        bool changed = playerPack.AddMember(member, setAsLeader: true);
        if (!changed && playerPack.packLeader != member)
        {
            reason = $"{member.DisplayName} could not become leader.";
            return false;
        }

        if (playerPack.packLeader == member)
            playerPack.SetPackFollowChain();

        return playerPack.packLeader == member;
    }

    private static bool TryJoinPlayerPackTail(WorldObject member, out string reason)
    {
        reason = string.Empty;
        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        if (member == null)
        {
            reason = "No agent selected.";
            return false;
        }

        if (playerPack == null)
        {
            reason = "No player pack available.";
            return false;
        }

        if (member.agentModule == null || member.packMemberModule == null)
            member.CreateModulesIfNeeded(ModuleFlagsTemplates.FullAgent);

        if (member.packMemberModule == null)
        {
            reason = $"{member.DisplayName} cannot join a pack.";
            return false;
        }

        Pack currentPack = member.packMemberModule.currentPack;
        if (currentPack == playerPack)
            return true;

        if (currentPack != null && !member.packMemberModule.LeaveCurrentPack())
        {
            reason = $"{member.DisplayName} could not leave {currentPack.packName}.";
            return false;
        }

        bool changed = playerPack.AddMember(member, setAsLeader: false);
        if (!changed && !IsInPlayerPack(member))
        {
            reason = $"{member.DisplayName} could not join the pack.";
            return false;
        }

        playerPack.SetPackFollowChain();
        return IsInPlayerPack(member);
    }

    private void OnLeavePackClicked()
    {
        WorldObject member = GetSelectedPackRightMember();
        PackMemberModule packMember = member != null ? member.packMemberModule : null;
        if (member == null || packMember == null)
        {
            ShowInteractionMessage("No pack member selected");
            return;
        }

        if (!IsInPlayerPack(member))
        {
            ShowInteractionMessage($"{member.DisplayName} is not in the pack");
            return;
        }

        if (!packMember.LeaveCurrentPack())
        {
            ShowInteractionMessage($"{member.DisplayName} cannot leave the pack");
            return;
        }

        ShowInteractionMessage($"{member.DisplayName} left the pack");
        selectedPackLeftIndex = 0;
        selectedPackRightIndex = 1;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnPackFormationClicked(FormationsEnum formation)
    {
        Pack pack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        if (pack == null)
        {
            ShowInteractionMessage("No player pack available");
            return;
        }

        pack.SetFormation(formation);
        ShowInteractionMessage($"Pack formation set to {GetPackFormationDisplayName(formation)}");
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnUnimplementedPackActionClicked()
    {
        ShowInteractionMessage("That pack command is not implemented yet");
    }

    private WorldObject GetSelectedPackLeftMember()
    {
        if (displayedPackLeft != null)
            return displayedPackLeft;

        BuildPackMemberOptions();
        return GetSelectedFromList(packMemberOptions, ref selectedPackLeftIndex);
    }

    private WorldObject GetSelectedPackRightMember()
    {
        if (displayedPackRight != null)
            return displayedPackRight;

        BuildPackMemberOptions();
        WorldObject leftMember = GetSelectedFromList(packMemberOptions, ref selectedPackLeftIndex);
        BuildPackRightOptions(leftMember);
        return GetSelectedFromList(packRightOptions, ref selectedPackRightIndex);
    }

    private static string GetPackBehaviorDisplayName(AgentDecisionType decisionType)
    {
        return decisionType switch
        {
            AgentDecisionType.Player => "Take Control",
            AgentDecisionType.Follower => "Regroup",
            AgentDecisionType.Immobile => "Wait Here",
            AgentDecisionType.Wanderer => "Patrol Room",
            AgentDecisionType.Explorer => "Explore",
            AgentDecisionType.TaskFollower => "AI",
            _ => decisionType.ToString()
        };
    }

    private static string GetPackFormationDisplayName(FormationsEnum formation)
    {
        return formation switch
        {
            FormationsEnum.LineAbreast => "Abreast",
            FormationsEnum.TwoColums => "Two Columns",
            FormationsEnum.Wedge => "Wedge",
            FormationsEnum.Circle => "Circle",
            FormationsEnum.SingleFile => "Follow",
            _ => formation.ToString()
        };
    }

    private static void CycleSelection(List<WorldObject> options, ref int selectedIndex, int direction)
    {
        if (options.Count <= 1)
            return;

        selectedIndex = (selectedIndex + direction + options.Count) % options.Count;
    }

    private static void ShowInteractionMessage(string message)
    {
        BottomBanner.LogInventoryMessage(message);
    }

    private static bool TryDropItemNearCarrier(ContainerModule source, WorldObject carrier, WorldObject item, out string reason)
    {
        if (source == null)
        {
            reason = "Source inventory is unavailable.";
            return false;
        }

        if (carrier == null)
        {
            reason = "No carrier selected.";
            return false;
        }

        if (item == null)
        {
            reason = "No item selected.";
            return false;
        }

        return source.DropItemOnGround(item, GetDropPositionNearCarrier(carrier, item), out reason);
    }

    private static Vector3 GetDropPositionNearCarrier(WorldObject carrier, WorldObject item)
    {
        Vector3 dropDirection = carrier.transform.forward;
        dropDirection.y = 0f;
        if (dropDirection.sqrMagnitude < 0.001f)
            dropDirection = Vector3.forward;
        dropDirection.Normalize();

        float itemRadius = item != null ? item.sizeRadius : 0f;
        float dropDistance = Mathf.Max(0.65f, carrier.sizeRadius + itemRadius + 0.2f);
        Vector3 dropPosition = carrier.transform.position + dropDirection * dropDistance;
        dropPosition.y = carrier.transform.position.y;
        return dropPosition;
    }

    private bool TryThrowItemFromCarrier(ContainerModule source, WorldObject carrier, WorldObject item, out string reason)
    {
        if (source == null)
        {
            reason = "Source inventory is unavailable.";
            return false;
        }

        if (carrier == null)
        {
            reason = "No carrier selected.";
            return false;
        }

        if (item == null)
        {
            reason = "No item selected.";
            return false;
        }

        Vector3 direction = GetFacingDirection(carrier);
        KineticModule kinetic = EnsureKineticModule(item);
        if (kinetic == null)
        {
            reason = $"{item.DisplayName} could not add a KineticModule.";
            return false;
        }

        Vector3 releasePosition = GetThrowReleasePosition(carrier, item, direction);
        if (!source.DropItemOnGround(item, releasePosition, out reason))
            return false;

        kinetic.Stop();
        kinetic.ApplyImpulse((direction * throwForwardImpulse) + (Vector3.up * throwUpwardImpulse));
        NotifyFetchQuestModulesObjectThrown(item, carrier);
        reason = string.Empty;
        return true;
    }

    private static Vector3 GetFacingDirection(WorldObject carrier)
    {
        Vector3 direction = carrier.transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.forward;

        return direction.normalized;
    }

    private Vector3 GetThrowReleasePosition(WorldObject carrier, WorldObject item, Vector3 direction)
    {
        float itemRadius = item != null ? item.sizeRadius : 0f;
        float releaseDistance = Mathf.Max(0.65f, carrier.sizeRadius + itemRadius + 0.2f);
        Vector3 releasePosition = carrier.transform.position + direction * releaseDistance;
        releasePosition.y = carrier.transform.position.y + throwReleaseHeight;
        return releasePosition;
    }

    private static KineticModule EnsureKineticModule(WorldObject item)
    {
        if (item == null)
            return null;

        KineticModule kinetic = item.kineticModule != null
            ? item.kineticModule
            : item.GetComponent<KineticModule>();

        if (kinetic != null)
            return kinetic;

        item.CreateModulesIfNeeded(ModuleFlags.kineticModule);
        return item.kineticModule != null
            ? item.kineticModule
            : item.GetComponent<KineticModule>();
    }

    private static void NotifyFetchQuestModulesObjectThrown(WorldObject thrownItem, WorldObject thrower)
    {
        if (thrownItem == null)
            return;

        if (thrower != null && thrower.fetchQuestModule is FetchQuestModule throwerFetchQuest)
            throwerFetchQuest.ObserveObjectThrown(thrownItem);

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (registry == null)
            return;

        foreach (WorldObject candidate in registry.GetAllObjects())
        {
            if (candidate == null || candidate == thrower)
                continue;

            if (candidate.fetchQuestModule is FetchQuestModule fetchQuest && fetchQuest.IsRunning)
                fetchQuest.ObserveObjectThrown(thrownItem);
        }
    }

    private static bool TransferItem(ContainerModule source, ContainerModule destination, WorldObject item, out string reason)
    {
        if (source == null)
        {
            reason = "Source inventory is unavailable.";
            return false;
        }

        if (destination == null)
        {
            reason = "Destination inventory is unavailable.";
            return false;
        }

        if (source == destination)
        {
            reason = "Cannot transfer an item to the same inventory.";
            return false;
        }

        if (item == null)
        {
            reason = "No item selected.";
            return false;
        }

        if (!source.ReleaseItem(item, out reason))
            return false;

        if (destination.ReceiveItem(item, false, out reason))
            return true;

        string receiveFailure = reason;
        if (!source.ReceiveItem(item, false, out string rollbackReason))
            reason = $"{receiveFailure} {item.DisplayName} could not be returned: {rollbackReason}";
        else
            reason = receiveFailure;

        return false;
    }

    private static bool SwapItems(ContainerModule firstContainer, ContainerModule secondContainer, WorldObject firstItem, WorldObject secondItem, out string reason)
    {
        if (firstContainer == null || secondContainer == null)
        {
            reason = "One of the inventories is unavailable.";
            return false;
        }

        if (firstContainer == secondContainer)
        {
            reason = "Cannot trade within the same inventory.";
            return false;
        }

        if (firstItem == null || secondItem == null)
        {
            reason = "Both sides need an item selected to trade.";
            return false;
        }

        if (!firstContainer.ReleaseItem(firstItem, out reason))
            return false;

        if (!secondContainer.ReleaseItem(secondItem, out string secondReleaseReason))
        {
            RestoreItem(firstContainer, firstItem);
            reason = secondReleaseReason;
            return false;
        }

        bool firstReceivedSecond = firstContainer.ReceiveItem(secondItem, false, out string firstReceiveReason);
        bool secondReceivedFirst = secondContainer.ReceiveItem(firstItem, false, out string secondReceiveReason);

        if (firstReceivedSecond && secondReceivedFirst)
        {
            reason = string.Empty;
            return true;
        }

        if (firstReceivedSecond)
            firstContainer.ReleaseItem(secondItem, out _);

        if (secondReceivedFirst)
            secondContainer.ReleaseItem(firstItem, out _);

        RestoreItem(firstContainer, firstItem);
        RestoreItem(secondContainer, secondItem);

        reason = !firstReceivedSecond ? firstReceiveReason : secondReceiveReason;
        return false;
    }

    private static void RestoreItem(ContainerModule container, WorldObject item)
    {
        if (container != null && item != null)
            container.ReceiveItem(item, false, out _);
    }

    private static ContainerModule GetOrCreateContainer(WorldObject owner)
    {
        if (owner == null)
            return null;

        if (owner.containerModule == null)
            owner.CreateModulesIfNeeded(ModuleFlags.containerModule);

        return owner.containerModule;
    }

    private void EnsurePreviewWorld(PreviewSlot slot)
    {
        if (slot == null || slot.WorldRoot != null)
            return;

        slot.WorldRoot = new GameObject($"{slot.Image.name}World");
        slot.WorldRoot.hideFlags = HideFlags.HideAndDontSave;
        slot.WorldRoot.transform.position = slot.AnchorPosition;

        GameObject cameraObject = new("PreviewCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.transform.SetParent(slot.WorldRoot.transform, false);
        slot.Camera = cameraObject.AddComponent<Camera>();
        slot.Camera.enabled = false;
        slot.Camera.clearFlags = CameraClearFlags.SolidColor;
        slot.Camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        slot.Camera.orthographic = true;
        slot.Camera.nearClipPlane = 0.01f;
        slot.Camera.farClipPlane = 100f;
        slot.Camera.cullingMask = 1 << PreviewRenderLayer;
        slot.Camera.allowHDR = false;
        slot.Camera.allowMSAA = false;

        GameObject lightObject = new("PreviewLight");
        lightObject.hideFlags = HideFlags.HideAndDontSave;
        lightObject.transform.SetParent(slot.WorldRoot.transform, false);
        slot.Light = lightObject.AddComponent<Light>();
        slot.Light.type = LightType.Directional;
        slot.Light.intensity = 1.25f;
        slot.Light.color = Color.white;
        slot.Light.shadows = LightShadows.None;
        slot.Light.cullingMask = 1 << PreviewRenderLayer;
        slot.Light.transform.rotation = Quaternion.Euler(35f, 135f, 0f);

        EnsurePreviewTexture(slot);
    }

    private static void EnsurePreviewTexture(PreviewSlot slot)
    {
        if (slot == null || slot.Texture != null)
            return;

        slot.Texture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        slot.Texture.name = $"{slot.Image.name}RT";
        slot.Texture.useMipMap = false;
        slot.Texture.autoGenerateMips = false;
        slot.Texture.Create();

        slot.Image.texture = slot.Texture;
        slot.Camera.targetTexture = slot.Texture;
        ClearPreviewTexture(slot);
    }

    private void BuildPreviewClone(PreviewSlot slot, WorldObject worldObject, string label)
    {
        if (slot == null)
            return;

        DestroyPreviewClone(slot);
        PurgePreviewWorldRenderables(slot);
        slot.DisplayedObject = worldObject;

        if (worldObject == null)
        {
            if (slot.Image != null)
            {
                slot.Image.texture = null;
                slot.Image.enabled = false;
            }

            ClearPreviewTexture(slot);
            return;
        }

        EnsurePreviewWorld(slot);
        if (slot.Image != null)
        {
            slot.Image.texture = slot.Texture;
            slot.Image.enabled = true;
        }

        slot.Clone = CreateVisualClone(worldObject.gameObject);
        slot.Clone.name = $"{worldObject.name}_Interaction{label}Preview";
        slot.Clone.hideFlags = HideFlags.HideAndDontSave;
        slot.Clone.transform.SetParent(slot.WorldRoot.transform, false);
        slot.Clone.transform.position = slot.AnchorPosition;
        SetLayerRecursive(slot.Clone, PreviewRenderLayer);

        CenterPreviewClone(slot);
        RenderPreview(slot);
    }

    private static void CenterPreviewClone(PreviewSlot slot)
    {
        Renderer[] renderers = slot.Clone.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            slot.FramingRadius = 1f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        slot.Clone.transform.position += slot.AnchorPosition - bounds.center;

        Bounds centeredBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            centeredBounds.Encapsulate(renderers[i].bounds);

        slot.FramingRadius = Mathf.Max(centeredBounds.extents.y, Mathf.Max(centeredBounds.extents.x, centeredBounds.extents.z));
        if (slot.FramingRadius < 0.1f)
            slot.FramingRadius = 0.5f;
    }

    private void SpinPreview(PreviewSlot slot)
    {
        if (slot == null || slot.Clone == null)
            return;

        slot.Clone.transform.RotateAround(
            slot.AnchorPosition,
            Vector3.up,
            previewSpinDegreesPerSecond * Time.unscaledDeltaTime);

        RenderPreview(slot);
    }

    private void RenderPreview(PreviewSlot slot)
    {
        if (slot == null || slot.Camera == null)
            return;

        float distance = Mathf.Max(2f, slot.FramingRadius * 4f);
        float cameraHeight = Mathf.Tan(previewViewAngleDegrees * Mathf.Deg2Rad) * distance;
        slot.Camera.transform.position = slot.AnchorPosition + new Vector3(0f, cameraHeight, -distance);
        slot.Camera.transform.LookAt(slot.AnchorPosition + new Vector3(0f, slot.FramingRadius * 0.1f, 0f));
        slot.Camera.orthographicSize = slot.FramingRadius * slot.OrthographicPadding;
        ClearPreviewTexture(slot);
        slot.Camera.Render();
    }

    private static void PurgePreviewWorldRenderables(PreviewSlot slot)
    {
        if (slot == null || slot.WorldRoot == null)
            return;

        for (int i = slot.WorldRoot.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = slot.WorldRoot.transform.GetChild(i);
            if (child == null ||
                child == (slot.Camera != null ? slot.Camera.transform : null) ||
                child == (slot.Light != null ? slot.Light.transform : null))
            {
                continue;
            }

            child.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private static void ClearPreviewTexture(PreviewSlot slot)
    {
        if (slot == null || slot.Texture == null)
            return;

        RenderTexture previousActiveTexture = RenderTexture.active;
        RenderTexture.active = slot.Texture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = previousActiveTexture;
    }

    private static GameObject CreateVisualClone(GameObject sourceRoot)
    {
        Dictionary<Transform, Transform> transformMap = new();
        HashSet<Transform> skippedTransforms = new();
        WorldObject sourceRootWorldObject = sourceRoot.GetComponent<WorldObject>();

        GameObject cloneRoot = new(sourceRoot.name);
        CopyTransform(sourceRoot.transform, cloneRoot.transform);
        transformMap[sourceRoot.transform] = cloneRoot.transform;

        Transform[] sourceTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < sourceTransforms.Length; i++)
        {
            Transform source = sourceTransforms[i];
            if (ShouldSkipPreviewCloneTransform(source, sourceRoot.transform, sourceRootWorldObject) ||
                skippedTransforms.Contains(source.parent))
            {
                skippedTransforms.Add(source);
                continue;
            }

            GameObject child = new(source.name);
            Transform childTransform = child.transform;
            childTransform.SetParent(transformMap[source.parent], false);
            CopyTransform(source, childTransform);
            transformMap[source] = childTransform;
        }

        for (int i = 0; i < sourceTransforms.Length; i++)
        {
            Transform source = sourceTransforms[i];
            if (!transformMap.TryGetValue(source, out Transform destination))
                continue;

            MeshFilter sourceMeshFilter = source.GetComponent<MeshFilter>();
            MeshRenderer sourceMeshRenderer = source.GetComponent<MeshRenderer>();
            if (sourceMeshFilter != null && sourceMeshRenderer != null)
                CopyMeshRenderer(sourceMeshFilter, sourceMeshRenderer, destination.gameObject);

            SkinnedMeshRenderer sourceSkinnedRenderer = source.GetComponent<SkinnedMeshRenderer>();
            if (sourceSkinnedRenderer != null)
                CopySkinnedMeshRenderer(sourceSkinnedRenderer, destination.gameObject, transformMap);
        }

        Renderer[] renderers = cloneRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
            renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
        }

        return cloneRoot;
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++)
            SetLayerRecursive(rootTransform.GetChild(i).gameObject, layer);
    }

    private static bool ShouldSkipPreviewCloneTransform(
        Transform source,
        Transform sourceRoot,
        WorldObject sourceRootWorldObject)
    {
        if (source == null || source == sourceRoot)
            return false;

        if (source.GetComponentInParent<EmoteIconSpinner>() != null)
            return true;

        for (Transform current = source; current != null && current != sourceRoot; current = current.parent)
        {
            string objectName = current.name;
            if (objectName == EmoteIconVisualInstanceName || objectName == QuestIconVisualInstanceName)
                return true;
        }

        WorldObject[] parentWorldObjects = source.GetComponentsInParent<WorldObject>(true);
        for (int i = 0; i < parentWorldObjects.Length; i++)
        {
            WorldObject parentWorldObject = parentWorldObjects[i];
            if (parentWorldObject != null && parentWorldObject != sourceRootWorldObject)
                return true;
        }

        return false;
    }

    private static void CopyTransform(Transform source, Transform destination)
    {
        destination.localPosition = source.localPosition;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    private static void CopyMeshRenderer(MeshFilter sourceFilter, MeshRenderer sourceRenderer, GameObject destination)
    {
        MeshFilter destinationFilter = destination.AddComponent<MeshFilter>();
        destinationFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer destinationRenderer = destination.AddComponent<MeshRenderer>();
        destinationRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        destinationRenderer.enabled = true;
    }

    private static void CopySkinnedMeshRenderer(
        SkinnedMeshRenderer sourceRenderer,
        GameObject destination,
        Dictionary<Transform, Transform> transformMap)
    {
        SkinnedMeshRenderer destinationRenderer = destination.AddComponent<SkinnedMeshRenderer>();
        destinationRenderer.sharedMesh = sourceRenderer.sharedMesh;
        destinationRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        destinationRenderer.enabled = true;
        destinationRenderer.localBounds = sourceRenderer.localBounds;
        destinationRenderer.updateWhenOffscreen = sourceRenderer.updateWhenOffscreen;
        destinationRenderer.rootBone = sourceRenderer.rootBone != null && transformMap.TryGetValue(sourceRenderer.rootBone, out Transform mappedRootBone)
            ? mappedRootBone
            : null;

        Transform[] sourceBones = sourceRenderer.bones;
        Transform[] destinationBones = new Transform[sourceBones.Length];
        for (int i = 0; i < sourceBones.Length; i++)
        {
            Transform bone = sourceBones[i];
            if (bone != null && transformMap.TryGetValue(bone, out Transform mappedBone))
                destinationBones[i] = mappedBone;
        }

        destinationRenderer.bones = destinationBones;
    }

    private static void DestroyPreviewClone(PreviewSlot slot)
    {
        if (slot == null || slot.Clone == null)
            return;

        slot.Clone.SetActive(false);

        if (Application.isPlaying)
            Destroy(slot.Clone);
        else
            DestroyImmediate(slot.Clone);

        slot.Clone = null;
    }

    private static void ReleasePreviewSlot(PreviewSlot slot)
    {
        if (slot == null)
            return;

        DestroyPreviewClone(slot);

        if (slot.Camera != null)
            slot.Camera.targetTexture = null;

        if (slot.Image != null)
            slot.Image.texture = null;

        if (slot.Texture != null)
        {
            slot.Texture.Release();
            if (Application.isPlaying)
                Destroy(slot.Texture);
            else
                DestroyImmediate(slot.Texture);
        }

        if (slot.WorldRoot != null)
        {
            if (Application.isPlaying)
                Destroy(slot.WorldRoot);
            else
                DestroyImmediate(slot.WorldRoot);
        }

        slot.Texture = null;
        slot.WorldRoot = null;
        slot.Camera = null;
        slot.Light = null;
        slot.DisplayedObject = null;
    }

    private static void CreateTabLabel(Transform parent, string objectName, string text, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject labelObject = CreateUIObject(objectName, parent);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 1f);
        labelRect.anchorMax = new Vector2(0.5f, 1f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = anchoredPosition;
        labelRect.sizeDelta = size;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 42f;
        label.color = new Color(1f, 0.62f, 0.08f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private static string GetInteractionButtonTooltipText(string objectName)
    {
        return objectName switch
        {
            "CloseButton" => "Close",
            "SocialTabButton" => "Social",
            "PackTabButton" => "Pack",
            "ItemsTabButton" => "Items",
            "QuestsTabButton" => "Quests",
            "ScentTabButton" => "Scent",
            "GiveHotspot" => "Give Item",
            "ExchangeHotspot" => "Trade Items",
            "TakeHotspot" => "Take Item",
            "PreviousPlayerAgentButton" => "Previous Left Agent",
            "NextPlayerAgentButton" => "Next Left Agent",
            "PreviousPlayerItemButton" => "Previous Left Item",
            "NextPlayerItemButton" => "Next Left Item",
            "PreviousTargetAgentButton" => "Previous Right Agent",
            "NextTargetAgentButton" => "Next Right Agent",
            "PreviousTargetItemButton" => "Previous Right Item",
            "NextTargetItemButton" => "Next Right Item",
            "PlayerPackIndicatorButton" => "Left Pack Status",
            "TargetPackIndicatorButton" => "Right Pack Status",
            _ => FormatTooltipText(objectName)
        };
    }

    private static string FormatTooltipText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string trimmed = text.Trim()
            .Replace("_", " ")
            .Replace("-", " ")
            .Replace("Button", string.Empty)
            .Replace("Hotspot", string.Empty);
        if (trimmed == "AI")
            return "AI";

        System.Text.StringBuilder builder = new();
        char previous = '\0';
        for (int i = 0; i < trimmed.Length; i++)
        {
            char current = trimmed[i];
            if (i > 0 &&
                char.IsUpper(current) &&
                previous != ' ' &&
                !char.IsUpper(previous))
            {
                builder.Append(' ');
            }

            builder.Append(current);
            previous = current;
        }

        string spaced = builder.ToString();
        System.Text.StringBuilder title = new(spaced.Length);
        bool capitalizeNext = true;
        for (int i = 0; i < spaced.Length; i++)
        {
            char current = spaced[i];
            if (char.IsWhiteSpace(current))
            {
                title.Append(' ');
                capitalizeNext = true;
                continue;
            }

            title.Append(capitalizeNext ? char.ToUpperInvariant(current) : char.ToLowerInvariant(current));
            capitalizeNext = false;
        }

        return title.ToString().Trim();
    }

    private Button CreateInvisibleButton(string objectName, Transform parent, UnityEngine.Events.UnityAction clickHandler)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(clickHandler);
        ConfigureTooltip(buttonObject, GetInteractionButtonTooltipText(objectName));
        return button;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject uiObject = new(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }
}

public static class InteractionDialogBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInteractionDialogExists()
    {
        if (UnityEngine.Object.FindFirstObjectByType<InteractionDialogUI>() != null)
            return;

        GameObject interactionDialogObject = new("InteractionDialogUI");
        interactionDialogObject.AddComponent<InteractionDialogUI>();
    }
}

sealed class InteractionDialogTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    private InteractionDialogUI owner;

    public string TooltipText { get; private set; }

    public void Initialize(InteractionDialogUI owner, string tooltipText)
    {
        this.owner = owner;
        TooltipText = tooltipText;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.ShowTooltip(this, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        owner?.MoveTooltip(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.HideTooltip(this);
    }

    private void OnDisable()
    {
        owner?.HideTooltip(this);
    }
}

sealed class InteractionDialogInputBlocker :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IScrollHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
    }

    public void OnPointerClick(PointerEventData eventData)
    {
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnScroll(PointerEventData eventData)
    {
    }
}

sealed class InteractionDialogPackMemberRowClickTrigger : MonoBehaviour, IPointerClickHandler
{
    private InteractionDialogUI owner;
    private int rowIndex;

    public void Initialize(InteractionDialogUI owner, int rowIndex)
    {
        this.owner = owner;
        this.rowIndex = rowIndex;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        owner?.OnPackMemberListRowClicked(rowIndex);
    }
}

sealed class InteractionDialogPackMemberListHitArea :
    MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IScrollHandler
{
    private InteractionDialogUI owner;
    private bool suppressNextClick;

    public void Initialize(InteractionDialogUI owner)
    {
        this.owner = owner;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null ||
            eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (suppressNextClick)
        {
            suppressNextClick = false;
            return;
        }

        owner?.SelectPackMemberListRowAtScreenPosition(eventData.position, eventData.pressEventCamera);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        owner?.BeginPackMemberListDrag(eventData.position, eventData.pressEventCamera);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        suppressNextClick = true;
        owner?.DragPackMemberList(eventData.position, eventData.pressEventCamera);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        owner?.ScrollPackMemberList(eventData.scrollDelta);
    }
}

sealed class InteractionDialogScentSourceListHitArea :
    MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IScrollHandler
{
    public void Initialize(InteractionDialogUI owner)
    {
    }

    public void OnPointerClick(PointerEventData eventData)
    {
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
    }

    public void OnScroll(PointerEventData eventData)
    {
    }
}
