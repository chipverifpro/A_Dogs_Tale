using UnityEngine;
using UnityEngine.UI;

public class MenuSettingsDialog : MonoBehaviour
{
    [SerializeField] private MenuManager menuManager;

    [Header("Themed Dialog")]
    [SerializeField] private string themedDialogRootName = "SettingsDialogRoot";
    [SerializeField] private string themedDialogPrefabResourcePath = "Prefabs/UI/SettingsDialogRoot";
    [SerializeField] private string themedBackgroundImageName = "SettingsBackgroundImage";
    [SerializeField] private string tallThemedBackgroundResourcePath = "Sprites/Settings_Background_Vert_C";
    [SerializeField] private string mapTypeSpriteResourcePath = "Sprites/SettingsMapType";
    [SerializeField] private string graphicsQualitySpriteResourcePath = "Sprites/GraphicsQualitySprites_A";
    [SerializeField] private Vector2 scrollAnchorMin = new Vector2(0.08f, 0.12f);
    [SerializeField] private Vector2 scrollAnchorMax = new Vector2(0.92f, 0.72f);
    [SerializeField] private Vector2 tallScrollAnchorMin = new Vector2(0.12f, 0.13f);
    [SerializeField] private Vector2 tallScrollAnchorMax = new Vector2(0.88f, 0.64f);
    [SerializeField] private Vector2 tallThemedDialogSize = new Vector2(597f, 1091f);
    [SerializeField] private Vector2 closeButtonAnchor = new Vector2(0.865f, 0.82f);
    [SerializeField] private Vector2 closeButtonSize = new Vector2(110f, 72f);
    [SerializeField] private Vector2 tallCloseButtonAnchor = new Vector2(0.79f, 0.825f);
    [SerializeField] private Vector2 tallCloseButtonSize = new Vector2(145f, 105f);
    [SerializeField] private float mapTypeButtonHeight = 112f;
    [SerializeField] private float graphicsQualityButtonHeight = 112f;
    [SerializeField] private Color textColor = new Color(0.18f, 0.11f, 0.05f, 1f);
    [SerializeField] private Color sectionColor = new Color(0.23f, 0.13f, 0.05f, 1f);
    [SerializeField] private Color controlColor = new Color(0.96f, 0.86f, 0.61f, 0.72f);
    [SerializeField] private Color selectedControlColor = new Color(0.56f, 0.82f, 0.47f, 0.85f);

    private static readonly int[] GraphicsQualityLevels =
    {
        PersistentGameSettings.GraphicsLevelLow,
        PersistentGameSettings.GraphicsLevelMedium,
        PersistentGameSettings.GraphicsLevelHigh
    };

    private GameObject dialogRoot;
    private RectTransform panelRect;
    private Toggle chatGptToggle;
    private Toggle geminiToggle;
    private Toggle ollamaToggle;
    private Toggle touchscreenJoystickToggle;
    private Slider scentStepSlider;
    private Slider buttonSizeSlider;
    private Text scentStepValueLabel;
    private Text buttonSizeValueLabel;
    private Image buttonSizeSampleIconImage;
    private RectTransform buttonSizeSampleButtonRect;
    private LayoutElement buttonSizeSampleButtonLayout;
    private Image[] mapTypeButtonImages;
    private Image[] graphicsQualityButtonImages;
    private RectTransform themedScrollRect;
    private RectTransform themedBackgroundRect;
    private Image themedBackgroundImage;
    private Sprite defaultThemedBackgroundSprite;
    private Sprite tallThemedBackgroundSprite;
    private RectTransform closeButtonRect;
    private Vector2 defaultThemedDialogSize;
    private Vector2 defaultThemedBackgroundSize;
    private bool hasDefaultThemedLayout;
    private Sprite[] mapTypeSprites;
    private Sprite[] graphicsQualitySprites;
    private PersistentGameSettings.MapType selectedMapType = PersistentGameSettings.MapType.House;
    private int selectedGraphicsLevel = PersistentGameSettings.GraphicsLevelHigh;
    private Font runtimeFont;
    private RectTransform defaultScaleTarget;
    private Vector3 defaultScale = Vector3.one;
    private bool tallDisplayScaleEnabled;
    private float tallDisplayScaleMultiplier = 1f;
    private int lastButtonSizeSampleEmoteIndex = -1;

    public void Initialize(MenuManager owner)
    {
        menuManager = owner;
        HideSceneThemedDialogIfPresent();
    }

    public bool IsOpen => dialogRoot != null && dialogRoot.activeSelf;

    private void Awake()
    {
        MenuSettingsDialog[] dialogs = GetComponents<MenuSettingsDialog>();
        if (dialogs.Length <= 1)
            return;

        for (int i = 0; i < dialogs.Length; i++)
        {
            if (dialogs[i] != this)
                Destroy(dialogs[i]);
        }
    }

    public void Open()
    {
        EnsureBuilt();
        if (dialogRoot == null)
            return;

        ApplyResponsiveThemedLayout();
        RefreshPanelSize();
        ApplyResponsiveScaleToBuiltDialog();
        LoadCurrentValues();
        dialogRoot.SetActive(true);
        dialogRoot.transform.SetAsLastSibling();
    }

