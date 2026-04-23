using UnityEngine;
using UnityEngine.UI;

public class MenuSettingsDialog : MonoBehaviour
{
    [SerializeField] private MenuManager menuManager;

    private GameObject dialogRoot;
    private RectTransform panelRect;
    private Toggle chatGptToggle;
    private Toggle geminiToggle;
    private Toggle ollamaToggle;
    private Toggle wallpaperToggle;
    private Slider scentStepSlider;
    private Text scentStepValueLabel;
    private Font runtimeFont;

    public void Initialize(MenuManager owner)
    {
        menuManager = owner;
    }

    private void Awake()
    {
        MenuSettingsDialog[] dialogs = GetComponents<MenuSettingsDialog>();
        if (dialogs.Length > 1)
        {
            for (int i = 0; i < dialogs.Length; i++)
            {
                if (dialogs[i] != this)
                    Destroy(dialogs[i]);
            }
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

    private void EnsureBuilt()
    {
        if (TryBindExistingDialog())
            return;

        if (dialogRoot != null)
            return;

        Canvas canvas = ResolveMenuCanvas();
        if (canvas == null)
        {
            Debug.LogError("[MenuSettingsDialog] Could not find MenuCanvas.", this);
            return;
        }

        DefaultControls.Resources resources = new DefaultControls.Resources();

        dialogRoot = new GameObject("MenuSettingsDialog", typeof(RectTransform), typeof(Image));
        dialogRoot.transform.SetParent(canvas.transform, false);
        dialogRoot.layer = canvas.gameObject.layer;
        dialogRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform dialogRootRect = dialogRoot.GetComponent<RectTransform>();
        dialogRootRect.anchorMin = Vector2.zero;
        dialogRootRect.anchorMax = Vector2.one;
        dialogRootRect.offsetMin = Vector2.zero;
        dialogRootRect.offsetMax = Vector2.zero;

        GameObject panel = DefaultControls.CreatePanel(resources);
        panel.name = "Panel";
        panel.transform.SetParent(dialogRoot.transform, false);
        panel.layer = canvas.gameObject.layer;

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
            panelImage.color = new Color(0.22f, 0.24f, 0.30f, 0.98f);

        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 16, 16);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreateLabel(panel.transform, "Settings", 12, FontStyle.Bold, TextAnchor.MiddleCenter, 14f);

        CreateLabel(panel.transform, "AI Models", 16, FontStyle.Bold, TextAnchor.MiddleLeft, 22f);
        chatGptToggle = CreateToggle(panel.transform, resources, "ChatGPT", "ChatGptToggle");
        geminiToggle = CreateToggle(panel.transform, resources, "Gemini", "GeminiToggle");
        ollamaToggle = CreateToggle(panel.transform, resources, "Ollama", "OllamaToggle");

        CreateLabel(panel.transform, "Scent Simulation Time Step", 16, FontStyle.Bold, TextAnchor.MiddleLeft, 22f);

        GameObject sliderRow = new GameObject("ScentStepRow", typeof(RectTransform));
        sliderRow.transform.SetParent(panel.transform, false);
        sliderRow.layer = canvas.gameObject.layer;
        HorizontalLayoutGroup sliderLayout = sliderRow.AddComponent<HorizontalLayoutGroup>();
        sliderLayout.spacing = 8f;
        sliderLayout.childAlignment = TextAnchor.MiddleLeft;
        sliderLayout.childControlHeight = true;
        sliderLayout.childControlWidth = false;
        sliderLayout.childForceExpandHeight = false;
        sliderLayout.childForceExpandWidth = false;

        LayoutElement sliderRowLayout = sliderRow.AddComponent<LayoutElement>();
        sliderRowLayout.preferredHeight = 26f;

        GameObject sliderObject = DefaultControls.CreateSlider(resources);
        sliderObject.name = "ScentStepSlider";
        sliderObject.transform.SetParent(sliderRow.transform, false);
        sliderObject.layer = canvas.gameObject.layer;
        scentStepSlider = sliderObject.GetComponent<Slider>();
        scentStepSlider.minValue = 0.1f;
        scentStepSlider.maxValue = 1.0f;
        scentStepSlider.wholeNumbers = false;

        LayoutElement sliderLayoutElement = sliderObject.AddComponent<LayoutElement>();
        sliderLayoutElement.preferredWidth = 320f;
        sliderLayoutElement.preferredHeight = 20f;

        scentStepValueLabel = CreateLabel(sliderRow.transform, "0.1s", 15, FontStyle.Normal, TextAnchor.MiddleRight, 22f, "ScentStepValueLabel");
        LayoutElement valueLayout = scentStepValueLabel.gameObject.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 48f;

        wallpaperToggle = CreateToggle(panel.transform, resources, "Wallpaper on wall tiles", "WallpaperToggle");

     //   GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
     //   spacer.transform.SetParent(panel.transform, false);
     //   spacer.layer = canvas.gameObject.layer;
     //   spacer.GetComponent<LayoutElement>().flexibleHeight = 1f;

        GameObject buttonRow = new GameObject("ButtonRow", typeof(RectTransform));
        buttonRow.transform.SetParent(panel.transform, false);
        buttonRow.layer = canvas.gameObject.layer;
        HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonLayout.childControlHeight = true;
        buttonLayout.childControlWidth = false;
        buttonLayout.childForceExpandHeight = false;
        buttonLayout.childForceExpandWidth = false;

        LayoutElement buttonRowElement = buttonRow.AddComponent<LayoutElement>();
        buttonRowElement.preferredHeight = 34f;

        GameObject okButtonObject = DefaultControls.CreateButton(resources);
        okButtonObject.name = "OkButton";
        okButtonObject.transform.SetParent(buttonRow.transform, false);
        okButtonObject.layer = canvas.gameObject.layer;
        Text okButtonText = okButtonObject.GetComponentInChildren<Text>();
        if (okButtonText != null)
        {
            okButtonText.font = GetRuntimeFont();
            okButtonText.text = "OK";
            okButtonText.color = Color.black;
        }
        LayoutElement okLayout = okButtonObject.AddComponent<LayoutElement>();
        okLayout.preferredWidth = 110f;
        okLayout.preferredHeight = 30f;

        okButtonObject.GetComponent<Button>().onClick.AddListener(Close);

        ConfigureToggleVisual(chatGptToggle);
        ConfigureToggleVisual(geminiToggle);
        ConfigureToggleVisual(ollamaToggle);
        ConfigureToggleVisual(wallpaperToggle);

        chatGptToggle.onValueChanged.AddListener(_ => SaveFromControls());
        geminiToggle.onValueChanged.AddListener(_ => SaveFromControls());
        ollamaToggle.onValueChanged.AddListener(_ => SaveFromControls());
        wallpaperToggle.onValueChanged.AddListener(_ => SaveFromControls());
        scentStepSlider.onValueChanged.AddListener(OnScentStepChanged);

        dialogRoot.SetActive(false);
    }

    private bool TryBindExistingDialog()
    {
        if (dialogRoot != null)
            return true;

        Canvas canvas = ResolveMenuCanvas();
        if (canvas == null)
            return false;

        Transform existingRoot = canvas.transform.Find("MenuSettingsDialog");
        if (existingRoot == null)
            return false;

        dialogRoot = existingRoot.gameObject;
        panelRect = existingRoot.Find("Panel") as RectTransform;

        Toggle[] toggles = existingRoot.GetComponentsInChildren<Toggle>(includeInactive: true);
        for (int i = 0; i < toggles.Length; i++)
        {
            switch (toggles[i].name)
            {
                case "ChatGptToggle":
                    chatGptToggle = toggles[i];
                    break;
                case "GeminiToggle":
                    geminiToggle = toggles[i];
                    break;
                case "OllamaToggle":
                    ollamaToggle = toggles[i];
                    break;
                case "WallpaperToggle":
                    wallpaperToggle = toggles[i];
                    break;
            }
        }

        Slider[] sliders = existingRoot.GetComponentsInChildren<Slider>(includeInactive: true);
        for (int i = 0; i < sliders.Length; i++)
        {
            if (sliders[i].name == "ScentStepSlider")
            {
                scentStepSlider = sliders[i];
                break;
            }
        }

        Text[] texts = existingRoot.GetComponentsInChildren<Text>(includeInactive: true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == "ScentStepValueLabel")
            {
                scentStepValueLabel = texts[i];
                break;
            }
        }

        bool fullyBound = dialogRoot != null
            && panelRect != null
            && chatGptToggle != null
            && geminiToggle != null
            && ollamaToggle != null
            && wallpaperToggle != null
            && scentStepSlider != null
            && scentStepValueLabel != null;

        if (!fullyBound)
        {
            Destroy(existingRoot.gameObject);
            dialogRoot = null;
            panelRect = null;
            chatGptToggle = null;
            geminiToggle = null;
            ollamaToggle = null;
            wallpaperToggle = null;
            scentStepSlider = null;
            scentStepValueLabel = null;
        }
        else
        {
            ConfigureToggleVisual(chatGptToggle);
            ConfigureToggleVisual(geminiToggle);
            ConfigureToggleVisual(ollamaToggle);
            ConfigureToggleVisual(wallpaperToggle);
        }

        return fullyBound;
    }

