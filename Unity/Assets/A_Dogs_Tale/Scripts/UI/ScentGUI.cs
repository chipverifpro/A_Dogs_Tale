using System.Collections.Generic;
using DogGame;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class ScentGUI : MonoBehaviour
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
    [SerializeField] private string androidButtonSpriteResourcePath = "Sprites/AndroidButtonsAndQuests";
    [SerializeField] private string targetIconSpriteResourcePath = "Sprites/TargetIcon_D";
    [SerializeField] private float noseButtonSize = 176f;
    [SerializeField] private float noseButtonMargin = 24f;
    [SerializeField] private float modeButtonSpacing = 12f;
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
    [SerializeField] private float emoteDropdownWidth = 520f;
    [SerializeField] private float emoteDropdownMaxHeight = 520f;
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
    [SerializeField] private Color noseButtonColor = new Color(0.96f, 0.95f, 0.9f, 0.96f);
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

    private InputAction sniffAction;
    [SerializeField] private bool isSniffModeActive;

    private Canvas overlayCanvas;
    private RectTransform pulldownFrameRect;
    private Image pulldownFrameImage;
    private Sprite pulldownFrameSprite;
    private Sprite pulldownFrameTwoRowSprite;
    private RectTransform pulldownTabRect;
    private Image pulldownTabImage;
    private RectTransform pulldownRetractButtonRect;
    private RectTransform pulldownLeftRetractButtonRect;
    private RectTransform pulldownRightRetractButtonRect;
    private RectTransform noseButtonRect;
    private Image noseButtonImage;
    private Image noseIconImage;
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
    private RectTransform emoteDropdownRect;
    private RectTransform emoteDropdownContentRect;
    private ScrollRect emoteDropdownScrollRect;
    private RectTransform tooltipRect;
    private TextMeshProUGUI tooltipLabel;
    private Image tooltipBackgroundImage;
    private ScentGuiTooltipTrigger activeTooltipTrigger;
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
        WalkMode.Sneak,
        WalkMode.Walk,
        WalkMode.Run
    };

    private void Awake()
    {
        EnsureSniffAction();
    }

    private void Start()
    {
        EnsureDir();
        EnsureSniffVisuals();
        BuildRuntimeUIIfNeeded();
        RefreshNoseButtonSelectionState();
    }

    private void Update()
    {
        if (WasEscapePressedThisFrame())
            CloseOverlaysFromEscape();

        RefreshPersistentButtonSizePreference();
        RefreshModeButtonState();
        RefreshSpeedButtonState();
        RefreshSimulationButtonState();
        RefreshEmoteButtonState();
        RefreshTargetButtonPreview();
        SpinTargetButtonPreview();
        UpdateTopControlsAutoHide();

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            ToggleSniffMode("Keyboard.fKey");

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        CloseOpenPanelsIfClickedOutside(Mouse.current.position.ReadValue());
    }

    private bool WasEscapePressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return false;

        GameObject selectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return selectedObject == null ||
               (selectedObject.GetComponent<TMP_InputField>() == null &&
                selectedObject.GetComponent<InputField>() == null);
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

        PopupController[] popupControllers = FindObjectsByType<PopupController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < popupControllers.Length; i++)
            popupControllers[i]?.Close();
    }

    private void OnEnable()
    {
        EnsureSniffAction();
        sniffAction.performed += OnSniffToggle;
        sniffAction.Enable();
    }

    private void OnDisable()
    {
        if (sniffAction == null)
            return;

        sniffAction.performed -= OnSniffToggle;
        sniffAction.Disable();
    }

    private void OnDestroy()
    {
        DestroyTargetPreviewClone();
        ReleaseTargetPreviewTexture();
        DestroyTargetPreviewWorld();
    }

    private void EnsureSniffAction()
    {
        if (sniffAction != null)
            return;

        sniffAction = new InputAction(
            name: "Sniff",
            type: InputActionType.Button,
            binding: "<Keyboard>/f"
        );
    }

    private void OnSniffToggle(InputAction.CallbackContext ctx)
    {
        ToggleSniffMode("InputAction");
    }

    private void ToggleSniffMode(string triggerSource)
    {
        if (lastSniffToggleFrame == Time.frameCount)
            return;

        lastSniffToggleFrame = Time.frameCount;
        isSniffModeActive = !isSniffModeActive;

        EnsureSniffVisuals();

        if (sniffVisuals != null)
        {
            sniffVisuals.SetSniffMode(isSniffModeActive);
        }
        else if (logSniffToggleDiagnostics)
        {
            Debug.LogWarning("ScentGUI: sniffVisuals is not assigned and no SniffModeVisuals component was found.", this);
        }

        if (!EnsureDir() || dir.scentRegistry == null)
        {
            Debug.LogError("ScentGUI: scentRegistry is null!");
            LogSniffDiagnostic(triggerSource, "missing Dir or scentRegistry");
            return;
        }

        if (isSniffModeActive)
        {
            dir.scentRegistry.ActivateScentOverlay(dir.scentRegistry.SelectedTargetScent);
        }
        else
        {
            dir.scentRegistry.DeactivateScentOverlay();
            CloseDropdown();
        }

        LogSniffDiagnostic(triggerSource, isSniffModeActive ? "activated" : "deactivated");
    }

    private void LogSniffDiagnostic(string triggerSource, string result)
    {
        if (dir == null)
        {
            lastSniffDiagnostic = $"Sniff {triggerSource}: {result}; Dir=null";
        }
        else
        {
            ScentAirGround scentSystem = dir.scents != null ? dir.scents : dir.scentAirGround;
            Camera scentCamera = dir.scentCam;
            ScentSource selected = dir.scentRegistry != null ? dir.scentRegistry.SelectedTargetScent : null;

            string selectedDescription = DescribeScentSource(selected);
            string visualDescription = sniffVisuals != null ? $"sniffVisuals={sniffVisuals.name}" : "sniffVisuals=null";
            string cameraDescription = scentCamera != null
                ? $"{scentCamera.name}.enabled={scentCamera.enabled}"
                : "FogCamera=null";
            string scentSystemDescription = scentSystem != null
                ? $"currentAgentId={scentSystem.currentAgentId}, active={scentSystem.IsScentCameraActive}"
                : "ScentAirGround=null";

            lastSniffDiagnostic =
                $"Sniff {triggerSource}: {result}; mode={isSniffModeActive}; selected={selectedDescription}; {visualDescription}; {cameraDescription}; {scentSystemDescription}";
        }

        if (logSniffToggleDiagnostics)
            Debug.Log($"[ScentGUI] {lastSniffDiagnostic}", this);
    }

    private static string DescribeScentSource(ScentSource scentSource)
    {
        if (scentSource == null)
            return "null";

        string scentName = string.IsNullOrWhiteSpace(scentSource.scentName) ? scentSource.category.ToString() : scentSource.scentName;
        return $"{scentName}(agentId={scentSource.agentId})";
    }

    private void EnsureSniffVisuals()
    {
        if (sniffVisuals != null)
            return;

        sniffVisuals = GetComponent<SniffModeVisuals>();
        if (sniffVisuals != null)
            return;

        sniffVisuals = FindFirstObjectByType<SniffModeVisuals>(FindObjectsInactive.Include);
    }

    // Called by other systems (unchanged)
    public void OnSniff(Cell currentCell)
    {
        if (!EnsureDir() || dir.scentRegistry == null || dir.scents == null)
            return;

        var detections = dir.scentRegistry.CollectScentsAtCell(currentCell, dir.scents);
        // bind to UI
    }

    public void OnScentClicked(ScentDetection detection)
    {
        if (!EnsureDir() || dir.scentRegistry == null)
            return;

        ScentSource selectedSource = dir.scentRegistry.SetSelectedTargetScent(detection.scentSource);
        dir.scentRegistry.ActivateScentOverlay(selectedSource);
        RefreshNoseButtonSelectionState();
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
        BuildNoseButton(scentControlsTransform, canvasObject.transform);
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
        BuildTooltip(tooltipTransform, canvasObject.transform);
        if (!autoHideTopControls)
            topControlsVisibility = 1f;
        ApplyTopControlsSlidePosition();
        UpdatePulldownTabVisibility();
    }

    private Transform FindExistingScentTargetCanvas()
    {
        Transform localCanvas = FindDescendantByName(transform, CanvasName);
        if (localCanvas != null)
            return localCanvas;

        GameObject sceneCanvas = GameObject.Find(CanvasName);
        if (sceneCanvas != null)
            return sceneCanvas.transform;

        RectTransform[] rectTransforms = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform rectTransform = rectTransforms[i];
            if (rectTransform != null && rectTransform.name == CanvasName)
                return rectTransform;
        }

        return null;
    }

    private Transform EnsureSectionContainer(Transform canvasTransform, string containerName)
    {
        Transform sectionTransform = canvasTransform.Find(containerName);
        if (sectionTransform == null)
        {
            sectionTransform = FindDescendantByName(canvasTransform, containerName);
            if (sectionTransform != null && sectionTransform.parent != canvasTransform)
                sectionTransform.SetParent(canvasTransform, false);
        }

        if (sectionTransform == null)
        {
            GameObject sectionObject = new GameObject(containerName, typeof(RectTransform));
            sectionObject.transform.SetParent(canvasTransform, false);
            sectionTransform = sectionObject.transform;
        }

        RectTransform sectionRect = sectionTransform as RectTransform;
        if (sectionRect == null)
            sectionRect = GetOrAddComponent<RectTransform>(sectionTransform.gameObject);

        sectionRect.anchorMin = Vector2.zero;
        sectionRect.anchorMax = Vector2.one;
        sectionRect.offsetMin = Vector2.zero;
        sectionRect.offsetMax = Vector2.zero;
        sectionRect.pivot = new Vector2(0.5f, 0.5f);

        return sectionTransform;
    }

    private void ReparentExistingUiElement(Transform preferredParent, Transform searchRoot, string elementName)
    {
        Transform existing = FindDescendantByName(searchRoot, elementName);
        if (existing != null && existing.parent != preferredParent)
            existing.SetParent(preferredParent, false);
    }

    private Transform FindExistingUiElement(Transform preferredParent, Transform searchRoot, string elementName)
    {
        Transform existing = preferredParent.Find(elementName);
        if (existing != null)
            return existing;

        existing = FindDescendantByName(searchRoot, elementName);
        if (existing != null && existing.parent != preferredParent)
            existing.SetParent(preferredParent, false);

        return existing;
    }

    private Transform FindDescendantByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendantByName(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void BuildPulldownFrame(Transform canvasTransform)
    {
        Transform existingFrame = canvasTransform.Find("PulldownFrame");
        GameObject frameObject;
        if (existingFrame == null)
        {
            frameObject = new GameObject(
                "PulldownFrame",
                typeof(RectTransform),
                typeof(Image));
            frameObject.transform.SetParent(canvasTransform, false);
        }
        else
        {
            frameObject = existingFrame.gameObject;
        }

        frameObject.transform.SetAsFirstSibling();

        pulldownFrameRect = GetOrAddComponent<RectTransform>(frameObject);
        pulldownFrameRect.anchorMin = new Vector2(0.5f, 1f);
        pulldownFrameRect.anchorMax = new Vector2(0.5f, 1f);
        pulldownFrameRect.pivot = new Vector2(0.5f, 1f);
        pulldownFrameRect.localScale = Vector3.one * GetTopControlsFitScale();
        pulldownFrameRect.anchoredPosition = GetPulldownFrameShownPosition();
        pulldownFrameRect.sizeDelta = GetPulldownFrameSizeForCurrentButtonSize();

        pulldownFrameImage = GetOrAddComponent<Image>(frameObject);
        pulldownFrameImage.sprite = GetPulldownFrameSprite();
        pulldownFrameImage.color = Color.white;
        pulldownFrameImage.preserveAspect = false;
        pulldownFrameImage.raycastTarget = false;

        BuildPulldownRetractButton(frameObject.transform);
        BuildPulldownEndRetractButtons(frameObject.transform);
    }

    private void BuildPulldownRetractButton(Transform frameTransform)
    {
        Transform existingButton = frameTransform.Find("PulldownRetractButton");
        GameObject buttonObject;
        if (existingButton == null)
        {
            buttonObject = new GameObject(
                "PulldownRetractButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(frameTransform, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        pulldownRetractButtonRect = GetOrAddComponent<RectTransform>(buttonObject);
        pulldownRetractButtonRect.anchorMin = new Vector2(0.5f, 0f);
        pulldownRetractButtonRect.anchorMax = new Vector2(0.5f, 0f);
        pulldownRetractButtonRect.pivot = new Vector2(0.5f, 0f);
        pulldownRetractButtonRect.anchoredPosition = pulldownRetractButtonOffset;
        pulldownRetractButtonRect.sizeDelta = pulldownRetractButtonSize;

        Image buttonImage = GetOrAddComponent<Image>(buttonObject);
        buttonImage.color = new Color(1f, 1f, 1f, 0f);
        buttonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = buttonImage;
        button.onClick.RemoveListener(CollapseTopControlsToTab);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(CollapseTopControlsToTab);

        ConfigureTooltip(buttonObject, () => "Hide Controls");
    }

    private void BuildPulldownEndRetractButtons(Transform frameTransform)
    {
        pulldownLeftRetractButtonRect = BuildPulldownEndRetractButton(frameTransform, "PulldownLeftRetractButton", leftSide: true);
        pulldownRightRetractButtonRect = BuildPulldownEndRetractButton(frameTransform, "PulldownRightRetractButton", leftSide: false);
        ApplyPulldownEndRetractButtonRects();
    }

    private RectTransform BuildPulldownEndRetractButton(Transform frameTransform, string objectName, bool leftSide)
    {
        Transform existingButton = frameTransform.Find(objectName);
        GameObject buttonObject;
        if (existingButton == null)
        {
            buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(frameTransform, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        RectTransform rect = GetOrAddComponent<RectTransform>(buttonObject);
        rect.anchorMin = new Vector2(leftSide ? 0f : 1f, 0.5f);
        rect.anchorMax = new Vector2(leftSide ? 0f : 1f, 0.5f);
        rect.pivot = new Vector2(leftSide ? 0f : 1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        Image buttonImage = GetOrAddComponent<Image>(buttonObject);
        buttonImage.color = new Color(1f, 1f, 1f, 0f);
        buttonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = buttonImage;
        button.onClick.RemoveListener(CollapseTopControlsToTab);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(CollapseTopControlsToTab);

        ConfigureTooltip(buttonObject, () => "Hide Controls");
        return rect;
    }

    private void ApplyPulldownEndRetractButtonRects()
    {
        Vector2 frameSize = GetPulldownFrameSizeForCurrentButtonSize();
        Vector2 size = new Vector2(Mathf.Max(1f, pulldownEndRetractButtonWidth), frameSize.y);

        if (pulldownLeftRetractButtonRect != null)
            pulldownLeftRetractButtonRect.sizeDelta = size;

        if (pulldownRightRetractButtonRect != null)
            pulldownRightRetractButtonRect.sizeDelta = size;
    }

    private void BuildPulldownTab(Transform canvasTransform)
    {
        Transform existingTab = canvasTransform.Find("PulldownTab");
        GameObject tabObject;
        if (existingTab == null)
        {
            tabObject = new GameObject(
                "PulldownTab",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            tabObject.transform.SetParent(canvasTransform, false);
        }
        else
        {
            tabObject = existingTab.gameObject;
        }

        tabObject.transform.SetAsLastSibling();

        pulldownTabRect = GetOrAddComponent<RectTransform>(tabObject);
        pulldownTabRect.anchorMin = new Vector2(1f, 1f);
        pulldownTabRect.anchorMax = new Vector2(1f, 1f);
        pulldownTabRect.pivot = new Vector2(1f, 1f);
        pulldownTabRect.localScale = Vector3.one * GetTopControlsFitScale();
        pulldownTabRect.anchoredPosition = GetPulldownTabPosition();
        pulldownTabRect.sizeDelta = pulldownTabSize;

        pulldownTabImage = GetOrAddComponent<Image>(tabObject);
        pulldownTabImage.sprite = GetPulldownTabSprite();
        pulldownTabImage.color = Color.white;
        pulldownTabImage.preserveAspect = true;
        pulldownTabImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(tabObject);
        button.targetGraphic = pulldownTabImage;
        button.onClick.RemoveListener(ExpandTopControlsFromTab);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(ExpandTopControlsFromTab);

        ConfigureTooltip(tabObject, () => "Show Controls");
    }

    private Vector2 GetTopControlPosition(int slotFromRight)
    {
        float scale = GetTopControlsFitScale();
        if (UseTwoRowTopControls())
        {
            int indexFromLeft = Mathf.Clamp(TopControlButtonCount - 1 - slotFromRight, 0, TopControlButtonCount - 1);
            int row = indexFromLeft / TopControlColumnsWhenTwoRows;
            int column = indexFromLeft % TopControlColumnsWhenTwoRows;
            float frameWidth = GetPulldownFrameSizeForCurrentButtonSize().x * scale;
            float leftEdge = (pulldownFrameOffset.x * scale) - (frameWidth * 0.5f);
            float x = leftEdge +
                      (topControlsInset.x * scale) +
                      ((topControlButtonSize + modeButtonSpacing) * column * scale) +
                      (topControlButtonSize * scale);
            float y = -(((topControlsInset.y + ((topControlButtonSize + modeButtonSpacing) * row)) * scale) + GetTopSafeAreaInset());
            return new Vector2(x, y);
        }

        return new Vector2(
            GetTopControlsFrameRightEdge(scale) - GetTopControlRightInset(slotFromRight, scale),
            -((topControlsInset.y * scale) + GetTopSafeAreaInset()));
    }

    private Vector2 GetTopPanelPosition(int slotFromRight)
    {
        float scale = GetTopControlsFitScale();
        Vector2 buttonPosition = GetTopControlPosition(slotFromRight);
        int row = UseTwoRowTopControls()
            ? Mathf.Clamp(TopControlButtonCount - 1 - slotFromRight, 0, TopControlButtonCount - 1) / TopControlColumnsWhenTwoRows
            : 0;
        float y = -(((topControlsInset.y +
                      (topControlButtonSize * (row + 1)) +
                      (modeButtonSpacing * row)) * scale) + GetTopSafeAreaInset() + 12f);
        return new Vector2(buttonPosition.x, y);
    }

    private float GetTopControlsFrameRightEdge(float scale)
    {
        return (pulldownFrameOffset.x * scale) + ((GetPulldownFrameSizeForCurrentButtonSize().x * scale) * 0.5f);
    }

    private float GetTopControlRightInset(int slotFromRight, float scale)
    {
        float scaledButtonSize = topControlButtonSize * scale;
        float scaledButtonSpacing = modeButtonSpacing * scale;
        return (topControlsInset.x * scale) + ((scaledButtonSize + scaledButtonSpacing) * slotFromRight);
    }

    private Vector2 GetPulldownFrameShownPosition()
    {
        return new Vector2(pulldownFrameOffset.x * GetTopControlsFitScale(), GetPulldownFrameShownY());
    }

    private float GetPulldownFrameShownY()
    {
        return (pulldownFrameOffset.y * GetTopControlsFitScale()) - GetTopSafeAreaInset();
    }

    private Vector2 GetPulldownTabPosition()
    {
        float scale = GetTopControlsFitScale();
        return new Vector2(pulldownTabOffset.x * scale, (pulldownTabOffset.y * scale) - GetTopSafeAreaInset());
    }

    private float GetTopSafeAreaInset()
    {
        if (!respectTopSafeArea || overlayCanvas == null || Screen.height <= 0)
            return 0f;

        float topInsetPixels = Mathf.Max(0f, Screen.height - Screen.safeArea.yMax);
        if (topInsetPixels <= 0f)
            return 0f;

        RectTransform canvasRect = overlayCanvas.transform as RectTransform;
        float canvasHeight = canvasRect != null && canvasRect.rect.height > 0f
            ? canvasRect.rect.height
            : Screen.height;

        return topInsetPixels * (canvasHeight / Screen.height);
    }

    private float GetTopControlsFitScale()
    {
        if (!scaleTopControlsToFitWidth)
            return 1f;

        float canvasWidth = GetCanvasWidth();

        float frameWidth = GetPulldownFrameSizeForCurrentButtonSize().x;
        if (canvasWidth <= 0f || frameWidth <= 0f)
            return 1f;

        return Mathf.Min(1f, canvasWidth / frameWidth);
    }

    private Vector2 GetPulldownFrameSizeForCurrentButtonSize()
    {
        return UseTwoRowTopControls()
            ? GetPulldownFrameSize(columns: TopControlColumnsWhenTwoRows, rows: 2)
            : GetPulldownFrameSize(columns: TopControlButtonCount, rows: 1);
    }

    private Vector2 GetSingleRowPulldownFrameSizeForCurrentButtonSize()
    {
        return GetPulldownFrameSize(columns: TopControlButtonCount, rows: 1);
    }

    private Vector2 GetPulldownFrameSize(int columns, int rows)
    {
        float buttonSize = Mathf.Max(1f, topControlButtonSize);
        float spacing = Mathf.Max(0f, modeButtonSpacing);
        int clampedColumns = Mathf.Max(1, columns);
        int clampedRows = Mathf.Max(1, rows);
        float width = (topControlsInset.x * 2f) +
                      (buttonSize * clampedColumns) +
                      (spacing * Mathf.Max(0, clampedColumns - 1));
        float bottomPadding = Mathf.Max(0f, pulldownFrameSize.y - topControlsInset.y - PersistentGameSettings.DefaultButtonSize);
        float height = topControlsInset.y +
                       (buttonSize * clampedRows) +
                       (spacing * Mathf.Max(0, clampedRows - 1)) +
                       bottomPadding;
        return new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
    }

    private bool UseTwoRowTopControls()
    {
        if (!scaleTopControlsToFitWidth)
            return false;

        float canvasWidth = GetCanvasWidth();
        if (canvasWidth <= 0f)
            return false;

        return GetSingleRowPulldownFrameSizeForCurrentButtonSize().x > canvasWidth;
    }

    private float GetCanvasWidth()
    {
        RectTransform canvasRect = overlayCanvas != null ? overlayCanvas.transform as RectTransform : null;
        return canvasRect != null && canvasRect.rect.width > 0f
            ? canvasRect.rect.width
            : Screen.width;
    }

    private void ConfigureTopControlRect(RectTransform rectTransform, int slotFromRight)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        rectTransform.localScale = Vector3.one * GetTopControlsFitScale();
        rectTransform.anchoredPosition = GetTopControlPosition(slotFromRight);
        rectTransform.sizeDelta = new Vector2(topControlButtonSize, topControlButtonSize);
    }

    private void ConfigureTopPanelRect(RectTransform rectTransform, int slotFromRight)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        rectTransform.anchoredPosition = GetTopPanelPosition(slotFromRight);
    }

    private void ConfigureTopControlIconRect(RectTransform iconRect, float sizeScale)
    {
        if (iconRect == null)
            return;

        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.localScale = Vector3.one;
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = Vector2.one * (topControlButtonSize * sizeScale);
    }

    private void BuildNoseButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "TargetButton");
        if (existingButton == null)
            existingButton = FindExistingUiElement(parent, searchRoot, "ScentTargetButton");

        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "TargetButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        buttonObject.name = "TargetButton";

        noseButtonRect = buttonObject.GetComponent<RectTransform>();
        if (createdButton)
        {
            noseButtonRect.anchorMin = new Vector2(1f, 1f);
            noseButtonRect.anchorMax = new Vector2(1f, 1f);
            noseButtonRect.pivot = new Vector2(1f, 1f);
            noseButtonRect.anchoredPosition = new Vector2(-noseButtonMargin, -noseButtonMargin);
            noseButtonRect.sizeDelta = new Vector2(noseButtonSize, noseButtonSize);
        }
        ConfigureTopControlRect(noseButtonRect, 0);

        noseButtonImage = GetOrAddComponent<Image>(buttonObject);
        noseButtonImage.color = noseButtonColor;
        noseButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = noseButtonImage;
        button.onClick.RemoveListener(ToggleDropdown);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(ToggleDropdown);

        Transform existingPreview = buttonObject.transform.Find("AgentPreview");
        GameObject previewObject;
        if (existingPreview == null)
        {
            previewObject = new GameObject("AgentPreview", typeof(RectTransform), typeof(RawImage));
            previewObject.transform.SetParent(buttonObject.transform, false);
        }
        else
        {
            previewObject = existingPreview.gameObject;
        }

        RectTransform previewRect = previewObject.GetComponent<RectTransform>();
        ConfigureTopControlIconRect(previewRect, 0.82f);

        targetPreviewImage = GetOrAddComponent<RawImage>(previewObject);
        targetPreviewImage.color = Color.white;
        targetPreviewImage.raycastTarget = false;
        previewObject.transform.SetAsFirstSibling();

        Transform existingIcon = buttonObject.transform.Find("Icon");
        GameObject iconObject;
        bool createdIcon = existingIcon == null;
        if (createdIcon)
        {
            iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
        }
        else
        {
            iconObject = existingIcon.gameObject;
        }

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        if (createdIcon)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = Vector2.one * (noseButtonSize * 0.68f);
            iconRect.anchoredPosition = Vector2.zero;
        }
        ConfigureTopControlIconRect(iconRect, 0.68f);

        noseIconImage = GetOrAddComponent<Image>(iconObject);
        noseIconImage.sprite = GetTargetCrosshairSprite();
        noseIconImage.preserveAspect = true;
        noseIconImage.color = Color.white;
        noseIconImage.raycastTarget = false;
        targetCrosshairImage = noseIconImage;
        iconObject.transform.SetAsLastSibling();

        RefreshTargetButtonPreview(force: true);
        ConfigureTooltip(buttonObject, GetTargetButtonTooltipText);
    }

    private void BuildModeButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "DecisionModeButton");
        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "DecisionModeButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        modeButtonRect = buttonObject.GetComponent<RectTransform>();
        if (createdButton)
        {
            modeButtonRect.anchorMin = new Vector2(1f, 1f);
            modeButtonRect.anchorMax = new Vector2(1f, 1f);
            modeButtonRect.pivot = new Vector2(1f, 1f);
            modeButtonRect.anchoredPosition = new Vector2(
                -(noseButtonMargin + noseButtonSize + modeButtonSpacing),
                -noseButtonMargin);
            modeButtonRect.sizeDelta = new Vector2(noseButtonSize, noseButtonSize);
        }
        ConfigureTopControlRect(modeButtonRect, 1);

        modeButtonImage = GetOrAddComponent<Image>(buttonObject);
        modeButtonImage.color = noseButtonColor;
        modeButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = modeButtonImage;
        button.onClick.RemoveListener(ToggleModePanel);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(ToggleModePanel);

        Transform existingIcon = buttonObject.transform.Find("Icon");
        GameObject iconObject;
        bool createdIcon = existingIcon == null;
        if (createdIcon)
        {
            iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
        }
        else
        {
            iconObject = existingIcon.gameObject;
        }

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        if (createdIcon)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = Vector2.one * (noseButtonSize * 0.72f);
            iconRect.anchoredPosition = Vector2.zero;
        }
        ConfigureTopControlIconRect(iconRect, 0.72f);

        modeIconImage = GetOrAddComponent<Image>(iconObject);
        modeIconImage.preserveAspect = true;
        modeIconImage.color = Color.white;
        RefreshModeButtonState(force: true);

        ConfigureTooltip(buttonObject, () => "Movement Mode");
    }

    private void BuildSimulationButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "SimulationPauseButton");
        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "SimulationPauseButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        simulationButtonRect = buttonObject.GetComponent<RectTransform>();
        if (createdButton)
        {
            simulationButtonRect.anchorMin = new Vector2(1f, 1f);
            simulationButtonRect.anchorMax = new Vector2(1f, 1f);
            simulationButtonRect.pivot = new Vector2(1f, 1f);
            simulationButtonRect.anchoredPosition = new Vector2(
                -(noseButtonMargin + ((noseButtonSize + modeButtonSpacing) * 3f)),
                -noseButtonMargin);
            simulationButtonRect.sizeDelta = new Vector2(noseButtonSize, noseButtonSize);
        }
        ConfigureTopControlRect(simulationButtonRect, 3);

        simulationButtonImage = GetOrAddComponent<Image>(buttonObject);
        simulationButtonImage.color = noseButtonColor;
        simulationButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = simulationButtonImage;
        button.onClick.RemoveListener(ToggleSimulationPause);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(ToggleSimulationPause);

        Transform existingIcon = buttonObject.transform.Find("Icon");
        GameObject iconObject;
        bool createdIcon = existingIcon == null;
        if (createdIcon)
        {
            iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
        }
        else
        {
            iconObject = existingIcon.gameObject;
        }

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        if (createdIcon)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = Vector2.one * (noseButtonSize * 0.72f);
            iconRect.anchoredPosition = Vector2.zero;
        }
        ConfigureTopControlIconRect(iconRect, 0.72f);

        simulationIconImage = GetOrAddComponent<Image>(iconObject);
        simulationIconImage.preserveAspect = true;
        simulationIconImage.color = Color.white;
        RefreshSimulationButtonState(force: true);

        ConfigureTooltip(buttonObject, GetSimulationButtonTooltipText);
    }

    private void BuildSpeedButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "SpeedModeButton");
        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "SpeedModeButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        speedButtonRect = buttonObject.GetComponent<RectTransform>();
        speedButtonRect.anchorMin = new Vector2(1f, 1f);
        speedButtonRect.anchorMax = new Vector2(1f, 1f);
        speedButtonRect.pivot = new Vector2(1f, 1f);
        speedButtonRect.anchoredPosition = new Vector2(
            -(noseButtonMargin + ((noseButtonSize + modeButtonSpacing) * 3f)),
            -noseButtonMargin);
        speedButtonRect.sizeDelta = new Vector2(noseButtonSize, noseButtonSize);
        ConfigureTopControlRect(speedButtonRect, 2);

        speedButtonImage = GetOrAddComponent<Image>(buttonObject);
        speedButtonImage.color = noseButtonColor;
        speedButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = speedButtonImage;
        button.onClick.RemoveListener(ToggleSpeedPanel);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(ToggleSpeedPanel);

        Transform existingIcon = buttonObject.transform.Find("Icon");
        GameObject iconObject;
        bool createdIcon = existingIcon == null;
        if (createdIcon)
        {
            iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
        }
        else
        {
            iconObject = existingIcon.gameObject;
        }

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        if (createdIcon)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = Vector2.one * (noseButtonSize * 0.72f);
            iconRect.anchoredPosition = Vector2.zero;
        }
        ConfigureTopControlIconRect(iconRect, 0.72f);

        speedIconImage = GetOrAddComponent<Image>(iconObject);
        speedIconImage.preserveAspect = true;
        speedIconImage.color = Color.white;
        RefreshSpeedButtonState(force: true);

        ConfigureTooltip(buttonObject, () => "Speed Select");
    }

    private void BuildEmoteButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "EmoteButton");
        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "EmoteButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        emoteButtonRect = buttonObject.GetComponent<RectTransform>();
        if (createdButton)
        {
            emoteButtonRect.anchorMin = new Vector2(1f, 1f);
            emoteButtonRect.anchorMax = new Vector2(1f, 1f);
            emoteButtonRect.pivot = new Vector2(1f, 1f);
            emoteButtonRect.anchoredPosition = new Vector2(
                -(noseButtonMargin + ((noseButtonSize + modeButtonSpacing) * 4f)),
                -noseButtonMargin);
            emoteButtonRect.sizeDelta = new Vector2(noseButtonSize, noseButtonSize);
        }
        ConfigureTopControlRect(emoteButtonRect, 4);

        emoteButtonImage = GetOrAddComponent<Image>(buttonObject);
        emoteButtonImage.color = noseButtonColor;
        emoteButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = emoteButtonImage;
        button.onClick.RemoveListener(ToggleEmoteDropdown);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(ToggleEmoteDropdown);

        Transform existingIcon = buttonObject.transform.Find("Icon");
        GameObject iconObject;
        bool createdIcon = existingIcon == null;
        if (createdIcon)
        {
            iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
        }
        else
        {
            iconObject = existingIcon.gameObject;
        }

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        if (createdIcon)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = Vector2.one * (noseButtonSize * 0.72f);
            iconRect.anchoredPosition = Vector2.zero;
        }
        ConfigureTopControlIconRect(iconRect, 0.72f);

        emoteIconImage = GetOrAddComponent<Image>(iconObject);
        emoteIconImage.preserveAspect = true;
        emoteIconImage.color = Color.white;
        RefreshEmoteButtonState(force: true);

        ConfigureTooltip(buttonObject, GetEmoteButtonTooltipText);
    }

    private void BuildInventoryButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "InventoryButton");
        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "InventoryButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        inventoryButtonRect = buttonObject.GetComponent<RectTransform>();
        if (createdButton)
        {
            inventoryButtonRect.anchorMin = new Vector2(1f, 1f);
            inventoryButtonRect.anchorMax = new Vector2(1f, 1f);
            inventoryButtonRect.pivot = new Vector2(1f, 1f);
            inventoryButtonRect.anchoredPosition = new Vector2(
                -(noseButtonMargin + ((noseButtonSize + modeButtonSpacing) * 5f)),
                -noseButtonMargin);
            inventoryButtonRect.sizeDelta = new Vector2(noseButtonSize, noseButtonSize);
        }
        ConfigureTopControlRect(inventoryButtonRect, 5);

        inventoryButtonImage = GetOrAddComponent<Image>(buttonObject);
        inventoryButtonImage.color = noseButtonColor;
        inventoryButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = inventoryButtonImage;
        button.onClick.RemoveListener(OpenInventoryDialog);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(OpenInventoryDialog);

        Transform existingIcon = buttonObject.transform.Find("Icon");
        GameObject iconObject;
        bool createdIcon = existingIcon == null;
        if (createdIcon)
        {
            iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
        }
        else
        {
            iconObject = existingIcon.gameObject;
        }

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        if (createdIcon)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = Vector2.one * (noseButtonSize * 0.72f);
            iconRect.anchoredPosition = Vector2.zero;
        }
        ConfigureTopControlIconRect(iconRect, 0.72f);

        inventoryIconImage = GetOrAddComponent<Image>(iconObject);
        inventoryIconImage.sprite = GetInventoryButtonSprite();
        inventoryIconImage.preserveAspect = true;
        inventoryIconImage.color = Color.white;

        ConfigureTooltip(buttonObject, () => "Inventory");
    }

    private void BuildDigButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "DigButton");
        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "DigButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        digButtonRect = buttonObject.GetComponent<RectTransform>();
        digButtonRect.anchorMin = new Vector2(1f, 1f);
        digButtonRect.anchorMax = new Vector2(1f, 1f);
        digButtonRect.pivot = new Vector2(1f, 1f);
        digButtonRect.anchoredPosition = new Vector2(
            -(noseButtonMargin + ((noseButtonSize + modeButtonSpacing) * 6f)),
            -noseButtonMargin);
        digButtonRect.sizeDelta = new Vector2(noseButtonSize, noseButtonSize);
        ConfigureTopControlRect(digButtonRect, 6);

        digButtonImage = GetOrAddComponent<Image>(buttonObject);
        digButtonImage.color = noseButtonColor;
        digButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = digButtonImage;
        button.onClick.RemoveListener(HandleDigButtonPressed);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(HandleDigButtonPressed);

        Transform existingIcon = buttonObject.transform.Find("Icon");
        GameObject iconObject;
        bool createdIcon = existingIcon == null;
        if (createdIcon)
        {
            iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
        }
        else
        {
            iconObject = existingIcon.gameObject;
        }

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;

        digIconImage = GetOrAddComponent<Image>(iconObject);
        digIconImage.sprite = GetDigHoleButtonSprite();
        digIconImage.preserveAspect = true;
        digIconImage.color = Color.white;
        SetDigIconSize(iconRect, digIconImage.sprite);

        ConfigureTooltip(buttonObject, () => "Dig");
    }

    private void SetDigIconSize(RectTransform iconRect, Sprite sprite)
    {
        if (iconRect == null)
            return;

        float iconWidth = topControlButtonSize * 0.72f;
        float aspectRatio = sprite != null && sprite.rect.width > 0f
            ? sprite.rect.height / sprite.rect.width
            : 1f;
        float iconHeight = Mathf.Min(iconWidth * aspectRatio, topControlButtonSize * 0.9f);
        iconRect.sizeDelta = new Vector2(iconWidth, iconHeight);
    }

    private void BuildLeftActionButtons(Transform parent, Transform searchRoot)
    {
        homeButtonRect = BuildLeftActionButton(
            parent,
            searchRoot,
            "HomeButton",
            spriteIndex: 0,
            slotFromRight: HomeButtonTopSlotFromRight,
            HandleHomeButtonPressed,
            "Home",
            out homeButtonImage);

        cameraModeButtonRect = BuildLeftActionButton(
            parent,
            searchRoot,
            "CameraModeButton",
            spriteIndex: 2,
            slotFromRight: CameraModeButtonTopSlotFromRight,
            HandleCameraModeButtonPressed,
            "Camera Mode",
            out cameraModeButtonImage);

        questButtonRect = BuildLeftActionButton(
            parent,
            searchRoot,
            "QuestButton",
            spriteIndex: 1,
            slotFromRight: QuestButtonTopSlotFromRight,
            HandleQuestButtonPressed,
            "Quests",
            out questButtonImage);
    }

    private RectTransform BuildLeftActionButton(
        Transform parent,
        Transform searchRoot,
        string buttonName,
        int spriteIndex,
        int slotFromRight,
        UnityAction clickHandler,
        string tooltipText,
        out Image buttonImage)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, buttonName);
        GameObject buttonObject;
        if (existingButton == null)
        {
            buttonObject = new GameObject(
                buttonName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        RectTransform buttonRect = GetOrAddComponent<RectTransform>(buttonObject);
        ConfigureTopControlRect(buttonRect, slotFromRight);

        buttonImage = GetOrAddComponent<Image>(buttonObject);
        buttonImage.sprite = GetAndroidButtonSprite(spriteIndex);
        buttonImage.preserveAspect = true;
        buttonImage.color = Color.white;
        buttonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = buttonImage;
        button.onClick.RemoveListener(clickHandler);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(clickHandler);

        ConfigureTooltip(buttonObject, () => tooltipText);

        return buttonRect;
    }

    private void UpdateTopControlsAutoHide()
    {
        if (!uiBuilt)
            return;

        bool targetControlsVisible = !autoHideTopControls || pulldownOpenedByTab || IsAnyTopPanelOpen();
        float targetVisibility = targetControlsVisible ? 1f : 0f;

        if (!autoHideTopControls)
        {
            topControlsVisibility = targetVisibility;
            topControlsSlideVelocity = 0f;
        }
        else
        {
            topControlsVisibility = Mathf.SmoothDamp(
                topControlsVisibility,
                targetVisibility,
                ref topControlsSlideVelocity,
                Mathf.Max(0.01f, topControlsSlideDuration),
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            if (Mathf.Abs(topControlsVisibility - targetVisibility) < 0.001f)
            {
                topControlsVisibility = targetVisibility;
                topControlsSlideVelocity = 0f;
            }
        }

        ApplyTopControlsSlidePosition();
        UpdatePulldownTabVisibility(targetControlsVisible);

        if (targetVisibility <= 0f && topControlsVisibility < 0.05f)
            HideTooltip();
    }

    private bool IsAnyTopPanelOpen()
    {
        return (dropdownRect != null && dropdownRect.gameObject.activeSelf) ||
               (modePanelRect != null && modePanelRect.gameObject.activeSelf) ||
               (speedPanelRect != null && speedPanelRect.gameObject.activeSelf) ||
               (emoteDropdownRect != null && emoteDropdownRect.gameObject.activeSelf);
    }

    private void ApplyTopControlsSlidePosition()
    {
        float scale = GetTopControlsFitScale();
        float shownY = -((topControlsInset.y * scale) + GetTopSafeAreaInset());
        int rowCount = UseTwoRowTopControls() ? 2 : 1;
        float hiddenY = (((topControlButtonSize * rowCount) + (modeButtonSpacing * Mathf.Max(0, rowCount - 1))) * scale) + topControlsHiddenTopPadding;
        float y = Mathf.Lerp(hiddenY, shownY, topControlsVisibility);
        float frameY = Mathf.Lerp(GetPulldownFrameHiddenY(), GetPulldownFrameShownY(), topControlsVisibility);

        ApplyTopControlsFitScale();
        ApplyPulldownFramePosition(frameY);
        ApplyPulldownTabPosition();
        ApplyTopControlPosition(noseButtonRect, 0, y);
        ApplyTopControlPosition(modeButtonRect, 1, y);
        ApplyTopControlPosition(speedButtonRect, 2, y);
        ApplyTopControlPosition(simulationButtonRect, 3, y);
        ApplyTopControlPosition(emoteButtonRect, 4, y);
        ApplyTopControlPosition(inventoryButtonRect, 5, y);
        ApplyTopControlPosition(digButtonRect, 6, y);
        ApplyTopControlPosition(questButtonRect, QuestButtonTopSlotFromRight, y);
        ApplyTopControlPosition(cameraModeButtonRect, CameraModeButtonTopSlotFromRight, y);
        ApplyTopControlPosition(homeButtonRect, HomeButtonTopSlotFromRight, y);
        ApplyTopPanelPositions();
    }

    private void ApplyTopControlsFitScale()
    {
        float scale = GetTopControlsFitScale();
        ApplyTopControlSizesForCurrentButtonSize();
        ApplyTopControlScale(pulldownFrameRect, scale);
        ApplyTopControlScale(pulldownTabRect, scale);
        ApplyTopControlScale(noseButtonRect, scale);
        ApplyTopControlScale(modeButtonRect, scale);
        ApplyTopControlScale(speedButtonRect, scale);
        ApplyTopControlScale(simulationButtonRect, scale);
        ApplyTopControlScale(emoteButtonRect, scale);
        ApplyTopControlScale(inventoryButtonRect, scale);
        ApplyTopControlScale(digButtonRect, scale);
        ApplyTopControlScale(questButtonRect, scale);
        ApplyTopControlScale(cameraModeButtonRect, scale);
        ApplyTopControlScale(homeButtonRect, scale);
    }

    private void RefreshPersistentButtonSizePreference(bool force = false)
    {
        if (!force && Time.unscaledTime < nextPersistentButtonSizeRefreshTime)
            return;

        nextPersistentButtonSizeRefreshTime = Time.unscaledTime + 0.25f;
        float savedButtonSize = PersistentGameSettings.SnapButtonSize(PersistentGameSettings.GetCurrentOrSaved().buttonSize);
        if (!force && Mathf.Approximately(savedButtonSize, appliedPersistentButtonSize))
            return;

        appliedPersistentButtonSize = savedButtonSize;
        topControlButtonSize = savedButtonSize;
        noseButtonSize = savedButtonSize;

        if (pulldownFrameRect == null)
            return;

        ApplyTopControlSizesForCurrentButtonSize();
        ApplyTopControlsSlidePosition();
    }

    private void ApplyTopControlSizesForCurrentButtonSize()
    {
        if (pulldownFrameRect != null)
            pulldownFrameRect.sizeDelta = GetPulldownFrameSizeForCurrentButtonSize();

        if (pulldownFrameImage != null)
            pulldownFrameImage.sprite = GetPulldownFrameSprite();

        ApplyPulldownEndRetractButtonRects();

        ApplyTopControlButtonSize(noseButtonRect);
        ApplyTopControlButtonSize(modeButtonRect);
        ApplyTopControlButtonSize(speedButtonRect);
        ApplyTopControlButtonSize(simulationButtonRect);
        ApplyTopControlButtonSize(emoteButtonRect);
        ApplyTopControlButtonSize(inventoryButtonRect);
        ApplyTopControlButtonSize(digButtonRect);
        ApplyTopControlButtonSize(homeButtonRect);
        ApplyTopControlButtonSize(cameraModeButtonRect);
        ApplyTopControlButtonSize(questButtonRect);

        ConfigureTopControlIconRect(noseIconImage != null ? noseIconImage.rectTransform : null, 0.68f);
        ConfigureTopControlIconRect(targetPreviewImage != null ? targetPreviewImage.rectTransform : null, 0.82f);
        ConfigureTopControlIconRect(modeIconImage != null ? modeIconImage.rectTransform : null, 0.72f);
        ConfigureTopControlIconRect(speedIconImage != null ? speedIconImage.rectTransform : null, 0.72f);
        ConfigureTopControlIconRect(simulationIconImage != null ? simulationIconImage.rectTransform : null, 0.72f);
        ConfigureTopControlIconRect(emoteIconImage != null ? emoteIconImage.rectTransform : null, 0.72f);
        ConfigureTopControlIconRect(inventoryIconImage != null ? inventoryIconImage.rectTransform : null, 0.72f);
        SetDigIconSize(digIconImage != null ? digIconImage.rectTransform : null, digIconImage != null ? digIconImage.sprite : null);
    }

    private void ApplyTopControlButtonSize(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.sizeDelta = new Vector2(topControlButtonSize, topControlButtonSize);
    }

    private static void ApplyTopControlScale(RectTransform rectTransform, float scale)
    {
        if (rectTransform == null)
            return;

        rectTransform.localScale = Vector3.one * scale;
    }

    private void ExpandTopControlsFromTab()
    {
        pulldownOpenedByTab = true;
        HideTooltip();

        if (pulldownTabRect != null)
            pulldownTabRect.gameObject.SetActive(false);
    }

    private void CollapseTopControlsToTab()
    {
        pulldownOpenedByTab = false;
        CloseDropdown();
        CloseModePanel();
        CloseSpeedPanel();
        CloseEmoteDropdown();
        HideTooltip();
    }

    private void UpdatePulldownTabVisibility()
    {
        bool targetControlsVisible = !autoHideTopControls || pulldownOpenedByTab || IsAnyTopPanelOpen();
        UpdatePulldownTabVisibility(targetControlsVisible);
    }

    private void UpdatePulldownTabVisibility(bool targetControlsVisible)
    {
        if (pulldownTabRect == null)
            return;

        bool shouldShowTab = autoHideTopControls &&
                             !pulldownOpenedByTab &&
                             !targetControlsVisible &&
                             topControlsVisibility <= 0.05f;

        if (pulldownTabRect.gameObject.activeSelf != shouldShowTab)
            pulldownTabRect.gameObject.SetActive(shouldShowTab);
    }

    private void ApplyPulldownFramePosition(float y)
    {
        if (pulldownFrameRect == null)
            return;

        pulldownFrameRect.anchoredPosition = new Vector2(pulldownFrameOffset.x * GetTopControlsFitScale(), y);
    }

    private void ApplyTopControlPosition(RectTransform rectTransform, int slotFromRight, float y)
    {
        if (rectTransform == null)
            return;

        Vector2 anchoredPosition = GetTopControlPosition(slotFromRight);
        if (UseTwoRowTopControls())
        {
            float scale = GetTopControlsFitScale();
            float topRowShownY = -((topControlsInset.y * scale) + GetTopSafeAreaInset());
            anchoredPosition.y = y + (anchoredPosition.y - topRowShownY);
        }
        else
        {
            anchoredPosition.y = y;
        }

        rectTransform.anchoredPosition = anchoredPosition;
    }

    private void ApplyPulldownTabPosition()
    {
        if (pulldownTabRect == null)
            return;

        pulldownTabRect.anchoredPosition = GetPulldownTabPosition();
    }

    private void ApplyTopPanelPositions()
    {
        ApplyTopPanelPosition(dropdownRect, 0);
        ApplyTopPanelPosition(modePanelRect, 1);
        ApplyTopPanelPosition(speedPanelRect, 2);
        ApplyTopPanelPosition(emoteDropdownRect, 4);
    }

    private void ApplyTopPanelPosition(RectTransform rectTransform, int slotFromRight)
    {
        if (rectTransform == null)
            return;

        if (rectTransform == emoteDropdownRect)
        {
            ApplyCenteredEmoteDropdownPosition();
            return;
        }

        rectTransform.anchoredPosition = GetTopPanelPosition(slotFromRight);
    }

    private void ApplyCenteredEmoteDropdownPosition()
    {
        if (emoteDropdownRect == null)
            return;

        emoteDropdownRect.anchorMin = new Vector2(0.5f, 0.5f);
        emoteDropdownRect.anchorMax = new Vector2(0.5f, 0.5f);
        emoteDropdownRect.pivot = new Vector2(0.5f, 0.5f);
        emoteDropdownRect.anchoredPosition = Vector2.zero;
        ClampEmoteDropdownToCanvas();
    }

    private Transform GetTopLevelOverlayParent(Transform fallbackParent)
    {
        return overlayCanvas != null ? overlayCanvas.transform : fallbackParent;
    }

    private float GetPulldownFrameHiddenY()
    {
        if (pulldownFrameRect == null)
            return GetPulldownFrameShownY();

        float frameHeight = pulldownFrameRect.rect.height > 0f
            ? pulldownFrameRect.rect.height
            : GetPulldownFrameSizeForCurrentButtonSize().y;
        return GetPulldownFrameShownY() + (frameHeight * GetTopControlsFitScale()) + topControlsHiddenTopPadding;
    }

    private void BuildEmoteDropdown(Transform parent, Transform searchRoot)
    {
        Transform existingDropdown = FindExistingUiElement(parent, searchRoot, "EmoteDropdown");
        if (existingDropdown != null)
        {
            BindExistingEmoteDropdown(existingDropdown.gameObject);
            return;
        }

        GameObject dropdownObject = new GameObject(
            "EmoteDropdown",
            typeof(RectTransform),
            typeof(Image));
        dropdownObject.transform.SetParent(GetTopLevelOverlayParent(parent), false);

        emoteDropdownRect = dropdownObject.GetComponent<RectTransform>();
        ConfigureTopPanelRect(emoteDropdownRect, 4);
        emoteDropdownRect.sizeDelta = new Vector2(GetVisibleEmoteDropdownWidth(), emoteDropdownMaxHeight);
        ApplyCenteredEmoteDropdownPosition();

        Image dropdownImage = dropdownObject.GetComponent<Image>();
        dropdownImage.color = dropdownBackgroundColor;

        GameObject titleObject = CreateTMPLabel(
            parent: dropdownObject.transform,
            name: "Title",
            text: "Emotes",
            fontSize: 26f,
            alignment: TextAlignmentOptions.Left);

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(14f, -42f);
        titleRect.offsetMax = new Vector2(-14f, -10f);

        GameObject scrollObject = new GameObject(
            "ScrollView",
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect));
        scrollObject.transform.SetParent(dropdownObject.transform, false);

        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(12f, 12f);
        scrollRectTransform.offsetMax = new Vector2(-16f, -48f);

        Image scrollBackground = scrollObject.GetComponent<Image>();
        scrollBackground.color = new Color(1f, 1f, 1f, 0.08f);

        emoteDropdownScrollRect = scrollObject.GetComponent<ScrollRect>();
        emoteDropdownScrollRect.horizontal = false;
        emoteDropdownScrollRect.movementType = ScrollRect.MovementType.Clamped;
        emoteDropdownScrollRect.scrollSensitivity = 28f;

        GameObject viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(Mask));
        viewportObject.transform.SetParent(scrollObject.transform, false);

        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = new Vector2(-14f, 0f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(GridLayoutGroup),
            typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);

        emoteDropdownContentRect = contentObject.GetComponent<RectTransform>();
        emoteDropdownContentRect.anchorMin = new Vector2(0f, 1f);
        emoteDropdownContentRect.anchorMax = new Vector2(1f, 1f);
        emoteDropdownContentRect.pivot = new Vector2(0.5f, 1f);
        emoteDropdownContentRect.offsetMin = Vector2.zero;
        emoteDropdownContentRect.offsetMax = Vector2.zero;

        GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(8, 8, 8, 8);
        grid.cellSize = new Vector2(emoteTileSize, emoteTileSize);
        grid.spacing = new Vector2(8f, 8f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, emoteGridColumns);

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = CreateScrollbar(scrollObject.transform);
        emoteDropdownScrollRect.viewport = viewportRect;
        emoteDropdownScrollRect.content = emoteDropdownContentRect;
        emoteDropdownScrollRect.verticalScrollbar = scrollbar;
        emoteDropdownScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        emoteDropdownScrollRect.verticalScrollbarSpacing = 4f;

        dropdownObject.SetActive(false);
    }

    private void BindExistingEmoteDropdown(GameObject dropdownObject)
    {
        emoteDropdownRect = dropdownObject.GetComponent<RectTransform>();
        if (emoteDropdownRect == null)
            return;

        dropdownObject.transform.SetParent(GetTopLevelOverlayParent(dropdownObject.transform.parent), worldPositionStays: false);
        ConfigureTopPanelRect(emoteDropdownRect, 4);
        ApplyCenteredEmoteDropdownPosition();

        Image dropdownImage = dropdownObject.GetComponent<Image>();
        if (dropdownImage != null)
            dropdownImage.color = dropdownBackgroundColor;

        Transform scrollTransform = dropdownObject.transform.Find("ScrollView");
        emoteDropdownScrollRect = scrollTransform != null ? scrollTransform.GetComponent<ScrollRect>() : null;
        Transform contentTransform = dropdownObject.transform.Find("ScrollView/Viewport/Content");
        emoteDropdownContentRect = contentTransform != null ? contentTransform.GetComponent<RectTransform>() : null;

        dropdownObject.SetActive(false);
    }

    private void BuildDropdown(Transform parent, Transform searchRoot)
    {
        Transform existingDropdown = FindExistingUiElement(parent, searchRoot, "ScentTargetDropdown");
        if (existingDropdown != null)
        {
            BindExistingDropdown(existingDropdown.gameObject);
            return;
        }

        GameObject dropdownObject = new GameObject(
            "ScentTargetDropdown",
            typeof(RectTransform),
            typeof(Image));
        dropdownObject.transform.SetParent(parent, false);

        dropdownRect = dropdownObject.GetComponent<RectTransform>();
        ConfigureTopPanelRect(dropdownRect, 0);
        dropdownRect.sizeDelta = new Vector2(dropdownWidth, dropdownMaxHeight);

        Image dropdownImage = dropdownObject.GetComponent<Image>();
        dropdownImage.color = dropdownBackgroundColor;

        GameObject titleObject = CreateTMPLabel(
            parent: dropdownObject.transform,
            name: "Title",
            text: "Select target",
            fontSize: 26f,
            alignment: TextAlignmentOptions.Left);

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(14f, -42f);
        titleRect.offsetMax = new Vector2(-14f, -10f);

        GameObject scrollObject = new GameObject(
            "ScrollView",
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect));
        scrollObject.transform.SetParent(dropdownObject.transform, false);

        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(12f, 12f);
        scrollRectTransform.offsetMax = new Vector2(-16f, -48f);

        Image scrollBackground = scrollObject.GetComponent<Image>();
        scrollBackground.color = new Color(1f, 1f, 1f, 0.08f);

        dropdownScrollRect = scrollObject.GetComponent<ScrollRect>();
        dropdownScrollRect.horizontal = false;
        dropdownScrollRect.movementType = ScrollRect.MovementType.Clamped;
        dropdownScrollRect.scrollSensitivity = 28f;

        GameObject viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(Mask));
        viewportObject.transform.SetParent(scrollObject.transform, false);

        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = new Vector2(-14f, 0f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);

        dropdownContentRect = contentObject.GetComponent<RectTransform>();
        dropdownContentRect.anchorMin = new Vector2(0f, 1f);
        dropdownContentRect.anchorMax = new Vector2(1f, 1f);
        dropdownContentRect.pivot = new Vector2(0.5f, 1f);
        dropdownContentRect.offsetMin = Vector2.zero;
        dropdownContentRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = CreateScrollbar(scrollObject.transform);
        dropdownScrollRect.viewport = viewportRect;
        dropdownScrollRect.content = dropdownContentRect;
        dropdownScrollRect.verticalScrollbar = scrollbar;
        dropdownScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        dropdownScrollRect.verticalScrollbarSpacing = 4f;

        dropdownObject.SetActive(false);
    }

    private void BindExistingDropdown(GameObject dropdownObject)
    {
        dropdownRect = dropdownObject.GetComponent<RectTransform>();
        if (dropdownRect == null)
            return;

        ConfigureTopPanelRect(dropdownRect, 0);

        Image dropdownImage = dropdownObject.GetComponent<Image>();
        if (dropdownImage != null)
            dropdownImage.color = dropdownBackgroundColor;

        Transform scrollTransform = dropdownObject.transform.Find("ScrollView");
        dropdownScrollRect = scrollTransform != null ? scrollTransform.GetComponent<ScrollRect>() : null;
        Transform contentTransform = dropdownObject.transform.Find("ScrollView/Viewport/Content");
        dropdownContentRect = contentTransform != null ? contentTransform.GetComponent<RectTransform>() : null;

        dropdownObject.SetActive(false);
    }

    private void BuildModePanel(Transform parent, Transform searchRoot)
    {
        Transform existingPanel = FindExistingUiElement(parent, searchRoot, "DecisionModePanel");
        if (existingPanel != null)
        {
            BindExistingModePanel(existingPanel.gameObject);
            return;
        }

        GameObject panelObject = new GameObject(
            "DecisionModePanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(GridLayoutGroup));
        panelObject.transform.SetParent(parent, false);

        modePanelRect = panelObject.GetComponent<RectTransform>();
        ConfigureTopPanelRect(modePanelRect, 1);

        float padding = 12f;
        float spacing = 8f;
        modePanelRect.sizeDelta = new Vector2(
            padding * 2f + modePanelIconSize * 3f + spacing * 2f,
            padding * 2f + modePanelIconSize * 2f + spacing);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = dropdownBackgroundColor;

        GridLayoutGroup grid = panelObject.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
        grid.cellSize = new Vector2(modePanelIconSize, modePanelIconSize);
        grid.spacing = new Vector2(spacing, spacing);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        for (int i = 0; i < selectableDecisionModes.Length; i++)
            CreateModePanelButton(selectableDecisionModes[i]);

        panelObject.SetActive(false);
    }

    private void BuildSpeedPanel(Transform parent, Transform searchRoot)
    {
        Transform existingPanel = FindExistingUiElement(parent, searchRoot, "SpeedModePanel");
        if (existingPanel != null)
        {
            BindExistingSpeedPanel(existingPanel.gameObject);
            return;
        }

        GameObject panelObject = new GameObject(
            "SpeedModePanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(GridLayoutGroup));
        panelObject.transform.SetParent(parent, false);

        speedPanelRect = panelObject.GetComponent<RectTransform>();
        ConfigureTopPanelRect(speedPanelRect, 2);

        float padding = 12f;
        float spacing = 8f;
        speedPanelRect.sizeDelta = new Vector2(
            padding * 2f + modePanelIconSize * 3f + spacing * 2f,
            padding * 2f + modePanelIconSize);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = dropdownBackgroundColor;

        GridLayoutGroup grid = panelObject.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
        grid.cellSize = new Vector2(modePanelIconSize, modePanelIconSize);
        grid.spacing = new Vector2(spacing, spacing);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        for (int i = 0; i < selectableSpeedModes.Length; i++)
            CreateSpeedPanelButton(selectableSpeedModes[i]);

        panelObject.SetActive(false);
    }

    private void BindExistingModePanel(GameObject panelObject)
    {
        modePanelRect = panelObject.GetComponent<RectTransform>();
        if (modePanelRect == null)
            return;

        ConfigureTopPanelRect(modePanelRect, 1);

        Image panelImage = panelObject.GetComponent<Image>();
        if (panelImage != null)
            panelImage.color = dropdownBackgroundColor;

        modeButtonBackgrounds.Clear();
        for (int i = 0; i < selectableDecisionModes.Length; i++)
        {
            AgentDecisionType decisionType = selectableDecisionModes[i];
            Transform buttonTransform = panelObject.transform.Find($"{decisionType}ModeButton");
            if (buttonTransform == null && i < panelObject.transform.childCount)
                buttonTransform = panelObject.transform.GetChild(i);

            if (buttonTransform == null)
                continue;

            BindExistingModePanelButton(buttonTransform.gameObject, decisionType);
        }

        panelObject.SetActive(false);
    }

    private void BindExistingSpeedPanel(GameObject panelObject)
    {
        speedPanelRect = panelObject.GetComponent<RectTransform>();
        if (speedPanelRect == null)
            return;

        ConfigureTopPanelRect(speedPanelRect, 2);

        Image panelImage = panelObject.GetComponent<Image>();
        if (panelImage != null)
            panelImage.color = dropdownBackgroundColor;

        speedButtonBackgrounds.Clear();
        for (int i = 0; i < selectableSpeedModes.Length; i++)
        {
            WalkMode walkMode = selectableSpeedModes[i];
            Transform buttonTransform = panelObject.transform.Find($"{walkMode}SpeedButton");
            if (buttonTransform == null && i < panelObject.transform.childCount)
                buttonTransform = panelObject.transform.GetChild(i);

            if (buttonTransform == null)
                continue;

            BindExistingSpeedPanelButton(buttonTransform.gameObject, walkMode);
        }

        panelObject.SetActive(false);
    }

    private void BindExistingModePanelButton(GameObject buttonObject, AgentDecisionType decisionType)
    {
        Image background = GetOrAddComponent<Image>(buttonObject);
        background.color = dropdownRowColor;
        modeButtonBackgrounds.Add(background);

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = background;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(() => HandleDecisionModeSelected(decisionType));

        Transform iconTransform = buttonObject.transform.Find("Icon");
        if (iconTransform == null)
            return;

        Image iconImage = GetOrAddComponent<Image>(iconTransform.gameObject);
        iconImage.sprite = GetDecisionModeSprite(decisionType);
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;

        ConfigureTooltip(buttonObject, () => GetDecisionModeTooltipText(decisionType));
    }

    private void CreateModePanelButton(AgentDecisionType decisionType)
    {
        GameObject buttonObject = new GameObject(
            $"{decisionType}ModeButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(modePanelRect, false);

        Image background = buttonObject.GetComponent<Image>();
        background.color = dropdownRowColor;
        modeButtonBackgrounds.Add(background);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(() => HandleDecisionModeSelected(decisionType));

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(7f, 7f);
        iconRect.offsetMax = new Vector2(-7f, -7f);

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = GetDecisionModeSprite(decisionType);
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;

        ConfigureTooltip(buttonObject, () => GetDecisionModeTooltipText(decisionType));
    }

    private void BindExistingSpeedPanelButton(GameObject buttonObject, WalkMode walkMode)
    {
        Image background = GetOrAddComponent<Image>(buttonObject);
        background.color = dropdownRowColor;
        speedButtonBackgrounds.Add(background);

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = background;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(() => HandleSpeedModeSelected(walkMode));

        Transform iconTransform = buttonObject.transform.Find("Icon");
        if (iconTransform == null)
            return;

        Image iconImage = GetOrAddComponent<Image>(iconTransform.gameObject);
        iconImage.sprite = GetSpeedModeSprite(walkMode);
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;

        ConfigureTooltip(buttonObject, () => GetSpeedModeTooltipText(walkMode));
    }

    private void CreateSpeedPanelButton(WalkMode walkMode)
    {
        GameObject buttonObject = new GameObject(
            $"{walkMode}SpeedButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(speedPanelRect, false);

        Image background = buttonObject.GetComponent<Image>();
        background.color = dropdownRowColor;
        speedButtonBackgrounds.Add(background);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(() => HandleSpeedModeSelected(walkMode));

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(7f, 7f);
        iconRect.offsetMax = new Vector2(-7f, -7f);

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = GetSpeedModeSprite(walkMode);
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;

        ConfigureTooltip(buttonObject, () => GetSpeedModeTooltipText(walkMode));
    }

    private void BuildTooltip(Transform parent, Transform searchRoot)
    {
        Transform existingTooltip = FindExistingUiElement(parent, searchRoot, "UpperRightTooltip");
        GameObject tooltipObject;
        bool createdTooltip = existingTooltip == null;
        if (createdTooltip)
        {
            tooltipObject = new GameObject(
                "UpperRightTooltip",
                typeof(RectTransform),
                typeof(Image));
            tooltipObject.transform.SetParent(parent, false);
        }
        else
        {
            tooltipObject = existingTooltip.gameObject;
        }

        tooltipRect = GetOrAddComponent<RectTransform>(tooltipObject);
        tooltipRect.anchorMin = new Vector2(0f, 1f);
        tooltipRect.anchorMax = new Vector2(0f, 1f);
        tooltipRect.pivot = new Vector2(0f, 1f);
        tooltipRect.sizeDelta = new Vector2(160f, 52f);

        tooltipBackgroundImage = GetOrAddComponent<Image>(tooltipObject);
        tooltipBackgroundImage.color = tooltipBackgroundColor;
        tooltipBackgroundImage.raycastTarget = false;

        Transform existingLabel = tooltipObject.transform.Find("Label");
        GameObject labelObject;
        if (existingLabel == null)
        {
            labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(tooltipObject.transform, false);
        }
        else
        {
            labelObject = existingLabel.gameObject;
        }

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(tooltipPadding.x, tooltipPadding.y);
        labelRect.offsetMax = new Vector2(-tooltipPadding.x, -tooltipPadding.y);

        tooltipLabel = GetOrAddComponent<TextMeshProUGUI>(labelObject);
        tooltipLabel.fontSize = tooltipFontSize;
        tooltipLabel.color = dropdownTextColor;
        tooltipLabel.alignment = TextAlignmentOptions.Center;
        tooltipLabel.textWrappingMode = TextWrappingModes.NoWrap;
        tooltipLabel.overflowMode = TextOverflowModes.Overflow;
        tooltipLabel.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            tooltipLabel.font = TMP_Settings.defaultFontAsset;

        tooltipObject.SetActive(false);
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
            return component;

        return target.AddComponent<T>();
    }

    private Scrollbar CreateScrollbar(Transform parent)
    {
        GameObject scrollbarObject = new GameObject(
            "Scrollbar",
            typeof(RectTransform),
            typeof(Image),
            typeof(Scrollbar));
        scrollbarObject.transform.SetParent(parent, false);

        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 1f);
        scrollbarRect.offsetMin = new Vector2(-12f, 2f);
        scrollbarRect.offsetMax = Vector2.zero;
        scrollbarRect.sizeDelta = new Vector2(12f, 0f);

        Image trackImage = scrollbarObject.GetComponent<Image>();
        trackImage.color = new Color(0.4f, 0.34f, 0.24f, 0.25f);

        GameObject slidingAreaObject = new GameObject("Sliding Area", typeof(RectTransform));
        slidingAreaObject.transform.SetParent(scrollbarObject.transform, false);
        RectTransform slidingAreaRect = slidingAreaObject.GetComponent<RectTransform>();
        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = new Vector2(1f, 2f);
        slidingAreaRect.offsetMax = new Vector2(-1f, -2f);

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(slidingAreaObject.transform, false);

        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 1f);
        handleRect.anchorMax = new Vector2(1f, 1f);
        handleRect.pivot = new Vector2(0.5f, 1f);
        handleRect.sizeDelta = new Vector2(0f, 48f);

        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = new Color(0.63f, 0.52f, 0.31f, 0.85f);

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scrollbar.size = 1f;
        scrollbar.value = 1f;

        return scrollbar;
    }

    private GameObject CreateTMPLabel(
        Transform parent,
        string name,
        string text,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = dropdownTextColor;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        return textObject;
    }

    private void ToggleDropdown()
    {
        if (dropdownRect == null)
            return;

        bool shouldOpen = !dropdownRect.gameObject.activeSelf;
        if (shouldOpen)
        {
            CloseModePanel();
            OpenDropdown();
        }
        else
            CloseDropdown();
    }

    private void ToggleModePanel()
    {
        if (modePanelRect == null)
            return;

        bool shouldOpen = !modePanelRect.gameObject.activeSelf;
        if (shouldOpen)
        {
            CloseSpeedPanel();
            CloseDropdown();
            CloseEmoteDropdown();
            RefreshModeButtonState(force: true);
            RefreshModePanelSelection();
            modePanelRect.gameObject.SetActive(true);
        }
        else
        {
            CloseModePanel();
        }
    }

    private void ToggleSpeedPanel()
    {
        if (speedPanelRect == null)
            return;

        bool shouldOpen = !speedPanelRect.gameObject.activeSelf;
        if (shouldOpen)
        {
            CloseModePanel();
            CloseDropdown();
            CloseEmoteDropdown();
            RefreshSpeedButtonState(force: true);
            RefreshSpeedPanelSelection();
            speedPanelRect.gameObject.SetActive(true);
        }
        else
        {
            CloseSpeedPanel();
        }
    }

    private void OpenDropdown()
    {
        if (dropdownRect == null)
            return;

        CloseSpeedPanel();
        CloseModePanel();
        CloseEmoteDropdown();
        RefreshDropdownContents();
        dropdownRect.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        if (dropdownScrollRect != null)
            dropdownScrollRect.verticalNormalizedPosition = 1f;
    }

    private void ToggleEmoteDropdown()
    {
        if (emoteDropdownRect == null)
            return;

        bool shouldOpen = !emoteDropdownRect.gameObject.activeSelf;
        if (shouldOpen)
            OpenEmoteDropdown();
        else
            CloseEmoteDropdown();
    }

    private void OpenEmoteDropdown()
    {
        if (emoteDropdownRect == null)
            return;

        CloseDropdown();
        CloseModePanel();
        CloseSpeedPanel();
        EnsureDefaultEmoteSelection();
        RefreshEmoteDropdownContents();
        ApplyCenteredEmoteDropdownPosition();
        emoteDropdownRect.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        ClampEmoteDropdownToCanvas();
        if (emoteDropdownScrollRect != null)
            emoteDropdownScrollRect.verticalNormalizedPosition = 1f;
    }

    private void CloseDropdown()
    {
        if (dropdownRect != null)
            dropdownRect.gameObject.SetActive(false);

        HideTooltip();
    }

    private void CloseModePanel()
    {
        if (modePanelRect != null)
            modePanelRect.gameObject.SetActive(false);

        HideTooltip();
    }

    private void CloseSpeedPanel()
    {
        if (speedPanelRect != null)
            speedPanelRect.gameObject.SetActive(false);

        HideTooltip();
    }

    private void CloseEmoteDropdown()
    {
        if (emoteDropdownRect != null)
            emoteDropdownRect.gameObject.SetActive(false);

        HideTooltip();
    }

    private void CloseOpenPanelsIfClickedOutside(Vector2 screenPoint)
    {
        bool scentDropdownOpen = dropdownRect != null && dropdownRect.gameObject.activeSelf;
        bool modePanelOpen = modePanelRect != null && modePanelRect.gameObject.activeSelf;
        bool speedPanelOpen = speedPanelRect != null && speedPanelRect.gameObject.activeSelf;
        bool emoteDropdownOpen = emoteDropdownRect != null && emoteDropdownRect.gameObject.activeSelf;
        if (!scentDropdownOpen && !modePanelOpen && !speedPanelOpen && !emoteDropdownOpen)
            return;

        bool clickedScentDropdown = scentDropdownOpen &&
                                    RectTransformUtility.RectangleContainsScreenPoint(dropdownRect, screenPoint, null);
        bool clickedNoseButton = noseButtonRect != null &&
                                 RectTransformUtility.RectangleContainsScreenPoint(noseButtonRect, screenPoint, null);
        bool clickedModePanel = modePanelOpen &&
                                RectTransformUtility.RectangleContainsScreenPoint(modePanelRect, screenPoint, null);
        bool clickedModeButton = modeButtonRect != null &&
                                 RectTransformUtility.RectangleContainsScreenPoint(modeButtonRect, screenPoint, null);
        bool clickedSpeedPanel = speedPanelOpen &&
                                 RectTransformUtility.RectangleContainsScreenPoint(speedPanelRect, screenPoint, null);
        bool clickedSpeedButton = speedButtonRect != null &&
                                  RectTransformUtility.RectangleContainsScreenPoint(speedButtonRect, screenPoint, null);
        bool clickedEmoteDropdown = emoteDropdownOpen &&
                                    RectTransformUtility.RectangleContainsScreenPoint(emoteDropdownRect, screenPoint, null);
        bool clickedEmoteButton = emoteButtonRect != null &&
                                  RectTransformUtility.RectangleContainsScreenPoint(emoteButtonRect, screenPoint, null);

        if (scentDropdownOpen && !clickedScentDropdown && !clickedNoseButton)
            CloseDropdown();

        if (modePanelOpen && !clickedModePanel && !clickedModeButton)
            CloseModePanel();

        if (speedPanelOpen && !clickedSpeedPanel && !clickedSpeedButton)
            CloseSpeedPanel();

        if (emoteDropdownOpen && !clickedEmoteDropdown && !clickedEmoteButton)
            CloseEmoteDropdown();
    }

    private void RefreshDropdownContents()
    {
        if (dropdownContentRect != null)
        {
            for (int childIndex = dropdownContentRect.childCount - 1; childIndex >= 0; childIndex--)
                Destroy(dropdownContentRect.GetChild(childIndex).gameObject);
        }
        else
        {
            for (int i = 0; i < dropdownRows.Count; i++)
            {
                if (dropdownRows[i] != null)
                    Destroy(dropdownRows[i]);
            }
        }
        dropdownRows.Clear();

        if (!EnsureDir() || dir.scentRegistry == null || dropdownContentRect == null)
            return;

        List<ScentSource> scentSources = dir.scentRegistry.GetAvailableScentSources();
        ScentSource selectedTarget = dir.scentRegistry.SelectedTargetScent;

        if (scentSources.Count == 0)
        {
            dropdownRows.Add(CreateInfoRow("No scents available yet."));
            ResizeDropdown(1);
            return;
        }

        for (int i = 0; i < scentSources.Count; i++)
        {
            ScentSource scentSource = scentSources[i];
            dropdownRows.Add(CreateScentRow(scentSource, scentSource == selectedTarget));
        }

        ResizeDropdown(scentSources.Count);
    }

    private void ResizeDropdown(int rowCount)
    {
        if (dropdownRect == null)
            return;

        float headerHeight = 56f;
        float rowHeight = 54f;
        float chrome = 22f;
        float desiredHeight = headerHeight + chrome + rowHeight * Mathf.Max(1, rowCount);
        dropdownRect.sizeDelta = new Vector2(dropdownWidth, Mathf.Min(dropdownMaxHeight, desiredHeight));
    }

    private void RefreshEmoteDropdownContents()
    {
        if (emoteDropdownContentRect != null)
        {
            for (int childIndex = emoteDropdownContentRect.childCount - 1; childIndex >= 0; childIndex--)
                Destroy(emoteDropdownContentRect.GetChild(childIndex).gameObject);
        }
        else
        {
            for (int i = 0; i < emoteDropdownTiles.Count; i++)
            {
                if (emoteDropdownTiles[i] != null)
                    Destroy(emoteDropdownTiles[i]);
            }
        }
        emoteDropdownTiles.Clear();

        if (emoteDropdownContentRect == null)
            return;

        int visibleEntryCount = 0;
        for (int i = 0; i < DogEmojiCatalog.Entries.Length; i++)
        {
            DogEmojiEntry entry = DogEmojiCatalog.Entries[i];
            if (GetEmoteSprite(entry) == null)
                continue;

            emoteDropdownTiles.Add(CreateEmoteTile(entry, entry.EntryId == GetSelectedEmoteId()));
            visibleEntryCount++;
        }

        if (visibleEntryCount == 0)
        {
            emoteDropdownTiles.Add(CreateInfoRowForParent(emoteDropdownContentRect, "No emotes found in the sprite sheets."));
            ResizeEmoteDropdown(1);
            return;
        }

        ResizeEmoteDropdown(visibleEntryCount);
    }

    private void ResizeEmoteDropdown(int entryCount)
    {
        if (emoteDropdownRect == null)
            return;

        int columnCount = Mathf.Max(1, emoteGridColumns);
        int rowCount = Mathf.Max(1, Mathf.CeilToInt(entryCount / (float)columnCount));
        float headerHeight = 56f;
        float chrome = 32f;
        float spacing = 8f;
        float desiredHeight = headerHeight + chrome + (rowCount * emoteTileSize) + (Mathf.Max(0, rowCount - 1) * spacing) + 16f;
        emoteDropdownRect.sizeDelta = new Vector2(GetVisibleEmoteDropdownWidth(), Mathf.Min(emoteDropdownMaxHeight, desiredHeight));
        ClampEmoteDropdownToCanvas();
    }

    private float GetVisibleEmoteDropdownWidth()
    {
        const float margin = 12f;
        RectTransform canvasRect = overlayCanvas != null ? overlayCanvas.transform as RectTransform : null;
        if (canvasRect == null || canvasRect.rect.width <= 0f)
            return emoteDropdownWidth;

        return Mathf.Min(emoteDropdownWidth, Mathf.Max(1f, canvasRect.rect.width - (margin * 2f)));
    }

    private void ClampEmoteDropdownToCanvas()
    {
        if (emoteDropdownRect == null || overlayCanvas == null)
            return;

        RectTransform canvasRect = overlayCanvas.transform as RectTransform;
        if (canvasRect == null || canvasRect.rect.width <= 0f || canvasRect.rect.height <= 0f)
            return;

        float margin = 12f;
        Vector2 size = emoteDropdownRect.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            size = emoteDropdownRect.sizeDelta;

        float canvasLeft = canvasRect.rect.xMin + margin;
        float canvasRight = canvasRect.rect.xMax - margin;
        float canvasBottom = canvasRect.rect.yMin + margin;
        float canvasTop = canvasRect.rect.yMax - margin;

        Vector2 pivot = emoteDropdownRect.pivot;
        Vector2 position = emoteDropdownRect.anchoredPosition;
        float left = position.x - (size.x * pivot.x);
        float right = left + size.x;
        float top = position.y + (size.y * (1f - pivot.y));
        float bottom = top - size.y;

        if (left < canvasLeft)
            position.x += canvasLeft - left;
        else if (right > canvasRight)
            position.x -= right - canvasRight;

        if (bottom < canvasBottom)
            position.y += canvasBottom - bottom;
        else if (top > canvasTop)
            position.y -= top - canvasTop;

        emoteDropdownRect.anchoredPosition = position;
    }

    private GameObject CreateInfoRowForParent(Transform parent, string message)
    {
        GameObject rowObject = new GameObject(
            "InfoRow",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        rowObject.transform.SetParent(parent, false);

        LayoutElement layout = rowObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 52f;

        Image background = rowObject.GetComponent<Image>();
        background.color = dropdownRowColor;

        GameObject labelObject = CreateTMPLabel(
            rowObject.transform,
            "Label",
            message,
            20f,
            TextAlignmentOptions.Left);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(14f, 8f);
        labelRect.offsetMax = new Vector2(-14f, -8f);

        return rowObject;
    }

    private GameObject CreateEmoteTile(DogEmojiEntry entry, bool isSelected)
    {
        GameObject tileObject = new GameObject(
            $"Emote_{entry.EntryId}",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        tileObject.transform.SetParent(emoteDropdownContentRect, false);

        LayoutElement layout = tileObject.GetComponent<LayoutElement>();
        layout.preferredWidth = emoteTileSize;
        layout.preferredHeight = emoteTileSize;

        Image background = tileObject.GetComponent<Image>();
        background.color = isSelected ? dropdownSelectedColor : dropdownRowColor;

        Button button = tileObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(() => HandleEmoteSelected(entry));

        GameObject iconObject = new GameObject(
            "Icon",
            typeof(RectTransform),
            typeof(Image));
        iconObject.transform.SetParent(tileObject.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = Vector2.one * (emoteTileSize - 20f);
        iconRect.anchoredPosition = Vector2.zero;

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = GetEmoteSprite(entry);
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;

        ConfigureTooltip(tileObject, () => entry.Name);
        return tileObject;
    }

    private GameObject CreateInfoRow(string message)
    {
        return CreateInfoRowForParent(dropdownContentRect, message);
    }

    private GameObject CreateScentRow(ScentSource scentSource, bool isSelected)
    {
        GameObject rowObject = new GameObject(
            "ScentRow",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        rowObject.transform.SetParent(dropdownContentRect, false);

        LayoutElement layout = rowObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 52f;

        Image background = rowObject.GetComponent<Image>();
        background.color = isSelected ? dropdownSelectedColor : dropdownRowColor;

        Button button = rowObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(() => HandleScentSelected(scentSource));

        GameObject swatchObject = new GameObject(
            "Swatch",
            typeof(RectTransform),
            typeof(Image));
        swatchObject.transform.SetParent(rowObject.transform, false);

        RectTransform swatchRect = swatchObject.GetComponent<RectTransform>();
        swatchRect.anchorMin = new Vector2(0f, 0.5f);
        swatchRect.anchorMax = new Vector2(0f, 0.5f);
        swatchRect.pivot = new Vector2(0f, 0.5f);
        swatchRect.anchoredPosition = new Vector2(12f, 0f);
        swatchRect.sizeDelta = new Vector2(18f, 18f);

        Image swatchImage = swatchObject.GetComponent<Image>();
        swatchImage.color = GetScentColor(scentSource);

        GameObject labelObject = CreateTMPLabel(
            rowObject.transform,
            "Label",
            BuildScentRowText(scentSource),
            20f,
            TextAlignmentOptions.Left);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(40f, 8f);
        labelRect.offsetMax = new Vector2(-14f, -8f);

        return rowObject;
    }

    private string BuildScentRowText(ScentSource scentSource)
    {
        if (scentSource == null)
            return "Unknown scent";

        string displayName = !string.IsNullOrWhiteSpace(scentSource.scentName)
            ? scentSource.scentName.Trim()
            : scentSource.category.ToString();

        return $"{displayName} ({scentSource.category})";
    }

    private Color GetScentColor(ScentSource scentSource)
    {
        if (scentSource == null)
            return new Color(0.85f, 0.85f, 0.85f, 1f);

        if (scentSource.sourceGroundColor.a > 0f)
            return scentSource.sourceGroundColor;

        if (scentSource.sourceAirColor.a > 0f)
            return scentSource.sourceAirColor;

        if (scentSource.categoryColor.a > 0f)
            return scentSource.categoryColor;

        return new Color(0.85f, 0.85f, 0.85f, 1f);
    }

    private void HandleScentSelected(ScentSource scentSource)
    {
        if (!EnsureDir() || dir.scentRegistry == null)
            return;

        ScentSource selectedSource = dir.scentRegistry.SetSelectedTargetScent(scentSource);
        if (selectedSource == null)
            return;

        if (isSniffModeActive)
            dir.scentRegistry.ActivateScentOverlay(selectedSource);

        BottomBanner.Show(
            BannerSense.Smell,
            BannerLevel.Low,
            $"Target scent set: {BuildScentRowText(selectedSource)}");

        RefreshNoseButtonSelectionState();
        RefreshTargetButtonPreview(force: true);
        RefreshActiveTooltipText();
        CloseDropdown();
    }

    private void HandleEmoteSelected(DogEmojiEntry entry)
    {
        SetSelectedEmote(entry);
        RefreshEmoteButtonState(force: true);
        BottomBanner.LogEmote(GetCurrentControlledWorldObject(), entry.EntryId);
        CloseEmoteDropdown();
    }

    private void HandleDecisionModeSelected(AgentDecisionType decisionType)
    {
        WorldObject controlledObject = GetCurrentControlledWorldObject();
        if (controlledObject == null)
        {
            Debug.LogWarning("ScentGUI: no controlled WorldObject available for decision mode selection.", this);
            return;
        }

        if (controlledObject.agentModule == null)
            controlledObject.CreateModulesIfNeeded(ModuleFlags.agentModule);

        if (controlledObject.agentModule == null)
        {
            Debug.LogWarning($"ScentGUI: {controlledObject.DisplayName} has no AgentModule.", controlledObject);
            return;
        }

        controlledObject.agentModule.SwitchDecisionModule(decisionType);
        RefreshModeButtonState(force: true);
        CloseModePanel();
    }

    private void HandleSpeedModeSelected(WalkMode walkMode)
    {
        WorldObject controlledObject = GetCurrentControlledWorldObject();
        if (controlledObject == null)
        {
            Debug.LogWarning("ScentGUI: no controlled WorldObject available for speed selection.", this);
            return;
        }

        Pack targetPack = controlledObject.packMemberModule != null
            ? controlledObject.packMemberModule.currentPack
            : null;

        int changedCount = targetPack != null
            ? targetPack.SetWalkMode(walkMode)
            : SetWalkModeForWorldObject(controlledObject, walkMode);

        if (changedCount <= 0)
        {
            Debug.LogWarning($"ScentGUI: no movement modules available for speed selection from {controlledObject.DisplayName}.", controlledObject);
            return;
        }

        RefreshSpeedButtonState(force: true);
        CloseSpeedPanel();
    }

    private void HandleDigButtonPressed()
    {
        CloseDropdown();
        CloseModePanel();
        CloseSpeedPanel();
        CloseEmoteDropdown();

        WorldObject controlledObject = GetCurrentControlledWorldObject();
        if (controlledObject == null)
        {
            Debug.LogWarning("ScentGUI: no controlled WorldObject available for digging.", this);
            BottomBanner.Show("No dog is selected to dig.");
            return;
        }

        TerrainDigService.TryDigAt(controlledObject);
    }

    private void HandleHomeButtonPressed()
    {
        CloseTopActionPanels();

        SceneFader sceneFader = EnsureDir() && dir.sceneFader != null
            ? dir.sceneFader
            : FindFirstObjectByType<SceneFader>();

        if (sceneFader == null)
        {
            Debug.LogWarning("ScentGUI: title menu fader is not available for Home button.", this);
            BottomBanner.Show("Home is not ready yet.");
            return;
        }

        sceneFader.ReturnToTitleMenu();
    }

    private void HandleCameraModeButtonPressed()
    {
        CloseTopActionPanels();

        CameraModeSwitcher cameraModeSwitcher = EnsureDir() && dir.cameraModeSwitcher != null
            ? dir.cameraModeSwitcher
            : FindFirstObjectByType<CameraModeSwitcher>();

        if (cameraModeSwitcher == null)
        {
            Debug.LogWarning("ScentGUI: camera mode switcher is not available.", this);
            BottomBanner.Show("Camera mode is not ready yet.");
            return;
        }

        cameraModeSwitcher.SelectNextView();
    }

    private void HandleQuestButtonPressed()
    {
        CloseTopActionPanels();

        QuestJournalUI questJournal = FindFirstObjectByType<QuestJournalUI>();
        if (questJournal == null)
        {
            _ = QuestManager.Instance;
            GameObject journalObject = new("QuestJournalUI");
            questJournal = journalObject.AddComponent<QuestJournalUI>();
        }

        questJournal.Toggle();
    }

    private void CloseTopActionPanels()
    {
        CloseDropdown();
        CloseModePanel();
        CloseSpeedPanel();
        CloseEmoteDropdown();
        HideTooltip();
    }

    private static int SetWalkModeForWorldObject(WorldObject worldObject, WalkMode walkMode)
    {
        if (worldObject == null)
            return 0;

        if (worldObject.agentMovementModule == null || worldObject.motionModule == null)
            worldObject.CreateModulesIfNeeded(ModuleFlags.agentMovementModule | ModuleFlags.motionModule);

        if (worldObject.agentMovementModule != null)
        {
            worldObject.agentMovementModule.SetWalkMode(walkMode);
            return 1;
        }

        if (worldObject.motionModule != null)
        {
            worldObject.motionModule.SetWalkMode(walkMode);
            return 1;
        }

        return 0;
    }

    private void RefreshTargetButtonPreview(bool force = false)
    {
        if (targetPreviewImage == null)
            return;

        WorldObject previewObject = GetCurrentTargetPreviewWorldObject();
        if (!force && previewObject == targetPreviewedAgent && targetPreviewClone != null)
            return;

        BuildTargetPreviewClone(previewObject);
    }

    private WorldObject GetCurrentTargetPreviewWorldObject()
    {
        ScentSource selectedSource = GetSelectedTargetScent();
        if (selectedSource == null)
            return GetCurrentControlledWorldObject();

        return ResolveScentSourceWorldObject(selectedSource);
    }

    private ScentSource GetSelectedTargetScent()
    {
        return EnsureDir() && dir.scentRegistry != null
            ? dir.scentRegistry.SelectedTargetScent
            : null;
    }

    private WorldObject ResolveScentSourceWorldObject(ScentSource scentSource)
    {
        if (scentSource == null)
            return null;

        if (scentSource.agent != null)
            return scentSource.agent;

        if (scentSource.agentId < 0)
            return null;

        if (EnsureDir() && dir.worldObjectRegistry != null && dir.worldObjectRegistry.TryGet(scentSource.agentId, out WorldObject dirObject))
            return dirObject;

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        return registry != null && registry.TryGet(scentSource.agentId, out WorldObject registryObject)
            ? registryObject
            : null;
    }

    private void EnsureTargetPreviewWorld()
    {
        if (targetPreviewWorldRoot != null)
            return;

        targetPreviewWorldRoot = new GameObject("ScentGuiTargetPreviewWorld");
        targetPreviewWorldRoot.hideFlags = HideFlags.HideAndDontSave;
        targetPreviewWorldRoot.transform.position = TargetPreviewAnchorPosition;

        GameObject cameraObject = new("TargetPreviewCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.transform.SetParent(targetPreviewWorldRoot.transform, false);
        targetPreviewCamera = cameraObject.AddComponent<Camera>();
        targetPreviewCamera.enabled = false;
        targetPreviewCamera.clearFlags = CameraClearFlags.SolidColor;
        targetPreviewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        targetPreviewCamera.orthographic = true;
        targetPreviewCamera.nearClipPlane = 0.01f;
        targetPreviewCamera.farClipPlane = 100f;

        GameObject lightObject = new("TargetPreviewLight");
        lightObject.hideFlags = HideFlags.HideAndDontSave;
        lightObject.transform.SetParent(targetPreviewWorldRoot.transform, false);
        targetPreviewLight = lightObject.AddComponent<Light>();
        targetPreviewLight.type = LightType.Directional;
        targetPreviewLight.intensity = 1.25f;
        targetPreviewLight.color = Color.white;
        targetPreviewLight.shadows = LightShadows.None;
        targetPreviewLight.transform.rotation = Quaternion.Euler(35f, 135f, 0f);

        EnsureTargetPreviewTexture();
    }

    private void EnsureTargetPreviewTexture()
    {
        if (targetPreviewTexture != null)
            return;

        targetPreviewTexture = new RenderTexture(384, 384, 16, RenderTextureFormat.ARGB32);
        targetPreviewTexture.name = "ScentGuiTargetPreviewRT";
        targetPreviewTexture.Create();

        if (targetPreviewImage != null)
            targetPreviewImage.texture = targetPreviewTexture;
        if (targetPreviewCamera != null)
            targetPreviewCamera.targetTexture = targetPreviewTexture;
    }

    private void BuildTargetPreviewClone(WorldObject agent)
    {
        DestroyTargetPreviewClone();
        targetPreviewedAgent = agent;

        if (agent == null)
        {
            ClearTargetPreviewTexture();
            return;
        }

        EnsureTargetPreviewWorld();
        targetPreviewClone = CreateTargetVisualClone(agent.gameObject);
        targetPreviewClone.name = $"{agent.name}_TargetButtonPreview";
        targetPreviewClone.hideFlags = HideFlags.HideAndDontSave;
        targetPreviewClone.transform.SetParent(targetPreviewWorldRoot.transform, false);
        targetPreviewClone.transform.position = TargetPreviewAnchorPosition;

        CenterTargetPreviewClone(targetPreviewClone);
        RenderTargetPreview();
    }

    private void CenterTargetPreviewClone(GameObject clone)
    {
        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            targetPreviewFramingRadius = 1f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        clone.transform.position += TargetPreviewAnchorPosition - bounds.center;

        Bounds centeredBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            centeredBounds.Encapsulate(renderers[i].bounds);

        targetPreviewFramingRadius = Mathf.Max(centeredBounds.extents.y, Mathf.Max(centeredBounds.extents.x, centeredBounds.extents.z));
        if (targetPreviewFramingRadius < 0.1f)
            targetPreviewFramingRadius = 0.5f;
    }

    private void SpinTargetButtonPreview()
    {
        if (targetPreviewClone == null)
            return;

        targetPreviewClone.transform.RotateAround(
            TargetPreviewAnchorPosition,
            Vector3.up,
            targetPreviewSpinDegreesPerSecond * Time.unscaledDeltaTime);

        RenderTargetPreview();
    }

    private void RenderTargetPreview()
    {
        if (targetPreviewCamera == null)
            return;

        float distance = Mathf.Max(2f, targetPreviewFramingRadius * 4f);
        float cameraHeight = Mathf.Tan(targetPreviewViewAngleDegrees * Mathf.Deg2Rad) * distance;
        targetPreviewCamera.transform.position = TargetPreviewAnchorPosition + new Vector3(0f, cameraHeight, -distance);
        targetPreviewCamera.transform.LookAt(TargetPreviewAnchorPosition + new Vector3(0f, targetPreviewFramingRadius * 0.1f, 0f));
        float figureScale = Mathf.Max(0.01f, targetPreviewFigureScale);
        targetPreviewCamera.orthographicSize = (targetPreviewFramingRadius * 1.45f) / figureScale;
        targetPreviewCamera.Render();
    }

    private void ClearTargetPreviewTexture()
    {
        if (targetPreviewTexture == null)
            return;

        RenderTexture previousActiveTexture = RenderTexture.active;
        RenderTexture.active = targetPreviewTexture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = previousActiveTexture;
    }

    private static GameObject CreateTargetVisualClone(GameObject sourceRoot)
    {
        Dictionary<Transform, Transform> transformMap = new();

        GameObject cloneRoot = new(sourceRoot.name);
        CopyTargetTransform(sourceRoot.transform, cloneRoot.transform);
        transformMap[sourceRoot.transform] = cloneRoot.transform;

        Transform[] sourceTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < sourceTransforms.Length; i++)
        {
            Transform source = sourceTransforms[i];
            GameObject child = new(source.name);
            Transform childTransform = child.transform;
            childTransform.SetParent(transformMap[source.parent], false);
            CopyTargetTransform(source, childTransform);
            transformMap[source] = childTransform;
        }

        for (int i = 0; i < sourceTransforms.Length; i++)
        {
            Transform source = sourceTransforms[i];
            Transform destination = transformMap[source];

            MeshFilter sourceMeshFilter = source.GetComponent<MeshFilter>();
            MeshRenderer sourceMeshRenderer = source.GetComponent<MeshRenderer>();
            if (sourceMeshFilter != null && sourceMeshRenderer != null)
                CopyTargetMeshRenderer(sourceMeshFilter, sourceMeshRenderer, destination.gameObject);

            SkinnedMeshRenderer sourceSkinnedRenderer = source.GetComponent<SkinnedMeshRenderer>();
            if (sourceSkinnedRenderer != null)
                CopyTargetSkinnedMeshRenderer(sourceSkinnedRenderer, destination.gameObject, transformMap);
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

    private static void CopyTargetTransform(Transform source, Transform destination)
    {
        destination.localPosition = source.localPosition;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    private static void CopyTargetMeshRenderer(MeshFilter sourceFilter, MeshRenderer sourceRenderer, GameObject destination)
    {
        MeshFilter destinationFilter = destination.AddComponent<MeshFilter>();
        destinationFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer destinationRenderer = destination.AddComponent<MeshRenderer>();
        destinationRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        destinationRenderer.enabled = true;
    }

    private static void CopyTargetSkinnedMeshRenderer(
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

    private void DestroyTargetPreviewClone()
    {
        if (targetPreviewClone == null)
            return;

        if (Application.isPlaying)
            Destroy(targetPreviewClone);
        else
            DestroyImmediate(targetPreviewClone);

        targetPreviewClone = null;
        targetPreviewedAgent = null;
    }

    private void DestroyTargetPreviewWorld()
    {
        if (targetPreviewWorldRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(targetPreviewWorldRoot);
        else
            DestroyImmediate(targetPreviewWorldRoot);

        targetPreviewWorldRoot = null;
        targetPreviewCamera = null;
        targetPreviewLight = null;
    }

    private void ReleaseTargetPreviewTexture()
    {
        if (targetPreviewCamera != null)
            targetPreviewCamera.targetTexture = null;

        if (targetPreviewImage != null)
            targetPreviewImage.texture = null;

        if (targetPreviewTexture != null)
        {
            targetPreviewTexture.Release();
            if (Application.isPlaying)
                Destroy(targetPreviewTexture);
            else
                DestroyImmediate(targetPreviewTexture);
        }

        targetPreviewTexture = null;
    }

    private void RefreshNoseButtonSelectionState()
    {
        if (noseButtonImage == null)
            return;

        ScentSource selectedSource = EnsureDir() && dir.scentRegistry != null
            ? dir.scentRegistry.SelectedTargetScent
            : null;

        if (selectedSource == null)
        {
            noseButtonImage.color = noseButtonColor;
            return;
        }

        Color accent = GetScentColor(selectedSource);
        accent.a = 0.94f;
        noseButtonImage.color = accent;
    }

    private void RefreshModeButtonState(bool force = false)
    {
        if (modeIconImage == null)
            return;

        AgentDecisionType currentDecisionType = GetCurrentDecisionType();
        if (!force && currentDecisionType == displayedDecisionType)
            return;

        displayedDecisionType = currentDecisionType;
        modeIconImage.sprite = GetDecisionModeSprite(currentDecisionType);
        modeButtonImage.color = currentDecisionType == AgentDecisionType.Undefined
            ? noseButtonColor
            : dropdownSelectedColor;

        RefreshModePanelSelection();
    }

    private void RefreshModePanelSelection()
    {
        AgentDecisionType currentDecisionType = GetCurrentDecisionType();

        for (int i = 0; i < modeButtonBackgrounds.Count && i < selectableDecisionModes.Length; i++)
        {
            Image background = modeButtonBackgrounds[i];
            if (background == null)
                continue;

            background.color = selectableDecisionModes[i] == currentDecisionType
                ? dropdownSelectedColor
                : dropdownRowColor;
        }
    }

    private void RefreshSpeedButtonState(bool force = false)
    {
        if (speedIconImage == null || speedButtonImage == null)
            return;

        WalkMode currentWalkMode = GetCurrentWalkMode();
        if (!force && currentWalkMode == displayedWalkMode)
            return;

        displayedWalkMode = currentWalkMode;
        speedIconImage.sprite = GetSpeedModeSprite(currentWalkMode);
        speedButtonImage.color = currentWalkMode == WalkMode.None
            ? noseButtonColor
            : dropdownSelectedColor;

        RefreshSpeedPanelSelection();
        RefreshActiveTooltipText();
    }

    private void RefreshSpeedPanelSelection()
    {
        WalkMode currentWalkMode = GetCurrentWalkMode();

        for (int i = 0; i < speedButtonBackgrounds.Count && i < selectableSpeedModes.Length; i++)
        {
            Image background = speedButtonBackgrounds[i];
            if (background == null)
                continue;

            background.color = selectableSpeedModes[i] == currentWalkMode
                ? dropdownSelectedColor
                : dropdownRowColor;
        }
    }

    private void ToggleSimulationPause()
    {
        GamePause.Toggle();
        RefreshSimulationButtonState(force: true);
    }

    private void OpenInventoryDialog()
    {
        CloseDropdown();
        CloseModePanel();
        CloseSpeedPanel();
        CloseEmoteDropdown();

        InventoryDialogUI inventoryDialog = FindFirstObjectByType<InventoryDialogUI>();
        if (inventoryDialog == null)
        {
            GameObject inventoryDialogObject = new GameObject("InventoryDialogUI");
            inventoryDialog = inventoryDialogObject.AddComponent<InventoryDialogUI>();
        }

        inventoryDialog.Show();
    }

    private void RefreshSimulationButtonState(bool force = false)
    {
        if (simulationIconImage == null || simulationButtonImage == null)
            return;

        bool isPaused = GamePause.IsPaused;
        if (!force && displayedPausedState.HasValue && displayedPausedState.Value == isPaused)
            return;

        displayedPausedState = isPaused;
        simulationIconImage.sprite = GetSimulationControlSprite(isPaused);
        simulationButtonImage.color = isPaused
            ? dropdownSelectedColor
            : noseButtonColor;

        RefreshActiveTooltipText();
    }

    private void RefreshEmoteButtonState(bool force = false)
    {
        if (emoteIconImage == null || emoteButtonImage == null)
            return;

        EnsureDefaultEmoteSelection();

        Sprite selectedSprite = selectedEmoteEntry.HasValue
            ? GetEmoteSprite(selectedEmoteEntry.Value)
            : null;

        if (!force && emoteIconImage.sprite == selectedSprite)
            return;

        emoteIconImage.sprite = selectedSprite;
        emoteButtonImage.color = selectedEmoteEntry.HasValue
            ? dropdownSelectedColor
            : noseButtonColor;

        RefreshActiveTooltipText();
    }

    private void EnsureDefaultEmoteSelection()
    {
        if (selectedEmoteEntry.HasValue)
            return;

        for (int i = 0; i < DogEmojiCatalog.Entries.Length; i++)
        {
            DogEmojiEntry entry = DogEmojiCatalog.Entries[i];
            if (entry.Name == "Happy" && GetEmoteSprite(entry) != null)
            {
                SetSelectedEmote(entry);
                return;
            }
        }

        for (int i = 0; i < DogEmojiCatalog.Entries.Length; i++)
        {
            DogEmojiEntry entry = DogEmojiCatalog.Entries[i];
            if (GetEmoteSprite(entry) != null)
            {
                SetSelectedEmote(entry);
                return;
            }
        }
    }

    private void SetSelectedEmote(DogEmojiEntry entry)
    {
        if (GetEmoteSprite(entry) == null)
            return;

        selectedEmoteEntry = entry;
    }

    private string GetSelectedEmoteId()
    {
        return selectedEmoteEntry.HasValue ? selectedEmoteEntry.Value.EntryId : string.Empty;
    }

    private AgentDecisionType GetCurrentDecisionType()
    {
        WorldObject controlledObject = GetCurrentControlledWorldObject();
        AgentModule agentModule = controlledObject != null ? controlledObject.agentModule : null;
        return agentModule != null && agentModule.currentDecisionModule != null
            ? agentModule.currentDecisionModule.DecisionType
            : AgentDecisionType.Undefined;
    }

    private WalkMode GetCurrentWalkMode()
    {
        WorldObject controlledObject = GetCurrentControlledWorldObject();
        if (controlledObject != null && controlledObject.motionModule != null)
            return controlledObject.motionModule.currentWalkMode;

        return WalkMode.Walk;
    }

    private WorldObject GetCurrentControlledWorldObject()
    {
        GameInputRouter router = GameInputRouter.Instance != null
            ? GameInputRouter.Instance
            : (EnsureDir() ? dir.gameInputRouter : null);

        if (router != null && router.currentControlledWorldObject != null)
            return router.currentControlledWorldObject;

        return EnsureDir() && dir.playerPack != null ? dir.playerPack.packLeader : null;
    }

    private Sprite GetScentIconSprite()
    {
        return SpriteServer.SpriteLookup("Sense_Smell_None")
            ?? SpriteServer.SpriteLookup("Sense_Smell_Low")
            ?? SpriteServer.SpriteLookup("Sense_Alert_None");
    }

    private Sprite GetTargetCrosshairSprite()
    {
        return SpriteServer.SpriteSheetLookup(targetIconSpriteResourcePath, 0)
            ?? SpriteServer.SpriteLookup("TargetIcon_D_0");
    }

    private Sprite GetPulldownFrameSprite()
    {
        bool useTwoRows = UseTwoRowTopControls();
        if (useTwoRows)
        {
            if (pulldownFrameTwoRowSprite == null)
                pulldownFrameTwoRowSprite = LoadPulldownFrameSprite(GetPulldownFrameTwoRowResourcePath());

            if (pulldownFrameTwoRowSprite != null)
                return pulldownFrameTwoRowSprite;
        }

        if (pulldownFrameSprite == null)
            pulldownFrameSprite = LoadPulldownFrameSprite(pulldownFrameResourcePath);

        return pulldownFrameSprite;
    }

    private string GetPulldownFrameTwoRowResourcePath()
    {
        if (string.IsNullOrWhiteSpace(pulldownFrameTwoRowResourcePath) ||
            pulldownFrameTwoRowResourcePath == LegacyTwoRowPulldownFrameResourcePath)
        {
            pulldownFrameTwoRowResourcePath = DefaultTwoRowPulldownFrameResourcePath;
        }

        return pulldownFrameTwoRowResourcePath;
    }

    private Sprite LoadPulldownFrameSprite(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture != null)
        {
            Sprite generatedSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            generatedSprite.name = texture.name;
            return generatedSprite;
        }

        Debug.LogWarning($"ScentGUI: could not load pulldown frame sprite at Resources/{resourcePath}.", this);
        return null;
    }

    private Sprite GetPulldownTabSprite()
    {
        if (string.IsNullOrWhiteSpace(pulldownTabResourcePath))
            return null;

        Sprite sprite = Resources.Load<Sprite>(pulldownTabResourcePath);
        if (sprite == null)
            Debug.LogWarning($"ScentGUI: could not load pulldown tab sprite at Resources/{pulldownTabResourcePath}.", this);

        return sprite;
    }

    private Sprite GetDecisionModeSprite(AgentDecisionType decisionType)
    {
        return SpriteServer.SpriteSheetLookup(modeSpriteResourcePath, GetDecisionModeSpriteIndex(decisionType))
            ?? SpriteServer.SpriteSheetLookup(modeSpriteResourcePath, GetDecisionModeSpriteIndex(AgentDecisionType.Player));
    }

    private Sprite GetSimulationControlSprite(bool isPaused)
    {
        int desiredIndex = isPaused ? 0 : 1;
        return SpriteServer.SpriteSheetLookup(playPauseSpriteResourcePath, desiredIndex)
            ?? SpriteServer.SpriteSheetLookup(playPauseSpriteResourcePath, 1)
            ?? SpriteServer.SpriteSheetLookup(playPauseSpriteResourcePath, 0);
    }

    private Sprite GetSpeedModeSprite(WalkMode walkMode)
    {
        return SpriteServer.SpriteSheetLookup(speedSpriteResourcePath, GetSpeedModeSpriteIndex(walkMode))
            ?? SpriteServer.SpriteSheetLookup(speedSpriteResourcePath, GetSpeedModeSpriteIndex(WalkMode.Walk));
    }

    private Sprite GetInventoryButtonSprite()
    {
        return SpriteServer.SpriteSheetLookup(inventoryActionSpriteResourcePath, 2);
    }

    private Sprite GetDigHoleButtonSprite()
    {
        return SpriteServer.SpriteSheetLookup(digHoleSpriteResourcePath, 0);
    }

    private Sprite GetAndroidButtonSprite(int index)
    {
        return SpriteServer.SpriteSheetLookup(androidButtonSpriteResourcePath, index);
    }

    private Sprite GetEmoteSprite(DogEmojiEntry entry)
    {
        return SpriteServer.SpriteSheetLookup($"DogEmojiSheet{entry.SheetId}", entry.SpriteIndex);
    }

    private int GetDecisionModeSpriteIndex(AgentDecisionType decisionType)
    {
        switch (decisionType)
        {
            case AgentDecisionType.Player:
                return 0;
            case AgentDecisionType.Follower:
                return 1;
            case AgentDecisionType.Explorer:
                return 2;
            case AgentDecisionType.Immobile:
                return 3;
            case AgentDecisionType.Wanderer:
                return 4;
            case AgentDecisionType.TaskFollower:
                return 5;
            default:
                return 0;
        }
    }

    private int GetSpeedModeSpriteIndex(WalkMode walkMode)
    {
        switch (walkMode)
        {
            case WalkMode.Sneak:
                return 0;
            case WalkMode.Walk:
                return 1;
            case WalkMode.Run:
                return 2;
            default:
                return 1;
        }
    }

    private void ConfigureTooltip(GameObject target, System.Func<string> textProvider)
    {
        if (target == null)
            return;

        ScentGuiTooltipTrigger trigger = GetOrAddComponent<ScentGuiTooltipTrigger>(target);
        trigger.Initialize(this, textProvider);
    }

    private string GetSimulationButtonTooltipText()
    {
        return GamePause.IsPaused ? "Play" : "Pause";
    }

    private string GetTargetButtonTooltipText()
    {
        ScentSource selectedSource = GetSelectedTargetScent();
        if (selectedSource == null)
            return "Target";

        WorldObject targetObject = ResolveScentSourceWorldObject(selectedSource);
        string targetName = targetObject != null && !string.IsNullOrWhiteSpace(targetObject.DisplayName)
            ? targetObject.DisplayName.Trim()
            : GetScentDisplayName(selectedSource);

        return string.IsNullOrWhiteSpace(targetName)
            ? "Target"
            : $"Target: {targetName}";
    }

    private static string GetScentDisplayName(ScentSource scentSource)
    {
        if (scentSource == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(scentSource.scentName)
            ? scentSource.scentName.Trim()
            : scentSource.category.ToString();
    }

    private string GetEmoteButtonTooltipText()
    {
        return selectedEmoteEntry.HasValue
            ? $"Emote: {selectedEmoteEntry.Value.Name}"
            : "Emote Catalog";
    }

    private string GetDecisionModeTooltipText(AgentDecisionType decisionType)
    {
        switch (decisionType)
        {
            case AgentDecisionType.Player:
                return "Player";
            case AgentDecisionType.Follower:
                return "Follow";
            case AgentDecisionType.Explorer:
                return "Explore";
            case AgentDecisionType.Immobile:
                return "Stay";
            case AgentDecisionType.Wanderer:
                return "Wander";
            case AgentDecisionType.TaskFollower:
                return "LLM Controlled";
            default:
                return decisionType.ToString();
        }
    }

    private string GetSpeedModeTooltipText(WalkMode walkMode)
    {
        switch (walkMode)
        {
            case WalkMode.Sneak:
                return "Sneak";
            case WalkMode.Walk:
                return "Walk";
            case WalkMode.Run:
                return "Run";
            default:
                return walkMode.ToString();
        }
    }

    private void RefreshActiveTooltipText()
    {
        if (activeTooltipTrigger == null || tooltipRect == null || !tooltipRect.gameObject.activeSelf)
            return;

        string text = activeTooltipTrigger.GetTooltipText();
        if (string.IsNullOrWhiteSpace(text))
            HideTooltip();
        else
            UpdateTooltipText(text);
    }

    internal void ShowTooltip(ScentGuiTooltipTrigger trigger, Vector2 screenPosition)
    {
        if (trigger == null || tooltipRect == null)
            return;

        string text = trigger.GetTooltipText();
        if (string.IsNullOrWhiteSpace(text))
            return;

        activeTooltipTrigger = trigger;
        tooltipRect.gameObject.SetActive(true);
        UpdateTooltipText(text);
        PositionTooltip(screenPosition);
        tooltipRect.SetAsLastSibling();
    }

    internal void MoveTooltip(ScentGuiTooltipTrigger trigger, Vector2 screenPosition)
    {
        if (trigger == null || trigger != activeTooltipTrigger || tooltipRect == null || !tooltipRect.gameObject.activeSelf)
            return;

        PositionTooltip(screenPosition);
    }

    internal void HideTooltip(ScentGuiTooltipTrigger trigger)
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
        if (tooltipLabel == null || tooltipRect == null)
            return;

        tooltipLabel.text = text;
        Vector2 preferred = tooltipLabel.GetPreferredValues(text, tooltipMaxWidth, 0f);
        float width = Mathf.Min(tooltipMaxWidth, preferred.x) + tooltipPadding.x * 2f;
        float height = preferred.y + tooltipPadding.y * 2f;
        tooltipRect.sizeDelta = new Vector2(Mathf.Max(80f, width), Mathf.Max(42f, height));
    }

    private void PositionTooltip(Vector2 screenPosition)
    {
        if (tooltipRect == null || overlayCanvas == null)
            return;

        RectTransform canvasRect = overlayCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Camera eventCamera = overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : overlayCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                eventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        float canvasScale = overlayCanvas.scaleFactor > 0f ? overlayCanvas.scaleFactor : 1f;
        Vector2 scaledOffset = tooltipScreenOffset / canvasScale;

        Vector2 anchoredPosition = new Vector2(
            localPoint.x + (canvasRect.rect.width * 0.5f),
            localPoint.y - (canvasRect.rect.height * 0.5f));
        anchoredPosition += scaledOffset;

        float minX = 12f;
        float maxX = canvasRect.rect.width - tooltipRect.sizeDelta.x - 12f;
        float minY = -(canvasRect.rect.height - tooltipRect.sizeDelta.y - 12f);
        float maxY = -12f;

        anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, Mathf.Max(minX, maxX));
        anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, Mathf.Min(minY, maxY), Mathf.Max(minY, maxY));
        tooltipRect.anchoredPosition = anchoredPosition;
    }
}

sealed class ScentGuiTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    private ScentGUI owner;
    private System.Func<string> tooltipTextProvider;

    public void Initialize(ScentGUI owner, System.Func<string> tooltipTextProvider)
    {
        this.owner = owner;
        this.tooltipTextProvider = tooltipTextProvider;
    }

    public string GetTooltipText()
    {
        return tooltipTextProvider != null ? tooltipTextProvider() : null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null)
            owner.ShowTooltip(this, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (owner != null)
            owner.MoveTooltip(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
            owner.HideTooltip(this);
    }

    private void OnDisable()
    {
        if (owner != null)
            owner.HideTooltip(this);
    }
}
