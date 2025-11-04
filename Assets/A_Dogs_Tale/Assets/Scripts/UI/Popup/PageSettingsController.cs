using UnityEngine;
using UnityEngine.UIElements;

public class PageSettingsController : MonoBehaviour
{
    public void Bind(VisualElement root)
    {
        var sfx   = root.Q<Slider>("SfxSlider");
        var music = root.Q<Slider>("MusicSlider");
        var full  = root.Q<Toggle>("Fullscreen");
        var qual  = root.Q<DropdownField>("Quality");

        if (sfx!=null)   sfx.RegisterValueChangedCallback(v => Debug.Log("SFX: " + v.newValue));
        if (music!=null) music.RegisterValueChangedCallback(v => Debug.Log("Music: " + v.newValue));
        if (full!=null)  full.RegisterValueChangedCallback(v => Screen.fullScreen = v.newValue);
        if (qual!=null)
        {
            qual.choices = new() { "Low", "Medium", "High", "Ultra" };
            qual.value = qual.choices[Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, qual.choices.Count-1)];
            qual.RegisterValueChangedCallback(v => QualitySettings.SetQualityLevel(qual.choices.IndexOf(v.newValue)));
        }
    }
}