using System.Collections.Generic;
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
    private static readonly Vector3 PlayerPreviewAnchorPosition = new(62000f, 60000f, 60000f);
    private static readonly Vector3 PlayerItemPreviewAnchorPosition = new(63000f, 60000f, 60000f);
    private static readonly Vector3 TargetPreviewAnchorPosition = new(64000f, 60000f, 60000f);
    private static readonly Vector3 TargetItemPreviewAnchorPosition = new(65000f, 60000f, 60000f);

    [Header("Resources")]
    [SerializeField] private string interactionFrameSpriteResourcePath = "Sprites/Frames/Interaction_Frame_F";
    [SerializeField] private string circleSpriteResourcePath = "Sprites/Frames/Circle";
    [SerializeField] private string circleWithArrowsSpriteResourcePath = "Sprites/Frames/CircleWithArrows";
    [SerializeField] private string tradeArrowsSpriteResourcePath = "Sprites/Frames/TradeArrows_A";
    [SerializeField] private string titleFontResourcePath = "TMP_Fonts/LuckiestGuy-Regular SDF";

    [Header("Layout")]
    [SerializeField] private int uiSortOrder = 5310;
    [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);
    [SerializeField] private Vector2 dialogSize = new(1536f, 1024f);
    [SerializeField, Range(0f, 75f)] private float dialogScaleReductionPercent = 25f;
    [SerializeField] private Vector2 closeButtonAnchoredPosition = new(-250f, -120f);
    [SerializeField] private Vector2 closeButtonSize = new(120f, 120f);
    [SerializeField] private float previewSpinDegreesPerSecond = 24f;
    [SerializeField, Range(0f, 85f)] private float previewViewAngleDegrees = 30f;
    [SerializeField, Min(0f)] private float tradePartnerSearchRadiusTiles = 2f;

    private Sprite interactionFrameSprite;
    private Canvas overlayCanvas;
    private RectTransform dialogRect;
    private GameObject dialogRoot;
    private TextMeshProUGUI playerNameLabel;
    private TextMeshProUGUI playerHeldItemLabel;
    private TextMeshProUGUI targetNameLabel;
    private TextMeshProUGUI targetHeldItemLabel;
    private Button previousPlayerAgentButton;
    private Button nextPlayerAgentButton;
    private Button previousPlayerItemButton;
    private Button nextPlayerItemButton;
    private Button previousTargetAgentButton;
    private Button nextTargetAgentButton;
    private Button previousTargetItemButton;
    private Button nextTargetItemButton;
    private PreviewSlot playerPreviewSlot;
    private PreviewSlot playerItemPreviewSlot;
    private PreviewSlot targetPreviewSlot;
    private PreviewSlot targetItemPreviewSlot;
    private readonly List<WorldObject> playerAgentOptions = new();
    private readonly List<WorldObject> playerItemOptions = new();
    private readonly List<WorldObject> targetAgentOptions = new();
    private readonly List<WorldObject> targetItemOptions = new();
    private int selectedPlayerAgentIndex;
    private int selectedPlayerItemIndex;
    private int selectedTargetAgentIndex;
    private int selectedTargetItemIndex;
    private WorldObject displayedPlayer;
    private WorldObject displayedPlayerItem;
    private WorldObject displayedTarget;
    private WorldObject displayedTargetItem;
    private bool isOpen;
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

        RefreshInteractionView();
        SpinPreview(playerPreviewSlot);
        SpinPreview(playerItemPreviewSlot);
        SpinPreview(targetPreviewSlot);
        SpinPreview(targetItemPreviewSlot);
    }

    private void OnDestroy()
    {
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
        isOpen = true;

        if (dialogRoot != null)
            dialogRoot.SetActive(true);

        RefreshInteractionView(forcePreviewRefresh: true);
    }

    public void Hide()
    {
        isOpen = false;

        if (dialogRoot != null)
            dialogRoot.SetActive(false);
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

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 1f;

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
        BuildHeader(dialogRoot.transform);
        BuildTopInfo(dialogRoot.transform);
        BuildTradeArrows(dialogRoot.transform);
        BuildTabLabels(dialogRoot.transform);
        BuildSelectionArrows(dialogRoot.transform);
        BuildCloseHotspot(dialogRoot.transform);
    }

    private void ApplyDialogScaleAndPosition()
    {
        if (dialogRect == null)
            return;

        float reduction01 = Mathf.Clamp01(dialogScaleReductionPercent / 100f);
        float scale = Mathf.Max(0.01f, 1f - reduction01);
        dialogRect.localScale = new Vector3(scale, scale, 1f);

        // The frame pivot/origin is top-center. Offset by half the scaled height so
        // the visible dialog remains centered on the screen.
        dialogRect.anchoredPosition = new Vector2(0f, dialogSize.y * scale * 0.5f);
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

        TextMeshProUGUI titleLabel = titleObject.AddComponent<TextMeshProUGUI>();
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
        CreateTabLabel(parent, "SocialTabLabel", "Social", new Vector2(-404f, -428f), new Vector2(260f, 74f));
        CreateTabLabel(parent, "PackTabLabel", "Pack", new Vector2(0f, -428f), new Vector2(260f, 74f));
        CreateTabHighlight(parent, "ItemsTabHighlight", new Vector2(404f, -428f), new Vector2(382f, 70f));
        CreateTabLabel(parent, "ItemsTabLabel", "Items", new Vector2(404f, -428f), new Vector2(260f, 74f));
    }

    private static void CreateTabHighlight(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size)
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
    }

    private void BuildTopInfo(Transform parent)
    {
        playerNameLabel = CreateInfoLabel(parent, "PlayerName", new Vector2(-190f, -232f), new Vector2(360f, 70f), 44f, TextAlignmentOptions.Left);
        playerHeldItemLabel = CreateInfoLabel(parent, "PlayerHeldItem", new Vector2(-110f, -334f), new Vector2(300f, 58f), 38f, TextAlignmentOptions.Left);
        targetNameLabel = CreateInfoLabel(parent, "TargetName", new Vector2(190f, -232f), new Vector2(360f, 70f), 44f, TextAlignmentOptions.Right);
        targetHeldItemLabel = CreateInfoLabel(parent, "TargetHeldItem", new Vector2(110f, -334f), new Vector2(300f, 58f), 38f, TextAlignmentOptions.Right);
    }

    private void BuildTradeArrows(Transform parent)
    {
        GameObject arrowsObject = CreateUIObject("TradeArrows", parent);
        RectTransform arrowsRect = arrowsObject.GetComponent<RectTransform>();
        arrowsRect.anchorMin = new Vector2(0.5f, 1f);
        arrowsRect.anchorMax = new Vector2(0.5f, 1f);
        arrowsRect.pivot = new Vector2(0.5f, 0.5f);
        arrowsRect.anchoredPosition = new Vector2(0f, -292f);
        arrowsRect.sizeDelta = new Vector2(86f, 186f);

        Image arrowsImage = arrowsObject.AddComponent<Image>();
        arrowsImage.sprite = tradeArrowsSprite;
        arrowsImage.preserveAspect = true;
        arrowsImage.color = Color.white;
        arrowsImage.raycastTarget = false;

        CreateTradeHotspot(parent, "GiveHotspot", new Vector2(0f, -244f), OnGiveClicked);
        CreateTradeHotspot(parent, "ExchangeHotspot", new Vector2(0f, -292f), OnTradeClicked);
        CreateTradeHotspot(parent, "TakeHotspot", new Vector2(0f, -340f), OnTakeItemClicked);
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
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private void BuildSelectionArrows(Transform parent)
    {
        previousPlayerAgentButton = CreateArrowButton(parent, "PreviousPlayerAgentButton", new Vector2(-593f, -290f), OnPreviousPlayerAgentClicked, 48f);
        nextPlayerAgentButton = CreateArrowButton(parent, "NextPlayerAgentButton", new Vector2(-400f, -290f), OnNextPlayerAgentClicked, 48f);
        previousPlayerItemButton = CreateArrowButton(parent, "PreviousPlayerItemButton", new Vector2(-419f, -320f), OnPreviousPlayerItemClicked, 32f);
        nextPlayerItemButton = CreateArrowButton(parent, "NextPlayerItemButton", new Vector2(-288f, -320f), OnNextPlayerItemClicked, 32f);

        previousTargetItemButton = CreateArrowButton(parent, "PreviousTargetItemButton", new Vector2(275f, -320f), OnPreviousTargetItemClicked, 32f);
        nextTargetItemButton = CreateArrowButton(parent, "NextTargetItemButton", new Vector2(405f, -320f), OnNextTargetItemClicked, 32f);
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
        playerPreviewSlot = CreatePreviewSlot(parent, "PlayerPreview", PlayerPreviewAnchorPosition, new Vector2(-497f, -290f), new Vector2(150f, 150f), new Vector2(171f, 171f), new Vector2(238f, 238f), 1.325f);
        playerItemPreviewSlot = CreatePreviewSlot(parent, "PlayerItemPreview", PlayerItemPreviewAnchorPosition, new Vector2(-354f, -320f), new Vector2(78f, 78f), new Vector2(96f, 96f), new Vector2(148f, 148f), 1.2f);
        targetItemPreviewSlot = CreatePreviewSlot(parent, "TargetItemPreview", TargetItemPreviewAnchorPosition, new Vector2(340f, -320f), new Vector2(78f, 78f), new Vector2(96f, 96f), new Vector2(148f, 148f), 1.2f);
        targetPreviewSlot = CreatePreviewSlot(parent, "TargetPreview", TargetPreviewAnchorPosition, new Vector2(480f, -290f), new Vector2(150f, 150f), new Vector2(171f, 171f), new Vector2(238f, 238f), 1.325f);
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
        BuildPlayerAgentOptions();
        WorldObject player = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
        WorldObject previousPlayerItem = GetSelectedFromList(playerItemOptions, ref selectedPlayerItemIndex);
        BuildItemOptions(player, playerItemOptions);
        KeepSelectedObject(playerItemOptions, previousPlayerItem, ref selectedPlayerItemIndex);
        WorldObject playerItem = GetSelectedFromList(playerItemOptions, ref selectedPlayerItemIndex);

        BuildTargetAgentOptions(player);
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

    private static bool CanUseAsTradeTarget(WorldObject candidate)
    {
        if (candidate == null)
            return false;

        return candidate.containerModule != null &&
               candidate.containerModule.itemCapacity > 0;
    }

    private void OnPreviousPlayerAgentClicked()
    {
        CycleSelection(playerAgentOptions, ref selectedPlayerAgentIndex, -1);
        selectedPlayerItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnNextPlayerAgentClicked()
    {
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
        CycleSelection(targetAgentOptions, ref selectedTargetAgentIndex, -1);
        selectedTargetItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnNextTargetAgentClicked()
    {
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

    private static void DestroyPreviewClone(PreviewSlot slot)
    {
        if (slot == null || slot.Clone == null)
            return;

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
