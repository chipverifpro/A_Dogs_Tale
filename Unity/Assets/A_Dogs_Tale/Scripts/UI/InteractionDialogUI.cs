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

    private Sprite interactionFrameSprite;
    private Canvas overlayCanvas;
    private CanvasScaler overlayCanvasScaler;
    private RectTransform dialogRect;
    private GameObject dialogRoot;
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
    private readonly List<WorldObject> socialTargetOptions = new();
    private readonly List<WorldObject> questTargetOptions = new();
    private readonly List<WorldObject> scentTargetOptions = new();
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
        SpinPreview(playerPreviewSlot);
        SpinPreview(playerItemPreviewSlot);
        SpinPreview(targetPreviewSlot);
        SpinPreview(targetItemPreviewSlot);
    }

    private void OnDestroy()
    {
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

        RefreshInteractionView(forcePreviewRefresh: true);
    }

    public void Hide()
    {
        bool wasOpen = isOpen;

        isOpen = false;

        if (dialogRoot != null)
            dialogRoot.SetActive(false);

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

        BuildPreviewSlots(dialogRoot.transform);
        BuildPackIndicatorButtons(dialogRoot.transform);
        BuildHeader(dialogRoot.transform);
        BuildTopInfo(dialogRoot.transform);
        BuildTradeArrows(dialogRoot.transform);
        BuildTabLabels(dialogRoot.transform);
        BuildSelectionArrows(dialogRoot.transform);
        BuildActionButtons(dialogRoot.transform);
        BuildPackActionButtons(dialogRoot.transform);
        BuildCloseHotspot(dialogRoot.transform);
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
        return button;
    }

    private void BuildActionButtons(Transform parent)
    {
        actionPanelObject = CreateUIObject("ActionPanel", parent);
        RectTransform actionPanelRect = actionPanelObject.GetComponent<RectTransform>();
        actionPanelRect.anchorMin = new Vector2(0.5f, 1f);
        actionPanelRect.anchorMax = new Vector2(0.5f, 1f);
        actionPanelRect.pivot = new Vector2(0.5f, 0.5f);
        actionPanelRect.anchoredPosition = new Vector2(0f, -680f);
        actionPanelRect.sizeDelta = new Vector2(880f, 260f);

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
        actionPanelRect.anchoredPosition = new Vector2(0f, -690f);
        actionPanelRect.sizeDelta = new Vector2(1120f, 300f);

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
        SetPackIndicatorButtonsActive(false);
        SetItemsControlsActive(false);
        SetPreviewSlotActive(playerItemPreviewSlot, false);
        SetPreviewSlotActive(targetItemPreviewSlot, false);
        SetItemSelectionTypeLabelsActive(false);

        BuildPlayerAgentOptions();
        ApplyPendingSelection(playerAgentOptions, pendingLeftAgentSelection, ref selectedPlayerAgentIndex);
        WorldObject leftMember = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
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
        SetPackIndicatorButtonsActive(false);
        SetItemsControlsActive(false);
        SetPreviewSlotActive(playerItemPreviewSlot, false);
        SetPreviewSlotActive(targetItemPreviewSlot, false);
        SetItemSelectionTypeLabelsActive(false);

        BuildPlayerAgentOptions();
        ApplyPendingSelection(playerAgentOptions, pendingLeftAgentSelection, ref selectedPlayerAgentIndex);
        WorldObject leftMember = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
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
        SetPackControlsActive(true);
        SetPackIndicatorButtonsActive(true);
        SetItemSelectionTypeLabelsActive(false);

        BuildPackMemberOptions();
        ApplyPendingSelection(packMemberOptions, pendingLeftAgentSelection, ref selectedPackLeftIndex);
        WorldObject leftMember = GetSelectedFromList(packMemberOptions, ref selectedPackLeftIndex);
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
    }

    private void SetPackControlsActive(bool active)
    {
        if (packActionPanelObject != null)
            packActionPanelObject.SetActive(active);
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

    private static Button CreateInvisibleButton(string objectName, Transform parent, UnityEngine.Events.UnityAction clickHandler)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(clickHandler);
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
