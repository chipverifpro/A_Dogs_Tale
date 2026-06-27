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
public sealed partial class InteractionDialogUI : MonoBehaviour
{
    #region Fields

    private static InteractionDialogUI activeInstance;

    [SerializeField] private string circleSpriteResourcePath = "Sprites/Frames/Circle_540_540";

    [SerializeField] private string circleWithArrowsSpriteResourcePath = "Sprites/Frames/CircleWithArrows_921_540";

    [SerializeField] private string tradeArrowsSpriteResourcePath = "Sprites/Frames/TradeArrows_A";

    [SerializeField] private string titleFontResourcePath = "TMP_Fonts/LuckiestGuy-Regular SDF";

    [SerializeField] private float actionButtonHeight = 112f;

    [SerializeField] private float previewSpinDegreesPerSecond = 24f;

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

    private TextMeshProUGUI bottomTargetAgentLabel;

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

    private InteractionDialogPreviewSlot playerPreviewSlot;

    private InteractionDialogPreviewSlot playerItemPreviewSlot;

    private InteractionDialogPreviewSlot targetPreviewSlot;

    private InteractionDialogPreviewSlot targetItemPreviewSlot;

    private const float PackMemberListPadding = 4f;

    private const float PackMemberListRowHeight = 42f;

    private const float PackMemberListRowSpacing = 5f;

    private bool isOpen;

    private bool pausedGameForDialog;

    private Sprite circleSprite;

    private Sprite circleWithArrowsSprite;

    private Sprite tradeArrowsSprite;

    private TMP_FontAsset titleFont;

    [Header("Resources")]
    [SerializeField] private string interactionFrameSpriteResourcePath = "Sprites/Frames/Interaction_5_Frame_B";

    [Header("Layout")]
    [SerializeField] private int uiSortOrder = 5310;

    [SerializeField, Range(0f, 75f)] private float dialogScaleReductionPercent = 25f;

    [SerializeField, Range(0f, 85f)] private float previewViewAngleDegrees = 30f;

    [SerializeField, Min(0f)] private float tradePartnerSearchRadiusTiles = 2f;

    [SerializeField, Min(80f)] private float tooltipMaxWidth = 300f;

    [SerializeField, Min(8f)] private float tooltipFontSize = 20f;

    private static readonly Vector3 PlayerPreviewAnchorPosition = new(62000f, 60000f, 60000f);

    private static readonly Vector3 PlayerItemPreviewAnchorPosition = new(63000f, 60000f, 60000f);

    private static readonly Vector3 TargetPreviewAnchorPosition = new(64000f, 60000f, 60000f);

    private static readonly Vector3 TargetItemPreviewAnchorPosition = new(65000f, 60000f, 60000f);

    [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);

    [SerializeField] private Vector2 dialogSize = new(1536f, 1024f);

    [SerializeField] private Vector2 closeButtonAnchoredPosition = new(-250f, -120f);

    [SerializeField] private Vector2 closeButtonSize = new(120f, 120f);

    [SerializeField] private Vector2 tooltipScreenOffset = new(18f, -18f);

    [SerializeField] private Vector2 tooltipPadding = new(12f, 8f);

    private readonly List<WorldObject> playerAgentOptions = new();

    #endregion

    #region Nested Types

    public enum InteractionTab
    {
        Social,
        Quests,
        Items,
        Pack,
        Scent
    }

    #endregion

    #region Lifecycle

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
        PlayerInputState inputState = GetInputState();
        if (inputState != null && TryGetShortcutTab(inputState.requestedPopupTabIndex, out InteractionTab requestedTab))
        {
            if (isOpen)
                SwitchToTab(requestedTab);
            else
                Show(requestedTab);
        }
        else if (inputState != null && inputState.interactionPanelTogglePressed)
        {
            Toggle();
        }

        if (!isOpen)
            return;

        if (inputState != null && inputState.closeDialogsPressed)
        {
            Hide();
            return;
        }

        ApplyDialogScaleAndPosition();
        RefreshInteractionView();
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

