using System.Collections.Generic;
using DogGame;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ScentGUI : MonoBehaviour
{
    private const string CanvasName = "ScentTargetCanvas";
    private const string ScentControlsContainerName = "ScentControls";
    private const string DecisionModeControlsContainerName = "DecisionModeControls";
    private const string SpeedControlsContainerName = "SpeedControls";
    private const string SimulationControlsContainerName = "SimulationControls";
    private const string EmoteControlsContainerName = "EmoteControls";
    private const string TooltipContainerName = "Tooltips";

    [Header("External object references")]
    private Dir dir;
    public SniffModeVisuals sniffVisuals;

    [Header("Target Scent Menu")]
    [SerializeField] private string scentSpriteResourcePath = "Sprites/SensesSymbolsColor_v4";
    [SerializeField] private string modeSpriteResourcePath = "Sprites/SpriteSheet_Modes_V2";
    [SerializeField] private string speedSpriteResourcePath = "Sprites/Speeds";
    [SerializeField] private string playPauseSpriteResourcePath = "Sprites/PlayAndPause_Dual";
    [SerializeField] private string emoteSheetAResourcePath = "Sprites/DogEmojiSheetA";
    [SerializeField] private string emoteSheetBResourcePath = "Sprites/DogEmojiSheetB";
    [SerializeField] private string emoteSheetCResourcePath = "Sprites/DogEmojiSheetC";
    [SerializeField] private float noseButtonSize = 176f;
    [SerializeField] private float noseButtonMargin = 24f;
    [SerializeField] private float modeButtonSpacing = 12f;
    [SerializeField] private float modePanelIconSize = 128f;
    [SerializeField] private float dropdownWidth = 320f;
    [SerializeField] private float dropdownMaxHeight = 420f;
    [SerializeField] private float emoteDropdownWidth = 520f;
    [SerializeField] private float emoteDropdownMaxHeight = 520f;
    [SerializeField] private float emoteTileSize = 96f;
    [SerializeField] private int emoteGridColumns = 4;
    [SerializeField] private int uiSortOrder = 5100;
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

    private readonly Dictionary<string, Sprite> scentSpriteLookup = new Dictionary<string, Sprite>();
    private readonly Dictionary<AgentDecisionType, Sprite> modeSpriteLookup = new Dictionary<AgentDecisionType, Sprite>();
    private readonly Dictionary<WalkMode, Sprite> speedSpriteLookup = new Dictionary<WalkMode, Sprite>();
    private readonly Dictionary<int, Sprite> playPauseSpriteLookup = new Dictionary<int, Sprite>();
    private readonly Dictionary<char, Dictionary<int, Sprite>> emoteSpriteLookupBySheet = new Dictionary<char, Dictionary<int, Sprite>>();
    private readonly List<GameObject> dropdownRows = new List<GameObject>();
    private readonly List<GameObject> emoteDropdownTiles = new List<GameObject>();
    private readonly List<Image> modeButtonBackgrounds = new List<Image>();
    private readonly List<Image> speedButtonBackgrounds = new List<Image>();

    private InputAction sniffAction;
    private bool isSniffModeActive;

    private Canvas overlayCanvas;
    private RectTransform noseButtonRect;
    private Image noseButtonImage;
    private Image noseIconImage;
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
    private bool uiBuilt;

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
        sniffAction = new InputAction(
            name: "Sniff",
            type: InputActionType.Button,
            binding: "<Keyboard>/f"
        );
    }

    private void Start()
    {
        EnsureDir();
        BuildRuntimeUIIfNeeded();
        RefreshNoseButtonSelectionState();
    }

    private void Update()
    {
        RefreshModeButtonState();
        RefreshSpeedButtonState();
        RefreshSimulationButtonState();
        RefreshEmoteButtonState();

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        CloseOpenPanelsIfClickedOutside(Mouse.current.position.ReadValue());
    }

    private void OnEnable()
    {
        sniffAction.Enable();
        sniffAction.performed += OnSniffToggle;
    }

    private void OnDisable()
    {
        sniffAction.performed -= OnSniffToggle;
        sniffAction.Disable();
    }

    private void OnSniffToggle(InputAction.CallbackContext ctx)
    {
        isSniffModeActive = !isSniffModeActive;

        if (sniffVisuals != null)
            sniffVisuals.SetSniffMode(isSniffModeActive);

        if (!EnsureDir() || dir.scentRegistry == null)
        {
            Debug.LogError("ScentGUI: scentRegistry is null!");
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
        Transform tooltipTransform = EnsureSectionContainer(canvasObject.transform, TooltipContainerName);

        ReparentExistingUiElement(decisionModeControlsTransform, canvasObject.transform, "DecisionModeTitle");

        BuildNoseButton(scentControlsTransform, canvasObject.transform);
        BuildModeButton(decisionModeControlsTransform, canvasObject.transform);
        BuildSpeedButton(speedControlsTransform, canvasObject.transform);
        BuildSimulationButton(simulationControlsTransform, canvasObject.transform);
        BuildEmoteButton(emoteControlsTransform, canvasObject.transform);
        BuildDropdown(scentControlsTransform, canvasObject.transform);
        BuildModePanel(decisionModeControlsTransform, canvasObject.transform);
        BuildSpeedPanel(speedControlsTransform, canvasObject.transform);
        BuildEmoteDropdown(emoteControlsTransform, canvasObject.transform);
        BuildTooltip(tooltipTransform, canvasObject.transform);
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

    private void BuildNoseButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "ScentTargetButton");
        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "ScentTargetButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        noseButtonRect = buttonObject.GetComponent<RectTransform>();
        if (createdButton)
        {
            noseButtonRect.anchorMin = new Vector2(1f, 1f);
            noseButtonRect.anchorMax = new Vector2(1f, 1f);
            noseButtonRect.pivot = new Vector2(1f, 1f);
            noseButtonRect.anchoredPosition = new Vector2(-noseButtonMargin, -noseButtonMargin);
            noseButtonRect.sizeDelta = new Vector2(noseButtonSize, noseButtonSize);
        }

        noseButtonImage = GetOrAddComponent<Image>(buttonObject);
        noseButtonImage.color = noseButtonColor;
        noseButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = noseButtonImage;
        button.onClick.RemoveListener(ToggleDropdown);
        button.onClick.AddListener(ToggleDropdown);

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

        noseIconImage = GetOrAddComponent<Image>(iconObject);
        noseIconImage.sprite = GetScentIconSprite();
        noseIconImage.preserveAspect = true;
        noseIconImage.color = Color.white;

        ConfigureTooltip(buttonObject, () => "Scent Selection");
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

        modeButtonImage = GetOrAddComponent<Image>(buttonObject);
        modeButtonImage.color = noseButtonColor;
        modeButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = modeButtonImage;
        button.onClick.RemoveListener(ToggleModePanel);
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

        simulationButtonImage = GetOrAddComponent<Image>(buttonObject);
        simulationButtonImage.color = noseButtonColor;
        simulationButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = simulationButtonImage;
        button.onClick.RemoveListener(ToggleSimulationPause);
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

        speedButtonImage = GetOrAddComponent<Image>(buttonObject);
        speedButtonImage.color = noseButtonColor;
        speedButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = speedButtonImage;
        button.onClick.RemoveListener(ToggleSpeedPanel);
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

        emoteButtonImage = GetOrAddComponent<Image>(buttonObject);
        emoteButtonImage.color = noseButtonColor;
        emoteButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = emoteButtonImage;
        button.onClick.RemoveListener(ToggleEmoteDropdown);
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

        emoteIconImage = GetOrAddComponent<Image>(iconObject);
        emoteIconImage.preserveAspect = true;
        emoteIconImage.color = Color.white;
        RefreshEmoteButtonState(force: true);

        ConfigureTooltip(buttonObject, GetEmoteButtonTooltipText);
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
        dropdownObject.transform.SetParent(parent, false);

        emoteDropdownRect = dropdownObject.GetComponent<RectTransform>();
        emoteDropdownRect.anchorMin = new Vector2(1f, 1f);
        emoteDropdownRect.anchorMax = new Vector2(1f, 1f);
        emoteDropdownRect.pivot = new Vector2(1f, 1f);
        emoteDropdownRect.anchoredPosition = new Vector2(
            -(noseButtonMargin + ((noseButtonSize + modeButtonSpacing) * 4f)),
            -(noseButtonMargin + noseButtonSize + 12f));
        emoteDropdownRect.sizeDelta = new Vector2(emoteDropdownWidth, emoteDropdownMaxHeight);

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
        dropdownRect.anchorMin = new Vector2(1f, 1f);
        dropdownRect.anchorMax = new Vector2(1f, 1f);
        dropdownRect.pivot = new Vector2(1f, 1f);
        dropdownRect.anchoredPosition = new Vector2(-noseButtonMargin, -(noseButtonMargin + noseButtonSize + 12f));
        dropdownRect.sizeDelta = new Vector2(dropdownWidth, dropdownMaxHeight);

        Image dropdownImage = dropdownObject.GetComponent<Image>();
        dropdownImage.color = dropdownBackgroundColor;

        GameObject titleObject = CreateTMPLabel(
            parent: dropdownObject.transform,
            name: "Title",
            text: "Follow this scent",
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
        modePanelRect.anchorMin = new Vector2(1f, 1f);
        modePanelRect.anchorMax = new Vector2(1f, 1f);
        modePanelRect.pivot = new Vector2(1f, 1f);
        modePanelRect.anchoredPosition = new Vector2(
            -(noseButtonMargin + noseButtonSize + modeButtonSpacing),
            -(noseButtonMargin + noseButtonSize + 12f));

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
        speedPanelRect.anchorMin = new Vector2(1f, 1f);
        speedPanelRect.anchorMax = new Vector2(1f, 1f);
        speedPanelRect.pivot = new Vector2(1f, 1f);
        speedPanelRect.anchoredPosition = new Vector2(
            -(noseButtonMargin + ((noseButtonSize + modeButtonSpacing) * 2f)),
            -(noseButtonMargin + noseButtonSize + 12f));

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
        emoteDropdownRect.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
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
        emoteDropdownRect.sizeDelta = new Vector2(emoteDropdownWidth, Mathf.Min(emoteDropdownMaxHeight, desiredHeight));
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
        CloseDropdown();
    }

    private void HandleEmoteSelected(DogEmojiEntry entry)
    {
        SetSelectedEmote(entry);
        RefreshEmoteButtonState(force: true);
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

        if (controlledObject.agentMovementModule == null || controlledObject.motionModule == null)
            controlledObject.CreateModulesIfNeeded(ModuleFlags.agentMovementModule | ModuleFlags.motionModule);

        if (controlledObject.agentMovementModule != null)
            controlledObject.agentMovementModule.SetWalkMode(walkMode);
        else if (controlledObject.motionModule != null)
            controlledObject.motionModule.SetWalkMode(walkMode);
        else
        {
            Debug.LogWarning($"ScentGUI: {controlledObject.DisplayName} has no movement modules for speed selection.", controlledObject);
            return;
        }

        RefreshSpeedButtonState(force: true);
        CloseSpeedPanel();
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
        if (scentSpriteLookup.Count == 0)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(scentSpriteResourcePath);
            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite sprite = sprites[i];
                if (sprite == null || string.IsNullOrEmpty(sprite.name))
                    continue;

                scentSpriteLookup[sprite.name] = sprite;
            }
        }

        string[] preferredNames =
        {
            "Sense_Smell_None",
            "Sense_Smell_Low",
            "Sense_Alert_None"
        };

        for (int i = 0; i < preferredNames.Length; i++)
        {
            if (scentSpriteLookup.TryGetValue(preferredNames[i], out Sprite sprite))
                return sprite;
        }

        return null;
    }

    private Sprite GetDecisionModeSprite(AgentDecisionType decisionType)
    {
        if (modeSpriteLookup.Count == 0)
            LoadDecisionModeSprites();

        if (modeSpriteLookup.TryGetValue(decisionType, out Sprite sprite))
            return sprite;

        if (modeSpriteLookup.TryGetValue(AgentDecisionType.Player, out Sprite fallback))
            return fallback;

        return null;
    }

    private Sprite GetSimulationControlSprite(bool isPaused)
    {
        if (playPauseSpriteLookup.Count == 0)
            LoadSimulationControlSprites();

        int desiredIndex = isPaused ? 0 : 1;
        if (playPauseSpriteLookup.TryGetValue(desiredIndex, out Sprite sprite))
            return sprite;

        if (playPauseSpriteLookup.TryGetValue(1, out Sprite pauseSprite))
            return pauseSprite;

        if (playPauseSpriteLookup.TryGetValue(0, out Sprite playSprite))
            return playSprite;

        return null;
    }

    private Sprite GetSpeedModeSprite(WalkMode walkMode)
    {
        if (speedSpriteLookup.Count == 0)
            LoadSpeedModeSprites();

        if (speedSpriteLookup.TryGetValue(walkMode, out Sprite sprite))
            return sprite;

        if (speedSpriteLookup.TryGetValue(WalkMode.Walk, out Sprite fallback))
            return fallback;

        return null;
    }

    private Sprite GetEmoteSprite(DogEmojiEntry entry)
    {
        if (!emoteSpriteLookupBySheet.TryGetValue(entry.SheetId, out Dictionary<int, Sprite> spriteLookup))
        {
            spriteLookup = LoadEmoteSheetSprites(entry.SheetId);
            emoteSpriteLookupBySheet[entry.SheetId] = spriteLookup;
        }

        return spriteLookup.TryGetValue(entry.SpriteIndex, out Sprite sprite) ? sprite : null;
    }

    private Dictionary<int, Sprite> LoadEmoteSheetSprites(char sheetId)
    {
        string resourcePath = NormalizeResourcePath(GetEmoteSheetResourcePath(sheetId));
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        Dictionary<int, Sprite> spriteLookup = new Dictionary<int, Sprite>();

        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning($"ScentGUI: no emote sprites found at Resources/{resourcePath}.", this);
            return spriteLookup;
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;

            int index = GetSpriteSheetIndex(sprite.name);
            if (index >= 0)
                spriteLookup[index] = sprite;
        }

        if (spriteLookup.Count == 0)
            Debug.LogWarning($"ScentGUI: loaded {sprites.Length} emote sprites from Resources/{resourcePath}, but none had numeric suffixes like '_0'.", this);

        return spriteLookup;
    }

    private string GetEmoteSheetResourcePath(char sheetId)
    {
        switch (sheetId)
        {
            case 'A':
                return emoteSheetAResourcePath;
            case 'B':
                return emoteSheetBResourcePath;
            case 'C':
                return emoteSheetCResourcePath;
            default:
                return string.Empty;
        }
    }

    private void LoadDecisionModeSprites()
    {
        string resourcePath = NormalizeResourcePath(modeSpriteResourcePath);
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning($"ScentGUI: no decision mode sprites found at Resources/{resourcePath}. Make sure the path has no file extension and the texture is imported as Sprite Mode = Multiple.", this);
            return;
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;

            switch (GetSpriteSheetIndex(sprite.name))
            {
                case 0:
                //case 6:
                    modeSpriteLookup[AgentDecisionType.Player] = sprite;
                    break;
                case 1:
                //case 7:
                    modeSpriteLookup[AgentDecisionType.Follower] = sprite;
                    break;
                case 2:
                //case 8:
                    modeSpriteLookup[AgentDecisionType.Explorer] = sprite;
                    break;
                case 3:
                //case 9:
                    modeSpriteLookup[AgentDecisionType.Immobile] = sprite;
                    break;
                case 4:
                //case 10:
                    modeSpriteLookup[AgentDecisionType.Wanderer] = sprite;
                    break;
                case 5:
                //case 11:
                    modeSpriteLookup[AgentDecisionType.TaskFollower] = sprite;
                    break;
            }
        }

        if (modeSpriteLookup.Count == 0)
            Debug.LogWarning($"ScentGUI: loaded {sprites.Length} sprites from Resources/{resourcePath}, but none had numeric suffixes like '_0' through '_5'.", this);
    }

    private void LoadSimulationControlSprites()
    {
        string resourcePath = NormalizeResourcePath(playPauseSpriteResourcePath);
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning($"ScentGUI: no play/pause sprites found at Resources/{resourcePath}. Make sure the path has no file extension and the texture is imported as Sprite Mode = Multiple.", this);
            return;
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;

            int index = GetSpriteSheetIndex(sprite.name);
            if (index >= 0)
                playPauseSpriteLookup[index] = sprite;
        }

        if (playPauseSpriteLookup.Count == 0)
            Debug.LogWarning($"ScentGUI: loaded {sprites.Length} sprites from Resources/{resourcePath}, but none had numeric suffixes like '_0' or '_1'.", this);
    }

    private void LoadSpeedModeSprites()
    {
        string resourcePath = NormalizeResourcePath(speedSpriteResourcePath);
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning($"ScentGUI: no speed mode sprites found at Resources/{resourcePath}. Make sure the path has no file extension and the texture is imported as Sprite Mode = Multiple.", this);
            return;
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;

            switch (GetSpriteSheetIndex(sprite.name))
            {
                case 0:
                    speedSpriteLookup[WalkMode.Sneak] = sprite;
                    break;
                case 1:
                    speedSpriteLookup[WalkMode.Walk] = sprite;
                    break;
                case 2:
                    speedSpriteLookup[WalkMode.Run] = sprite;
                    break;
            }
        }

        if (speedSpriteLookup.Count == 0)
            Debug.LogWarning($"ScentGUI: loaded {sprites.Length} sprites from Resources/{resourcePath}, but none had numeric suffixes like '_0' through '_2'.", this);
    }

    private static string NormalizeResourcePath(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return "";

        resourcePath = resourcePath.Trim();
        if (resourcePath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            resourcePath = resourcePath.Substring(0, resourcePath.Length - 4);

        return resourcePath;
    }

    private static int GetSpriteSheetIndex(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
            return -1;

        int separatorIndex = spriteName.LastIndexOf('_');
        if (separatorIndex < 0 || separatorIndex >= spriteName.Length - 1)
            return -1;

        return int.TryParse(spriteName.Substring(separatorIndex + 1), out int index)
            ? index
            : -1;
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