    private void RefreshPanelSize()
    {
        if (dialogRoot == null || panelRect == null)
            return;

        RectTransform rootRect = dialogRoot.GetComponent<RectTransform>();
        Rect rect = rootRect.rect;
        float width = Mathf.Clamp(rect.width * 0.82f, 400f, 560f);
        float height = Mathf.Clamp(rect.height * 0.82f, 250f, 340f);
        panelRect.sizeDelta = new Vector2(width, height);
    }

    private void OnScentStepChanged(float value)
    {
        float snappedValue = SnapScentStep(value);
        if (!Mathf.Approximately(snappedValue, scentStepSlider.value))
            scentStepSlider.SetValueWithoutNotify(snappedValue);

        UpdateScentStepLabel(snappedValue);
        SaveFromControls();
    }

    private void LoadCurrentValues()
    {
        PersistentGameSettings.Data settings = PersistentGameSettings.GetCurrentOrSaved();

        chatGptToggle.SetIsOnWithoutNotify(settings.chatGptEnabled);
        geminiToggle.SetIsOnWithoutNotify(settings.geminiEnabled);
        ollamaToggle.SetIsOnWithoutNotify(settings.ollamaEnabled);
        wallpaperToggle.SetIsOnWithoutNotify(settings.wallpaperEnabled);

        float snappedValue = SnapScentStep(settings.scentSimulationTimeStep);
        scentStepSlider.SetValueWithoutNotify(snappedValue);
        UpdateScentStepLabel(snappedValue);
        RefreshToggleVisuals();
    }

