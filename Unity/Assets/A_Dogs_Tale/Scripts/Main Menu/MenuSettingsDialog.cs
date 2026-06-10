using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DogGame.Settings;

public class MenuSettingsDialog : MonoBehaviour
{
    private const string StoredSecretMask = "********";

    [SerializeField] private MenuManager menuManager;

    [Header("Themed Dialog")]
    [SerializeField] private string themedDialogRootName = "SettingsDialogRoot";
    [SerializeField] private string themedDialogPrefabResourcePath = "Prefabs/UI/SettingsDialogRoot";
    [SerializeField] private string themedBackgroundImageName = "SettingsBackgroundImage";
    [SerializeField] private string tallThemedBackgroundResourcePath = "Sprites/Settings_Background_Vert_C";
    [SerializeField] private string mapTypeSpriteResourcePath = "Sprites/SettingsMapType";
    [SerializeField] private string graphicsQualitySpriteResourcePath = "Sprites/GraphicsQualitySprites_A";
    [SerializeField] private string settingsIconSpriteResourcePath = "Sprites/SettingsIcons_B";
    [SerializeField] private Vector2 scrollAnchorMin = new Vector2(0.08f, 0.12f);
    [SerializeField] private Vector2 scrollAnchorMax = new Vector2(0.92f, 0.72f);
    [SerializeField] private Vector2 tallScrollAnchorMin = new Vector2(0.12f, 0.13f);
    [SerializeField] private Vector2 tallScrollAnchorMax = new Vector2(0.88f, 0.64f);
    [SerializeField] private Vector2 tallThemedDialogSize = new Vector2(597f, 1091f);
    [SerializeField] private Vector2 closeButtonAnchor = new Vector2(0.86f, 0.80f);
    [SerializeField] private Vector2 closeButtonSize = new Vector2(150f, 130f);
    [SerializeField] private Vector2 tallCloseButtonAnchor = new Vector2(0.79f, 0.805f);
    [SerializeField] private Vector2 tallCloseButtonSize = new Vector2(190f, 165f);
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
    private Toggle mistralToggle;
    private Toggle localQwenToggle;
    private Toggle localGemmaToggle;
    private Toggle localMistralToggle;
    private InputField chatGptModelInput;
    private InputField geminiModelInput;
    private InputField mistralModelInput;
    private InputField localQwenModelInput;
    private InputField localGemmaModelInput;
    private InputField localMistralModelInput;
    private InputField openAIApiKeyInput;
    private InputField geminiApiKeyInput;
    private InputField mistralApiKeyInput;
    private Text openAIApiKeyStatusLabel;
    private Text geminiApiKeyStatusLabel;
    private Text mistralApiKeyStatusLabel;
    private Toggle musicToggle;
    private Toggle sfxToggle;
    private Toggle uiToggle;
    private Toggle touchscreenJoystickToggle;
    private Toggle androidSafeAreaToggle;
    private Toggle androidFullscreenToggle;
    private Slider scentStepSlider;
    private Slider musicVolumeSlider;
    private Slider sfxVolumeSlider;
    private Slider uiVolumeSlider;
    private Slider buttonSizeSlider;
    private Text scentStepValueLabel;
    private Text musicVolumeValueLabel;
    private Text sfxVolumeValueLabel;
    private Text uiVolumeValueLabel;
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
    private readonly List<SettingsIconBinding> settingsIconBindings = new List<SettingsIconBinding>();
    private PersistentGameSettings.MapType selectedMapType = PersistentGameSettings.MapType.House;
    private int selectedGraphicsLevel = PersistentGameSettings.GraphicsLevelHigh;
    private Font runtimeFont;
    private RectTransform defaultScaleTarget;
    private Vector3 defaultScale = Vector3.one;
    private bool tallDisplayScaleEnabled;
    private float tallDisplayScaleMultiplier = 1f;
    private int lastButtonSizeSampleEmoteIndex = -1;

    private sealed class SettingsIconBinding
    {
        public RectTransform rect;
        public LayoutElement layout;
        public Text shiftedText;
        public float layoutWidthPadding;
        public float absoluteLeft = -1f;
        public float textGap;
        public float textBaseOffsetMinX;
    }

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

        settingsIconBindings.Clear();

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

        CreateSectionHeader(content, "AI MODELS");
        CreateAiModelRows(content);
        CreateSecretStoreRow(content, "OPENAI_API_KEY", SecretStore.OpenAIApiKey, out openAIApiKeyInput, out openAIApiKeyStatusLabel);
        CreateSecretStoreRow(content, "GEMINI_API_KEY", SecretStore.GeminiApiKey, out geminiApiKeyInput, out geminiApiKeyStatusLabel);
        CreateSecretStoreRow(content, "MISTRAL_API_KEY", SecretStore.MistralApiKey, out mistralApiKeyInput, out mistralApiKeyStatusLabel);

        CreateSectionHeader(content, "SCENT PHYSICS");
        CreateScentSliderRow(content);

        CreateSectionHeader(content, "GRAPHICS LEVEL");
        CreateGraphicsQualityRow(content);

        CreateSectionHeader(content, "SOUND");
        CreateSoundSettingsRows(content);

        CreateSectionHeader(content, "CONTROLS");
        GameObject controlsRow = CreateRow(content, "ControlsRow", 42f);
        touchscreenJoystickToggle = CreateToggle(controlsRow.transform, "Touchscreen joystick visible", "TouchscreenJoystickToggle");
        CreateAndroidDisplayModeRow(content);
        CreateButtonSizeRow(content);

