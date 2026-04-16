using System.Collections.Generic;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ScentGUI : MonoBehaviour
{
    [Header("External object references")]
    private Dir dir;
    public SniffModeVisuals sniffVisuals;

    [Header("Target Scent Menu")]
    [SerializeField] private string scentSpriteResourcePath = "Sprites/SensesSymbolsColor_v4";
    [SerializeField] private string modeSpriteResourcePath = "Sprites/SpriteSheet_Modes_V2";
    [SerializeField] private float noseButtonSize = 176f;
    [SerializeField] private float noseButtonMargin = 24f;
    [SerializeField] private float modeButtonSpacing = 12f;
    [SerializeField] private float modePanelIconSize = 128f;
    [SerializeField] private float dropdownWidth = 320f;
    [SerializeField] private float dropdownMaxHeight = 420f;
    [SerializeField] private int uiSortOrder = 5100;
    [SerializeField] private Color noseButtonColor = new Color(0.96f, 0.95f, 0.9f, 0.96f);
    [SerializeField] private Color dropdownBackgroundColor = new Color(0.97f, 0.96f, 0.91f, 0f);
    [SerializeField] private Color dropdownRowColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Color dropdownSelectedColor = new Color(0.88f, 0.79f, 0.55f, 0.95f);
    [SerializeField] private Color dropdownTextColor = new Color(0.19f, 0.15f, 0.08f, 1f);

    private readonly Dictionary<string, Sprite> scentSpriteLookup = new Dictionary<string, Sprite>();
    private readonly Dictionary<AgentDecisionType, Sprite> modeSpriteLookup = new Dictionary<AgentDecisionType, Sprite>();
    private readonly List<GameObject> dropdownRows = new List<GameObject>();
    private readonly List<Image> modeButtonBackgrounds = new List<Image>();

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
    private AgentDecisionType displayedDecisionType = AgentDecisionType.Undefined;
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

        BuildNoseButton(canvasObject.transform);
        BuildModeButton(canvasObject.transform);
        BuildDropdown(canvasObject.transform);
        BuildModePanel(canvasObject.transform);
    }

    private Transform FindExistingScentTargetCanvas()
    {
        Transform localCanvas = transform.Find("ScentTargetCanvas");
        if (localCanvas != null)
            return localCanvas;

        GameObject sceneCanvas = GameObject.Find("ScentTargetCanvas");
        if (sceneCanvas != null)
            return sceneCanvas.transform;

        RectTransform[] rectTransforms = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform rectTransform = rectTransforms[i];
            if (rectTransform != null && rectTransform.name == "ScentTargetCanvas")
                return rectTransform;
        }

        return null;
    }

    private void BuildNoseButton(Transform parent)
    {
        Transform existingButton = parent.Find("ScentTargetButton");
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
    }

    private void BuildModeButton(Transform parent)
    {
        Transform existingButton = parent.Find("DecisionModeButton");
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
    }

    private void BuildDropdown(Transform parent)
    {
        Transform existingDropdown = parent.Find("ScentTargetDropdown");
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

    private void BuildModePanel(Transform parent)
    {
        Transform existingPanel = parent.Find("DecisionModePanel");
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
            CloseDropdown();
            RefreshModeButtonState(force: true);
            RefreshModePanelSelection();
            modePanelRect.gameObject.SetActive(true);
        }
        else
        {
            CloseModePanel();
        }
    }

    private void OpenDropdown()
    {
        if (dropdownRect == null)
            return;

        RefreshDropdownContents();
        dropdownRect.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        if (dropdownScrollRect != null)
            dropdownScrollRect.verticalNormalizedPosition = 1f;
    }

    private void CloseDropdown()
    {
        if (dropdownRect != null)
            dropdownRect.gameObject.SetActive(false);
    }

    private void CloseModePanel()
    {
        if (modePanelRect != null)
            modePanelRect.gameObject.SetActive(false);
    }

    private void CloseOpenPanelsIfClickedOutside(Vector2 screenPoint)
    {
        bool scentDropdownOpen = dropdownRect != null && dropdownRect.gameObject.activeSelf;
        bool modePanelOpen = modePanelRect != null && modePanelRect.gameObject.activeSelf;
        if (!scentDropdownOpen && !modePanelOpen)
            return;

        bool clickedScentDropdown = scentDropdownOpen &&
                                    RectTransformUtility.RectangleContainsScreenPoint(dropdownRect, screenPoint, null);
        bool clickedNoseButton = noseButtonRect != null &&
                                 RectTransformUtility.RectangleContainsScreenPoint(noseButtonRect, screenPoint, null);
        bool clickedModePanel = modePanelOpen &&
                                RectTransformUtility.RectangleContainsScreenPoint(modePanelRect, screenPoint, null);
        bool clickedModeButton = modeButtonRect != null &&
                                 RectTransformUtility.RectangleContainsScreenPoint(modeButtonRect, screenPoint, null);

        if (scentDropdownOpen && !clickedScentDropdown && !clickedNoseButton)
            CloseDropdown();

        if (modePanelOpen && !clickedModePanel && !clickedModeButton)
            CloseModePanel();
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

    private GameObject CreateInfoRow(string message)
    {
        GameObject rowObject = new GameObject(
            "InfoRow",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        rowObject.transform.SetParent(dropdownContentRect, false);

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

    private AgentDecisionType GetCurrentDecisionType()
    {
        WorldObject controlledObject = GetCurrentControlledWorldObject();
        AgentModule agentModule = controlledObject != null ? controlledObject.agentModule : null;
        return agentModule != null && agentModule.currentDecisionModule != null
            ? agentModule.currentDecisionModule.DecisionType
            : AgentDecisionType.Undefined;
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
}
