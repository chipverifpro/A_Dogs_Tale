#define TMP_PRESENT

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif

public class DungeonBuildSettingsUI : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("The ScriptableObject instance you actually use at runtime.")]
    public DungeonSettings currentSettings;

    [Tooltip("The Main DungeonGenerator MonoBehavior script.")]
    public DungeonGenerator dungeonGenerator;

    [Tooltip("Folder under Application.persistentDataPath where JSON presets are stored.")]
    public string subFolder = "DungeonConfigs";

    [Header("UI (assign UGUI OR TMP controls)")]
    // UGUI
    public Dropdown uguiDropdown;
    public InputField uguiNameInput;

    // TMP
#if TMP_PRESENT || UNITY_TEXTMESHPRO
    public TMP_Dropdown BuildPresetsList;
    public TMP_InputField NewBuildPresetName;
#endif

    [Header("Optional")]
#if TMP_PRESENT || UNITY_TEXTMESHPRO
    public TMP_Text StatusMessages;
#else
    public Text StatusMessages;
#endif

    // Internal cache of preset names (no extension)
    private List<string> _names = new List<string>();

    void Start()
    {
        RefreshDropdown();
        ShowStatus($"Preset folder: {DungeonBuildSettingsJson.GetConfigsFolder(subFolder)}");
    }

    // --- Public API you can hook to buttons ---

    // Button: Save (use text from the input field)
    public void SaveFromInput()
    {
        string name = ReadNameField();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowStatus("Enter a preset name first.");
            return;
        }

        string path = currentSettings.SaveToJsonFile(name, subFolder);
        ShowStatus($"Saved: {System.IO.Path.GetFileName(path)}");
        RefreshDropdown(selectName: name);
    }

    // Button: Load (use current dropdown selection)
    public void LoadSelected()
    {
        string name = GetSelectedName();
        if (string.IsNullOrEmpty(name))
        {
            ShowStatus("Pick a preset to load.");
            return;
        }

        LoadMapSettingsByName(name);
    }

    public void LoadMapSettingsByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            ShowStatus("Preset name is empty.");
            return;
        }

        bool ok = currentSettings.LoadFromJsonFileOverwrite(name, subFolder);
        ShowStatus(ok ? $"Loaded: {name}" : $"Load failed: {name}");

        //dungeonGenerator.StopAllCoroutines();
        StartCoroutine(dungeonGenerator.RegenerateDungeon(tm: null));
    }

    // Button: Delete (remove selected preset file)
    public void DeleteSelected()
    {
        string name = GetSelectedName();
        if (string.IsNullOrEmpty(name))
        {
            ShowStatus("Pick a preset to delete.");
            return;
        }

        string folder = DungeonBuildSettingsJson.GetConfigsFolder(subFolder);
        string path = System.IO.Path.Combine(folder, name + ".json");
        if (!System.IO.File.Exists(path))
        {
            ShowStatus("File not found.");
            return;
        }

        try
        {
            System.IO.File.Delete(path);
            ShowStatus($"Deleted: {name}.json");
            RefreshDropdown();
        }
        catch (Exception ex)
        {
            ShowStatus($"Delete failed: {ex.Message}");
        }
    }

    // Button: Refresh list (if files changed externally)
    public void RefreshListButton() => RefreshDropdown();

    // Dropdown event: on value change (optional – loads immediately)
    public void OnDropdownChanged(int _)
    {
        // If you want auto-load on selection, uncomment:
        LoadSelected();
    }

#if UNITY_EDITOR
    // Button: Reveal folder in Finder/Explorer (Editor only)
    public void RevealFolder()
    {
        string folder = DungeonBuildSettingsJson.GetConfigsFolder(subFolder);
        UnityEditor.EditorUtility.RevealInFinder(folder);
    }
#endif

    // --- Helpers ---

    private void RefreshDropdown(string selectName = null)
    {
        _names.Clear();
        _names.AddRange(DungeonBuildSettingsJson.ListConfigFiles(subFolder));

        // UGUI dropdown
        if (uguiDropdown)
        {
            uguiDropdown.ClearOptions();
            uguiDropdown.AddOptions(_names);
            uguiDropdown.value = IndexOfOrZero(_names, selectName);
            uguiDropdown.RefreshShownValue();
        }

        // TMP dropdown
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (BuildPresetsList)
        {
            BuildPresetsList.ClearOptions();
            BuildPresetsList.AddOptions(_names);
            BuildPresetsList.value = IndexOfOrZero(_names, selectName);
            BuildPresetsList.RefreshShownValue();
        }
#endif
    }

    private string GetSelectedName()
    {
        if (_names.Count == 0) return null;

        // Prefer TMP if assigned
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (BuildPresetsList)
        {
            int idx = Mathf.Clamp(BuildPresetsList.value, 0, _names.Count - 1);
            return _names[idx];
        }
#endif
        if (uguiDropdown)
        {
            int idx = Mathf.Clamp(uguiDropdown.value, 0, _names.Count - 1);
            return _names[idx];
        }
        return null;
    }

    private string ReadNameField()
    {
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (NewBuildPresetName) return NewBuildPresetName.text;
#endif
        if (uguiNameInput) return uguiNameInput.text;
        return null;
    }

    private static int IndexOfOrZero(List<string> list, string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        int i = list.FindIndex(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 ? i : 0;
    }

    private void ShowStatus(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (StatusMessages == null)
        {
            // If you prefer TMP_Text, change the field type and this cast.
        }
#endif
        if (StatusMessages) StatusMessages.text = msg;
        else Debug.Log(msg);
    }
}