    private void SaveFromControls()
    {
        PersistentGameSettings.SaveAndApply(new PersistentGameSettings.Data
        {
            chatGptEnabled = chatGptToggle.isOn,
            geminiEnabled = geminiToggle.isOn,
            ollamaEnabled = ollamaToggle.isOn,
            wallpaperEnabled = wallpaperToggle.isOn,
            scentSimulationTimeStep = SnapScentStep(scentStepSlider.value)
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

        return FindFirstObjectByType<Canvas>();
    }

    private Toggle CreateToggle(Transform parent, DefaultControls.Resources resources, string labelText, string objectName)
    {
        GameObject toggleObject = DefaultControls.CreateToggle(resources);
        toggleObject.name = objectName;
        toggleObject.transform.SetParent(parent, false);
        Text toggleText = toggleObject.GetComponentInChildren<Text>();
        if (toggleText != null)
        {
            toggleText.font = GetRuntimeFont();
            toggleText.text = labelText;
            toggleText.color = Color.white;
        }

        Image background = toggleObject.transform.Find("Background")?.GetComponent<Image>();
        if (background != null)
            background.color = new Color(0.9f, 0.9f, 0.9f, 1f);

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        ConfigureToggleVisual(toggle);

        LayoutElement layout = toggleObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 22f;

        return toggle;
    }

    private Text CreateLabel(Transform parent, string textValue, int fontSize, FontStyle fontStyle, TextAnchor alignment, float height, string objectName = null)
    {
        string resolvedName = string.IsNullOrWhiteSpace(objectName)
            ? textValue.Replace(" ", string.Empty) + "Label"
            : objectName;
        GameObject labelObject = new GameObject(resolvedName, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        Text label = labelObject.GetComponent<Text>();
        label.text = textValue;
        label.font = GetRuntimeFont();
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.color = Color.white;

        LayoutElement layout = labelObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;

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
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
        toggle.targetGraphic = backgroundImage;
        SetToggleGraphicVisible(toggle);
    }

    private void RefreshToggleVisuals()
    {
        SetToggleGraphicVisible(chatGptToggle);
        SetToggleGraphicVisible(geminiToggle);
        SetToggleGraphicVisible(ollamaToggle);
        SetToggleGraphicVisible(wallpaperToggle);
    }

    private static void SetToggleGraphicVisible(Toggle toggle)
    {
        if (toggle?.graphic == null)
            return;

        toggle.graphic.canvasRenderer.SetAlpha(toggle.isOn ? 1f : 0f);
    }
}
