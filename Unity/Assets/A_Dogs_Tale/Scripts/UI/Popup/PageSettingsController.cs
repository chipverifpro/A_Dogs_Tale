using UnityEngine;
using UnityEngine.UIElements;

public class PageSettingsController : MonoBehaviour
{
    private const float ScentStepIncrement = 0.1f;

    private PopupController popupController;

    public void Bind(VisualElement root)
    {
        popupController ??= GetComponent<PopupController>();
        if (popupController == null)
            popupController = FindFirstObjectByType<PopupController>(FindObjectsInactive.Include);

        Toggle chatGptToggle = root.Q<Toggle>("ChatGptToggle");
        Toggle geminiToggle = root.Q<Toggle>("GeminiToggle");
        Toggle ollamaToggle = root.Q<Toggle>("OllamaToggle");
        Toggle wallpaperToggle = root.Q<Toggle>("WallpaperToggle");
        Slider scentStepSlider = root.Q<Slider>("ScentSimulationTimeStepSlider");
        Label scentStepValue = root.Q<Label>("ScentSimulationTimeStepValue");
        Button okButton = root.Q<Button>("OkButton");

        PersistentGameSettings.Data settings = PersistentGameSettings.GetCurrentOrSaved();

        if (chatGptToggle != null)
            chatGptToggle.SetValueWithoutNotify(settings.chatGptEnabled);
        if (geminiToggle != null)
            geminiToggle.SetValueWithoutNotify(settings.geminiEnabled);
        if (ollamaToggle != null)
            ollamaToggle.SetValueWithoutNotify(settings.ollamaEnabled);
        if (wallpaperToggle != null)
            wallpaperToggle.SetValueWithoutNotify(settings.wallpaperEnabled);
        if (scentStepSlider != null)
            scentStepSlider.SetValueWithoutNotify(settings.scentSimulationTimeStep);

        UpdateScentStepLabel(scentStepValue, settings.scentSimulationTimeStep);

        void SaveCurrentValues()
        {
            PersistentGameSettings.SaveAndApply(new PersistentGameSettings.Data
            {
                chatGptEnabled = chatGptToggle?.value ?? settings.chatGptEnabled,
                geminiEnabled = geminiToggle?.value ?? settings.geminiEnabled,
                ollamaEnabled = ollamaToggle?.value ?? settings.ollamaEnabled,
                wallpaperEnabled = wallpaperToggle?.value ?? settings.wallpaperEnabled,
                scentSimulationTimeStep = SnapScentStep(scentStepSlider?.value ?? settings.scentSimulationTimeStep)
            });
        }

        if (chatGptToggle != null)
            chatGptToggle.RegisterValueChangedCallback(_ => SaveCurrentValues());

        if (geminiToggle != null)
            geminiToggle.RegisterValueChangedCallback(_ => SaveCurrentValues());

        if (ollamaToggle != null)
            ollamaToggle.RegisterValueChangedCallback(_ => SaveCurrentValues());

        if (wallpaperToggle != null)
            wallpaperToggle.RegisterValueChangedCallback(_ => SaveCurrentValues());

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

        if (okButton != null)
            okButton.clicked += () => popupController?.Close();
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
}
