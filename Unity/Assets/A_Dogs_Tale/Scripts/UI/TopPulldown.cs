using System.Collections.Generic;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public partial class TopPulldown : MonoBehaviour
{
    private static readonly Vector3 TargetPreviewAnchorPosition = new(62000f, 60000f, 60000f);

    private const string CanvasName = "ScentTargetCanvas";
    private const string ScentControlsContainerName = "ScentControls";
    private const string DecisionModeControlsContainerName = "DecisionModeControls";
    private const string SpeedControlsContainerName = "SpeedControls";
    private const string SimulationControlsContainerName = "SimulationControls";
    private const string EmoteControlsContainerName = "EmoteControls";
    private const string InventoryControlsContainerName = "InventoryControls";
    private const string DigControlsContainerName = "DigControls";
    private const string LeftActionControlsContainerName = "LeftActionControls";
    private const string TooltipContainerName = "Tooltips";
    private const int TopControlButtonCount = 10;
    private const int QuestButtonTopSlotFromRight = 7;
    private const int CameraModeButtonTopSlotFromRight = 8;
    private const int HomeButtonTopSlotFromRight = 9;
    private const int TopControlColumnsWhenTwoRows = 5;
    private const string DefaultTwoRowPulldownFrameResourcePath = "Sprites/PulldownFrame_2x5";
    private const string LegacyTwoRowPulldownFrameResourcePath = "Sprites/PulldownFrame_2row";
    private const float PanelSpritePixelsPerUnit = 100f;

    [Header("External object references")]
    private Dir dir;
    public SniffModeVisuals sniffVisuals;

    [Header("Target Scent Menu")]
    [SerializeField] private string modeSpriteResourcePath = "Sprites/SpriteSheet_Modes_V3";
    [SerializeField] private string speedSpriteResourcePath = "Sprites/Speeds";
    [SerializeField] private string playPauseSpriteResourcePath = "Sprites/PlayAndPause_Dual";
    [SerializeField] private string inventoryActionSpriteResourcePath = "Sprites/InventoryActionsSheetA";
    [SerializeField] private string digHoleSpriteResourcePath = "Sprites/DigHoleSpriteA";
    [SerializeField] private string pulldownFrameResourcePath = "Sprites/PulldownFrame";
    [SerializeField] private string pulldownFrameTwoRowResourcePath = DefaultTwoRowPulldownFrameResourcePath;
    [SerializeField] private string pulldownTabResourcePath = "Sprites/PulldownTab";
    [SerializeField] private string behaviorFrameResourcePath = "Sprites/Behavior_Frame_A";
    [SerializeField] private string gaitFrameResourcePath = "Sprites/Gait_Frame_AB";
    [SerializeField] private string emoteFrameResourcePath = "Sprites/Emotes_Frame_A";
    [SerializeField] private string androidButtonSpriteResourcePath = "Sprites/AndroidButtonsAndQuests_B";
    [SerializeField] private string targetIconSpriteResourcePath = "Sprites/TargetIcon_D";
    [FormerlySerializedAs("noseButtonMargin")]
    [SerializeField] private float topControlButtonMargin = 24f;
    [SerializeField] private float modeButtonSpacing = 12f;
    [FormerlySerializedAs("noseButtonSize")]
    [SerializeField] private float topControlButtonSize = 176f;
    [SerializeField] private Vector2 topControlsInset = new Vector2(162f, 52f);
    [SerializeField] private Vector2 pulldownFrameSize = new Vector2(1620f, 341f);
    [SerializeField] private Vector2 pulldownFrameOffset = new Vector2(0f, 0f);
    [SerializeField] private Vector2 pulldownTabSize = new Vector2(428f, 98f);
    [SerializeField] private Vector2 pulldownTabOffset = new Vector2(0f, 0f);
    [SerializeField] private Vector2 pulldownRetractButtonSize = new Vector2(428f, 98f);
    [SerializeField] private Vector2 pulldownRetractButtonOffset = new Vector2(0f, 0f);
    [SerializeField] private float pulldownEndRetractButtonWidth = 150f;
    [SerializeField] private float modePanelIconSize = 128f;
    [SerializeField] private float dropdownWidth = 320f;
    [SerializeField] private float dropdownMaxHeight = 420f;
    [SerializeField] private float emoteDropdownWidth = 620f;
    [SerializeField] private float emoteDropdownMaxHeight = 620f;
    [SerializeField] private float emoteTileSize = 96f;
    [SerializeField] private int emoteGridColumns = 4;
    [SerializeField] private int uiSortOrder = 5100;
    [SerializeField] private bool autoHideTopControls = true;
    [SerializeField] private float topControlsSlideDuration = 0.18f;
    [SerializeField] private float topControlsHiddenTopPadding = 8f;
    [SerializeField] private bool respectTopSafeArea = true;
    [SerializeField] private bool scaleTopControlsToFitWidth = true;
    [SerializeField] private float targetPreviewSpinDegreesPerSecond = 24f;
    [SerializeField] private float targetPreviewViewAngleDegrees = 8f;
    [SerializeField] private float targetPreviewFigureScale = 2f;
    [SerializeField] private float tooltipFontSize = 22f;
    [SerializeField] private float tooltipMaxWidth = 340f;
    [SerializeField] private Vector2 tooltipPadding = new Vector2(16f, 10f);
    [SerializeField] private Vector2 tooltipScreenOffset = new Vector2(18f, -18f);
    [FormerlySerializedAs("noseButtonColor")]
    [SerializeField] private Color topControlButtonColor = new Color(0.96f, 0.95f, 0.9f, 0.96f);
    [SerializeField] private Color dropdownBackgroundColor = new Color(0.97f, 0.96f, 0.91f, 0f);
    [SerializeField] private Color dropdownRowColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Color dropdownSelectedColor = new Color(0.88f, 0.79f, 0.55f, 0.95f);
    [SerializeField] private Color dropdownTextColor = new Color(0.19f, 0.15f, 0.08f, 1f);
    [SerializeField] private Color tooltipBackgroundColor = new Color(0.97f, 0.96f, 0.91f, 0.96f);
    [SerializeField] private bool logSniffToggleDiagnostics = true;
    [SerializeField] private string lastSniffDiagnostic = string.Empty;

    private readonly List<GameObject> dropdownRows = new List<GameObject>();
    private readonly List<GameObject> emoteDropdownTiles = new List<GameObject>();
    private readonly List<Image> modeButtonBackgrounds = new List<Image>();
    private readonly List<Image> speedButtonBackgrounds = new List<Image>();

    [SerializeField] private bool isSniffModeActive;

    private Canvas overlayCanvas;
    private RectTransform pulldownFrameRect;
    private Image pulldownFrameImage;
    private Sprite pulldownFrameSprite;
    private Sprite pulldownFrameTwoRowSprite;
    private Sprite behaviorFrameSprite;
    private Sprite gaitFrameSprite;
    private Sprite emoteFrameSprite;
    private RectTransform pulldownTabRect;
    private Image pulldownTabImage;
    private RectTransform pulldownRetractButtonRect;
    private RectTransform pulldownLeftRetractButtonRect;
    private RectTransform pulldownRightRetractButtonRect;
    private RectTransform targetButtonRect;
    private Image targetButtonImage;
    private Image targetButtonIconImage;
    private RawImage targetPreviewImage;
    private Image targetCrosshairImage;
    private RenderTexture targetPreviewTexture;
    private GameObject targetPreviewWorldRoot;
    private GameObject targetPreviewClone;
    private Camera targetPreviewCamera;
    private Light targetPreviewLight;
    private WorldObject targetPreviewedAgent;
    private float targetPreviewFramingRadius = 1f;
    private RectTransform dropdownRect;
    private RectTransform dropdownContentRect;
    private ScrollRect dropdownScrollRect;
    private RectTransform modeButtonRect;
    private Image modeButtonImage;
    private Image modeIconImage;
    private RectTransform modePanelRect;
    private RectTransform speedButtonRect;
    private Image speedButtonImage;
    private Image speedIconImage;
    private RectTransform speedPanelRect;
    private RectTransform simulationButtonRect;
    private Image simulationButtonImage;
    private Image simulationIconImage;
    private RectTransform emoteButtonRect;
    private Image emoteButtonImage;
    private Image emoteIconImage;
    private RectTransform inventoryButtonRect;
    private Image inventoryButtonImage;
    private Image inventoryIconImage;
    private RectTransform digButtonRect;
    private Image digButtonImage;
    private Image digIconImage;
    private RectTransform homeButtonRect;
    private Image homeButtonImage;
    private RectTransform cameraModeButtonRect;
    private Image cameraModeButtonImage;
    private RectTransform questButtonRect;
    private Image questButtonImage;
    private RectTransform sniffResultsOverlayRect;
    private TextMeshProUGUI sniffResultsTitleLabel;
    private Image sniffGroundBarFill;
    private TextMeshProUGUI sniffGroundValueLabel;
    private Image sniffAirBarFill;
    private TextMeshProUGUI sniffAirValueLabel;
    private ScentAirGround subscribedSniffOverlayScentSystem;
    private WorldObject sniffOverlayAgent;
    private Cell sniffOverlayCell;
    private string sniffOverlayScentKey = string.Empty;
    private RectTransform emoteDropdownRect;
    private RectTransform emoteDropdownContentRect;
    private ScrollRect emoteDropdownScrollRect;
    private RectTransform tooltipRect;
    private TextMeshProUGUI tooltipLabel;
    private Image tooltipBackgroundImage;
    private TopPulldownTooltipTrigger activeTooltipTrigger;
    private AgentDecisionType displayedDecisionType = AgentDecisionType.Undefined;
    private WalkMode displayedWalkMode = WalkMode.None;
    private bool? displayedPausedState;
    private DogEmojiEntry? selectedEmoteEntry;
    private float topControlsVisibility;
    private float topControlsSlideVelocity;
    private bool pulldownOpenedByTab;
    private bool uiBuilt;
    private int lastSniffToggleFrame = -1;
    private float appliedPersistentButtonSize = -1f;
    private float nextPersistentButtonSizeRefreshTime;

    private readonly AgentDecisionType[] selectableDecisionModes =
    {
        AgentDecisionType.Player,
        AgentDecisionType.Follower,
        AgentDecisionType.Explorer,
        AgentDecisionType.Immobile,
        AgentDecisionType.Wanderer,
        AgentDecisionType.TaskFollower
    };

    private readonly WalkMode[] selectableSpeedModes =
    {
        WalkMode.Run,
        WalkMode.Walk,
        WalkMode.Sneak
    };

    private void Start()
    {
        EnsureDir();
        EnsureSniffVisuals();
        BuildRuntimeUIIfNeeded();
        RefreshTargetButtonSelectionState();
        EnsureSniffOverlaySubscription();
        RefreshSniffResultsOverlay();
    }

    private void Update()
    {
        PlayerInputState inputState = GetInputState();

        if (inputState != null && inputState.closeDialogsPressed)
            CloseOverlaysFromEscape();

        RefreshPersistentButtonSizePreference();
        RefreshModeButtonState();
        RefreshSpeedButtonState();
        RefreshSimulationButtonState();
        RefreshEmoteButtonState();
        RefreshTargetButtonPreview();
        SpinTargetButtonPreview();
        UpdateTopControlsAutoHide();
        RefreshSniffOverlayForContextChanges();

        if (inputState != null && inputState.scentFogViewTogglePressed)
            ToggleSniffMode("Keyboard.scentFogView");

        if (inputState != null && inputState.emotePressed)
            RepeatSelectedEmote();

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        CloseOpenPanelsIfClickedOutside(Mouse.current.position.ReadValue());
    }

    private PlayerInputState GetInputState()
    {
        EnsureDir();
        return dir != null && dir.gameInputRouter != null ? dir.gameInputRouter.InputState : GameInputRouter.Instance?.InputState;
    }

    private void CloseOverlaysFromEscape()
    {
        CloseDropdown();
        CloseModePanel();
        CloseSpeedPanel();
        CloseEmoteDropdown();
        CollapseTopControlsToTab();
        BottomBanner.Collapse();

        MenuSettingsDialog[] settingsDialogs = FindObjectsByType<MenuSettingsDialog>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < settingsDialogs.Length; i++)
            settingsDialogs[i]?.Close();

        InventoryDialogUI[] inventoryDialogs = FindObjectsByType<InventoryDialogUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < inventoryDialogs.Length; i++)
            inventoryDialogs[i]?.Hide();

        QuestJournalUI[] questDialogs = FindObjectsByType<QuestJournalUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < questDialogs.Length; i++)
            questDialogs[i]?.Hide();