        CreateSectionHeader(content, "LINKS");
        GameObject aiUpdatesRow = CreateRow(content, "AIModelUpdatesRow", 44f);
        Button aiModelUpdatesButton = CreateButton(aiUpdatesRow.transform, "AI Model Updates", "AIModelUpdatesButton");
        aiModelUpdatesButton.onClick.AddListener(() => menuManager?.OpenAiModelUpdates());
        GameObject linkRow = CreateRow(content, "LinksRow", 44f);
        Button docsButton = CreateButton(linkRow.transform, "Documentation", "DocumentationButton");
        docsButton.onClick.AddListener(() => menuManager?.OpenDocs());
        Button splashReviewButton = CreateButton(linkRow.transform, "Review Splash Screens", "ReviewSplashScreensButton");
        splashReviewButton.onClick.AddListener(() => menuManager?.ReviewSplashScreens());
        Button closeButton = CreateButton(linkRow.transform, "Close", "CloseButton");
        closeButton.onClick.AddListener(Close);

        CreateVersionFooter(content);
        CreateCloseButtonOverlay(root, canvas);

        AddAiModelListeners();
        touchscreenJoystickToggle.onValueChanged.AddListener(_ => SaveFromControls());
        musicToggle.onValueChanged.AddListener(_ => SaveFromControls());
        sfxToggle.onValueChanged.AddListener(_ => SaveFromControls());
        uiToggle.onValueChanged.AddListener(_ => SaveFromControls());
        AddAndroidDisplayModeListeners();
        scentStepSlider.onValueChanged.AddListener(OnScentStepChanged);
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        uiVolumeSlider.onValueChanged.AddListener(OnUiVolumeChanged);
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
        scrollRect.scrollSensitivity = 5f;

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

        tallThemedBackgroundSprite = SpriteServer.SpriteLookup(tallThemedBackgroundResourcePath)
            ?? SpriteServer.SpriteResourceLookup(tallThemedBackgroundResourcePath);
        if (tallThemedBackgroundSprite != null)
            return tallThemedBackgroundSprite;

        Debug.LogWarning($"[MenuSettingsDialog] Could not load tall settings background at Resources/{tallThemedBackgroundResourcePath}.", this);
        return null;
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

    private void CreateSoundSettingsRows(Transform parent)
    {
        GameObject enableRow = CreateRow(parent, "SoundEnableRow", 42f);
        musicToggle = CreateToggle(enableRow.transform, "Music Enable", "MusicEnableToggle");
        sfxToggle = CreateToggle(enableRow.transform, "SFX Enable", "SfxEnableToggle");
        uiToggle = CreateToggle(enableRow.transform, "UI Enable", "UiEnableToggle");

        CreateMusicVolumeRow(parent);
        CreateSfxVolumeRow(parent);
        CreateUiVolumeRow(parent);

        GameObject testRow = CreateRow(parent, "SoundTestRow", 42f);
        Button testButton = CreateButton(testRow.transform, "Test Bark", "TestBarkButton");
        testButton.onClick.AddListener(PlayTestBark);
    }

    private void CreateMusicVolumeRow(Transform parent)
    {
        musicVolumeSlider = CreateVolumeSliderRow(
            parent,
            "MusicVolumeRow",
            "Music Volume",
            "MusicVolumeSlider",
            "MusicVolumeValueLabel",
            out musicVolumeValueLabel);
    }

    private void CreateSfxVolumeRow(Transform parent)
    {
        sfxVolumeSlider = CreateVolumeSliderRow(
            parent,
            "SfxVolumeRow",
            "SFX Volume",
            "SfxVolumeSlider",
            "SfxVolumeValueLabel",
            out sfxVolumeValueLabel);
    }

    private void CreateUiVolumeRow(Transform parent)
    {
        uiVolumeSlider = CreateVolumeSliderRow(
            parent,
            "UiVolumeRow",
            "UI Volume",
            "UiVolumeSlider",
            "UiVolumeValueLabel",
            out uiVolumeValueLabel);
    }

    private Slider CreateVolumeSliderRow(Transform parent, string rowName, string labelText, string sliderName, string valueLabelName, out Text valueLabel)
    {
        GameObject row = CreateRow(parent, rowName, 42f);

        Text label = CreateLabel(row.transform, labelText, 17, FontStyle.Bold, TextAnchor.MiddleLeft, 100f);
        LayoutElement labelLayout = label.gameObject.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = 120f;
        labelLayout.minWidth = 100f;

        GameObject sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
        sliderObject.name = sliderName;
        sliderObject.transform.SetParent(row.transform, false);
        SetLayerRecursive(sliderObject, row.layer);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        LayoutElement sliderLayout = sliderObject.AddComponent<LayoutElement>();
        sliderLayout.flexibleWidth = 1f;
        sliderLayout.minWidth = 140f;
        sliderLayout.preferredHeight = 20f;

        valueLabel = CreateLabel(row.transform, "100%", 16, FontStyle.Bold, TextAnchor.MiddleRight, 54f, valueLabelName);
        LayoutElement valueLayout = valueLabel.gameObject.GetComponent<LayoutElement>();
        valueLayout.preferredWidth = 60f;
        valueLayout.minWidth = 54f;

        return slider;
    }

    private void CreateAiModelRows(Transform parent)
    {
        chatGptToggle = CreateAiModelRow(parent, "ChatGPT", "ChatGptToggle", "ChatGptModelInput", PersistentGameSettings.DefaultChatGptModelName, out chatGptModelInput);
        geminiToggle = CreateAiModelRow(parent, "Gemini", "GeminiToggle", "GeminiModelInput", PersistentGameSettings.DefaultGeminiModelName, out geminiModelInput);
        mistralToggle = CreateAiModelRow(parent, "Mistral", "MistralToggle", "MistralModelInput", PersistentGameSettings.DefaultMistralModelName, out mistralModelInput);
        localQwenToggle = CreateAiModelRow(parent, "Local Qwen", "LocalQwenToggle", "LocalQwenModelInput", PersistentGameSettings.DefaultLocalQwenModelName, out localQwenModelInput);
        localGemmaToggle = CreateAiModelRow(parent, "Local Gemma", "LocalGemmaToggle", "LocalGemmaModelInput", PersistentGameSettings.DefaultLocalGemmaModelName, out localGemmaModelInput);
        localMistralToggle = CreateAiModelRow(parent, "Local Mistral", "LocalMistralToggle", "LocalMistralModelInput", PersistentGameSettings.DefaultLocalMistralModelName, out localMistralModelInput);
    }

