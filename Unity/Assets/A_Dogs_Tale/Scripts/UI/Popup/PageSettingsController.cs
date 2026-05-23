using UnityEngine;
using UnityEngine.UIElements;

public class PageSettingsController : MonoBehaviour
{
    private const float ScentStepIncrement = 0.1f;
    private const string GraphicsQualitySpriteResourcePath = "Sprites/GraphicsQualitySprites_A";

    private static readonly int[] GraphicsQualityLevels =
    {
        PersistentGameSettings.GraphicsLevelLow,
        PersistentGameSettings.GraphicsLevelMedium,
        PersistentGameSettings.GraphicsLevelHigh
    };

    private PopupController popupController;
    private Sprite[] graphicsQualitySprites;

    public void Bind(VisualElement root)
    {
        popupController ??= GetComponent<PopupController>();
        if (popupController == null)
            popupController = FindFirstObjectByType<PopupController>(FindObjectsInactive.Include);

        Toggle chatGptToggle = root.Q<Toggle>("ChatGptToggle");
        Toggle geminiToggle = root.Q<Toggle>("GeminiToggle");
        Toggle ollamaToggle = root.Q<Toggle>("OllamaToggle");
        Slider scentStepSlider = root.Q<Slider>("ScentSimulationTimeStepSlider");
        Label scentStepValue = root.Q<Label>("ScentSimulationTimeStepValue");
        Toggle musicToggle = root.Q<Toggle>("MusicEnableToggle");
        Slider musicVolumeSlider = root.Q<Slider>("MusicVolumeSlider");
        Label musicVolumeValue = root.Q<Label>("MusicVolumeValue");
        Toggle sfxToggle = root.Q<Toggle>("SfxEnableToggle");
        Slider sfxVolumeSlider = root.Q<Slider>("SfxVolumeSlider");
        Label sfxVolumeValue = root.Q<Label>("SfxVolumeValue");
        Toggle uiToggle = root.Q<Toggle>("UiEnableToggle");
        Slider uiVolumeSlider = root.Q<Slider>("UiVolumeSlider");
        Label uiVolumeValue = root.Q<Label>("UiVolumeValue");
        Button testBarkButton = root.Q<Button>("TestBarkButton");
        Button[] graphicsQualityButtons =
        {
            root.Q<Button>("GraphicsQuality1985Button"),
            root.Q<Button>("GraphicsQuality1990Button"),
            root.Q<Button>("GraphicsQuality1995Button")
        };
        Button okButton = root.Q<Button>("OkButton");

        PersistentGameSettings.Data settings = PersistentGameSettings.GetCurrentOrSaved();
        int selectedGraphicsLevel = PersistentGameSettings.SnapGraphicsLevel(settings.graphicsLevel);

        if (chatGptToggle != null)
            chatGptToggle.SetValueWithoutNotify(settings.chatGptEnabled);
        if (geminiToggle != null)
            geminiToggle.SetValueWithoutNotify(settings.geminiEnabled);
        if (ollamaToggle != null)
            ollamaToggle.SetValueWithoutNotify(settings.ollamaEnabled);
        if (scentStepSlider != null)
            scentStepSlider.SetValueWithoutNotify(settings.scentSimulationTimeStep);
        if (musicToggle != null)
            musicToggle.SetValueWithoutNotify(settings.musicEnabled);
        if (musicVolumeSlider != null)
            musicVolumeSlider.SetValueWithoutNotify(settings.musicVolume);
        if (sfxToggle != null)
            sfxToggle.SetValueWithoutNotify(settings.sfxEnabled);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(settings.sfxVolume);
        if (uiToggle != null)
            uiToggle.SetValueWithoutNotify(settings.uiEnabled);
        if (uiVolumeSlider != null)
            uiVolumeSlider.SetValueWithoutNotify(settings.uiVolume);

        UpdateScentStepLabel(scentStepValue, settings.scentSimulationTimeStep);
        UpdateVolumeLabel(musicVolumeValue, settings.musicVolume);
        UpdateVolumeLabel(sfxVolumeValue, settings.sfxVolume);
        UpdateVolumeLabel(uiVolumeValue, settings.uiVolume);
        RefreshGraphicsQualityButtons(graphicsQualityButtons, selectedGraphicsLevel);

        void SaveCurrentValues()
        {
            PersistentGameSettings.Data current = PersistentGameSettings.GetCurrentOrSaved();
            PersistentGameSettings.SaveAndApply(new PersistentGameSettings.Data
            {
                mapType = current.mapType,
                chatGptEnabled = chatGptToggle?.value ?? current.chatGptEnabled,
                geminiEnabled = geminiToggle?.value ?? current.geminiEnabled,
                ollamaEnabled = ollamaToggle?.value ?? current.ollamaEnabled,
                musicEnabled = musicToggle?.value ?? current.musicEnabled,
                musicVolume = SnapVolume(musicVolumeSlider?.value ?? current.musicVolume),
                sfxEnabled = sfxToggle?.value ?? current.sfxEnabled,
                sfxVolume = SnapVolume(sfxVolumeSlider?.value ?? current.sfxVolume),
                uiEnabled = uiToggle?.value ?? current.uiEnabled,
                uiVolume = SnapVolume(uiVolumeSlider?.value ?? current.uiVolume),
                touchscreenJoystickVisible = current.touchscreenJoystickVisible,
                graphicsLevel = selectedGraphicsLevel,
                scentSimulationTimeStep = SnapScentStep(scentStepSlider?.value ?? current.scentSimulationTimeStep),
                buttonSize = current.buttonSize,
                androidFullscreenEnabled = current.androidFullscreenEnabled
            });
        }

        if (chatGptToggle != null)
            chatGptToggle.RegisterValueChangedCallback(_ => SaveCurrentValues());

        if (geminiToggle != null)
            geminiToggle.RegisterValueChangedCallback(_ => SaveCurrentValues());

        if (ollamaToggle != null)
            ollamaToggle.RegisterValueChangedCallback(_ => SaveCurrentValues());

        if (musicToggle != null)
            musicToggle.RegisterValueChangedCallback(_ => SaveCurrentValues());

        if (sfxToggle != null)
            sfxToggle.RegisterValueChangedCallback(_ => SaveCurrentValues());

        if (uiToggle != null)
            uiToggle.RegisterValueChangedCallback(_ => SaveCurrentValues());

        if (scentStepSlider != null)
        {
            scentStepSlider.RegisterValueChangedCallback(evt =>
            {
                float snappedValue = SnapScentStep(evt.newValue);
                if (!Mathf.Approximately(snappedValue, scentStepSlider.value))
                    scentStepSlider.SetValueWithoutNotify(snappedValue);

                UpdateScentStepLabel(scentStepValue, snappedValue);
                SaveCurrentValues();
            });
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.RegisterValueChangedCallback(evt =>
            {
                float snappedValue = SnapVolume(evt.newValue);
                if (!Mathf.Approximately(snappedValue, musicVolumeSlider.value))
                    musicVolumeSlider.SetValueWithoutNotify(snappedValue);

                UpdateVolumeLabel(musicVolumeValue, snappedValue);
                SaveCurrentValues();
            });
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.RegisterValueChangedCallback(evt =>
            {
                float snappedValue = SnapVolume(evt.newValue);
                if (!Mathf.Approximately(snappedValue, sfxVolumeSlider.value))
                    sfxVolumeSlider.SetValueWithoutNotify(snappedValue);

                UpdateVolumeLabel(sfxVolumeValue, snappedValue);
                SaveCurrentValues();
            });
        }

        if (uiVolumeSlider != null)
        {
            uiVolumeSlider.RegisterValueChangedCallback(evt =>
            {
                float snappedValue = SnapVolume(evt.newValue);
                if (!Mathf.Approximately(snappedValue, uiVolumeSlider.value))
                    uiVolumeSlider.SetValueWithoutNotify(snappedValue);

                UpdateVolumeLabel(uiVolumeValue, snappedValue);
                SaveCurrentValues();
            });
        }

        if (testBarkButton != null)
        {
            testBarkButton.clicked += () =>
            {
                AudioPlayer.PlayUiButtonClick();
                SaveCurrentValues();
                AudioPlayer player = AudioPlayer.Instance ?? Dir.Instance?.audioPlayer;
                player?.PlayClip("Bark", 1f);
            };
        }

        for (int i = 0; i < graphicsQualityButtons.Length; i++)
        {
            int capturedIndex = i;
            Button button = graphicsQualityButtons[i];
            if (button == null)
                continue;

            button.clicked += () =>
            {
                AudioPlayer.PlayUiButtonClick();
                selectedGraphicsLevel = GraphicsQualityLevels[capturedIndex];
                RefreshGraphicsQualityButtons(graphicsQualityButtons, selectedGraphicsLevel);
                SaveCurrentValues();
            };
        }

        if (okButton != null)
            okButton.clicked += () => { AudioPlayer.PlayUiButtonClick(); popupController?.Close(); };
    }

