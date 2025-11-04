using UnityEngine;
using UnityEngine.UI;
#if UNITY_TEXTMESHPRO
using TMPro;
#endif

public class DungeonBuildSettingsUIBinder : MonoBehaviour
{
    public DungeonBuildSettingsUI ui;
    public Button saveBtn, loadBtn, deleteBtn, refreshBtn;
#if UNITY_EDITOR
    public Button revealBtn;
#endif
    public Dropdown uguiDropdown;          // or:
#if UNITY_TEXTMESHPRO
    public TMP_Dropdown tmpDropdown;
#endif

    void Awake()
    {
        if (saveBtn)   saveBtn.onClick.AddListener(ui.SaveFromInput);
        if (loadBtn)   loadBtn.onClick.AddListener(ui.LoadSelected);
        if (deleteBtn) deleteBtn.onClick.AddListener(ui.DeleteSelected);
        if (refreshBtn)refreshBtn.onClick.AddListener(ui.RefreshListButton);
#if UNITY_EDITOR
        if (revealBtn) revealBtn.onClick.AddListener(ui.RevealFolder);
#endif

        if (uguiDropdown) uguiDropdown.onValueChanged.AddListener(ui.OnDropdownChanged);
#if UNITY_TEXTMESHPRO
        if (tmpDropdown)  tmpDropdown.onValueChanged.AddListener(ui.OnDropdownChanged);
#endif
    }
}