//        PopupController[] popupControllers = FindObjectsByType<PopupController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
//        for (int i = 0; i < popupControllers.Length; i++)
//            popupControllers[i]?.Close();
    }

    private void OnEnable()
    {
        EnsureSniffOverlaySubscription();
    }

    private void OnDisable()
    {
        RemoveSniffOverlaySubscription();
    }

    private void OnDestroy()
    {
        RemoveSniffOverlaySubscription();
        DestroyTargetPreviewClone();
        ReleaseTargetPreviewTexture();
        DestroyTargetPreviewWorld();
    }

    private bool EnsureDir()
    {
        if (dir == null)
            dir = Dir.Instance;

        return dir != null;
    }

    private void BuildRuntimeUIIfNeeded()
    {
        if (uiBuilt)
            return;

        uiBuilt = true;
        RefreshPersistentButtonSizePreference(force: true);

        Transform canvasTransform = FindExistingScentTargetCanvas();
        GameObject canvasObject;
        if (canvasTransform != null)
        {
            canvasObject = canvasTransform.gameObject;
        }
        else
        {
            canvasObject = new GameObject(
                "ScentTargetCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
        }

        overlayCanvas = GetOrAddComponent<Canvas>(canvasObject);
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = uiSortOrder;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        GetOrAddComponent<GraphicRaycaster>(canvasObject);

        Transform scentControlsTransform = EnsureSectionContainer(canvasObject.transform, ScentControlsContainerName);
        Transform decisionModeControlsTransform = EnsureSectionContainer(canvasObject.transform, DecisionModeControlsContainerName);
        Transform speedControlsTransform = EnsureSectionContainer(canvasObject.transform, SpeedControlsContainerName);
        Transform simulationControlsTransform = EnsureSectionContainer(canvasObject.transform, SimulationControlsContainerName);
        Transform emoteControlsTransform = EnsureSectionContainer(canvasObject.transform, EmoteControlsContainerName);
        Transform inventoryControlsTransform = EnsureSectionContainer(canvasObject.transform, InventoryControlsContainerName);
        Transform digControlsTransform = EnsureSectionContainer(canvasObject.transform, DigControlsContainerName);
        Transform leftActionControlsTransform = EnsureSectionContainer(canvasObject.transform, LeftActionControlsContainerName);
        Transform tooltipTransform = EnsureSectionContainer(canvasObject.transform, TooltipContainerName);

        ReparentExistingUiElement(decisionModeControlsTransform, canvasObject.transform, "DecisionModeTitle");

        BuildPulldownFrame(canvasObject.transform);
        BuildPulldownTab(canvasObject.transform);
        BuildTargetButton(scentControlsTransform, canvasObject.transform);
        BuildModeButton(decisionModeControlsTransform, canvasObject.transform);
        BuildSpeedButton(speedControlsTransform, canvasObject.transform);
        BuildSimulationButton(simulationControlsTransform, canvasObject.transform);
        BuildEmoteButton(emoteControlsTransform, canvasObject.transform);
        BuildInventoryButton(inventoryControlsTransform, canvasObject.transform);
        BuildDigButton(digControlsTransform, canvasObject.transform);
        BuildLeftActionButtons(leftActionControlsTransform, canvasObject.transform);
        BuildDropdown(scentControlsTransform, canvasObject.transform);
        BuildModePanel(decisionModeControlsTransform, canvasObject.transform);
        BuildSpeedPanel(speedControlsTransform, canvasObject.transform);
        BuildEmoteDropdown(emoteControlsTransform, canvasObject.transform);
        BuildCornerControls(canvasObject.transform, canvasObject.transform);
        BuildSniffResultsOverlay(canvasObject.transform, canvasObject.transform);
        BuildTooltip(tooltipTransform, canvasObject.transform);
        if (!autoHideTopControls)
            topControlsVisibility = 1f;
        ApplyTopControlsSlidePosition();
        UpdatePulldownTabVisibility();
    }
}
