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

    private PreviewSlot playerPreviewSlot;

    private PreviewSlot playerItemPreviewSlot;

    private PreviewSlot targetPreviewSlot;

    private PreviewSlot targetItemPreviewSlot;

    private readonly List<WorldObject> playerAgentOptions = new();

    private const float PackMemberListPadding = 4f;

    private const float PackMemberListRowHeight = 42f;

    private const float PackMemberListRowSpacing = 5f;

    private int selectedPlayerAgentIndex;

    private WorldObject displayedPlayer;

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

    private enum InteractionTab
        {
            Social,
            Quests,
            Items,
            Pack,
            Scent
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