    public void Close()
    {
        if (dialogRoot != null)
            dialogRoot.SetActive(false);
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void ApplyTallDisplayScale(bool enabled, float scaleMultiplier)
    {
        tallDisplayScaleEnabled = enabled;
        tallDisplayScaleMultiplier = Mathf.Max(0.01f, scaleMultiplier);
        ApplyResponsiveThemedLayout();
        ApplyResponsiveScaleToBuiltDialog();
    }

    private void EnsureBuilt()
    {
        if (dialogRoot != null)
            return;

        if (TryBindThemedDialog())
            return;

        BuildFallbackDialog();
    }

    private bool TryBindThemedDialog()
    {
        Canvas canvas = ResolveMenuCanvas();
        if (canvas == null)
            return false;

        Transform existingRoot = FindDescendant(canvas.transform, themedDialogRootName);
        GameObject rootObject = existingRoot != null ? existingRoot.gameObject : null;

        if (rootObject == null)
        {
            GameObject prefab = Resources.Load<GameObject>(themedDialogPrefabResourcePath);
            if (prefab != null)
            {
                rootObject = Instantiate(prefab, canvas.transform, false);
                rootObject.name = themedDialogRootName;
            }
        }

        if (rootObject == null)
            return false;

        dialogRoot = rootObject;
        panelRect = dialogRoot.GetComponent<RectTransform>();
        if (dialogRoot.transform.parent != canvas.transform)
            dialogRoot.transform.SetParent(canvas.transform, false);

        CaptureThemedDialogReferences();
        BuildThemedScrollContent();
        ApplyResponsiveThemedLayout();
        dialogRoot.SetActive(false);
        return true;
    }

    private void BuildThemedScrollContent()
    {
        if (dialogRoot == null)
            return;

        Canvas canvas = ResolveMenuCanvas();
        Transform root = dialogRoot.transform;

        Image rootImage = dialogRoot.GetComponent<Image>();
        if (rootImage != null)
            rootImage.raycastTarget = true;

        ScrollRect scrollRect = EnsureScrollView(root);
        themedScrollRect = scrollRect.GetComponent<RectTransform>();
        RectTransform content = scrollRect.content;
        ClearChildren(content);

        VerticalLayoutGroup layout = content.gameObject.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.gameObject.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateSectionHeader(content, "MAP TYPE");
        CreateMapTypeRow(content);

        CreateSectionHeader(content, "AI MODEL");
        GameObject aiRow = CreateRow(content, "AIModelRow", 42f);
        chatGptToggle = CreateToggle(aiRow.transform, "ChatGPT", "ChatGptToggle");
        geminiToggle = CreateToggle(aiRow.transform, "Gemini", "GeminiToggle");
        ollamaToggle = CreateToggle(aiRow.transform, "Local / Ollama", "OllamaToggle");

        CreateSectionHeader(content, "SCENT PHYSICS");
        CreateScentSliderRow(content);

        CreateSectionHeader(content, "GRAPHICS LEVEL");
        CreateGraphicsQualityRow(content);

        CreateSectionHeader(content, "CONTROLS");
        GameObject controlsRow = CreateRow(content, "ControlsRow", 42f);
        touchscreenJoystickToggle = CreateToggle(controlsRow.transform, "Touchscreen joystick visible", "TouchscreenJoystickToggle");
        CreateButtonSizeRow(content);

        CreateSectionHeader(content, "LINKS");
        GameObject linkRow = CreateRow(content, "LinksRow", 44f);
        Button docsButton = CreateButton(linkRow.transform, "Documentation", "DocumentationButton");
        docsButton.onClick.AddListener(() => menuManager?.OpenDocs());
        Button closeButton = CreateButton(linkRow.transform, "Close", "CloseButton");
        closeButton.onClick.AddListener(Close);

        CreateCloseButtonOverlay(root, canvas);

        chatGptToggle.onValueChanged.AddListener(_ => SaveFromControls());
        geminiToggle.onValueChanged.AddListener(_ => SaveFromControls());
        ollamaToggle.onValueChanged.AddListener(_ => SaveFromControls());
        touchscreenJoystickToggle.onValueChanged.AddListener(_ => SaveFromControls());
        scentStepSlider.onValueChanged.AddListener(OnScentStepChanged);
        buttonSizeSlider.onValueChanged.AddListener(OnButtonSizeChanged);
    }

    private ScrollRect EnsureScrollView(Transform root)
    {
        Transform existing = FindDescendant(root, "ScrollView");
        GameObject scrollObject = existing != null
            ? existing.gameObject
            : new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));

        if (scrollObject.transform.parent != root)
            scrollObject.transform.SetParent(root, false);

        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        Stretch(scrollRectTransform);
        themedScrollRect = scrollRectTransform;
        ApplyScrollAnchorsForCurrentLayout(scrollRectTransform);

        Image scrollImage = scrollObject.GetComponent<Image>();
        if (scrollImage == null)
            scrollImage = scrollObject.AddComponent<Image>();
        scrollImage.color = new Color(1f, 1f, 1f, 0f);
        scrollImage.raycastTarget = true;

        ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        RectTransform viewport = EnsureViewport(scrollObject.transform);
        RectTransform content = EnsureContent(viewport);
        scrollRect.viewport = viewport;
        scrollRect.content = content;

        Scrollbar scrollbar = scrollObject.GetComponentInChildren<Scrollbar>(includeInactive: true);
        if (scrollbar != null && scrollbar.handleRect != null)
            scrollRect.verticalScrollbar = scrollbar;
        else
            scrollRect.verticalScrollbar = null;