        questsState.InteractionQuestListDirty = true;
        itemsState.PackHeldItemListDirty = true;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    public void Show(InteractionTab tab)
    {
        Show();
        SwitchToTab(tab);
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

    public static bool IsPointerOverScrollableList(Vector2 screenPoint)
    {
        InteractionDialogUI instance = activeInstance;
        return instance != null &&
            instance.isOpen &&
            (instance.IsPointerOverActiveRect(instance.socialEmoteGridObject, screenPoint) ||
             instance.IsPointerOverActiveRect(instance.packMemberListObject, screenPoint) ||
             instance.IsPointerOverActiveRect(instance.packHeldItemListObject, screenPoint) ||
             instance.IsPointerOverActiveRect(instance.questListObject, screenPoint) ||
             instance.IsPointerOverActiveRect(instance.scentSourceListObject, screenPoint));
    }

    private bool IsPointerOverActiveRect(GameObject uiObject, Vector2 screenPoint)
    {
        if (uiObject == null || !uiObject.activeInHierarchy)
            return false;

        RectTransform rect = uiObject.GetComponent<RectTransform>();
        return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, null);
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

    private static PlayerInputState GetInputState()
    {
        GameInputRouter router = GameInputRouter.Instance;
        if (router != null)
            return router.InputState;

        Dir dir = Dir.Instance;
        return dir != null && dir.gameInputRouter != null ? dir.gameInputRouter.InputState : null;
    }

    private static bool TryGetShortcutTab(int shortcutIndex, out InteractionTab tab)
    {
        switch (shortcutIndex)
        {
            case 1:
                tab = InteractionTab.Social;
                return true;
            case 2:
                tab = InteractionTab.Pack;
                return true;
            case 3:
                tab = InteractionTab.Items;
                return true;
            case 4:
                tab = InteractionTab.Quests;
                return true;
            default:
                tab = InteractionTab.Items;
                return false;
        }
    }

    #endregion

    #region UI Construction

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
        BuildBottomTargetAgentLabel(dialogRoot.transform);
        BuildTradeArrows(dialogRoot.transform);
        BuildTabLabels(dialogRoot.transform);
        BuildSelectionArrows(dialogRoot.transform);
        BuildActionButtons(dialogRoot.transform);
        BuildPackHeldItemList(dialogRoot.transform);
        BuildPackMemberList(dialogRoot.transform);
        BuildInteractionQuestList(dialogRoot.transform);
        BuildScentSourceList(dialogRoot.transform);
        BuildScentActionButtons(dialogRoot.transform);
        BuildSocialEmoteGrid(dialogRoot.transform);
        BuildPackActionButtons(dialogRoot.transform);
        BuildCloseHotspot(dialogRoot.transform);
        BuildTooltip(canvasObject.transform);
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
        socialTabButton = CreateTabHotspot(parent, "SocialTabButton", new Vector2(-480f, -428f), tabSize, () => SwitchToTab(InteractionTab.Social));
        packTabButton = CreateTabHotspot(parent, "PackTabButton", new Vector2(-240f, -428f), tabSize, () => SwitchToTab(InteractionTab.Pack));
        itemsTabButton = CreateTabHotspot(parent, "ItemsTabButton", new Vector2(0f, -428f), tabSize, () => SwitchToTab(InteractionTab.Items));
        questsTabButton = CreateTabHotspot(parent, "QuestsTabButton", new Vector2(240f, -428f), tabSize, () => SwitchToTab(InteractionTab.Quests));
        scentTabButton = CreateTabHotspot(parent, "ScentTabButton", new Vector2(480f, -428f), tabSize, () => SwitchToTab(InteractionTab.Scent));
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

    private void BuildBottomTargetAgentLabel(Transform parent)
    {
        bottomTargetAgentLabel = CreateInfoLabel(parent, "BottomTargetAgent", new Vector2(70f, -512f), new Vector2(690f, 54f), 36f, TextAlignmentOptions.Center);
        bottomTargetAgentLabel.fontStyle = FontStyles.Bold;
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

    private InteractionDialogPreviewSlot CreatePreviewSlot(
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

        return new InteractionDialogPreviewSlot
        {
            CircleImage = circleImage,
            Image = image,
            AnchorPosition = anchorPosition,
            CircleSize = circleSize,
            CircleWithArrowsSize = circleWithArrowsSize,
            OrthographicPadding = orthographicPadding
        };
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

    #endregion

    #region UI Refresh

    private void UpdateTooltipText(string text)
    {
        tooltipLabel.text = text;
        Vector2 preferred = tooltipLabel.GetPreferredValues(text, tooltipMaxWidth, 0f);
        float width = Mathf.Min(tooltipMaxWidth, preferred.x) + tooltipPadding.x * 2f;
        float height = preferred.y + tooltipPadding.y * 2f;
        tooltipRect.sizeDelta = new Vector2(Mathf.Max(80f, width), Mathf.Max(38f, height));
    }

    private void SetItemSelectionTypeLabelsActive(bool active)
    {
        if (playerItemSelectionTypeLabel != null)
            playerItemSelectionTypeLabel.gameObject.SetActive(active);
        if (targetItemSelectionTypeLabel != null)
            targetItemSelectionTypeLabel.gameObject.SetActive(active);
    }

    private static void SetPreviewSlotActive(InteractionDialogPreviewSlot slot, bool active)
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
            socialTabHighlight.gameObject.SetActive(sharedState.CurrentTab == InteractionTab.Social);
        if (questsTabHighlight != null)
            questsTabHighlight.gameObject.SetActive(sharedState.CurrentTab == InteractionTab.Quests);
        if (packTabHighlight != null)
            packTabHighlight.gameObject.SetActive(sharedState.CurrentTab == InteractionTab.Pack);
        if (itemsTabHighlight != null)
            itemsTabHighlight.gameObject.SetActive(sharedState.CurrentTab == InteractionTab.Items);
        if (scentTabHighlight != null)
            scentTabHighlight.gameObject.SetActive(sharedState.CurrentTab == InteractionTab.Scent);
        if (titleLabel != null)
            titleLabel.text = sharedState.CurrentTab.ToString().ToUpperInvariant();
        if (targetSelectionTypeLabel != null)
            targetSelectionTypeLabel.text = GetTargetSelectionTypeLabel();
    }

    private void RefreshCircleAndHotspot(InteractionDialogPreviewSlot slot, int optionCount, Button previousButton, Button nextButton)
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

    private static void SetLabelText(TextMeshProUGUI label, string text)
    {
        if (label != null)
            label.text = text ?? string.Empty;
    }

    private void SetBottomTargetAgentLabel(WorldObject agent)
    {
        SetLabelText(bottomTargetAgentLabel, agent != null ? agent.DisplayName : string.Empty);
    }

    #endregion

    #region Selection State

    private void BuildPlayerAgentOptions()
    {
        WorldObject previousSelection = GetSelectedFromList(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex);
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

        KeepSelectedObject(playerAgentOptions, previousSelection, ref sharedState.SelectedPlayerAgentIndex);
    }

    private void ClearPendingSelections()
    {
        sharedState.PendingLeftAgentSelection = null;
        sharedState.PendingRightAgentSelection = null;
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

    private string GetTargetSelectionTypeLabel()
    {
        return sharedState.CurrentTab switch
        {
            InteractionTab.Social => "Nearby Agent",
            InteractionTab.Pack => "Pack/Nearby",
            InteractionTab.Items => "Nearby Agent",
            InteractionTab.Quests => "Quest Giver",
            InteractionTab.Scent => "Scent Source",
            _ => string.Empty
        };
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

    #endregion

    #region Input And Actions

    private void SwitchToTab(InteractionTab tab)
    {
        if (sharedState.CurrentTab == tab)
            return;

        PreserveCurrentAgentsForTabSwitch();
        sharedState.CurrentTab = tab;
        ResetDisplayedStateForTab(tab);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void ResetDisplayedStateForTab(InteractionTab tab)
    {
        switch (tab)
        {
            case InteractionTab.Social:
                socialState.DisplayedLeft = null;
                socialState.DisplayedRight = null;
                break;
            case InteractionTab.Quests:
                questsState.DisplayedLeft = null;
                questsState.DisplayedRight = null;
                questsState.InteractionQuestListDirty = true;
                break;
            case InteractionTab.Pack:
                packState.DisplayedLeft = null;
                packState.DisplayedRight = null;
                break;
            case InteractionTab.Items:
                sharedState.DisplayedPlayer = null;
                itemsState.DisplayedPlayerItem = null;
                itemsState.DisplayedTarget = null;
                itemsState.DisplayedTargetItem = null;
                break;
            case InteractionTab.Scent:
                scentState.DisplayedLeft = null;
                scentState.DisplayedRight = null;
                break;
        }
    }

    private void OnPreviousPlayerAgentClicked()
    {
        if (sharedState.CurrentTab == InteractionTab.Pack)
        {
            CyclePackLeftSelection(-1);
            return;
        }

        if (sharedState.CurrentTab == InteractionTab.Social)
        {
            CycleSocialLeftSelection(-1);
            return;
        }

        if (sharedState.CurrentTab == InteractionTab.Quests)
        {
            CycleQuestLeftSelection(-1);
            return;
        }

        if (sharedState.CurrentTab == InteractionTab.Scent)
        {
            CycleScentLeftSelection(-1);
            return;
        }

        CycleSelection(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex, -1);
        itemsState.SelectedPlayerItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnNextPlayerAgentClicked()
    {
        if (sharedState.CurrentTab == InteractionTab.Pack)
        {
            CyclePackLeftSelection(1);
            return;
        }

        if (sharedState.CurrentTab == InteractionTab.Social)
        {
            CycleSocialLeftSelection(1);
            return;
        }

        if (sharedState.CurrentTab == InteractionTab.Quests)
        {
            CycleQuestLeftSelection(1);
            return;
        }

        if (sharedState.CurrentTab == InteractionTab.Scent)
        {
            CycleScentLeftSelection(1);
            return;
        }

        CycleSelection(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex, 1);
        itemsState.SelectedPlayerItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnPreviousTargetAgentClicked()
    {
        if (sharedState.CurrentTab == InteractionTab.Pack)
        {
            CyclePackRightSelection(-1);
            return;
        }

        if (sharedState.CurrentTab == InteractionTab.Social)
        {
            CycleSocialRightSelection(-1);
            return;
        }

        if (sharedState.CurrentTab == InteractionTab.Quests)
        {
            CycleQuestRightSelection(-1);
            return;
        }

        if (sharedState.CurrentTab == InteractionTab.Scent)
        {
            CycleScentRightSelection(-1);
            return;
        }

        CycleSelection(targetAgentOptions, ref itemsState.SelectedTargetAgentIndex, -1);
        itemsState.SelectedTargetItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnNextTargetAgentClicked()
    {
        if (sharedState.CurrentTab == InteractionTab.Pack)
        {
            CyclePackRightSelection(1);
            return;
        }

        if (sharedState.CurrentTab == InteractionTab.Social)
        {
            CycleSocialRightSelection(1);
            return;
        }

        if (sharedState.CurrentTab == InteractionTab.Quests)
        {
            CycleQuestRightSelection(1);
            return;
        }

        if (sharedState.CurrentTab == InteractionTab.Scent)
        {
            CycleScentRightSelection(1);
            return;
        }

        CycleSelection(targetAgentOptions, ref itemsState.SelectedTargetAgentIndex, 1);
        itemsState.SelectedTargetItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
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

    private static void ShowInteractionMessage(WorldObject agent, string message)
    {
        BottomBanner.LogAgentInventoryMessage(agent, message);
    }

    #endregion

    #region List Helpers

    private static void ClearListContent(RectTransform contentRect)
    {
        if (contentRect == null)
            return;

        for (int i = contentRect.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRect.GetChild(i);
            child.SetParent(null, false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static void ScrollListToSelectionNormalized(ScrollRect scrollRect, int selectedIndex, int rowCount)
    {
        if (scrollRect == null)
            return;

        if (rowCount <= 1)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(selectedIndex / (float)(rowCount - 1));
    }

    private static void ScrollListToSelectionWithVisibleRows(
        ScrollRect scrollRect,
        int selectedIndex,
        int rowCount,
        int visibleRows)
    {
        if (scrollRect == null || rowCount <= 0 || selectedIndex < 0)
            return;

        int clampedVisibleRows = Mathf.Max(1, visibleRows);
        if (rowCount <= clampedVisibleRows)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        float denominator = Mathf.Max(1, rowCount - clampedVisibleRows);
        scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(selectedIndex / denominator);
    }

    private static void ScrollFixedRowListToSelection(
        ScrollRect scrollRect,
        RectTransform contentRect,
        RectTransform viewportRect,
        int selectedIndex,
        int rowCount)
    {
        if (scrollRect == null || contentRect == null || rowCount <= 1)
        {
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;
            if (contentRect != null)
                contentRect.anchoredPosition = Vector2.zero;
            return;
        }

        float stride = PackMemberListRowHeight + PackMemberListRowSpacing;
        SetFixedRowListScrollOffset(scrollRect, contentRect, viewportRect, rowCount, selectedIndex * stride);
    }

    private static void SetFixedRowListScrollOffset(
        ScrollRect scrollRect,
        RectTransform contentRect,
        RectTransform viewportRect,
        int rowCount,
        float offsetY)
    {
        if (contentRect == null)
            return;

        float maxOffset = GetFixedRowListMaxScrollOffset(viewportRect, rowCount);
        Vector2 anchoredPosition = contentRect.anchoredPosition;
        anchoredPosition.y = Mathf.Clamp(offsetY, 0f, maxOffset);
        contentRect.anchoredPosition = anchoredPosition;

        if (scrollRect != null)
            scrollRect.StopMovement();
    }

    private static float GetFixedRowListMaxScrollOffset(RectTransform viewportRect, int rowCount)
    {
        if (viewportRect == null || rowCount <= 0)
            return 0f;

        float contentHeight =
        PackMemberListPadding * 2f +
        rowCount * PackMemberListRowHeight +
        Mathf.Max(0, rowCount - 1) * PackMemberListRowSpacing;
        return Mathf.Max(0f, contentHeight - viewportRect.rect.height);
    }

    private static bool TryGetListLocalPoint(
        RectTransform viewportRect,
        Vector2 screenPosition,
        Camera eventCamera,
        out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        return viewportRect != null &&
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewportRect,
            screenPosition,
            eventCamera,
            out localPoint);
    }

    private static int GetFixedRowListRowIndexAtScreenPosition(
        int rowCount,
        RectTransform viewportRect,
        RectTransform contentRect,
        Vector2 screenPosition,
        Camera eventCamera)
    {
        if (rowCount <= 0 ||
            viewportRect == null ||
            contentRect == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(viewportRect, screenPosition, eventCamera) ||
            !TryGetListLocalPoint(viewportRect, screenPosition, eventCamera, out Vector2 localPoint))
        {
            return -1;
        }

        float visibleDistanceFromTop = viewportRect.rect.height * 0.5f - localPoint.y;
        float contentDistanceFromTop = visibleDistanceFromTop + contentRect.anchoredPosition.y;
        float rowOffset = contentDistanceFromTop - PackMemberListPadding;
        if (rowOffset < 0f)
            return -1;

        float stride = PackMemberListRowHeight + PackMemberListRowSpacing;
        int rowIndex = Mathf.FloorToInt(rowOffset / stride);
        float rowLocalY = rowOffset - rowIndex * stride;
        if (rowIndex < 0 || rowIndex >= rowCount || rowLocalY > PackMemberListRowHeight)
            return -1;

        return rowIndex;
    }

    #endregion

    #region Preview Rendering

    private void BuildPreviewClone(InteractionDialogPreviewSlot slot, WorldObject worldObject, string label)
    {
        InteractionDialogPreviewRenderer.BuildPreviewClone(slot, worldObject, label, previewViewAngleDegrees);
    }

    private void SpinPreview(InteractionDialogPreviewSlot slot)
    {
        InteractionDialogPreviewRenderer.SpinPreview(slot, previewSpinDegreesPerSecond, previewViewAngleDegrees);
    }

    private static void ReleasePreviewSlot(InteractionDialogPreviewSlot slot)
    {
        InteractionDialogPreviewRenderer.ReleasePreviewSlot(slot);
    }

    #endregion

    #region Helpers

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

    private void PreserveCurrentAgentsForTabSwitch()
    {
        WorldObject leftAgent = sharedState.DisplayedPlayer ?? packState.DisplayedLeft ?? socialState.DisplayedLeft ?? questsState.DisplayedLeft ?? scentState.DisplayedLeft;
        WorldObject rightAgent = itemsState.DisplayedTarget ?? packState.DisplayedRight ?? socialState.DisplayedRight ?? questsState.DisplayedRight ?? scentState.DisplayedRight;

        sharedState.PendingLeftAgentSelection = leftAgent;
        sharedState.PendingRightAgentSelection = rightAgent;

        RememberSelection(playerAgentOptions, leftAgent, ref sharedState.SelectedPlayerAgentIndex);
        RememberSelection(packMemberOptions, leftAgent, ref packState.SelectedLeftIndex);
        RememberSelection(targetAgentOptions, rightAgent, ref itemsState.SelectedTargetAgentIndex);
        RememberSelection(socialTargetOptions, rightAgent, ref socialState.SelectedTargetIndex);
        RememberSelection(questTargetOptions, rightAgent, ref questsState.SelectedTargetIndex);
        RememberSelection(scentTargetOptions, rightAgent, ref scentState.SelectedTargetIndex);
        RememberSelection(packRightOptions, rightAgent, ref packState.SelectedRightIndex);
    }

    #endregion
}