    private static float SnapScentStep(float value)
    {
        return Mathf.Clamp(
            Mathf.Round(value / ScentStepIncrement) * ScentStepIncrement,
            0.1f,
            1.0f);
    }

    private static void UpdateScentStepLabel(Label label, float value)
    {
        if (label != null)
            label.text = $"{SnapScentStep(value):0.0}s";
    }

    private static float SnapVolume(float value)
    {
        return Mathf.Clamp01(Mathf.Round(value * 100f) / 100f);
    }

    private static void UpdateVolumeLabel(Label label, float value)
    {
        if (label != null)
            label.text = $"{Mathf.RoundToInt(SnapVolume(value) * 100f)}%";
    }

    private void RefreshGraphicsQualityButtons(Button[] buttons, int selectedGraphicsLevel)
    {
        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            int graphicsLevel = GraphicsQualityLevels[i];
            bool selected = graphicsLevel == selectedGraphicsLevel;
            Sprite sprite = GetGraphicsQualitySprite(i);
            button.text = sprite != null ? string.Empty : PersistentGameSettings.GetGraphicsLevelLabel(graphicsLevel);
            button.tooltip = PersistentGameSettings.GetGraphicsLevelLabel(graphicsLevel);
            button.EnableInClassList("selected", selected);
            button.style.opacity = selected ? 1f : 0.52f;
            button.style.height = 96f;
            button.style.flexGrow = 1f;

            if (sprite != null)
                button.style.backgroundImage = new StyleBackground(sprite);
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

    private void EnsureGraphicsQualitySpritesLoaded()
    {
        if (graphicsQualitySprites != null)
            return;

        graphicsQualitySprites = Resources.LoadAll<Sprite>(GraphicsQualitySpriteResourcePath);
        if (graphicsQualitySprites == null || graphicsQualitySprites.Length == 0)
            Debug.LogWarning($"[PageSettingsController] Could not load graphics quality sprites from Resources/{GraphicsQualitySpriteResourcePath}.", this);
    }
}