        return scrollRect;
    }

    private void CaptureThemedDialogReferences()
    {
        if (dialogRoot == null)
            return;

        RectTransform rootRect = dialogRoot.GetComponent<RectTransform>();
        if (rootRect != null && !hasDefaultThemedLayout)
            defaultThemedDialogSize = rootRect.sizeDelta;

        Transform backgroundTransform = FindDescendant(dialogRoot.transform, themedBackgroundImageName);
        if (backgroundTransform != null)
        {
            themedBackgroundRect = backgroundTransform as RectTransform;
            themedBackgroundImage = backgroundTransform.GetComponent<Image>();
        }

        if (themedBackgroundImage == null)
        {
            themedBackgroundImage = dialogRoot.GetComponent<Image>();
            themedBackgroundRect = dialogRoot.GetComponent<RectTransform>();
        }

        if (themedBackgroundImage != null && !hasDefaultThemedLayout)
            defaultThemedBackgroundSprite = themedBackgroundImage.sprite;

        if (themedBackgroundRect != null && !hasDefaultThemedLayout)
            defaultThemedBackgroundSize = themedBackgroundRect.sizeDelta;

        hasDefaultThemedLayout = true;
    }

    private void ApplyResponsiveThemedLayout()
    {
        if (dialogRoot == null || dialogRoot.name != themedDialogRootName)
            return;

        CaptureThemedDialogReferences();

        RectTransform rootRect = dialogRoot.GetComponent<RectTransform>();
        if (rootRect != null)
            rootRect.sizeDelta = tallDisplayScaleEnabled ? tallThemedDialogSize : defaultThemedDialogSize;

        ApplyThemedBackgroundForCurrentLayout();
        ApplyScrollAnchorsForCurrentLayout(themedScrollRect);
        ApplyCloseButtonHitArea();
    }

    private void ApplyThemedBackgroundForCurrentLayout()
    {
        if (themedBackgroundImage == null)
            return;

        Sprite sprite = tallDisplayScaleEnabled ? GetTallThemedBackgroundSprite() : defaultThemedBackgroundSprite;
        if (sprite != null)
            themedBackgroundImage.sprite = sprite;

        themedBackgroundImage.color = Color.white;
        themedBackgroundImage.preserveAspect = true;
        themedBackgroundImage.raycastTarget = false;

        if (themedBackgroundRect != null)
            themedBackgroundRect.sizeDelta = tallDisplayScaleEnabled ? tallThemedDialogSize : defaultThemedBackgroundSize;
    }

    private void ApplyScrollAnchorsForCurrentLayout(RectTransform scrollRectTransform)
    {
        if (scrollRectTransform == null)
            return;

        Stretch(scrollRectTransform);
        scrollRectTransform.anchorMin = tallDisplayScaleEnabled ? tallScrollAnchorMin : scrollAnchorMin;
        scrollRectTransform.anchorMax = tallDisplayScaleEnabled ? tallScrollAnchorMax : scrollAnchorMax;
    }

    private Sprite GetTallThemedBackgroundSprite()
    {
        if (tallThemedBackgroundSprite != null)
            return tallThemedBackgroundSprite;

        tallThemedBackgroundSprite = Resources.Load<Sprite>(tallThemedBackgroundResourcePath);
        if (tallThemedBackgroundSprite != null)
            return tallThemedBackgroundSprite;

        Texture2D texture = Resources.Load<Texture2D>(tallThemedBackgroundResourcePath);
        if (texture == null)
        {
            Debug.LogWarning($"[MenuSettingsDialog] Could not load tall settings background at Resources/{tallThemedBackgroundResourcePath}.", this);
            return null;
        }

        tallThemedBackgroundSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        tallThemedBackgroundSprite.name = texture.name;
        return tallThemedBackgroundSprite;
    }

    private RectTransform EnsureViewport(Transform scrollView)
    {
        Transform existing = scrollView.Find("Viewport");
        GameObject viewportObject = existing != null
            ? existing.gameObject
            : new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));

        if (viewportObject.transform.parent != scrollView)
            viewportObject.transform.SetParent(scrollView, false);

        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        Stretch(viewport);

        Image image = viewportObject.GetComponent<Image>();
        if (image == null)
            image = viewportObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.01f);
        image.raycastTarget = true;

        if (viewportObject.GetComponent<RectMask2D>() == null)
            viewportObject.AddComponent<RectMask2D>();

        return viewport;
    }

    private RectTransform EnsureContent(RectTransform viewport)
    {
        Transform existing = viewport.Find("Content");
        GameObject contentObject = existing != null
            ? existing.gameObject
            : new GameObject("Content", typeof(RectTransform));

        if (contentObject.transform.parent != viewport)
            contentObject.transform.SetParent(viewport, false);

        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);
        return content;
    }

    private void CreateScentSliderRow(Transform parent)
    {
        GameObject row = CreateRow(parent, "ScentStepRow", 42f);

        Text label = CreateLabel(row.transform, "Scent step", 17, FontStyle.Bold, TextAnchor.MiddleLeft, 100f);
        LayoutElement labelLayout = label.gameObject.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = 120f;
        labelLayout.minWidth = 100f;

        GameObject sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
        sliderObject.name = "ScentStepSlider";
        sliderObject.transform.SetParent(row.transform, false);
        scentStepSlider = sliderObject.GetComponent<Slider>();
        scentStepSlider.minValue = 0.1f;
        scentStepSlider.maxValue = 1.0f;
        scentStepSlider.wholeNumbers = false;

        LayoutElement sliderLayout = sliderObject.AddComponent<LayoutElement>();
        sliderLayout.flexibleWidth = 1f;
        sliderLayout.minWidth = 140f;
        sliderLayout.preferredHeight = 20f;

        scentStepValueLabel = CreateLabel(row.transform, "0.1s", 16, FontStyle.Bold, TextAnchor.MiddleRight, 46f, "ScentStepValueLabel");
        LayoutElement valueLayout = scentStepValueLabel.gameObject.GetComponent<LayoutElement>();
        valueLayout.preferredWidth = 54f;
        valueLayout.minWidth = 48f;
    }

    private void CreateButtonSizeRow(Transform parent)
    {
        GameObject row = CreateRow(parent, "ButtonSizeRow", PersistentGameSettings.MaxButtonSize + 16f);
        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        if (rowLayout != null)
        {
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandHeight = false;
        }

        Text label = CreateLabel(row.transform, "Button Size", 17, FontStyle.Bold, TextAnchor.MiddleLeft, 100f);
        LayoutElement labelLayout = label.gameObject.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = 116f;
        labelLayout.minWidth = 96f;

        GameObject sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
        sliderObject.name = "ButtonSizeSlider";
        sliderObject.transform.SetParent(row.transform, false);
        SetLayerRecursive(sliderObject, row.layer);

        buttonSizeSlider = sliderObject.GetComponent<Slider>();
        buttonSizeSlider.minValue = PersistentGameSettings.MinButtonSize;
        buttonSizeSlider.maxValue = PersistentGameSettings.MaxButtonSize;
        buttonSizeSlider.wholeNumbers = true;

        LayoutElement sliderLayout = sliderObject.AddComponent<LayoutElement>();
        sliderLayout.flexibleWidth = 1f;
        sliderLayout.minWidth = 120f;
        sliderLayout.preferredHeight = 20f;

        buttonSizeValueLabel = CreateLabel(row.transform, "176", 16, FontStyle.Bold, TextAnchor.MiddleRight, 46f, "ButtonSizeValueLabel");
        LayoutElement valueLayout = buttonSizeValueLabel.gameObject.GetComponent<LayoutElement>();
        valueLayout.preferredWidth = 54f;
        valueLayout.minWidth = 46f;

        Button sampleButton = CreateButtonSizeSampleButton(row.transform);
        sampleButton.onClick.AddListener(ShowRandomButtonSizeSampleEmote);
        ShowRandomButtonSizeSampleEmote();
    }

    private Button CreateButtonSizeSampleButton(Transform parent)
    {
        GameObject buttonObject = DefaultControls.CreateButton(new DefaultControls.Resources());
        buttonObject.name = "ButtonSizeSampleButton";
        buttonObject.transform.SetParent(parent, false);
        SetLayerRecursive(buttonObject, parent.gameObject.layer);

        Button button = buttonObject.GetComponent<Button>();
        Image backgroundImage = buttonObject.GetComponent<Image>();
        if (backgroundImage != null)
        {
            backgroundImage.color = controlColor;
            button.targetGraphic = backgroundImage;
        }

        Text text = buttonObject.GetComponentInChildren<Text>(includeInactive: true);
        if (text != null)
            text.gameObject.SetActive(false);

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);
        SetLayerRecursive(iconObject, buttonObject.layer);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        Stretch(iconRect);
        iconRect.offsetMin = new Vector2(8f, 8f);
        iconRect.offsetMax = new Vector2(-8f, -8f);

        buttonSizeSampleIconImage = iconObject.GetComponent<Image>();
        buttonSizeSampleIconImage.preserveAspect = true;
        buttonSizeSampleIconImage.raycastTarget = false;
        buttonSizeSampleIconImage.color = Color.white;

        buttonSizeSampleButtonRect = buttonObject.GetComponent<RectTransform>();
        buttonSizeSampleButtonLayout = buttonObject.AddComponent<LayoutElement>();
        return button;
    }

    private void CreateMapTypeRow(Transform parent)
    {
        EnsureMapTypeSpritesLoaded();
        mapTypeButtonImages = new Image[5];

        GameObject row = CreateRow(parent, "MapTypeRow", mapTypeButtonHeight);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
            layout.spacing = 8f;

        for (int i = 0; i < mapTypeButtonImages.Length; i++)
        {
            int capturedIndex = i;
            PersistentGameSettings.MapType mapType = (PersistentGameSettings.MapType)i;
            Button button = CreateMapTypeButton(row.transform, mapType);
            mapTypeButtonImages[i] = button.targetGraphic as Image;
            button.onClick.AddListener(() => SelectMapType((PersistentGameSettings.MapType)capturedIndex, save: true));
        }

        RefreshMapTypeButtonSprites();
    }

    private void CreateGraphicsQualityRow(Transform parent)
    {
        EnsureGraphicsQualitySpritesLoaded();
        graphicsQualityButtonImages = new Image[GraphicsQualityLevels.Length];

        GameObject row = CreateRow(parent, "GraphicsQualityRow", graphicsQualityButtonHeight);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
            layout.spacing = 8f;

        for (int i = 0; i < GraphicsQualityLevels.Length; i++)
        {
            int capturedIndex = i;
            int graphicsLevel = GraphicsQualityLevels[i];
            Button button = CreateGraphicsQualityButton(row.transform, graphicsLevel, capturedIndex);
            graphicsQualityButtonImages[i] = button.targetGraphic as Image;
            button.onClick.AddListener(() => SelectGraphicsLevel(graphicsLevel, save: true));
        }

        RefreshGraphicsQualityButtonSprites();
    }

    private Button CreateMapTypeButton(Transform parent, PersistentGameSettings.MapType mapType)
    {
        GameObject buttonObject = new GameObject($"{mapType}MapTypeButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        SetLayerRecursive(buttonObject, parent.gameObject.layer);

        Image image = buttonObject.GetComponent<Image>();
        image.color = Color.white;
        image.preserveAspect = true;
        image.sprite = GetMapTypeSprite((int)mapType, selected: false);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minWidth = 74f;
        layout.preferredHeight = mapTypeButtonHeight;
        return button;
    }

    private Button CreateGraphicsQualityButton(Transform parent, int graphicsLevel, int spriteIndex)
    {
        GameObject buttonObject = new GameObject($"GraphicsQuality{graphicsLevel}Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        SetLayerRecursive(buttonObject, parent.gameObject.layer);

        Image image = buttonObject.GetComponent<Image>();
        image.color = Color.white;
        image.preserveAspect = true;
        image.sprite = GetGraphicsQualitySprite(spriteIndex);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minWidth = 74f;
        layout.preferredHeight = graphicsQualityButtonHeight;
        return button;
    }

    private void SelectMapType(PersistentGameSettings.MapType mapType, bool save)
    {
        selectedMapType = mapType;
        RefreshMapTypeButtonSprites();

        if (save)
            SaveFromControls();
    }

    private void SelectGraphicsLevel(int graphicsLevel, bool save)
    {
        selectedGraphicsLevel = PersistentGameSettings.SnapGraphicsLevel(graphicsLevel);
        RefreshGraphicsQualityButtonSprites();

        if (save)
            SaveFromControls();
    }

    private void RefreshMapTypeButtonSprites()
    {
        if (mapTypeButtonImages == null)
            return;

        for (int i = 0; i < mapTypeButtonImages.Length; i++)
        {
            Image image = mapTypeButtonImages[i];
            if (image == null)
                continue;

            bool selected = i == (int)selectedMapType;
            Sprite sprite = GetMapTypeSprite(i, selected);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
            }
            else
            {
                image.color = selected ? selectedControlColor : controlColor;
            }
        }
    }

    private Sprite GetMapTypeSprite(int mapTypeIndex, bool selected)
    {
        EnsureMapTypeSpritesLoaded();
        if (mapTypeSprites == null)
            return null;

        int spriteIndex = mapTypeIndex + (selected ? 5 : 0);
        string spriteName = $"SettingsMapType_{spriteIndex}";
        for (int i = 0; i < mapTypeSprites.Length; i++)
        {
            if (mapTypeSprites[i] != null && mapTypeSprites[i].name == spriteName)
                return mapTypeSprites[i];
        }

        return null;
    }

    private void RefreshGraphicsQualityButtonSprites()
    {
        if (graphicsQualityButtonImages == null)
            return;

        for (int i = 0; i < graphicsQualityButtonImages.Length; i++)
        {
            Image image = graphicsQualityButtonImages[i];
            if (image == null)
                continue;

            bool selected = GraphicsQualityLevels[i] == selectedGraphicsLevel;
            Sprite sprite = GetGraphicsQualitySprite(i);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.52f);
            }
            else
            {
                image.color = selected ? selectedControlColor : controlColor;
            }
        }
    }

    private Sprite GetGraphicsQualitySprite(int spriteIndex)
    {
        EnsureGraphicsQualitySpritesLoaded();
        if (graphicsQualitySprites == null)
            return null;

        string spriteName = $"GraphicsQualitySprites_A_{spriteIndex}";
        for (int i = 0; i < graphicsQualitySprites.Length; i++)
        {
            if (graphicsQualitySprites[i] != null && graphicsQualitySprites[i].name == spriteName)
                return graphicsQualitySprites[i];
        }

        return spriteIndex >= 0 && spriteIndex < graphicsQualitySprites.Length
            ? graphicsQualitySprites[spriteIndex]
            : null;
    }

    private void EnsureMapTypeSpritesLoaded()
    {
        if (mapTypeSprites != null)
            return;

        mapTypeSprites = Resources.LoadAll<Sprite>(mapTypeSpriteResourcePath);
        if (mapTypeSprites == null || mapTypeSprites.Length == 0)
            Debug.LogWarning($"[MenuSettingsDialog] Could not load map type sprites from Resources/{mapTypeSpriteResourcePath}.", this);
    }

    private void EnsureGraphicsQualitySpritesLoaded()
    {
        if (graphicsQualitySprites != null)
            return;

        graphicsQualitySprites = Resources.LoadAll<Sprite>(graphicsQualitySpriteResourcePath);
        if (graphicsQualitySprites == null || graphicsQualitySprites.Length == 0)
            Debug.LogWarning($"[MenuSettingsDialog] Could not load graphics quality sprites from Resources/{graphicsQualitySpriteResourcePath}.", this);
    }

    private GameObject CreateRow(Transform parent, string name, float height)
    {
        GameObject row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        SetLayerRecursive(row, parent.gameObject.layer);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        LayoutElement layoutElement = row.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        layoutElement.minHeight = height;
        return row;
    }

    private void CreateSectionHeader(Transform parent, string labelText)
    {
        Text label = CreateLabel(parent, labelText, 20, FontStyle.Bold, TextAnchor.MiddleLeft, 30f);
        label.color = sectionColor;
    }

    private Toggle CreateToggle(Transform parent, string labelText, string objectName)
    {
        GameObject toggleObject = DefaultControls.CreateToggle(new DefaultControls.Resources());
        toggleObject.name = objectName;
        toggleObject.transform.SetParent(parent, false);
        SetLayerRecursive(toggleObject, parent.gameObject.layer);

        Image rowImage = toggleObject.GetComponent<Image>();
        if (rowImage == null)
            rowImage = toggleObject.AddComponent<Image>();
        rowImage.color = controlColor;

        Text toggleText = toggleObject.GetComponentInChildren<Text>(includeInactive: true);
        if (toggleText != null)
        {
            toggleText.font = GetRuntimeFont();
            toggleText.text = labelText;
            toggleText.color = textColor;
            toggleText.fontSize = 16;
            toggleText.fontStyle = FontStyle.Bold;
        }

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        ConfigureToggleVisual(toggle);
        toggle.targetGraphic = rowImage;
        toggle.onValueChanged.AddListener(isOn => rowImage.color = isOn ? selectedControlColor : controlColor);

        LayoutElement layout = toggleObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minWidth = 120f;
        layout.preferredHeight = 36f;
        return toggle;
    }

    private Button CreateButton(Transform parent, string labelText, string objectName)
    {
        GameObject buttonObject = DefaultControls.CreateButton(new DefaultControls.Resources());
        buttonObject.name = objectName;
        buttonObject.transform.SetParent(parent, false);
        SetLayerRecursive(buttonObject, parent.gameObject.layer);

        Image image = buttonObject.GetComponent<Image>();
        if (image != null)
            image.color = new Color(0.49f, 0.69f, 0.88f, 0.86f);

        Text text = buttonObject.GetComponentInChildren<Text>(includeInactive: true);
        if (text != null)
        {
            text.font = GetRuntimeFont();
            text.text = labelText;
            text.color = Color.white;
            text.fontSize = 17;
            text.fontStyle = FontStyle.Bold;
        }

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minWidth = 140f;
        layout.preferredHeight = 38f;
        return buttonObject.GetComponent<Button>();
    }

    private void CreateCloseButtonOverlay(Transform root, Canvas canvas)
    {
        Transform existing = root.Find("CloseSettingsButton");
        Button closeButton = existing != null ? existing.GetComponent<Button>() : null;

        if (closeButton == null)
        {
            GameObject closeObject = DefaultControls.CreateButton(new DefaultControls.Resources());
            closeObject.name = "CloseSettingsButton";
            closeObject.transform.SetParent(root, false);
            SetLayerRecursive(closeObject, canvas != null ? canvas.gameObject.layer : root.gameObject.layer);
            closeButton = closeObject.GetComponent<Button>();
        }

        closeButton.onClick.RemoveListener(Close);
        closeButton.onClick.AddListener(Close);
        closeButton.interactable = true;
        closeButton.transition = Selectable.Transition.None;

        Image targetImage = closeButton.targetGraphic as Image;
        if (targetImage == null)
            targetImage = closeButton.GetComponent<Image>();
        if (targetImage != null)
        {
            targetImage.raycastTarget = true;
            targetImage.color = Color.clear;
            closeButton.targetGraphic = targetImage;
        }

        HideCloseButtonVisuals(closeButton.gameObject);

        closeButtonRect = closeButton.GetComponent<RectTransform>();
        ApplyCloseButtonHitArea();
        if (closeButtonRect != null)
            closeButtonRect.SetAsLastSibling();
    }

    private void ApplyCloseButtonHitArea()
    {
        if (closeButtonRect == null)
            return;

        Vector2 anchor = tallDisplayScaleEnabled ? tallCloseButtonAnchor : closeButtonAnchor;
        closeButtonRect.anchorMin = anchor;
        closeButtonRect.anchorMax = anchor;
        closeButtonRect.pivot = new Vector2(0.5f, 0.5f);
        closeButtonRect.anchoredPosition = Vector2.zero;
        closeButtonRect.sizeDelta = tallDisplayScaleEnabled ? tallCloseButtonSize : closeButtonSize;
    }

    private static void HideCloseButtonVisuals(GameObject closeObject)
    {
        if (closeObject == null)
            return;

        Text[] texts = closeObject.GetComponentsInChildren<Text>(includeInactive: true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null)
                continue;

            texts[i].text = string.Empty;
            texts[i].raycastTarget = false;
            texts[i].enabled = false;
        }

        Graphic[] graphics = closeObject.GetComponentsInChildren<Graphic>(includeInactive: true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
                continue;

            graphic.color = Color.clear;
            graphic.raycastTarget = graphic.gameObject == closeObject;
        }
    }

    private void BuildFallbackDialog()
    {
        Canvas canvas = ResolveMenuCanvas();
        if (canvas == null)
        {
            Debug.LogError("[MenuSettingsDialog] Could not find MenuCanvas.", this);
            return;
        }

        dialogRoot = new GameObject("MenuSettingsDialog", typeof(RectTransform), typeof(Image));
        dialogRoot.transform.SetParent(canvas.transform, false);
        dialogRoot.layer = canvas.gameObject.layer;
        dialogRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform dialogRootRect = dialogRoot.GetComponent<RectTransform>();
        Stretch(dialogRootRect);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(dialogRoot.transform, false);
        panel.layer = canvas.gameObject.layer;
        panel.GetComponent<Image>().color = new Color(0.22f, 0.24f, 0.30f, 0.98f);

        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 16, 16);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreateSectionHeader(panel.transform, "Map Type");
        CreateMapTypeRow(panel.transform);

        CreateSectionHeader(panel.transform, "AI Models");
        chatGptToggle = CreateToggle(panel.transform, "ChatGPT", "ChatGptToggle");
        geminiToggle = CreateToggle(panel.transform, "Gemini", "GeminiToggle");
        ollamaToggle = CreateToggle(panel.transform, "Ollama", "OllamaToggle");
        CreateScentSliderRow(panel.transform);
        CreateSectionHeader(panel.transform, "Graphics Level");
        CreateGraphicsQualityRow(panel.transform);
        CreateSectionHeader(panel.transform, "Controls");
        touchscreenJoystickToggle = CreateToggle(panel.transform, "Touchscreen joystick visible", "TouchscreenJoystickToggle");
        CreateButtonSizeRow(panel.transform);

        Button closeButton = CreateButton(panel.transform, "Close", "CloseButton");
        closeButton.onClick.AddListener(Close);

        chatGptToggle.onValueChanged.AddListener(_ => SaveFromControls());
        geminiToggle.onValueChanged.AddListener(_ => SaveFromControls());
        ollamaToggle.onValueChanged.AddListener(_ => SaveFromControls());
        touchscreenJoystickToggle.onValueChanged.AddListener(_ => SaveFromControls());
        scentStepSlider.onValueChanged.AddListener(OnScentStepChanged);
        buttonSizeSlider.onValueChanged.AddListener(OnButtonSizeChanged);

        dialogRoot.SetActive(false);
    }

    private void RefreshPanelSize()
    {
        if (dialogRoot == null || panelRect == null)
            return;

        if (dialogRoot.name == themedDialogRootName)
            return;

        RectTransform rootRect = dialogRoot.GetComponent<RectTransform>();
        Rect rect = rootRect.rect;
        float width = Mathf.Clamp(rect.width * 0.82f, 400f, 560f);
        float height = Mathf.Clamp(rect.height * 0.82f, 250f, 340f);
        panelRect.sizeDelta = new Vector2(width, height);
    }

    private void ApplyResponsiveScaleToBuiltDialog()
    {
        RectTransform target = GetResponsiveScaleTarget();
        if (target == null)
            return;

        if (defaultScaleTarget != target)
        {
            defaultScaleTarget = target;
            defaultScale = target.localScale;
        }

        float multiplier = tallDisplayScaleEnabled ? tallDisplayScaleMultiplier : 1f;
        target.localScale = defaultScale * multiplier;
    }

    private RectTransform GetResponsiveScaleTarget()
    {
        if (dialogRoot == null)
            return null;

        if (dialogRoot.name == themedDialogRootName)
            return dialogRoot.GetComponent<RectTransform>();

        return panelRect;
    }

    private void OnScentStepChanged(float value)
    {
        if (scentStepSlider == null)
            return;

        float snappedValue = SnapScentStep(value);
        if (!Mathf.Approximately(snappedValue, scentStepSlider.value))
            scentStepSlider.SetValueWithoutNotify(snappedValue);

        UpdateScentStepLabel(snappedValue);
        SaveFromControls();
    }

    private void OnButtonSizeChanged(float value)
    {
        if (buttonSizeSlider == null)
            return;

        float snappedValue = PersistentGameSettings.SnapButtonSize(value);
        if (!Mathf.Approximately(snappedValue, buttonSizeSlider.value))
            buttonSizeSlider.SetValueWithoutNotify(snappedValue);

        UpdateButtonSizeSample(snappedValue);
        SaveFromControls();
    }

    private void LoadCurrentValues()
    {
        PersistentGameSettings.Data settings = PersistentGameSettings.GetCurrentOrSaved();

        SelectMapType(settings.mapType, save: false);
        SelectGraphicsLevel(settings.graphicsLevel, save: false);
        chatGptToggle?.SetIsOnWithoutNotify(settings.chatGptEnabled);
        geminiToggle?.SetIsOnWithoutNotify(settings.geminiEnabled);
        ollamaToggle?.SetIsOnWithoutNotify(settings.ollamaEnabled);
        touchscreenJoystickToggle?.SetIsOnWithoutNotify(settings.touchscreenJoystickVisible);

        float snappedValue = SnapScentStep(settings.scentSimulationTimeStep);
        if (scentStepSlider != null)
            scentStepSlider.SetValueWithoutNotify(snappedValue);
        UpdateScentStepLabel(snappedValue);

        float buttonSize = PersistentGameSettings.SnapButtonSize(settings.buttonSize);
        if (buttonSizeSlider != null)
            buttonSizeSlider.SetValueWithoutNotify(buttonSize);
        UpdateButtonSizeSample(buttonSize);

        RefreshToggleVisuals();
    }

    private void SaveFromControls()
    {
        PersistentGameSettings.Data current = PersistentGameSettings.GetCurrentOrSaved();
        PersistentGameSettings.SaveAndApply(new PersistentGameSettings.Data
        {
            mapType = selectedMapType,
            chatGptEnabled = chatGptToggle != null ? chatGptToggle.isOn : current.chatGptEnabled,
            geminiEnabled = geminiToggle != null ? geminiToggle.isOn : current.geminiEnabled,
            ollamaEnabled = ollamaToggle != null ? ollamaToggle.isOn : current.ollamaEnabled,
            touchscreenJoystickVisible = touchscreenJoystickToggle != null ? touchscreenJoystickToggle.isOn : current.touchscreenJoystickVisible,
            graphicsLevel = selectedGraphicsLevel,
            scentSimulationTimeStep = SnapScentStep(scentStepSlider != null ? scentStepSlider.value : current.scentSimulationTimeStep),
            buttonSize = PersistentGameSettings.SnapButtonSize(buttonSizeSlider != null ? buttonSizeSlider.value : current.buttonSize)
        });
    }

    private void UpdateScentStepLabel(float value)
    {
        if (scentStepValueLabel != null)
            scentStepValueLabel.text = $"{SnapScentStep(value):0.0}s";
    }

    private static float SnapScentStep(float value)
    {
        return Mathf.Clamp(Mathf.Round(value * 10f) / 10f, 0.1f, 1.0f);
    }

    private void UpdateButtonSizeSample(float value)
    {
        float buttonSize = PersistentGameSettings.SnapButtonSize(value);
        if (buttonSizeValueLabel != null)
            buttonSizeValueLabel.text = buttonSize.ToString("0");

        if (buttonSizeSampleButtonLayout != null)
        {
            buttonSizeSampleButtonLayout.minWidth = buttonSize;
            buttonSizeSampleButtonLayout.minHeight = buttonSize;
            buttonSizeSampleButtonLayout.preferredWidth = buttonSize;
            buttonSizeSampleButtonLayout.preferredHeight = buttonSize;
        }

        if (buttonSizeSampleButtonRect != null)
            buttonSizeSampleButtonRect.sizeDelta = new Vector2(buttonSize, buttonSize);
    }

    private void ShowRandomButtonSizeSampleEmote()
    {
        if (buttonSizeSampleIconImage == null || DogEmojiCatalog.Entries == null || DogEmojiCatalog.Entries.Length == 0)
            return;

        int startIndex = UnityEngine.Random.Range(0, DogEmojiCatalog.Entries.Length);
        for (int offset = 0; offset < DogEmojiCatalog.Entries.Length; offset++)
        {
            int index = (startIndex + offset) % DogEmojiCatalog.Entries.Length;
            if (DogEmojiCatalog.Entries.Length > 1 && index == lastButtonSizeSampleEmoteIndex)
                continue;

            DogEmojiEntry entry = DogEmojiCatalog.Entries[index];
            Sprite sprite = SpriteServer.SpriteSheetLookup($"DogEmojiSheet{entry.SheetId}", entry.SpriteIndex);
            if (sprite == null)
                continue;

            lastButtonSizeSampleEmoteIndex = index;
            buttonSizeSampleIconImage.sprite = sprite;
            return;
        }
    }

    private Canvas ResolveMenuCanvas()
    {
        if (menuManager != null && menuManager.btnSettings != null)
            return menuManager.btnSettings.GetComponentInParent<Canvas>();

        GameObject menuCanvas = GameObject.Find("MenuCanvas");
        if (menuCanvas != null)
            return menuCanvas.GetComponent<Canvas>();

        return FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
    }

    private void HideSceneThemedDialogIfPresent()
    {
        Canvas canvas = ResolveMenuCanvas();
        if (canvas == null)
            return;

        Transform existingRoot = FindDescendant(canvas.transform, themedDialogRootName);
        if (existingRoot != null)
            existingRoot.gameObject.SetActive(false);
    }

    private Text CreateLabel(Transform parent, string textValue, int fontSize, FontStyle fontStyle, TextAnchor alignment, float height, string objectName = null)
    {
        string resolvedName = string.IsNullOrWhiteSpace(objectName)
            ? textValue.Replace(" ", string.Empty) + "Label"
            : objectName;

        GameObject labelObject = new GameObject(resolvedName, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        labelObject.transform.SetParent(parent, false);
        SetLayerRecursive(labelObject, parent.gameObject.layer);

        Text label = labelObject.GetComponent<Text>();
        label.text = textValue;
        label.font = GetRuntimeFont();
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.color = textColor;

        LayoutElement layout = labelObject.GetComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;
        return label;
    }

    private Font GetRuntimeFont()
    {
        if (runtimeFont == null)
            runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return runtimeFont;
    }

    private void ConfigureToggleVisual(Toggle toggle)
    {
        if (toggle == null)
            return;

        Transform background = toggle.transform.Find("Background");
        if (background == null)
            return;

        Image backgroundImage = background.GetComponent<Image>();
        if (backgroundImage != null)
            backgroundImage.color = Color.white;

        Transform existingImageCheckmark = background.Find("Checkmark");
        if (existingImageCheckmark != null)
            existingImageCheckmark.gameObject.SetActive(false);

        Transform existingTextCheckmark = background.Find("CheckmarkX");
        Text checkmarkText;
        if (existingTextCheckmark != null)
        {
            checkmarkText = existingTextCheckmark.GetComponent<Text>();
        }
        else
        {
            GameObject checkmarkObject = new GameObject("CheckmarkX", typeof(RectTransform), typeof(Text));
            checkmarkObject.transform.SetParent(background, false);
            checkmarkText = checkmarkObject.GetComponent<Text>();

            RectTransform rect = checkmarkObject.GetComponent<RectTransform>();
            Stretch(rect);
        }

        if (checkmarkText == null)
            return;

        checkmarkText.text = "X";
        checkmarkText.font = GetRuntimeFont();
        checkmarkText.fontSize = 18;
        checkmarkText.fontStyle = FontStyle.Bold;
        checkmarkText.alignment = TextAnchor.MiddleCenter;
        checkmarkText.color = Color.black;
        checkmarkText.raycastTarget = false;

        toggle.graphic = checkmarkText;
        SetToggleGraphicVisible(toggle);
    }

    private void RefreshToggleVisuals()
    {
        RefreshToggleVisual(chatGptToggle);
        RefreshToggleVisual(geminiToggle);
        RefreshToggleVisual(ollamaToggle);
        RefreshToggleVisual(touchscreenJoystickToggle);
    }

    private void RefreshToggleVisual(Toggle toggle)
    {
        if (toggle == null)
            return;

        SetToggleGraphicVisible(toggle);
        Image image = toggle.targetGraphic as Image;
        if (image != null)
            image.color = toggle.isOn ? selectedControlColor : controlColor;
    }

    private static void SetToggleGraphicVisible(Toggle toggle)
    {
        if (toggle?.graphic == null)
            return;

        toggle.graphic.canvasRenderer.SetAlpha(toggle.isOn ? 1f : 0f);
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    private static Transform FindDescendant(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindDescendant(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private static void SetLayerRecursive(GameObject target, int layer)
    {
        if (target == null)
            return;

        target.layer = layer;
        for (int i = 0; i < target.transform.childCount; i++)
            SetLayerRecursive(target.transform.GetChild(i).gameObject, layer);
    }
}