    private Toggle CreateAiModelRow(Transform parent, string labelText, string toggleName, string inputName, string defaultModelName, out InputField modelInput)
    {
        GameObject row = CreateRow(parent, $"{inputName}Row", 42f);
        Toggle toggle = CreateToggle(row.transform, labelText, toggleName);
        ConfigureCompactToggle(toggle, 170f);

        modelInput = CreateModelNameInputField(row.transform, inputName, defaultModelName);

        Button resetButton = CreateButton(row.transform, "Reset", $"{inputName}ResetButton");
        ConfigureCompactButton(resetButton, 72f);
        InputField capturedInput = modelInput;
        resetButton.onClick.AddListener(() => ResetModelInput(capturedInput, defaultModelName));

        return toggle;
    }

    private InputField CreateModelNameInputField(Transform parent, string objectName, string defaultModelName)
    {
        GameObject inputObject = DefaultControls.CreateInputField(new DefaultControls.Resources());
        inputObject.name = objectName;
        inputObject.transform.SetParent(parent, false);
        SetLayerRecursive(inputObject, parent.gameObject.layer);

        Image image = inputObject.GetComponent<Image>();
        if (image != null)
            image.color = new Color(1f, 0.94f, 0.78f, 0.82f);

        InputField inputField = inputObject.GetComponent<InputField>();
        inputField.contentType = InputField.ContentType.Standard;
        inputField.lineType = InputField.LineType.SingleLine;
        inputField.text = defaultModelName;

        Text text = inputField.textComponent;
        if (text != null)
        {
            text.font = GetRuntimeFont();
            text.color = textColor;
            text.fontSize = 14;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        Text placeholder = inputField.placeholder as Text;
        if (placeholder != null)
        {
            placeholder.font = GetRuntimeFont();
            placeholder.text = defaultModelName;
            placeholder.color = new Color(textColor.r, textColor.g, textColor.b, 0.42f);
            placeholder.fontSize = 14;
            placeholder.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        LayoutElement layout = inputObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minWidth = 220f;
        layout.preferredHeight = 36f;
        return inputField;
    }

    private void AddAiModelListeners()
    {
        chatGptToggle.onValueChanged.AddListener(_ => SaveFromControls());
        geminiToggle.onValueChanged.AddListener(_ => SaveFromControls());
        mistralToggle.onValueChanged.AddListener(_ => SaveFromControls());
        localQwenToggle.onValueChanged.AddListener(_ => SaveFromControls());
        localGemmaToggle.onValueChanged.AddListener(_ => SaveFromControls());
        localMistralToggle.onValueChanged.AddListener(_ => SaveFromControls());

        chatGptModelInput.onEndEdit.AddListener(_ => SaveFromControls());
        geminiModelInput.onEndEdit.AddListener(_ => SaveFromControls());
        mistralModelInput.onEndEdit.AddListener(_ => SaveFromControls());
        localQwenModelInput.onEndEdit.AddListener(_ => SaveFromControls());
        localGemmaModelInput.onEndEdit.AddListener(_ => SaveFromControls());
        localMistralModelInput.onEndEdit.AddListener(_ => SaveFromControls());
    }

    private void CreateSecretStoreRow(Transform parent, string labelText, string secretKey, out InputField inputField, out Text statusLabel)
    {
        GameObject row = CreateRow(parent, $"{labelText}Row", 42f);

        Text label = CreateLabel(row.transform, labelText, 15, FontStyle.Bold, TextAnchor.MiddleLeft, 36f);
        ApplySecretStoreRowIcon(row.transform, labelText);
        LayoutElement labelLayout = label.gameObject.GetComponent<LayoutElement>();
        labelLayout.flexibleWidth = 0f;
        labelLayout.preferredWidth = 150f;
        labelLayout.minWidth = 138f;

        inputField = CreateSecretInputField(row.transform, $"{labelText}Input");

        Button saveButton = CreateButton(row.transform, "Save", $"{labelText}SaveButton");
        ConfigureCompactButton(saveButton, 72f);

        Button clearButton = CreateButton(row.transform, "Clear", $"{labelText}ClearButton");
        ConfigureCompactButton(clearButton, 72f);

        statusLabel = CreateLabel(parent, "", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 22f, $"{labelText}StatusLabel");
        statusLabel.color = new Color(textColor.r, textColor.g, textColor.b, 0.76f);
        RefreshSecretRow(inputField, statusLabel, secretKey);

        InputField capturedInput = inputField;
        Text capturedStatus = statusLabel;
        saveButton.onClick.AddListener(() => SaveSecretFromInput(capturedInput, capturedStatus, secretKey));
        clearButton.onClick.AddListener(() => ClearSecret(capturedInput, capturedStatus, secretKey));
    }

    private void ApplySecretStoreRowIcon(Transform rowTransform, string labelText)
    {
        int iconIndex = labelText switch
        {
            "OPENAI_API_KEY" => 4,
            "GEMINI_API_KEY" => 5,
            "MISTRAL_API_KEY" => 15,
            _ => -1
        };

        if (iconIndex < 0)
            return;

        Sprite icon = GetSettingsIconSprite(iconIndex);
        if (icon == null)
            return;

        GameObject iconObject = new GameObject("SecretIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(rowTransform, false);
        SetLayerRecursive(iconObject, rowTransform.gameObject.layer);
        iconObject.transform.SetSiblingIndex(0);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(30f, 30f);

        LayoutElement layout = iconObject.GetComponent<LayoutElement>();
        layout.minWidth = 34f;
        layout.preferredWidth = 34f;
        layout.minHeight = 30f;
        layout.preferredHeight = 30f;
        layout.flexibleWidth = 0f;

        Image image = iconObject.GetComponent<Image>();
        image.sprite = icon;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;

        RegisterSettingsIcon(iconRect, layout, null, 4f, -1f, 0f);
    }

    private InputField CreateSecretInputField(Transform parent, string objectName)
    {
        GameObject inputObject = DefaultControls.CreateInputField(new DefaultControls.Resources());
        inputObject.name = objectName;
        inputObject.transform.SetParent(parent, false);
        SetLayerRecursive(inputObject, parent.gameObject.layer);

        Image image = inputObject.GetComponent<Image>();
        if (image != null)
            image.color = new Color(1f, 0.94f, 0.78f, 0.82f);

        InputField inputField = inputObject.GetComponent<InputField>();
        inputField.contentType = InputField.ContentType.Password;
        inputField.lineType = InputField.LineType.SingleLine;
        inputField.asteriskChar = '*';
        inputField.text = "";

        Text text = inputField.textComponent;
        if (text != null)
        {
            text.font = GetRuntimeFont();
            text.color = textColor;
            text.fontSize = 15;
        }

        Text placeholder = inputField.placeholder as Text;
        if (placeholder != null)
        {
            placeholder.font = GetRuntimeFont();
            placeholder.text = "API key";
            placeholder.color = new Color(textColor.r, textColor.g, textColor.b, 0.42f);
            placeholder.fontSize = 15;
        }

        LayoutElement layout = inputObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minWidth = 160f;
        layout.preferredHeight = 36f;
        return inputField;
    }

    private static void ConfigureCompactButton(Button button, float width)
    {
        if (button == null)
            return;

        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout == null)
            layout = button.gameObject.AddComponent<LayoutElement>();

        layout.flexibleWidth = 0f;
        layout.minWidth = width;
        layout.preferredWidth = width;
    }

    private static void ConfigureCompactToggle(Toggle toggle, float width)
    {
        if (toggle == null)
            return;

        LayoutElement layout = toggle.GetComponent<LayoutElement>();
        if (layout == null)
            layout = toggle.gameObject.AddComponent<LayoutElement>();

        layout.flexibleWidth = 0f;
        layout.minWidth = width;
        layout.preferredWidth = width;
    }

    private void ResetModelInput(InputField inputField, string defaultModelName)
    {
        if (inputField == null)
            return;

        inputField.text = defaultModelName;
        SaveFromControls();
    }

    private void SaveSecretFromInput(InputField inputField, Text statusLabel, string secretKey)
    {
        string value = inputField != null ? inputField.text : "";
        if (value == StoredSecretMask && SecretStore.HasSecret(secretKey))
        {
            RefreshSecretRow(inputField, statusLabel, secretKey);
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            SetSecretStatus(statusLabel, "Enter a value, then Save. Use Clear to remove a stored key.");
            return;
        }

        if (SecretStore.TrySetSecret(secretKey, value, out string error))
        {
            RefreshSecretRow(inputField, statusLabel, secretKey);
            return;
        }

        SetSecretStatus(statusLabel, $"Could not save: {error}");
    }

    private void ClearSecret(InputField inputField, Text statusLabel, string secretKey)
    {
        if (SecretStore.TryDeleteSecret(secretKey, out string error))
        {
            RefreshSecretRow(inputField, statusLabel, secretKey);
            return;
        }

        SetSecretStatus(statusLabel, $"Could not clear: {error}");
    }

    private void RefreshSecretRows()
    {
        RefreshSecretRow(openAIApiKeyInput, openAIApiKeyStatusLabel, SecretStore.OpenAIApiKey);
        RefreshSecretRow(geminiApiKeyInput, geminiApiKeyStatusLabel, SecretStore.GeminiApiKey);
        RefreshSecretRow(mistralApiKeyInput, mistralApiKeyStatusLabel, SecretStore.MistralApiKey);
    }

    private void RefreshSecretRow(InputField inputField, Text statusLabel, string secretKey)
    {
        bool hasSecret = SecretStore.HasSecret(secretKey);
        if (inputField != null)
            inputField.text = hasSecret ? StoredSecretMask : "";

        string status = hasSecret
            ? $"Stored encrypted locally in {SecretStore.Current.BackendName}"
            : $"Not found in {SecretStore.Current.BackendName}";

        SetSecretStatus(statusLabel, status);
    }

    private static void SetSecretStatus(Text statusLabel, string status)
    {
        if (statusLabel != null)
            statusLabel.text = status;
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
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        return button;
    }

    private void CreateAndroidDisplayModeRow(Transform parent)
    {
        if (!PersistentGameSettings.ShouldShowAndroidDisplayModeSetting())
            return;

        GameObject row = CreateRow(parent, "AndroidDisplayModeRow", 42f);

        Text label = CreateLabel(row.transform, "Android screen", 17, FontStyle.Bold, TextAnchor.MiddleLeft, 100f);
        LayoutElement labelLayout = label.gameObject.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = 130f;
        labelLayout.minWidth = 120f;

        androidSafeAreaToggle = CreateToggle(row.transform, "Safe area", "AndroidSafeAreaToggle");
        androidFullscreenToggle = CreateToggle(row.transform, "Fullscreen", "AndroidFullscreenToggle");
    }

    private void AddAndroidDisplayModeListeners()
    {
        if (androidSafeAreaToggle != null)
        {
            androidSafeAreaToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                    SetAndroidDisplayMode(fullscreen: false, save: true);
                else if (androidFullscreenToggle == null || !androidFullscreenToggle.isOn)
                    SetAndroidDisplayMode(fullscreen: false, save: false);
            });
        }

        if (androidFullscreenToggle != null)
        {
            androidFullscreenToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                    SetAndroidDisplayMode(fullscreen: true, save: true);
                else if (androidSafeAreaToggle == null || !androidSafeAreaToggle.isOn)
                    SetAndroidDisplayMode(fullscreen: true, save: false);
            });
        }
    }

    private void CreateMapTypeRow(Transform parent)
    {
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
            button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
            button.onClick.AddListener(() => SelectMapType((PersistentGameSettings.MapType)capturedIndex, save: true));
        }

        RefreshMapTypeButtonSprites();
    }

    private void CreateGraphicsQualityRow(Transform parent)
    {
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
            button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
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
        int spriteIndex = mapTypeIndex + (selected ? 5 : 0);
        string semanticName = mapTypeIndex switch
        {
            0 => selected ? "House_En" : "House_Dis",
            1 => selected ? "Yard_En" : "Yard_Dis",
            2 => selected ? "DogPark_En" : "DogPark_Dis",
            3 => selected ? "Forest_En" : "Forest_Dis",
            4 => selected ? "Castle_En" : "Castle_Dis",
            _ => string.Empty
        };
        string spriteName = $"SettingsMapType_{spriteIndex}";
        return SpriteServer.SpriteLookup(semanticName)
            ?? SpriteServer.SpriteLookup(spriteName)
            ?? SpriteServer.SpriteSheetLookupByName(mapTypeSpriteResourcePath, spriteName);
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
        string semanticName = spriteIndex switch
        {
            0 => "Graphics_Low",
            1 => "Graphics_Medium",
            2 => "Graphics_High",
            _ => string.Empty
        };
        string spriteName = $"GraphicsQualitySprites_A_{spriteIndex}";
        return SpriteServer.SpriteLookup(semanticName)
            ?? SpriteServer.SpriteLookup(spriteName)
            ?? SpriteServer.SpriteSheetLookupByName(graphicsQualitySpriteResourcePath, spriteName)
            ?? SpriteServer.SpriteSheetLookup(graphicsQualitySpriteResourcePath, spriteIndex);
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
        if (string.Equals(labelText, "SOUND", StringComparison.OrdinalIgnoreCase))
        {
            GameObject headerRow = CreateRow(parent, "SoundSectionHeader", 30f);
            HorizontalLayoutGroup layout = headerRow.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 8f;
                layout.childForceExpandWidth = false;
            }

            AddInlineSettingsIcon(headerRow.transform, 7, "SoundSectionIcon", 28f, 32f);
            Text soundLabel = CreateLabel(headerRow.transform, labelText, 20, FontStyle.Bold, TextAnchor.MiddleLeft, 30f);
            soundLabel.color = sectionColor;

            LayoutElement soundLabelLayout = soundLabel.GetComponent<LayoutElement>();
            if (soundLabelLayout != null)
                soundLabelLayout.flexibleWidth = 1f;
            return;
        }

        Text label = CreateLabel(parent, labelText, 20, FontStyle.Bold, TextAnchor.MiddleLeft, 30f);
        label.color = sectionColor;
    }

    private void CreateVersionFooter(Transform parent)
    {
        Text label = CreateLabel(
            parent,
            GetVersionFooterText(),
            14,
            FontStyle.Normal,
            TextAnchor.MiddleCenter,
            24f,
            "VersionFooterLabel");

        label.color = new Color(textColor.r, textColor.g, textColor.b, 0.72f);
    }

    private static string GetVersionFooterText()
    {
        string version = Application.version;
        return string.IsNullOrWhiteSpace(version) ? "Version unknown" : $"Version {version}";
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
            toggleText.text = FormatToggleLabel(labelText, objectName);
            toggleText.color = textColor;
            toggleText.fontSize = 16;
            toggleText.fontStyle = FontStyle.Bold;
            toggleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            toggleText.verticalOverflow = VerticalWrapMode.Truncate;
            toggleText.alignment = TextAnchor.MiddleLeft;
            toggleText.lineSpacing = 0.85f;
        }

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        ConfigureToggleVisual(toggle);
        ApplyProviderToggleIcon(toggleObject, toggleText, objectName);
        toggle.targetGraphic = rowImage;
        toggle.onValueChanged.AddListener(isOn => rowImage.color = isOn ? selectedControlColor : controlColor);

        LayoutElement layout = toggleObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minWidth = 120f;
        float toggleHeight = IsTwoLineToggle(objectName) ? 56f : 36f;
        layout.minHeight = toggleHeight;
        layout.preferredHeight = toggleHeight;
        return toggle;
    }

    private void ApplyProviderToggleIcon(GameObject toggleObject, Text toggleText, string objectName)
    {
        int iconIndex = objectName switch
        {
            "ChatGptToggle" => 0,
            "GeminiToggle" => 1,
            "MistralToggle" => 13,
            "OllamaToggle" => 3,
            "LocalQwenToggle" => 16,
            "LocalGemmaToggle" => 17,
            "LocalMistralToggle" => 18,
            "TouchscreenJoystickToggle" => 9,
            _ => -1
        };

        if (iconIndex < 0)
            return;

        Sprite icon = GetSettingsIconSprite(iconIndex);
        if (icon == null)
            return;

        GameObject iconObject = new GameObject("ProviderIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(toggleObject.transform, false);
        SetLayerRecursive(iconObject, toggleObject.layer);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(56f, 0f);
        iconRect.sizeDelta = new Vector2(28f, 28f);

        Image image = iconObject.GetComponent<Image>();
        image.sprite = icon;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;

        RegisterSettingsIcon(iconRect, null, toggleText, 0f, 42f, 8f);

        if (toggleText != null)
        {
            RectTransform textRect = toggleText.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            Vector2 offsetMin = textRect.offsetMin;
            offsetMin.x = Mathf.Max(offsetMin.x, 78f);
            offsetMin.y = 2f;
            textRect.offsetMin = offsetMin;
            Vector2 offsetMax = textRect.offsetMax;
            offsetMax.y = -2f;
            textRect.offsetMax = offsetMax;
        }
    }

    private static string FormatToggleLabel(string labelText, string objectName)
    {
        return labelText;
    }

    private static bool IsTwoLineToggle(string objectName)
    {
        return false;
    }

    private Sprite GetSettingsIconSprite(int index)
    {
        string semanticName = index switch
        {
            0 => "ChatGPT",
            1 => "Gemini",
            2 => "Qwen",
            3 => "Ollama",
            4 => "OpenAI_API_KEY",
            5 => "Gemini_API_KEY",
            6 => "HappyDog",
            7 => "HeadphonesDog",
            8 => "JoystickDog_A",
            9 => "JoystickDog_B",
            10 => "JoystickDog_C",
            11 => "Documents",
            12 => "Gemma",
            13 => "Mistral",
            14 => "MetaAI",
            15 => "Mistral_API_KEY",
            16 => "Ollama_Qwen",
            17 => "Ollama_Gemma",
            18 => "Ollama_Mistral",
            _ => string.Empty
        };
        string spriteName = $"SettingsIcons_B_{index}";
        return SpriteServer.SpriteLookup(semanticName)
            ?? SpriteServer.SpriteLookup(spriteName)
            ?? SpriteServer.SpriteSheetLookupByName(settingsIconSpriteResourcePath, spriteName)
            ?? SpriteServer.SpriteSheetLookup(settingsIconSpriteResourcePath, index);
    }

    private Image AddInlineSettingsIcon(Transform parent, int iconIndex, string objectName, float size, float width)
    {
        Sprite icon = GetSettingsIconSprite(iconIndex);
        if (icon == null)
            return null;

        GameObject iconObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(parent, false);
        SetLayerRecursive(iconObject, parent.gameObject.layer);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(size, size);

        LayoutElement layout = iconObject.GetComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.minHeight = size;
        layout.preferredHeight = size;
        layout.flexibleWidth = 0f;

        Image image = iconObject.GetComponent<Image>();
        image.sprite = icon;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;
        RegisterSettingsIcon(iconRect, layout, null, width - size, -1f, 0f);
        return image;
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
        ApplyButtonIcon(buttonObject, text, objectName);
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        return button;
    }

    private void ApplyButtonIcon(GameObject buttonObject, Text buttonText, string objectName)
    {
        int iconIndex = objectName switch
        {
            "DocumentationButton" => 11,
            _ => -1
        };

        if (iconIndex < 0)
            return;

        Sprite icon = GetSettingsIconSprite(iconIndex);
        if (icon == null)
            return;

        GameObject iconObject = new GameObject("ButtonIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);
        SetLayerRecursive(iconObject, buttonObject.layer);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(24f, 0f);
        iconRect.sizeDelta = new Vector2(24f, 24f);

        Image image = iconObject.GetComponent<Image>();
        image.sprite = icon;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;

        RegisterSettingsIcon(iconRect, null, buttonText, 0f, 12f, 10f);

        if (buttonText != null)
        {
            RectTransform textRect = buttonText.rectTransform;
            Vector2 offsetMin = textRect.offsetMin;
            offsetMin.x = Mathf.Max(offsetMin.x, 46f);
            textRect.offsetMin = offsetMin;
        }
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
        closeButton.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        closeButton.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
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
        settingsIconBindings.Clear();

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
        CreateAiModelRows(panel.transform);
        CreateSecretStoreRow(panel.transform, "OPENAI_API_KEY", SecretStore.OpenAIApiKey, out openAIApiKeyInput, out openAIApiKeyStatusLabel);
        CreateSecretStoreRow(panel.transform, "GEMINI_API_KEY", SecretStore.GeminiApiKey, out geminiApiKeyInput, out geminiApiKeyStatusLabel);
        CreateSecretStoreRow(panel.transform, "MISTRAL_API_KEY", SecretStore.MistralApiKey, out mistralApiKeyInput, out mistralApiKeyStatusLabel);
        CreateScentSliderRow(panel.transform);
        CreateSectionHeader(panel.transform, "Graphics Level");
        CreateGraphicsQualityRow(panel.transform);
        CreateSectionHeader(panel.transform, "Sound");
        CreateSoundSettingsRows(panel.transform);
        CreateSectionHeader(panel.transform, "Controls");
        touchscreenJoystickToggle = CreateToggle(panel.transform, "Touchscreen joystick visible", "TouchscreenJoystickToggle");
        CreateAndroidDisplayModeRow(panel.transform);
        CreateButtonSizeRow(panel.transform);

        Button closeButton = CreateButton(panel.transform, "Close", "CloseButton");
        closeButton.onClick.AddListener(Close);

        CreateVersionFooter(panel.transform);

        AddAiModelListeners();
        touchscreenJoystickToggle.onValueChanged.AddListener(_ => SaveFromControls());
        musicToggle.onValueChanged.AddListener(_ => SaveFromControls());
        sfxToggle.onValueChanged.AddListener(_ => SaveFromControls());
        uiToggle.onValueChanged.AddListener(_ => SaveFromControls());
        AddAndroidDisplayModeListeners();
        scentStepSlider.onValueChanged.AddListener(OnScentStepChanged);
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        uiVolumeSlider.onValueChanged.AddListener(OnUiVolumeChanged);
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

    private void OnMusicVolumeChanged(float value)
    {
        if (musicVolumeSlider == null)
            return;

        float snappedValue = SnapVolume(value);
        if (!Mathf.Approximately(snappedValue, musicVolumeSlider.value))
            musicVolumeSlider.SetValueWithoutNotify(snappedValue);

        UpdateMusicVolumeLabel(snappedValue);
        SaveFromControls();
    }

    private void OnSfxVolumeChanged(float value)
    {
        if (sfxVolumeSlider == null)
            return;

        float snappedValue = SnapVolume(value);
        if (!Mathf.Approximately(snappedValue, sfxVolumeSlider.value))
            sfxVolumeSlider.SetValueWithoutNotify(snappedValue);

        UpdateSfxVolumeLabel(snappedValue);
        SaveFromControls();
    }

    private void OnUiVolumeChanged(float value)
    {
        if (uiVolumeSlider == null)
            return;

        float snappedValue = SnapVolume(value);
        if (!Mathf.Approximately(snappedValue, uiVolumeSlider.value))
            uiVolumeSlider.SetValueWithoutNotify(snappedValue);

        UpdateUiVolumeLabel(snappedValue);
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
        mistralToggle?.SetIsOnWithoutNotify(settings.mistralEnabled);
        localQwenToggle?.SetIsOnWithoutNotify(settings.localQwenEnabled);
        localGemmaToggle?.SetIsOnWithoutNotify(settings.localGemmaEnabled);
        localMistralToggle?.SetIsOnWithoutNotify(settings.localMistralEnabled);
        SetInputTextWithoutNotify(chatGptModelInput, settings.chatGptModelName);
        SetInputTextWithoutNotify(geminiModelInput, settings.geminiModelName);
        SetInputTextWithoutNotify(mistralModelInput, settings.mistralModelName);
        SetInputTextWithoutNotify(localQwenModelInput, settings.localQwenModelName);
        SetInputTextWithoutNotify(localGemmaModelInput, settings.localGemmaModelName);
        SetInputTextWithoutNotify(localMistralModelInput, settings.localMistralModelName);
        RefreshSecretRows();
        musicToggle?.SetIsOnWithoutNotify(settings.musicEnabled);
        sfxToggle?.SetIsOnWithoutNotify(settings.sfxEnabled);
        uiToggle?.SetIsOnWithoutNotify(settings.uiEnabled);
        touchscreenJoystickToggle?.SetIsOnWithoutNotify(settings.touchscreenJoystickVisible);
        SetAndroidDisplayMode(settings.androidFullscreenEnabled, save: false);

        float snappedValue = SnapScentStep(settings.scentSimulationTimeStep);
        if (scentStepSlider != null)
            scentStepSlider.SetValueWithoutNotify(snappedValue);
        UpdateScentStepLabel(snappedValue);

        float musicVolume = SnapVolume(settings.musicVolume);
        if (musicVolumeSlider != null)
            musicVolumeSlider.SetValueWithoutNotify(musicVolume);
        UpdateMusicVolumeLabel(musicVolume);

        float sfxVolume = SnapVolume(settings.sfxVolume);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(sfxVolume);
        UpdateSfxVolumeLabel(sfxVolume);

        float uiVolume = SnapVolume(settings.uiVolume);
        if (uiVolumeSlider != null)
            uiVolumeSlider.SetValueWithoutNotify(uiVolume);
        UpdateUiVolumeLabel(uiVolume);

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
            mistralEnabled = mistralToggle != null ? mistralToggle.isOn : current.mistralEnabled,
            localQwenEnabled = localQwenToggle != null ? localQwenToggle.isOn : current.localQwenEnabled,
            localGemmaEnabled = localGemmaToggle != null ? localGemmaToggle.isOn : current.localGemmaEnabled,
            localMistralEnabled = localMistralToggle != null ? localMistralToggle.isOn : current.localMistralEnabled,
            chatGptModelName = GetModelInputValue(chatGptModelInput, current.chatGptModelName),
            geminiModelName = GetModelInputValue(geminiModelInput, current.geminiModelName),
            mistralModelName = GetModelInputValue(mistralModelInput, current.mistralModelName),
            localQwenModelName = GetModelInputValue(localQwenModelInput, current.localQwenModelName),
            localGemmaModelName = GetModelInputValue(localGemmaModelInput, current.localGemmaModelName),
            localMistralModelName = GetModelInputValue(localMistralModelInput, current.localMistralModelName),
            ollamaEnabled =
                localQwenToggle != null || localGemmaToggle != null || localMistralToggle != null
                    ? (localQwenToggle != null && localQwenToggle.isOn) ||
                      (localGemmaToggle != null && localGemmaToggle.isOn) ||
                      (localMistralToggle != null && localMistralToggle.isOn)
                    : current.ollamaEnabled,
            musicEnabled = musicToggle != null ? musicToggle.isOn : current.musicEnabled,
            musicVolume = SnapVolume(musicVolumeSlider != null ? musicVolumeSlider.value : current.musicVolume),
            sfxEnabled = sfxToggle != null ? sfxToggle.isOn : current.sfxEnabled,
            sfxVolume = SnapVolume(sfxVolumeSlider != null ? sfxVolumeSlider.value : current.sfxVolume),
            uiEnabled = uiToggle != null ? uiToggle.isOn : current.uiEnabled,
            uiVolume = SnapVolume(uiVolumeSlider != null ? uiVolumeSlider.value : current.uiVolume),
            touchscreenJoystickVisible = touchscreenJoystickToggle != null ? touchscreenJoystickToggle.isOn : current.touchscreenJoystickVisible,
            graphicsLevel = selectedGraphicsLevel,
            scentSimulationTimeStep = SnapScentStep(scentStepSlider != null ? scentStepSlider.value : current.scentSimulationTimeStep),
            buttonSize = PersistentGameSettings.SnapButtonSize(buttonSizeSlider != null ? buttonSizeSlider.value : current.buttonSize),
            androidFullscreenEnabled = androidFullscreenToggle != null ? androidFullscreenToggle.isOn : current.androidFullscreenEnabled
        });
    }

    private static void SetInputTextWithoutNotify(InputField inputField, string value)
    {
        if (inputField == null)
            return;

        inputField.SetTextWithoutNotify(value ?? string.Empty);
    }

    private static string GetModelInputValue(InputField inputField, string fallback)
    {
        if (inputField == null)
            return fallback;

        return string.IsNullOrWhiteSpace(inputField.text) ? fallback : inputField.text.Trim();
    }

    private void UpdateScentStepLabel(float value)
    {
        if (scentStepValueLabel != null)
            scentStepValueLabel.text = $"{SnapScentStep(value):0.0}s";
    }

    private void UpdateMusicVolumeLabel(float value)
    {
        if (musicVolumeValueLabel != null)
            musicVolumeValueLabel.text = $"{Mathf.RoundToInt(SnapVolume(value) * 100f)}%";
    }

    private void UpdateSfxVolumeLabel(float value)
    {
        if (sfxVolumeValueLabel != null)
            sfxVolumeValueLabel.text = $"{Mathf.RoundToInt(SnapVolume(value) * 100f)}%";
    }

    private void UpdateUiVolumeLabel(float value)
    {
        if (uiVolumeValueLabel != null)
            uiVolumeValueLabel.text = $"{Mathf.RoundToInt(SnapVolume(value) * 100f)}%";
    }

    private static float SnapScentStep(float value)
    {
        return Mathf.Clamp(Mathf.Round(value * 10f) / 10f, 0.1f, 1.0f);
    }

    private static float SnapVolume(float value)
    {
        return Mathf.Clamp01(Mathf.Round(value * 100f) / 100f);
    }

    private void PlayTestBark()
    {
        SaveFromControls();
        AudioPlayer player = AudioPlayer.Instance;
        if (player == null && menuManager != null)
            player = menuManager.audioPlayer;
        if (player == null && Dir.Instance != null)
            player = Dir.Instance.audioPlayer;

        player?.PlayClip("Bark", 1f);
    }

    private void SetAndroidDisplayMode(bool fullscreen, bool save)
    {
        androidSafeAreaToggle?.SetIsOnWithoutNotify(!fullscreen);
        androidFullscreenToggle?.SetIsOnWithoutNotify(fullscreen);
        RefreshToggleVisual(androidSafeAreaToggle);
        RefreshToggleVisual(androidFullscreenToggle);

        if (save)
            SaveFromControls();
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

        UpdateSettingsIconSizes(buttonSize);
    }

    private void RegisterSettingsIcon(RectTransform rect, LayoutElement layout, Text shiftedText, float layoutWidthPadding, float absoluteLeft, float textGap)
    {
        if (rect == null)
            return;

        SettingsIconBinding binding = new SettingsIconBinding
        {
            rect = rect,
            layout = layout,
            shiftedText = shiftedText,
            layoutWidthPadding = Mathf.Max(0f, layoutWidthPadding),
            absoluteLeft = absoluteLeft,
            textGap = Mathf.Max(0f, textGap),
            textBaseOffsetMinX = shiftedText != null ? shiftedText.rectTransform.offsetMin.x : 0f
        };

        settingsIconBindings.Add(binding);
        ApplySettingsIconSize(binding, GetCurrentSettingsIconSize());
    }

    private void UpdateSettingsIconSizes(float value)
    {
        float iconSize = PersistentGameSettings.SnapButtonSize(value);
        for (int i = settingsIconBindings.Count - 1; i >= 0; i--)
        {
            SettingsIconBinding binding = settingsIconBindings[i];
            if (binding == null || binding.rect == null)
            {
                settingsIconBindings.RemoveAt(i);
                continue;
            }

            ApplySettingsIconSize(binding, iconSize);
        }
    }

    private void ApplySettingsIconSize(SettingsIconBinding binding, float iconSize)
    {
        binding.rect.sizeDelta = new Vector2(iconSize, iconSize);

        if (binding.absoluteLeft >= 0f)
        {
            Vector2 anchoredPosition = binding.rect.anchoredPosition;
            anchoredPosition.x = binding.absoluteLeft + iconSize * 0.5f;
            binding.rect.anchoredPosition = anchoredPosition;
        }

        if (binding.layout != null)
        {
            float layoutWidth = iconSize + binding.layoutWidthPadding;
            binding.layout.minWidth = layoutWidth;
            binding.layout.preferredWidth = layoutWidth;
            binding.layout.minHeight = iconSize;
            binding.layout.preferredHeight = iconSize;
            binding.layout.flexibleWidth = 0f;
        }

        if (binding.shiftedText != null)
        {
            RectTransform textRect = binding.shiftedText.rectTransform;
            Vector2 offsetMin = textRect.offsetMin;
            offsetMin.x = Mathf.Max(binding.textBaseOffsetMinX, binding.absoluteLeft + iconSize + binding.textGap);
            textRect.offsetMin = offsetMin;
        }
    }

    private float GetCurrentSettingsIconSize()
    {
        if (buttonSizeSlider != null)
            return PersistentGameSettings.SnapButtonSize(buttonSizeSlider.value);

        return PersistentGameSettings.SnapButtonSize(PersistentGameSettings.GetCurrentOrSaved().buttonSize);
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
            Sprite sprite = SpriteServer.SpriteLookup(entry.EntryId)
                ?? SpriteServer.SpriteSheetLookup($"DogEmojiSheet{entry.SheetId}", entry.SpriteIndex);
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
        RefreshToggleVisual(mistralToggle);
        RefreshToggleVisual(localQwenToggle);
        RefreshToggleVisual(localGemmaToggle);
        RefreshToggleVisual(localMistralToggle);
        RefreshToggleVisual(musicToggle);
        RefreshToggleVisual(sfxToggle);
        RefreshToggleVisual(uiToggle);
        RefreshToggleVisual(touchscreenJoystickToggle);
        RefreshToggleVisual(androidSafeAreaToggle);
        RefreshToggleVisual(androidFullscreenToggle);
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
