using System.Collections.Generic;
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
    [SerializeField] private float noseButtonSize = 88f;
    [SerializeField] private float noseButtonMargin = 24f;
    [SerializeField] private float dropdownWidth = 320f;
    [SerializeField] private float dropdownMaxHeight = 420f;
    [SerializeField] private int uiSortOrder = 5100;
    [SerializeField] private Color noseButtonColor = new Color(0.96f, 0.95f, 0.9f, 0.96f);
    [SerializeField] private Color dropdownBackgroundColor = new Color(0.97f, 0.96f, 0.91f, 0.98f);
    [SerializeField] private Color dropdownRowColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Color dropdownSelectedColor = new Color(0.88f, 0.79f, 0.55f, 0.95f);
    [SerializeField] private Color dropdownTextColor = new Color(0.19f, 0.15f, 0.08f, 1f);

    private readonly Dictionary<string, Sprite> scentSpriteLookup = new Dictionary<string, Sprite>();
    private readonly List<GameObject> dropdownRows = new List<GameObject>();

    private InputAction sniffAction;
    private bool isSniffModeActive;

    private Canvas overlayCanvas;
    private RectTransform noseButtonRect;
    private Image noseButtonImage;
    private Image noseIconImage;
    private RectTransform dropdownRect;
    private RectTransform dropdownContentRect;
    private ScrollRect dropdownScrollRect;
    private bool uiBuilt;

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
        if (dropdownRect == null || !dropdownRect.gameObject.activeSelf)
            return;

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        Vector2 screenPoint = Mouse.current.position.ReadValue();
        bool clickedDropdown = RectTransformUtility.RectangleContainsScreenPoint(dropdownRect, screenPoint, null);
        bool clickedButton = noseButtonRect != null &&
                             RectTransformUtility.RectangleContainsScreenPoint(noseButtonRect, screenPoint, null);

        if (!clickedDropdown && !clickedButton)
            CloseDropdown();
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

        GameObject canvasObject = new GameObject(
            "ScentTargetCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
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
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        BuildNoseButton(canvasObject.transform);
        BuildDropdown(canvasObject.transform);
    }

    private void BuildNoseButton(Transform parent)
    {
        GameObject buttonObject = new GameObject(
            "ScentTargetButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        noseButtonRect = buttonObject.GetComponent<RectTransform>();
        noseButtonRect.anchorMin = new Vector2(1f, 1f);
        noseButtonRect.anchorMax = new Vector2(1f, 1f);
        noseButtonRect.pivot = new Vector2(1f, 1f);
        noseButtonRect.anchoredPosition = new Vector2(-noseButtonMargin, -noseButtonMargin);
        noseButtonRect.sizeDelta = new Vector2(noseButtonSize, noseButtonSize);

        noseButtonImage = buttonObject.GetComponent<Image>();
        noseButtonImage.color = noseButtonColor;
        noseButtonImage.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = noseButtonImage;
        button.onClick.AddListener(ToggleDropdown);

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = Vector2.one * (noseButtonSize * 0.68f);
        iconRect.anchoredPosition = Vector2.zero;

        noseIconImage = iconObject.GetComponent<Image>();
        noseIconImage.sprite = GetScentIconSprite();
        noseIconImage.preserveAspect = true;
        noseIconImage.color = Color.white;
    }

    private void BuildDropdown(Transform parent)
    {
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
            OpenDropdown();
        else
            CloseDropdown();
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

    private void RefreshDropdownContents()
    {
        for (int i = 0; i < dropdownRows.Count; i++)
        {
            if (dropdownRows[i] != null)
                Destroy(dropdownRows[i]);
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
}
