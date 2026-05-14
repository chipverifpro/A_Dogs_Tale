using UnityEngine;
using UnityEngine.UI;

public class MenuSettingsDialog : MonoBehaviour
{
    [SerializeField] private MenuManager menuManager;

    [Header("Themed Dialog")]
    [SerializeField] private string themedDialogRootName = "SettingsDialogRoot";
    [SerializeField] private string themedDialogPrefabResourcePath = "Prefabs/UI/SettingsDialogRoot";
    [SerializeField] private string mapTypeSpriteResourcePath = "Sprites/SettingsMapType";
    [SerializeField] private string graphicsQualitySpriteResourcePath = "Sprites/GraphicsQualitySprites_A";
    [SerializeField] private Vector2 scrollAnchorMin = new Vector2(0.08f, 0.12f);
    [SerializeField] private Vector2 scrollAnchorMax = new Vector2(0.92f, 0.72f);
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
    private Slider scentStepSlider;
    private Text scentStepValueLabel;
    private Image[] mapTypeButtonImages;
    private Image[] graphicsQualityButtonImages;
    private Sprite[] mapTypeSprites;
    private Sprite[] graphicsQualitySprites;
    private PersistentGameSettings.MapType selectedMapType = PersistentGameSettings.MapType.House;
    private int selectedGraphicsLevel = PersistentGameSettings.GraphicsLevelHigh;
    private Font runtimeFont;

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

        RefreshPanelSize();
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

        BuildThemedScrollContent();
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
        scentStepSlider.onValueChanged.AddListener(OnScentStepChanged);
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
        scrollRectTransform.anchorMin = scrollAnchorMin;
        scrollRectTransform.anchorMax = scrollAnchorMax;

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

            Text closeText = closeObject.GetComponentInChildren<Text>(includeInactive: true);
            if (closeText != null)
            {
                closeText.font = GetRuntimeFont();
                closeText.text = "X";
                closeText.color = textColor;
                closeText.fontSize = 18;
                closeText.fontStyle = FontStyle.Bold;
            }

            Image image = closeObject.GetComponent<Image>();
            if (image != null)
                image.color = new Color(0.96f, 0.86f, 0.61f, 0.78f);
        }

        closeButton.onClick.RemoveListener(Close);
        closeButton.onClick.AddListener(Close);
        closeButton.interactable = true;

        Image targetImage = closeButton.targetGraphic as Image;
        if (targetImage == null)
            targetImage = closeButton.GetComponent<Image>();
        if (targetImage != null)
        {
            targetImage.raycastTarget = true;
            closeButton.targetGraphic = targetImage;
        }

        RectTransform rect = closeButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-34f, -34f);
        rect.sizeDelta = new Vector2(32f, 32f);
        rect.SetAsLastSibling();
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

        Button closeButton = CreateButton(panel.transform, "Close", "CloseButton");
        closeButton.onClick.AddListener(Close);

        chatGptToggle.onValueChanged.AddListener(_ => SaveFromControls());
        geminiToggle.onValueChanged.AddListener(_ => SaveFromControls());
        ollamaToggle.onValueChanged.AddListener(_ => SaveFromControls());
        scentStepSlider.onValueChanged.AddListener(OnScentStepChanged);

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

    private void LoadCurrentValues()
    {
        PersistentGameSettings.Data settings = PersistentGameSettings.GetCurrentOrSaved();

        SelectMapType(settings.mapType, save: false);
        SelectGraphicsLevel(settings.graphicsLevel, save: false);
        chatGptToggle?.SetIsOnWithoutNotify(settings.chatGptEnabled);
        geminiToggle?.SetIsOnWithoutNotify(settings.geminiEnabled);
        ollamaToggle?.SetIsOnWithoutNotify(settings.ollamaEnabled);

        float snappedValue = SnapScentStep(settings.scentSimulationTimeStep);
        if (scentStepSlider != null)
            scentStepSlider.SetValueWithoutNotify(snappedValue);
        UpdateScentStepLabel(snappedValue);

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
            graphicsLevel = selectedGraphicsLevel,
            scentSimulationTimeStep = SnapScentStep(scentStepSlider != null ? scentStepSlider.value : current.scentSimulationTimeStep)
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
