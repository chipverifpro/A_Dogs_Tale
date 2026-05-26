using System;
using System.Collections.Generic;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class InventoryDialogUI : MonoBehaviour
{
    private static readonly Vector3 PreviewAnchorPosition = new(60000f, 60000f, 60000f);
    private static readonly Vector3 TradePartnerPreviewAnchorPosition = new(61000f, 60000f, 60000f);
    private const float HeldTripleActionButtonScale = 0.86f;
    private static readonly Vector2 PreviewArrowButtonSize = new(41f, 41f);

    [Header("Resources")]
    [SerializeField] private string arrowsSpriteResourcePath = "Sprites/ArrowsSpriteSheetA";
    [SerializeField] private string inventoryActionsSpriteResourcePath = "Sprites/InventoryActionsSheetA";
    [SerializeField] private string dogActionsSpriteResourcePath = "Sprites/DogActions_B";
    [SerializeField] private string inventoryBackgroundSpriteResourcePath = "Sprites/InventoryBackground_B";
    [SerializeField] private string tradeArrowSpriteResourcePath = "Sprites/TradeArrows_B";

    [Header("Layout")]
    [SerializeField] private int uiSortOrder = 5300;
    [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);
    [SerializeField] private Vector2 dialogSize = new(820f, 820f);
    [SerializeField, Range(0f, 75f)] private float dialogScaleReductionPercent = 25f;
    [SerializeField] private Vector2 themedBallCloseButtonAnchoredPosition = new(-110f, -100f);
    [SerializeField] private Vector2 themedBallCloseButtonSize = new(110f, 110f);
    [SerializeField] private float actionButtonHeight = 112f;
    [SerializeField] private float tradeActionButtonHeight = 52f;
    [SerializeField] private float tradeActionButtonSpacing = 4f;
    [SerializeField] private float previewSpinDegreesPerSecond = 24f;
    [SerializeField, Range(0f, 85f)] private float previewViewAngleDegrees = 30f;
    [SerializeField, Min(0f)] private float tradePartnerSearchRadiusTiles = 2f;
    [SerializeField] private Vector2 tooltipPadding = new(18f, 10f);
    [SerializeField] private Vector2 tooltipOffset = new(18f, -18f);

    [Header("Throw")]
    [SerializeField, Min(0f)] private float throwForwardImpulse = 7f;
    [SerializeField, Min(0f)] private float throwUpwardImpulse = 2f;
    [SerializeField, Min(0f)] private float throwReleaseHeight = 0.5f;

    private readonly Dictionary<int, Sprite> arrowSprites = new();
    private readonly Dictionary<int, Sprite> actionSprites = new();
    private readonly Dictionary<int, Sprite> dogActionSprites = new();
    private readonly Dictionary<int, Sprite> tradeArrowSprites = new();
    private readonly List<Button> actionButtons = new();
    private readonly List<TradeTargetOption> tradeTargetOptions = new();

    private Sprite inventoryBackgroundSprite;
    private Canvas overlayCanvas;
    private RectTransform dialogRect;
    private GameObject dialogRoot;
    private RawImage previewImage;
    private RawImage tradePartnerPreviewImage;
    private TextMeshProUGUI itemNameLabel;
    private TextMeshProUGUI tradePartnerNameLabel;
    private RectTransform tooltipRect;
    private TextMeshProUGUI tooltipLabel;
    private Button leftArrowButton;
    private Button rightArrowButton;
    private Button tradePartnerLeftArrowButton;
    private Button tradePartnerRightArrowButton;

    private RenderTexture previewTexture;
    private GameObject previewWorldRoot;
    private GameObject previewClone;
    private Camera previewCamera;
    private Light previewLight;
    private float framingRadius = 1f;

    private RenderTexture tradePartnerPreviewTexture;
    private GameObject tradePartnerPreviewWorldRoot;
    private GameObject tradePartnerPreviewClone;
    private Camera tradePartnerPreviewCamera;
    private Light tradePartnerPreviewLight;
    private float tradePartnerFramingRadius = 1f;

    private int selectedIndex;
    private int selectedTradeTargetIndex;
    private WorldObject previewedItem;
    private WorldObject displayedTradePartner;
    private WorldObject displayedTradePartnerItem;
    private WorldObject displayedTradePartnerPreviewObject;
    private ContainerModule displayedContainer;
    private bool isOpen;
    private bool keepSelectedTradeTargetIndex;

    private enum InventoryAction
    {
        Use = 0,
        Eat = 1,
        Give = 2,
        Trade = 3,
        Drop = 4,
        PickUp = 5,
        Throw = 6
    }

    private enum TradeTargetKind
    {
        WorldObject,
        Ground
    }

    private bool HasThemedBackground => inventoryBackgroundSprite != null;

    private readonly struct TradeTargetOption
    {
        public TradeTargetOption(TradeTargetKind kind, WorldObject agent, WorldObject item, float distanceSqr)
        {
            Kind = kind;
            Agent = agent;
            Item = item;
            DistanceSqr = distanceSqr;
        }

        public TradeTargetKind Kind { get; }
        public WorldObject Agent { get; }
        public WorldObject Item { get; }
        public float DistanceSqr { get; }
        public bool IsGround => Kind == TradeTargetKind.Ground;
        public WorldObject PreviewObject => IsGround ? Item : Agent;

        public static TradeTargetOption ForWorldObject(WorldObject agent, WorldObject item, float distanceSqr)
        {
            return new TradeTargetOption(TradeTargetKind.WorldObject, agent, item, distanceSqr);
        }

        public static TradeTargetOption ForGroundItem(WorldObject item, float distanceSqr)
        {
            return new TradeTargetOption(TradeTargetKind.Ground, null, item, distanceSqr);
        }

        public static TradeTargetOption ForGround(float distanceSqr)
        {
            return new TradeTargetOption(TradeTargetKind.Ground, null, null, distanceSqr);
        }
    }

    private void Awake()
    {
        BuildUI();
        Hide();
    }

    private void OnValidate()
    {
        ApplyDialogScale();
    }

    private void Update()
    {
        if (WasInventoryTogglePressedThisFrame())
            Toggle();

        if (!isOpen)
            return;

        RefreshInventoryView();
        UpdateTooltipPosition();
        SpinPreview();
        SpinTradePartnerPreview();
    }

    private void OnDestroy()
    {
        DestroyPreviewClone();
        DestroyPreviewWorld();
        ReleasePreviewTexture();
        DestroyTradePartnerPreviewClone();
        DestroyTradePartnerPreviewWorld();
        ReleaseTradePartnerPreviewTexture();
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
        isOpen = true;
        dialogRoot.SetActive(true);
        RefreshInventoryView(forcePreviewRefresh: true);
    }

    public void Hide()
    {
        isOpen = false;
        selectedIndex = 0;
        selectedTradeTargetIndex = 0;
        previewedItem = null;
        displayedTradePartner = null;
        displayedTradePartnerItem = null;
        displayedTradePartnerPreviewObject = null;
        displayedContainer = null;

        if (dialogRoot != null)
            dialogRoot.SetActive(false);

        HideTooltip();
        DestroyPreviewClone();
        DestroyTradePartnerPreviewClone();
    }

    private bool WasInventoryTogglePressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.iKey.wasPressedThisFrame)
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
        LoadSprites();
        EnsureEventSystem();

        GameObject canvasObject = new("InventoryDialogCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 1f;

        dialogRoot = CreateUIObject("InventoryDialog", canvasObject.transform);
        dialogRect = dialogRoot.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero;
        dialogRect.sizeDelta = dialogSize;
        ApplyDialogScale();

        Image dialogImage = dialogRoot.AddComponent<Image>();
        if (inventoryBackgroundSprite != null)
        {
            dialogImage.sprite = inventoryBackgroundSprite;
            dialogImage.type = Image.Type.Simple;
            dialogImage.preserveAspect = true;
            dialogImage.color = Color.white;
        }
        else
        {
            dialogImage.color = new Color(0.08f, 0.075f, 0.055f, 0.94f);
        }

        BuildHeader(dialogRoot.transform);
        BuildPreviewArea(dialogRoot.transform);
        BuildActionButtons(dialogRoot.transform);
        BuildTooltip(canvasObject.transform);
        EnsurePreviewWorld();
        EnsureTradePartnerPreviewWorld();
    }

    private void ApplyDialogScale()
    {
        if (dialogRect == null)
            return;

        float reduction01 = Mathf.Clamp01(dialogScaleReductionPercent / 100f);
        float scale = Mathf.Max(0.01f, 1f - reduction01);
        dialogRect.localScale = new Vector3(scale, scale, 1f);
    }

    private void BuildHeader(Transform parent)
    {
        bool hasThemedBackground = HasThemedBackground;
        if (!hasThemedBackground)
        {
            GameObject titleObject = CreateUIObject("Title", parent);
            RectTransform titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(32f, -72f);
            titleRect.offsetMax = new Vector2(-96f, -18f);

            TextMeshProUGUI titleLabel = titleObject.AddComponent<TextMeshProUGUI>();
            titleLabel.text = "Inventory";
            titleLabel.fontSize = 34f;
            titleLabel.color = new Color(0.98f, 0.93f, 0.78f, 1f);
            titleLabel.alignment = TextAlignmentOptions.MidlineLeft;
        }

        Button closeButton = CreateInvisibleButton("CloseButton", parent, OnCloseClicked, "Close");
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = hasThemedBackground ? new Vector2(0.5f, 0.5f) : new Vector2(1f, 1f);
        closeRect.anchoredPosition = hasThemedBackground ? themedBallCloseButtonAnchoredPosition : new Vector2(-20f, -18f);
        closeRect.sizeDelta = hasThemedBackground ? themedBallCloseButtonSize : new Vector2(54f, 54f);
    }

    private void BuildPreviewArea(Transform parent)
    {
        GameObject previewPanel = CreateUIObject("PreviewPanel", parent);
        RectTransform previewPanelRect = previewPanel.GetComponent<RectTransform>();
        if (HasThemedBackground)
        {
            previewPanelRect.anchorMin = Vector2.zero;
            previewPanelRect.anchorMax = Vector2.one;
            previewPanelRect.offsetMin = new Vector2(84f, 356f);
            previewPanelRect.offsetMax = new Vector2(-84f, -184f);
        }
        else
        {
            previewPanelRect.anchorMin = new Vector2(0f, 0.42f);
            previewPanelRect.anchorMax = new Vector2(1f, 0.9f);
            previewPanelRect.offsetMin = new Vector2(44f, 18f);
            previewPanelRect.offsetMax = new Vector2(-44f, -12f);
        }

        Image previewBackground = previewPanel.AddComponent<Image>();
        previewBackground.color = HasThemedBackground ? Color.clear : new Color(0.97f, 0.91f, 0.74f, 0.12f);

        GameObject heldItemPane = CreatePreviewPane("HeldItemPane", previewPanel.transform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(18f, 12f), new Vector2(-10f, -12f));
        GameObject tradePartnerPane = CreatePreviewPane("TradePartnerPane", previewPanel.transform, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(10f, 12f), new Vector2(-18f, -12f));
        CreatePreviewDivider(previewPanel.transform);
        CreateTradeActionButtonStrip(previewPanel.transform);

        GameObject rawImageObject = CreateUIObject("ItemPreview", heldItemPane.transform);
        RectTransform rawImageRect = rawImageObject.GetComponent<RectTransform>();
        rawImageRect.anchorMin = new Vector2(0.16f, 0.22f);
        rawImageRect.anchorMax = new Vector2(0.84f, 0.92f);
        rawImageRect.offsetMin = Vector2.zero;
        rawImageRect.offsetMax = Vector2.zero;

        previewImage = rawImageObject.AddComponent<RawImage>();
        previewImage.raycastTarget = false;
        previewImage.color = Color.white;

        AspectRatioFitter previewAspect = rawImageObject.AddComponent<AspectRatioFitter>();
        previewAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        previewAspect.aspectRatio = 1f;

        GameObject tradePartnerRawImageObject = CreateUIObject("TradePartnerPreview", tradePartnerPane.transform);
        RectTransform tradePartnerRawImageRect = tradePartnerRawImageObject.GetComponent<RectTransform>();
        tradePartnerRawImageRect.anchorMin = new Vector2(0.16f, 0.22f);
        tradePartnerRawImageRect.anchorMax = new Vector2(0.84f, 0.92f);
        tradePartnerRawImageRect.offsetMin = Vector2.zero;
        tradePartnerRawImageRect.offsetMax = Vector2.zero;

        tradePartnerPreviewImage = tradePartnerRawImageObject.AddComponent<RawImage>();
        tradePartnerPreviewImage.raycastTarget = false;
        tradePartnerPreviewImage.color = Color.white;

        AspectRatioFitter tradePartnerPreviewAspect = tradePartnerRawImageObject.AddComponent<AspectRatioFitter>();
        tradePartnerPreviewAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        tradePartnerPreviewAspect.aspectRatio = 1f;

        GameObject labelObject = CreateUIObject("ItemName", heldItemPane.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.offsetMin = new Vector2(24f, 16f);
        labelRect.offsetMax = new Vector2(-24f, 58f);

        itemNameLabel = labelObject.AddComponent<TextMeshProUGUI>();
        itemNameLabel.fontSize = 28f;
        itemNameLabel.color = GetPreviewLabelColor();
        itemNameLabel.alignment = TextAlignmentOptions.Center;

        GameObject tradePartnerLabelObject = CreateUIObject("TradePartnerName", tradePartnerPane.transform);
        RectTransform tradePartnerLabelRect = tradePartnerLabelObject.GetComponent<RectTransform>();
        tradePartnerLabelRect.anchorMin = new Vector2(0f, 0f);
        tradePartnerLabelRect.anchorMax = new Vector2(1f, 0f);
        tradePartnerLabelRect.pivot = new Vector2(0.5f, 0f);
        tradePartnerLabelRect.offsetMin = new Vector2(24f, 16f);
        tradePartnerLabelRect.offsetMax = new Vector2(-24f, 58f);

        tradePartnerNameLabel = tradePartnerLabelObject.AddComponent<TextMeshProUGUI>();
        tradePartnerNameLabel.fontSize = 24f;
        tradePartnerNameLabel.color = GetPreviewLabelColor();
        tradePartnerNameLabel.alignment = TextAlignmentOptions.Center;

        tradePartnerLeftArrowButton = CreateSpriteButton(
            "PreviousTradeTargetButton",
            tradePartnerPane.transform,
            arrowSprites.TryGetValue(0, out Sprite tradeLeftSprite) ? tradeLeftSprite : null,
            "<",
            OnPreviousTradeTargetClicked,
            "Previous trade target");

        RectTransform tradeLeftRect = tradePartnerLeftArrowButton.GetComponent<RectTransform>();
        tradeLeftRect.anchorMin = new Vector2(0f, 0.5f);
        tradeLeftRect.anchorMax = new Vector2(0f, 0.5f);
        tradeLeftRect.pivot = new Vector2(0f, 0.5f);
        tradeLeftRect.anchoredPosition = new Vector2(30f, 0f);
        tradeLeftRect.sizeDelta = PreviewArrowButtonSize;

        tradePartnerRightArrowButton = CreateSpriteButton(
            "NextTradeTargetButton",
            tradePartnerPane.transform,
            arrowSprites.TryGetValue(1, out Sprite tradeRightSprite) ? tradeRightSprite : null,
            ">",
            OnNextTradeTargetClicked,
            "Next trade target");

        RectTransform tradeRightRect = tradePartnerRightArrowButton.GetComponent<RectTransform>();
        tradeRightRect.anchorMin = new Vector2(1f, 0.5f);
        tradeRightRect.anchorMax = new Vector2(1f, 0.5f);
        tradeRightRect.pivot = new Vector2(1f, 0.5f);
        tradeRightRect.anchoredPosition = new Vector2(-30f, 0f);
        tradeRightRect.sizeDelta = PreviewArrowButtonSize;

        leftArrowButton = CreateSpriteButton(
            "PreviousItemButton",
            heldItemPane.transform,
            arrowSprites.TryGetValue(0, out Sprite leftSprite) ? leftSprite : null,
            "<",
            OnPreviousItemClicked,
            "Previous item");

        RectTransform leftRect = leftArrowButton.GetComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0f, 0.5f);
        leftRect.anchorMax = new Vector2(0f, 0.5f);
        leftRect.pivot = new Vector2(0f, 0.5f);
        leftRect.anchoredPosition = new Vector2(30f, 0f);
        leftRect.sizeDelta = PreviewArrowButtonSize;

        rightArrowButton = CreateSpriteButton(
            "NextItemButton",
            heldItemPane.transform,
            arrowSprites.TryGetValue(1, out Sprite rightSprite) ? rightSprite : null,
            ">",
            OnNextItemClicked,
            "Next item");

        RectTransform rightRect = rightArrowButton.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(1f, 0.5f);
        rightRect.anchorMax = new Vector2(1f, 0.5f);
        rightRect.pivot = new Vector2(1f, 0.5f);
        rightRect.anchoredPosition = new Vector2(-30f, 0f);
        rightRect.sizeDelta = PreviewArrowButtonSize;
    }

    private GameObject CreatePreviewPane(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject pane = CreateUIObject(objectName, parent);
        RectTransform paneRect = pane.GetComponent<RectTransform>();
        paneRect.anchorMin = anchorMin;
        paneRect.anchorMax = anchorMax;
        paneRect.offsetMin = offsetMin;
        paneRect.offsetMax = offsetMax;

        Image paneBackground = pane.AddComponent<Image>();
        paneBackground.color = HasThemedBackground ? Color.clear : new Color(0.02f, 0.018f, 0.014f, 0.12f);

        return pane;
    }

    private void CreatePreviewDivider(Transform parent)
    {
        GameObject divider = CreateUIObject("PreviewDivider", parent);
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0.5f, 0.08f);
        dividerRect.anchorMax = new Vector2(0.5f, 0.92f);
        dividerRect.pivot = new Vector2(0.5f, 0.5f);
        dividerRect.sizeDelta = new Vector2(2f, 0f);
        dividerRect.anchoredPosition = Vector2.zero;

        Image dividerImage = divider.AddComponent<Image>();
        dividerImage.color = new Color(0.98f, 0.93f, 0.78f, 0.28f);
    }

    private void BuildActionButtons(Transform parent)
    {
        GameObject actionPanel = CreateUIObject("ActionPanel", parent);
        RectTransform actionPanelRect = actionPanel.GetComponent<RectTransform>();
        if (HasThemedBackground)
        {
            actionPanelRect.anchorMin = Vector2.zero;
            actionPanelRect.anchorMax = Vector2.one;
            actionPanelRect.offsetMin = new Vector2(76f, 70f);
            actionPanelRect.offsetMax = new Vector2(-76f, -484f);
        }
        else
        {
            actionPanelRect.anchorMin = new Vector2(0f, 0f);
            actionPanelRect.anchorMax = new Vector2(1f, 0.39f);
            actionPanelRect.offsetMin = new Vector2(44f, 34f);
            actionPanelRect.offsetMax = new Vector2(-44f, -20f);
        }

        Image actionBackground = actionPanel.AddComponent<Image>();
        actionBackground.color = HasThemedBackground ? Color.clear : new Color(0.02f, 0.018f, 0.014f, 0.18f);

        VerticalLayoutGroup layout = actionPanel.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 0f;
        layout.padding = HasThemedBackground ? new RectOffset(16, 16, 8, 8) : new RectOffset(16, 16, 16, 16);

        Transform leftTopRow = CreateActionButtonRow("HeldItemActionRowTop", actionPanel.transform);
        Transform leftBottomRow = CreateActionButtonRow("HeldItemActionRowBottom", actionPanel.transform);

        CreateActionButton(leftTopRow, InventoryAction.Use, OnUseClicked);
        CreateActionButton(leftTopRow, InventoryAction.Eat, OnEatClicked);
        CreateActionButton(leftBottomRow, InventoryAction.Drop, OnDropClicked, HeldTripleActionButtonScale);
        CreateThrowActionButton(leftBottomRow, HeldTripleActionButtonScale);
        CreateActionButton(leftBottomRow, InventoryAction.PickUp, OnPickUpClicked, HeldTripleActionButtonScale);
    }

    private void CreateTradeActionButtonStrip(Transform parent)
    {
        GameObject stripObject = CreateUIObject("TradeActionButtons", parent);
        RectTransform stripRect = stripObject.GetComponent<RectTransform>();
        stripRect.anchorMin = new Vector2(0.5f, 0.5f);
        stripRect.anchorMax = new Vector2(0.5f, 0.5f);
        stripRect.pivot = new Vector2(0.5f, 0.5f);
        stripRect.anchoredPosition = Vector2.zero;
        stripRect.sizeDelta = new Vector2(142f, tradeActionButtonHeight * 3f + tradeActionButtonSpacing * 2f);

        VerticalLayoutGroup layout = stripObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = tradeActionButtonSpacing;
        layout.padding = new RectOffset(0, 0, 0, 0);

        CreateTradeArrowButton(stripObject.transform, "GiveButton", 0, "GIVE", OnGiveClicked);
        CreateTradeArrowButton(stripObject.transform, "TradeButton", 1, "TRADE", OnTradeClicked);
        CreateTradeArrowButton(stripObject.transform, "TakeItemButton", 2, "TAKE", OnTakeItemClicked);
    }

    private Transform CreateActionButtonRow(string rowName, Transform parent)
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
        layoutElement.preferredHeight = actionButtonHeight;
        layoutElement.minHeight = actionButtonHeight;

        return rowObject.transform;
    }

    private void CreateActionButton(Transform parent, InventoryAction action, UnityEngine.Events.UnityAction clickHandler, float heightScale = 1f)
    {
        int index = (int)action;
        Sprite sprite = actionSprites.TryGetValue(index, out Sprite foundSprite) ? foundSprite : null;
        string actionText = GetActionFallbackText(action);
        Button button = CreateSpriteButton($"{action}Button", parent, sprite, actionText, clickHandler, actionText);

        float buttonHeight = actionButtonHeight * Mathf.Max(0.01f, heightScale);
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

        actionButtons.Add(button);
    }

    private void CreateTradeArrowButton(
        Transform parent,
        string objectName,
        int spriteIndex,
        string actionText,
        UnityEngine.Events.UnityAction clickHandler)
    {
        Sprite sprite = tradeArrowSprites.TryGetValue(spriteIndex, out Sprite foundSprite) ? foundSprite : null;
        Button button = CreateSpriteButton(objectName, parent, sprite, actionText, clickHandler, actionText);

        float buttonHeight = tradeActionButtonHeight;
        float width = buttonHeight * 1.55f;
        if (sprite != null && sprite.rect.height > 0f)
            width = buttonHeight * (sprite.rect.width / sprite.rect.height);

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, buttonHeight);

        LayoutElement layoutElement = button.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = buttonHeight;
        layoutElement.minWidth = width;
        layoutElement.minHeight = buttonHeight;

        actionButtons.Add(button);
    }

    private void CreateThrowActionButton(Transform parent, float heightScale = 1f)
    {
        Sprite sprite = dogActionSprites.TryGetValue(0, out Sprite foundSprite) ? foundSprite : null;
        Button button = CreateSpriteButton("ThrowButton", parent, sprite, "THROW", OnThrowClicked, "THROW");

        float buttonHeight = actionButtonHeight * Mathf.Max(0.01f, heightScale);
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

        actionButtons.Add(button);
    }

    private Button CreateSpriteButton(
        string objectName,
        Transform parent,
        Sprite sprite,
        string fallbackText,
        UnityEngine.Events.UnityAction clickHandler,
        string tooltipText = null)
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

        if (!string.IsNullOrWhiteSpace(tooltipText))
            AddTooltip(buttonObject, tooltipText);

        return button;
    }

    private Button CreateInvisibleButton(
        string objectName,
        Transform parent,
        UnityEngine.Events.UnityAction clickHandler,
        string tooltipText = null)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(clickHandler);

        if (!string.IsNullOrWhiteSpace(tooltipText))
            AddTooltip(buttonObject, tooltipText);

        return button;
    }

    private void BuildTooltip(Transform parent)
    {
        GameObject tooltipObject = CreateUIObject("InventoryTooltip", parent);
        tooltipRect = tooltipObject.GetComponent<RectTransform>();
        tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0f, 1f);
        tooltipRect.sizeDelta = new Vector2(160f, 48f);

        Image background = tooltipObject.AddComponent<Image>();
        background.color = new Color(0.97f, 0.91f, 0.72f, 0.97f);

        GameObject labelObject = CreateUIObject("Label", tooltipObject.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = tooltipPadding;
        labelRect.offsetMax = -tooltipPadding;

        tooltipLabel = labelObject.AddComponent<TextMeshProUGUI>();
        tooltipLabel.fontSize = 22f;
        tooltipLabel.color = new Color(0.08f, 0.06f, 0.03f, 1f);
        tooltipLabel.alignment = TextAlignmentOptions.Center;
        tooltipLabel.raycastTarget = false;

        tooltipObject.SetActive(false);
    }

    private void AddTooltip(GameObject target, string tooltipText)
    {
        InventoryDialogTooltipTrigger trigger = target.AddComponent<InventoryDialogTooltipTrigger>();
        trigger.Initialize(this, tooltipText);
    }

    public void ShowTooltip(string tooltipText)
    {
        if (tooltipRect == null || tooltipLabel == null || !isOpen)
            return;

        tooltipLabel.text = tooltipText;
        Vector2 preferredSize = tooltipLabel.GetPreferredValues(tooltipText, 360f, 0f);
        tooltipRect.sizeDelta = preferredSize + tooltipPadding * 2f;
        tooltipRect.gameObject.SetActive(true);
        UpdateTooltipPosition();
    }

    public void HideTooltip()
    {
        if (tooltipRect != null)
            tooltipRect.gameObject.SetActive(false);
    }

    private void UpdateTooltipPosition()
    {
        if (tooltipRect == null || !tooltipRect.gameObject.activeSelf || Mouse.current == null)
            return;

        RectTransform canvasRect = overlayCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Vector2 screenPoint = Mouse.current.position.ReadValue() + tooltipOffset;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);
        tooltipRect.anchoredPosition = localPoint;
    }

    private void AddFallbackButtonText(Transform parent, string text)
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

    private void RefreshInventoryView(bool forcePreviewRefresh = false)
    {
        ContainerModule container = GetCurrentContainer();
        if (container != displayedContainer)
        {
            displayedContainer = container;
            selectedIndex = 0;
            forcePreviewRefresh = true;
        }

        RefreshTradePartnerView(forcePreviewRefresh);

        int itemCount = container != null ? container.HeldItemCount : 0;
        if (itemCount <= 0)
        {
            SetNoHeldItemState();
            return;
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, itemCount - 1);
        WorldObject item = container.HeldItems[selectedIndex];

        itemNameLabel.text = item != null ? item.DisplayName : "No item held";
        bool hasMultipleItems = itemCount > 1;
        leftArrowButton.gameObject.SetActive(hasMultipleItems);
        rightArrowButton.gameObject.SetActive(hasMultipleItems);

        if (forcePreviewRefresh || item != previewedItem)
            BuildPreviewClone(item);
    }

    private void SetNoHeldItemState()
    {
        itemNameLabel.text = "No item held";
        leftArrowButton.gameObject.SetActive(false);
        rightArrowButton.gameObject.SetActive(false);

        if (previewedItem != null || previewClone != null)
        {
            previewedItem = null;
            DestroyPreviewClone();
        }

        ClearPreviewTexture();
    }

    private void RefreshTradePartnerView(bool forcePreviewRefresh = false)
    {
        WorldObject previousPartner = displayedTradePartner;
        WorldObject previousItem = displayedTradePartnerItem;
        BuildTradeTargetOptions();
        if (tradeTargetOptions.Count == 0)
        {
            SetNoTradePartnerState();
            return;
        }

        int matchedIndex = FindTradeTargetOptionIndex(previousPartner, previousItem);
        if (!keepSelectedTradeTargetIndex && matchedIndex >= 0)
            selectedTradeTargetIndex = matchedIndex;
        else
            selectedTradeTargetIndex = Mathf.Clamp(selectedTradeTargetIndex, 0, tradeTargetOptions.Count - 1);
        keepSelectedTradeTargetIndex = false;

        TradeTargetOption selectedOption = tradeTargetOptions[selectedTradeTargetIndex];
        WorldObject selectedPreviewObject = selectedOption.PreviewObject;
        bool selectedPreviewChanged = selectedPreviewObject != displayedTradePartnerPreviewObject;
        displayedTradePartner = selectedOption.Agent;
        displayedTradePartnerItem = selectedOption.Item;
        displayedTradePartnerPreviewObject = selectedPreviewObject;

        tradePartnerNameLabel.text = BuildTradePartnerLabel(selectedOption);
        bool hasMultipleTradeTargets = tradeTargetOptions.Count > 1;
        tradePartnerLeftArrowButton.gameObject.SetActive(hasMultipleTradeTargets);
        tradePartnerRightArrowButton.gameObject.SetActive(hasMultipleTradeTargets);

        if (forcePreviewRefresh || selectedPreviewChanged || (selectedPreviewObject != null && tradePartnerPreviewClone == null))
            BuildTradePartnerPreviewClone(selectedPreviewObject);
    }

    private void SetNoTradePartnerState()
    {
        if (tradePartnerNameLabel != null)
            tradePartnerNameLabel.text = "No one nearby";

        if (tradePartnerLeftArrowButton != null)
            tradePartnerLeftArrowButton.gameObject.SetActive(false);
        if (tradePartnerRightArrowButton != null)
            tradePartnerRightArrowButton.gameObject.SetActive(false);

        displayedTradePartner = null;
        displayedTradePartnerItem = null;
        displayedTradePartnerPreviewObject = null;
        DestroyTradePartnerPreviewClone();

        ClearTradePartnerPreviewTexture();
    }

    private string BuildTradePartnerLabel(TradeTargetOption option)
    {
        if (option.IsGround)
        {
            if (option.Item != null)
                return $"Ground\nHolding {option.Item.DisplayName}";

            return "Ground\nHolding nothing";
        }

        if (option.Agent == null)
            return "No one nearby";

        if (option.Item != null)
            return $"{option.Agent.DisplayName}\nHolding {option.Item.DisplayName}";

        return $"{option.Agent.DisplayName}\nHolding nothing";
    }

    private ContainerModule GetCurrentContainer()
    {
        WorldObject controlledObject = GetCurrentControlledWorldObject();
        if (controlledObject == null)
            return null;

        if (controlledObject.containerModule == null)
            controlledObject.CreateModulesIfNeeded(ModuleFlags.containerModule);

        return controlledObject.containerModule;
    }

    private WorldObject GetCurrentControlledWorldObject()
    {
        Dir dir = Dir.Instance;
        return dir != null && dir.playerPack != null ? dir.playerPack.packLeader : null;
    }

    private void BuildTradeTargetOptions()
    {
        tradeTargetOptions.Clear();

        WorldObject controlledObject = GetCurrentControlledWorldObject();
        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (controlledObject == null)
            return;

        float radiusSqr = tradePartnerSearchRadiusTiles * tradePartnerSearchRadiusTiles;
        Vector3 controlledPosition = controlledObject.pos3d_map;

        if (registry == null)
        {
            tradeTargetOptions.Add(TradeTargetOption.ForGround(0f));
            return;
        }

        bool foundGroundItem = false;

        foreach (WorldObject candidate in registry.GetAllObjects())
        {
            if (candidate == null || candidate == controlledObject || !candidate.gameObject.activeInHierarchy)
                continue;

            Vector3 delta = candidate.pos3d_map - controlledPosition;
            delta.y = 0f;
            float distanceSqr = delta.sqrMagnitude;
            if (distanceSqr > radiusSqr)
                continue;

            if (CanUseAsGroundItem(candidate))
            {
                foundGroundItem = true;
                tradeTargetOptions.Add(TradeTargetOption.ForGroundItem(candidate, distanceSqr));
                continue;
            }

            if (!CanUseAsTradeTarget(candidate))
                continue;

            ContainerModule container = candidate.containerModule;
            if (container == null || container.HeldItemCount == 0)
            {
                tradeTargetOptions.Add(TradeTargetOption.ForWorldObject(candidate, null, distanceSqr));
                continue;
            }

            for (int i = 0; i < container.HeldItemCount; i++)
            {
                WorldObject item = container.HeldItems[i];
                if (item != null)
                    tradeTargetOptions.Add(TradeTargetOption.ForWorldObject(candidate, item, distanceSqr));
            }
        }

        if (!foundGroundItem)
            tradeTargetOptions.Add(TradeTargetOption.ForGround(0f));

        tradeTargetOptions.Sort(CompareTradeTargetOptions);
    }

    private static bool CanUseAsTradeTarget(WorldObject candidate)
    {
        if (candidate == null)
            return false;

        return candidate.containerModule != null &&
               candidate.containerModule.itemCapacity > 0;
    }

    private static bool CanUseAsGroundItem(WorldObject candidate)
    {
        if (candidate == null || candidate.Kind != WorldObjectKind.Item)
            return false;

        if (!candidate.gameObject.activeInHierarchy)
            return false;

        if (candidate.transform.parent == null)
            return true;

        WorldObject parentWorldObject = candidate.transform.parent.GetComponentInParent<WorldObject>();
        return parentWorldObject == null || parentWorldObject == candidate;
    }

    private int FindTradeTargetOptionIndex(WorldObject agent, WorldObject item)
    {
        if (agent == null && item == null)
            return -1;

        for (int i = 0; i < tradeTargetOptions.Count; i++)
        {
            TradeTargetOption option = tradeTargetOptions[i];
            if (option.Agent == agent && option.Item == item)
                return i;
        }

        return -1;
    }

    private static int CompareTradeTargetOptions(TradeTargetOption a, TradeTargetOption b)
    {
        bool aIsEmptyGround = IsEmptyGroundOption(a);
        bool bIsEmptyGround = IsEmptyGroundOption(b);
        if (aIsEmptyGround != bIsEmptyGround)
            return aIsEmptyGround ? 1 : -1;

        int distanceComparison = a.DistanceSqr.CompareTo(b.DistanceSqr);
        if (distanceComparison != 0)
            return distanceComparison;

        string aName = a.IsGround ? "Ground" : a.Agent != null ? a.Agent.DisplayName : string.Empty;
        string bName = b.IsGround ? "Ground" : b.Agent != null ? b.Agent.DisplayName : string.Empty;
        int nameComparison = string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
        if (nameComparison != 0)
            return nameComparison;

        string aItemName = a.Item != null ? a.Item.DisplayName : string.Empty;
        string bItemName = b.Item != null ? b.Item.DisplayName : string.Empty;
        return string.Compare(aItemName, bItemName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEmptyGroundOption(TradeTargetOption option)
    {
        return option.IsGround && option.Item == null;
    }

    private bool TryGetSelectedTradeTargetOption(out TradeTargetOption option)
    {
        if (tradeTargetOptions.Count <= 0)
        {
            option = default;
            return false;
        }

        selectedTradeTargetIndex = Mathf.Clamp(selectedTradeTargetIndex, 0, tradeTargetOptions.Count - 1);
        option = tradeTargetOptions[selectedTradeTargetIndex];
        return true;
    }

    private WorldObject GetSelectedHeldItem()
    {
        if (displayedContainer == null || displayedContainer.HeldItemCount <= 0)
            return null;

        selectedIndex = Mathf.Clamp(selectedIndex, 0, displayedContainer.HeldItemCount - 1);
        return displayedContainer.HeldItems[selectedIndex];
    }

    private void OnPreviousItemClicked()
    {
        int itemCount = displayedContainer != null ? displayedContainer.HeldItemCount : 0;
        if (itemCount <= 1)
            return;

        selectedIndex = (selectedIndex - 1 + itemCount) % itemCount;
        RefreshInventoryView(forcePreviewRefresh: true);
    }

    private void OnNextItemClicked()
    {
        int itemCount = displayedContainer != null ? displayedContainer.HeldItemCount : 0;
        if (itemCount <= 1)
            return;

        selectedIndex = (selectedIndex + 1) % itemCount;
        RefreshInventoryView(forcePreviewRefresh: true);
    }

    private void OnPreviousTradeTargetClicked()
    {
        if (tradeTargetOptions.Count <= 1)
            return;

        selectedTradeTargetIndex = (selectedTradeTargetIndex - 1 + tradeTargetOptions.Count) % tradeTargetOptions.Count;
        keepSelectedTradeTargetIndex = true;
        RefreshTradePartnerView(forcePreviewRefresh: true);
    }

    private void OnNextTradeTargetClicked()
    {
        if (tradeTargetOptions.Count <= 1)
            return;

        selectedTradeTargetIndex = (selectedTradeTargetIndex + 1) % tradeTargetOptions.Count;
        keepSelectedTradeTargetIndex = true;
        RefreshTradePartnerView(forcePreviewRefresh: true);
    }

    private void OnCloseClicked()
    {
        Hide();
    }

    private void OnUseClicked()
    {
        WorldObject user = GetCurrentControlledWorldObject();
        WorldObject item = GetSelectedHeldItem();
        if (user == null)
            return;

        if (item == null)
        {
            ShowInventoryMessage($"{user.DisplayName} has no item to use");
            return;
        }

        if (item.activatorModule == null)
        {
            ShowInventoryMessage($"{item.DisplayName} cannot be used");
            return;
        }

        WorldObject otherAgent = displayedTradePartner;
        ActivatorModule activator = item.activatorModule;
        string itemName = item.DisplayName;
        bool success = activator.TryUseItem(user, otherAgent);
        if (success && activator.parameterDestruct)
        {
            ContainerModule container = GetCurrentContainer();
            if (container != null && !container.ReleaseItem(item, out string reason))
            {
                ShowInventoryMessage(reason);
                Debug.LogWarning($"InventoryDialogUI: failed to destroy used item {itemName}: {reason}", this);
                RefreshInventoryView(forcePreviewRefresh: true);
                return;
            }

            Destroy(item.gameObject);
            selectedIndex = container != null && container.HeldItemCount > 0
                ? Mathf.Clamp(selectedIndex, 0, container.HeldItemCount - 1)
                : 0;
        }

        ShowInventoryMessage(success
            ? $"{user.DisplayName} used {itemName}"
            : $"{user.DisplayName} could not use {itemName}");

        RefreshInventoryView(forcePreviewRefresh: true);
    }

    private void OnEatClicked()
    {
        WorldObject eater = GetCurrentControlledWorldObject();
        ContainerModule container = GetCurrentContainer();
        if (eater == null || container == null)
            return;

        WorldObject item = GetSelectedHeldItem();
        if (item == null)
        {
            ShowInventoryMessage($"{eater.DisplayName} has no item to eat");
            return;
        }

        string itemName = item.DisplayName;
        if (!container.ReleaseItem(item, out string reason))
        {
            ShowInventoryMessage(reason);
            Debug.LogWarning($"InventoryDialogUI: failed to eat {itemName}: {reason}", this);
            return;
        }

        Destroy(item.gameObject);
        ShowInventoryMessage($"{eater.DisplayName} ate {itemName}");
        selectedIndex = container.HeldItemCount > 0
            ? Mathf.Clamp(selectedIndex, 0, container.HeldItemCount - 1)
            : 0;
        RefreshInventoryView(forcePreviewRefresh: true);
    }

    private void OnGiveClicked()
    {
        WorldObject giver = GetCurrentControlledWorldObject();
        WorldObject item = GetSelectedHeldItem();
        WorldObject recipient = displayedTradePartner;
        ContainerModule giverContainer = GetCurrentContainer();
        ContainerModule recipientContainer = GetOrCreateContainer(recipient);

        if (giver == null || giverContainer == null)
            return;

        if (item == null)
        {
            ShowInventoryMessage($"{giver.DisplayName} has no item to give");
            return;
        }

        if (TryGetSelectedTradeTargetOption(out TradeTargetOption selectedOption) && selectedOption.IsGround)
        {
            if (TryDropItemNearCarrier(giverContainer, giver, item, out string dropReason))
            {
                ShowInventoryMessage($"{giver.DisplayName} dropped {item.DisplayName} on the ground");
                selectedIndex = Mathf.Clamp(selectedIndex, 0, giverContainer.HeldItemCount - 1);
                RefreshInventoryView(forcePreviewRefresh: true);
                return;
            }

            ShowInventoryMessage(dropReason);
            Debug.LogWarning($"InventoryDialogUI: failed to drop {item.DisplayName}: {dropReason}", this);
            return;
        }

        if (recipient == null || recipientContainer == null)
        {
            ShowInventoryMessage("No one nearby to give an item to");
            return;
        }

        if (TransferItem(giverContainer, recipientContainer, item, out string reason))
        {
            ShowInventoryMessage($"{giver.DisplayName} gave {item.DisplayName} to {recipient.DisplayName}");
            RefreshInventoryView(forcePreviewRefresh: true);
            return;
        }

        ShowInventoryMessage(reason);
        Debug.LogWarning($"InventoryDialogUI: failed to give {item.DisplayName}: {reason}", this);
    }

    private void OnTakeItemClicked()
    {
        WorldObject taker = GetCurrentControlledWorldObject();
        WorldObject giver = displayedTradePartner;
        WorldObject item = displayedTradePartnerItem;
        ContainerModule takerContainer = GetCurrentContainer();
        ContainerModule giverContainer = GetOrCreateContainer(giver);

        if (taker == null || takerContainer == null)
            return;

        if (TryGetSelectedTradeTargetOption(out TradeTargetOption selectedOption) && selectedOption.IsGround)
        {
            item = selectedOption.Item;
            if (item == null)
            {
                ShowInventoryMessage("No ground item selected to take");
                return;
            }

            string itemName = item.DisplayName;
            if (TryTakeGroundItem(takerContainer, item, out string pickupReason))
            {
                ShowInventoryMessage($"{taker.DisplayName} picked up {itemName} from the ground");
                SelectHeldItem(item);
                RefreshInventoryView(forcePreviewRefresh: true);
                return;
            }

            ShowInventoryMessage(pickupReason);
            Debug.LogWarning($"InventoryDialogUI: failed to pick up {itemName}: {pickupReason}", this);
            return;
        }

        if (giver == null || giverContainer == null)
        {
            ShowInventoryMessage("No one nearby to take an item from");
            return;
        }

        if (item == null)
        {
            ShowInventoryMessage($"{giver.DisplayName} has no selected item to take");
            return;
        }

        if (TransferItem(giverContainer, takerContainer, item, out string reason))
        {
            ShowInventoryMessage($"{taker.DisplayName} took {item.DisplayName} from {giver.DisplayName}");
            RefreshInventoryView(forcePreviewRefresh: true);
            return;
        }

        ShowInventoryMessage(reason);
        Debug.LogWarning($"InventoryDialogUI: failed to take {item.DisplayName}: {reason}", this);
    }

    private void OnTradeClicked()
    {
        WorldObject trader = GetCurrentControlledWorldObject();
        WorldObject partner = displayedTradePartner;
        WorldObject traderItem = GetSelectedHeldItem();
        WorldObject partnerItem = displayedTradePartnerItem;
        ContainerModule traderContainer = GetCurrentContainer();
        ContainerModule partnerContainer = GetOrCreateContainer(partner);

        if (trader == null || traderContainer == null)
            return;

        if (TryGetSelectedTradeTargetOption(out TradeTargetOption selectedOption) && selectedOption.IsGround)
        {
            partnerItem = selectedOption.Item;
            if (traderItem == null)
            {
                if (partnerItem != null)
                {
                    OnTakeItemClicked();
                    return;
                }

                ShowInventoryMessage($"{trader.DisplayName} has no item to trade");
                return;
            }

            if (partnerItem == null)
            {
                if (TryDropItemNearCarrier(traderContainer, trader, traderItem, out string dropReason))
                {
                    ShowInventoryMessage($"{trader.DisplayName} dropped {traderItem.DisplayName} on the ground");
                    selectedIndex = Mathf.Clamp(selectedIndex, 0, traderContainer.HeldItemCount - 1);
                    RefreshInventoryView(forcePreviewRefresh: true);
                    return;
                }

                ShowInventoryMessage(dropReason);
                Debug.LogWarning($"InventoryDialogUI: failed to drop {traderItem.DisplayName}: {dropReason}", this);
                return;
            }

            string traderItemName = traderItem.DisplayName;
            string partnerItemName = partnerItem.DisplayName;
            if (SwapHeldItemWithGroundItem(traderContainer, trader, traderItem, partnerItem, out string swapReason))
            {
                ShowInventoryMessage($"{trader.DisplayName} swapped {traderItemName} for {partnerItemName} on the ground");
                SelectHeldItem(partnerItem);
                RefreshInventoryView(forcePreviewRefresh: true);
                return;
            }

            ShowInventoryMessage(swapReason);
            Debug.LogWarning($"InventoryDialogUI: failed to swap {traderItemName} for ground item {partnerItemName}: {swapReason}", this);
            return;
        }

        if (partner == null || partnerContainer == null)
        {
            ShowInventoryMessage("No one nearby to trade with");
            return;
        }

        if (traderItem == null)
        {
            if (partnerItem != null)
            {
                OnTakeItemClicked(); // fall back to Take
                return;
            }
            ShowInventoryMessage($"{trader.DisplayName} has no item to trade");
            return;
            
        }

        if (partnerItem == null)
        {
            if (traderItem != null)
            {
                OnGiveClicked();    // fall back to Give
                return;
            }
            ShowInventoryMessage($"{partner.DisplayName} has no selected item to trade");
            return;
        }

        if (SwapItems(traderContainer, partnerContainer, traderItem, partnerItem, out string reason))
        {
            ShowInventoryMessage($"{trader.DisplayName} traded {traderItem.DisplayName} to {partner.DisplayName} for {partnerItem.DisplayName}");
            RefreshInventoryView(forcePreviewRefresh: true);
            return;
        }

        ShowInventoryMessage(reason);
        Debug.LogWarning($"InventoryDialogUI: failed to trade {traderItem.DisplayName} for {partnerItem.DisplayName}: {reason}", this);
    }

    private void OnDropClicked()
    {
        if (displayedContainer == null)
            return;

        WorldObject item = GetSelectedHeldItem();
        WorldObject carrier = GetCurrentControlledWorldObject();
        if (item == null || carrier == null)
            return;

        if (!TryDropItemNearCarrier(displayedContainer, carrier, item, out string reason))
        {
            Debug.LogWarning($"InventoryDialogUI: failed to drop {item.DisplayName}: {reason}", this);
            return;
        }

        ShowInventoryMessage($"{carrier.DisplayName} dropped {item.DisplayName}");
        selectedIndex = Mathf.Clamp(selectedIndex, 0, displayedContainer.HeldItemCount - 1);
        RefreshInventoryView(forcePreviewRefresh: true);
    }

    private void OnThrowClicked()
    {
        if (displayedContainer == null)
            return;

        WorldObject item = GetSelectedHeldItem();
        WorldObject carrier = GetCurrentControlledWorldObject();
        if (item == null || carrier == null)
            return;

        if (!TryThrowItemFromCarrier(displayedContainer, carrier, item, out string reason))
        {
            ShowInventoryMessage(reason);
            Debug.LogWarning($"InventoryDialogUI: failed to throw {item.DisplayName}: {reason}", this);
            return;
        }

        ShowInventoryMessage($"{carrier.DisplayName} threw {item.DisplayName}");
        selectedIndex = Mathf.Clamp(selectedIndex, 0, displayedContainer.HeldItemCount - 1);
        RefreshInventoryView(forcePreviewRefresh: true);
    }

    private void OnPickUpClicked()
    {
        WorldObject carrier = GetCurrentControlledWorldObject();
        ContainerModule container = GetCurrentContainer();
        if (carrier == null || container == null)
            return;

        if (!container.TryPickupNearestItem(out WorldObject pickedUpItem, out string reason))
        {
            ShowInventoryMessage(reason);
            return;
        }

        ShowInventoryMessage($"{carrier.DisplayName} picked up {pickedUpItem.DisplayName}");
        for (int i = 0; i < container.HeldItemCount; i++)
        {
            if (container.HeldItems[i] == pickedUpItem)
            {
                selectedIndex = i;
                break;
            }
        }

        RefreshInventoryView(forcePreviewRefresh: true);
    }

    private void SelectHeldItem(WorldObject item)
    {
        if (item == null || displayedContainer == null)
            return;

        for (int i = 0; i < displayedContainer.HeldItemCount; i++)
        {
            if (displayedContainer.HeldItems[i] == item)
            {
                selectedIndex = i;
                return;
            }
        }
    }

    private static ContainerModule GetOrCreateContainer(WorldObject owner)
    {
        if (owner == null)
            return null;

        if (owner.containerModule == null)
            owner.CreateModulesIfNeeded(ModuleFlags.containerModule);

        return owner.containerModule;
    }

    private static void ShowInventoryMessage(string message)
    {
        BottomBanner.LogInventoryMessage(message);
    }

    private static bool TryTakeGroundItem(ContainerModule destination, WorldObject item, out string reason)
    {
        if (destination == null)
        {
            reason = "Destination inventory is unavailable.";
            return false;
        }

        if (item == null)
        {
            reason = "No ground item selected.";
            return false;
        }

        if (!CanUseAsGroundItem(item))
        {
            reason = $"{item.DisplayName} is no longer on the ground.";
            return false;
        }

        return destination.ReceiveItem(item, false, out reason);
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

    private static bool SwapHeldItemWithGroundItem(ContainerModule heldContainer, WorldObject carrier, WorldObject heldItem, WorldObject groundItem, out string reason)
    {
        if (heldContainer == null)
        {
            reason = "Inventory is unavailable.";
            return false;
        }

        if (heldItem == null || groundItem == null)
        {
            reason = "Both sides need an item selected to trade.";
            return false;
        }

        if (!CanUseAsGroundItem(groundItem))
        {
            reason = $"{groundItem.DisplayName} is no longer on the ground.";
            return false;
        }

        if (!TryDropItemNearCarrier(heldContainer, carrier, heldItem, out reason))
            return false;

        if (heldContainer.ReceiveItem(groundItem, false, out reason))
            return true;

        string receiveFailure = reason;
        if (!heldContainer.ReceiveItem(heldItem, false, out string rollbackReason))
            reason = $"{receiveFailure} {heldItem.DisplayName} could not be returned: {rollbackReason}";
        else
            reason = receiveFailure;

        return false;
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

    private void LoadSprites()
    {
        inventoryBackgroundSprite = Resources.Load<Sprite>(inventoryBackgroundSpriteResourcePath);
        LoadSpriteSheet(arrowsSpriteResourcePath, arrowSprites);
        LoadSpriteSheet(inventoryActionsSpriteResourcePath, actionSprites);
        LoadSpriteSheet(dogActionsSpriteResourcePath, dogActionSprites);
        LoadSpriteSheet(tradeArrowSpriteResourcePath, tradeArrowSprites);
    }

    private Color GetPreviewLabelColor()
    {
        return HasThemedBackground
            ? new Color(0.2f, 0.11f, 0.04f, 1f)
            : new Color(0.98f, 0.93f, 0.78f, 1f);
    }

    private void LoadSpriteSheet(string resourcePath, Dictionary<int, Sprite> lookup)
    {
        lookup.Clear();

        foreach (KeyValuePair<int, Sprite> entry in SpriteServer.GetSpriteSheet(resourcePath))
            lookup[entry.Key] = entry.Value;
    }

    private void EnsurePreviewWorld()
    {
        if (previewWorldRoot != null)
            return;

        previewWorldRoot = new GameObject("InventoryDialogPreviewWorld");
        previewWorldRoot.hideFlags = HideFlags.HideAndDontSave;
        previewWorldRoot.transform.position = PreviewAnchorPosition;

        GameObject cameraObject = new("PreviewCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.transform.SetParent(previewWorldRoot.transform, false);
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.enabled = false;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.orthographic = true;
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 100f;

        GameObject lightObject = new("PreviewLight");
        lightObject.hideFlags = HideFlags.HideAndDontSave;
        lightObject.transform.SetParent(previewWorldRoot.transform, false);
        previewLight = lightObject.AddComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.intensity = 1.25f;
        previewLight.color = Color.white;
        previewLight.shadows = LightShadows.None;
        previewLight.transform.rotation = Quaternion.Euler(35f, 135f, 0f);

        EnsurePreviewTexture();
    }

    private void EnsurePreviewTexture()
    {
        if (previewTexture != null)
            return;

        previewTexture = new RenderTexture(768, 768, 16, RenderTextureFormat.ARGB32);
        previewTexture.name = "InventoryDialogPreviewRT";
        previewTexture.Create();

        previewImage.texture = previewTexture;
        previewCamera.targetTexture = previewTexture;
    }

    private void BuildPreviewClone(WorldObject item)
    {
        DestroyPreviewClone();
        previewedItem = item;

        if (item == null)
            return;

        EnsurePreviewWorld();
        previewClone = CreateVisualClone(item.gameObject);
        previewClone.name = $"{item.name}_InventoryPreview";
        previewClone.hideFlags = HideFlags.HideAndDontSave;
        previewClone.transform.SetParent(previewWorldRoot.transform, false);
        previewClone.transform.position = PreviewAnchorPosition;

        CenterPreviewClone(previewClone);
        RenderPreview();
    }

    private void CenterPreviewClone(GameObject clone)
    {
        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            framingRadius = 1f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        clone.transform.position += PreviewAnchorPosition - bounds.center;

        Bounds centeredBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            centeredBounds.Encapsulate(renderers[i].bounds);

        framingRadius = Mathf.Max(centeredBounds.extents.y, Mathf.Max(centeredBounds.extents.x, centeredBounds.extents.z));
        if (framingRadius < 0.1f)
            framingRadius = 0.5f;
    }

    private void SpinPreview()
    {
        if (previewClone == null)
            return;

        previewClone.transform.RotateAround(
            PreviewAnchorPosition,
            Vector3.up,
            previewSpinDegreesPerSecond * Time.unscaledDeltaTime);

        RenderPreview();
    }

    private void RenderPreview()
    {
        if (previewCamera == null)
            return;

        float distance = Mathf.Max(2f, framingRadius * 4f);
        float cameraHeight = Mathf.Tan(previewViewAngleDegrees * Mathf.Deg2Rad) * distance;
        previewCamera.transform.position = PreviewAnchorPosition + new Vector3(0f, cameraHeight, -distance);
        previewCamera.transform.LookAt(PreviewAnchorPosition + new Vector3(0f, framingRadius * 0.1f, 0f));
        previewCamera.orthographicSize = framingRadius * 1.45f;
        previewCamera.Render();
    }

    private void ClearPreviewTexture()
    {
        if (previewTexture == null)
            return;

        RenderTexture previousActiveTexture = RenderTexture.active;
        RenderTexture.active = previewTexture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = previousActiveTexture;
    }

    private void EnsureTradePartnerPreviewWorld()
    {
        if (tradePartnerPreviewWorldRoot != null)
            return;

        tradePartnerPreviewWorldRoot = new GameObject("InventoryDialogTradePartnerPreviewWorld");
        tradePartnerPreviewWorldRoot.hideFlags = HideFlags.HideAndDontSave;
        tradePartnerPreviewWorldRoot.transform.position = TradePartnerPreviewAnchorPosition;

        GameObject cameraObject = new("PreviewCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.transform.SetParent(tradePartnerPreviewWorldRoot.transform, false);
        tradePartnerPreviewCamera = cameraObject.AddComponent<Camera>();
        tradePartnerPreviewCamera.enabled = false;
        tradePartnerPreviewCamera.clearFlags = CameraClearFlags.SolidColor;
        tradePartnerPreviewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        tradePartnerPreviewCamera.orthographic = true;
        tradePartnerPreviewCamera.nearClipPlane = 0.01f;
        tradePartnerPreviewCamera.farClipPlane = 100f;

        GameObject lightObject = new("PreviewLight");
        lightObject.hideFlags = HideFlags.HideAndDontSave;
        lightObject.transform.SetParent(tradePartnerPreviewWorldRoot.transform, false);
        tradePartnerPreviewLight = lightObject.AddComponent<Light>();
        tradePartnerPreviewLight.type = LightType.Directional;
        tradePartnerPreviewLight.intensity = 1.25f;
        tradePartnerPreviewLight.color = Color.white;
        tradePartnerPreviewLight.shadows = LightShadows.None;
        tradePartnerPreviewLight.transform.rotation = Quaternion.Euler(35f, 135f, 0f);

        EnsureTradePartnerPreviewTexture();
    }

    private void EnsureTradePartnerPreviewTexture()
    {
        if (tradePartnerPreviewTexture != null)
            return;

        tradePartnerPreviewTexture = new RenderTexture(768, 768, 16, RenderTextureFormat.ARGB32);
        tradePartnerPreviewTexture.name = "InventoryDialogTradePartnerPreviewRT";
        tradePartnerPreviewTexture.Create();

        tradePartnerPreviewImage.texture = tradePartnerPreviewTexture;
        tradePartnerPreviewCamera.targetTexture = tradePartnerPreviewTexture;
    }

    private void BuildTradePartnerPreviewClone(WorldObject tradePartner)
    {
        DestroyTradePartnerPreviewClone();

        if (tradePartner == null)
        {
            ClearTradePartnerPreviewTexture();
            return;
        }

        EnsureTradePartnerPreviewWorld();
        tradePartnerPreviewClone = CreateVisualClone(tradePartner.gameObject);
        tradePartnerPreviewClone.name = $"{tradePartner.name}_InventoryTradePartnerPreview";
        tradePartnerPreviewClone.hideFlags = HideFlags.HideAndDontSave;
        tradePartnerPreviewClone.transform.SetParent(tradePartnerPreviewWorldRoot.transform, false);
        tradePartnerPreviewClone.transform.position = TradePartnerPreviewAnchorPosition;

        CenterTradePartnerPreviewClone(tradePartnerPreviewClone);
        RenderTradePartnerPreview();
    }

    private void CenterTradePartnerPreviewClone(GameObject clone)
    {
        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            tradePartnerFramingRadius = 1f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        clone.transform.position += TradePartnerPreviewAnchorPosition - bounds.center;

        Bounds centeredBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            centeredBounds.Encapsulate(renderers[i].bounds);

        tradePartnerFramingRadius = Mathf.Max(centeredBounds.extents.y, Mathf.Max(centeredBounds.extents.x, centeredBounds.extents.z));
        if (tradePartnerFramingRadius < 0.1f)
            tradePartnerFramingRadius = 0.5f;
    }

    private void SpinTradePartnerPreview()
    {
        if (tradePartnerPreviewClone == null)
            return;

        tradePartnerPreviewClone.transform.RotateAround(
            TradePartnerPreviewAnchorPosition,
            Vector3.up,
            previewSpinDegreesPerSecond * Time.unscaledDeltaTime);

        RenderTradePartnerPreview();
    }

    private void RenderTradePartnerPreview()
    {
        if (tradePartnerPreviewCamera == null)
            return;

        float distance = Mathf.Max(2f, tradePartnerFramingRadius * 4f);
        float cameraHeight = Mathf.Tan(previewViewAngleDegrees * Mathf.Deg2Rad) * distance;
        tradePartnerPreviewCamera.transform.position = TradePartnerPreviewAnchorPosition + new Vector3(0f, cameraHeight, -distance);
        tradePartnerPreviewCamera.transform.LookAt(TradePartnerPreviewAnchorPosition + new Vector3(0f, tradePartnerFramingRadius * 0.1f, 0f));
        tradePartnerPreviewCamera.orthographicSize = tradePartnerFramingRadius * 1.45f;
        tradePartnerPreviewCamera.Render();
    }

    private void ClearTradePartnerPreviewTexture()
    {
        if (tradePartnerPreviewTexture == null)
            return;

        RenderTexture previousActiveTexture = RenderTexture.active;
        RenderTexture.active = tradePartnerPreviewTexture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = previousActiveTexture;
    }

    private static GameObject CreateVisualClone(GameObject sourceRoot)
    {
        Dictionary<Transform, Transform> transformMap = new();

        GameObject cloneRoot = new(sourceRoot.name);
        CopyTransform(sourceRoot.transform, cloneRoot.transform);
        transformMap[sourceRoot.transform] = cloneRoot.transform;

        Transform[] sourceTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < sourceTransforms.Length; i++)
        {
            Transform source = sourceTransforms[i];
            GameObject child = new(source.name);
            Transform childTransform = child.transform;
            childTransform.SetParent(transformMap[source.parent], false);
            CopyTransform(source, childTransform);
            transformMap[source] = childTransform;
        }

        for (int i = 0; i < sourceTransforms.Length; i++)
        {
            Transform source = sourceTransforms[i];
            Transform destination = transformMap[source];

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

    private void DestroyPreviewClone()
    {
        if (previewClone == null)
            return;

        if (Application.isPlaying)
            Destroy(previewClone);
        else
            DestroyImmediate(previewClone);

        previewClone = null;
    }

    private void DestroyPreviewWorld()
    {
        if (previewWorldRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(previewWorldRoot);
        else
            DestroyImmediate(previewWorldRoot);

        previewWorldRoot = null;
        previewCamera = null;
        previewLight = null;
    }

    private void ReleasePreviewTexture()
    {
        if (previewCamera != null)
            previewCamera.targetTexture = null;

        if (previewImage != null)
            previewImage.texture = null;

        if (previewTexture != null)
        {
            previewTexture.Release();
            if (Application.isPlaying)
                Destroy(previewTexture);
            else
                DestroyImmediate(previewTexture);
        }

        previewTexture = null;
    }

    private void DestroyTradePartnerPreviewClone()
    {
        if (tradePartnerPreviewClone == null)
            return;

        if (Application.isPlaying)
            Destroy(tradePartnerPreviewClone);
        else
            DestroyImmediate(tradePartnerPreviewClone);

        tradePartnerPreviewClone = null;
    }

    private void DestroyTradePartnerPreviewWorld()
    {
        if (tradePartnerPreviewWorldRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(tradePartnerPreviewWorldRoot);
        else
            DestroyImmediate(tradePartnerPreviewWorldRoot);

        tradePartnerPreviewWorldRoot = null;
        tradePartnerPreviewCamera = null;
        tradePartnerPreviewLight = null;
    }

    private void ReleaseTradePartnerPreviewTexture()
    {
        if (tradePartnerPreviewCamera != null)
            tradePartnerPreviewCamera.targetTexture = null;

        if (tradePartnerPreviewImage != null)
            tradePartnerPreviewImage.texture = null;

        if (tradePartnerPreviewTexture != null)
        {
            tradePartnerPreviewTexture.Release();
            if (Application.isPlaying)
                Destroy(tradePartnerPreviewTexture);
            else
                DestroyImmediate(tradePartnerPreviewTexture);
        }

        tradePartnerPreviewTexture = null;
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

    private static string GetActionFallbackText(InventoryAction action)
    {
        return action switch
        {
            InventoryAction.Use => "USE",
            InventoryAction.Eat => "EAT",
            InventoryAction.Give => "GIVE",
            InventoryAction.Trade => "TRADE",
            InventoryAction.Drop => "DROP",
            InventoryAction.PickUp => "PICK UP",
            InventoryAction.Throw => "THROW",
            _ => action.ToString()
        };
    }

}

public static class InventoryDialogBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInventoryDialogExists()
    {
        if (UnityEngine.Object.FindFirstObjectByType<InventoryDialogUI>() != null)
            return;

        GameObject inventoryDialogObject = new("InventoryDialogUI");
        inventoryDialogObject.AddComponent<InventoryDialogUI>();
    }
}

public sealed class InventoryDialogTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private InventoryDialogUI owner;
    private string tooltipText;

    public void Initialize(InventoryDialogUI owner, string tooltipText)
    {
        this.owner = owner;
        this.tooltipText = tooltipText;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.ShowTooltip(tooltipText);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.HideTooltip();
    }
}
