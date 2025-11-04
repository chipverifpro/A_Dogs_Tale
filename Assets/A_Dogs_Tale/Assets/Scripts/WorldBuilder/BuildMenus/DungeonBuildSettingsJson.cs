using System;
using System.IO;
using UnityEngine;

public static class DungeonBuildSettingsJson
{
    public static string GetConfigsFolder(string subFolder = "DungeonConfigs")
    {
        string path = Path.Combine(Application.persistentDataPath, subFolder);
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        return path;
    }

    public static string SaveToJsonFile(this DungeonSettings s, string fileNameNoExt, string subFolder = "DungeonConfigs")
    {
        if (s == null) throw new ArgumentNullException(nameof(s));
        if (string.IsNullOrWhiteSpace(fileNameNoExt)) fileNameNoExt = "DungeonSettings";

        string json = JsonUtility.ToJson(s, true); // serialize the ScriptableObject itself

        string folder = GetConfigsFolder(subFolder);
        string safe = MakeSafeFileName(fileNameNoExt);
        string path = Path.Combine(folder, safe + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    public static bool LoadFromJsonFileOverwrite(this DungeonSettings target, string filePathOrName, string subFolder = "DungeonConfigs")
    {
        if (target == null) { Debug.LogWarning("Load target is null."); return false; }

        try
        {
            string path = filePathOrName;
            if (!Path.IsPathRooted(path))
                path = Path.Combine(GetConfigsFolder(subFolder), path);
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                path += ".json";

            if (!File.Exists(path)) { Debug.LogWarning($"Config not found: {path}"); return false; }

            string json = File.ReadAllText(path);

            // Overwrite fields on the existing ScriptableObject instance
            JsonUtility.FromJsonOverwrite(json, target);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(target);
#endif
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"LoadFromJsonFileOverwrite error: {ex.Message}");
            return false;
        }
    }

    public static string[] ListConfigFiles(string subFolder = "DungeonConfigs")
    {
        string folder = GetConfigsFolder(subFolder);
        if (!Directory.Exists(folder)) return Array.Empty<string>();
        var files = Directory.GetFiles(folder, "*.json");
        for (int i = 0; i < files.Length; i++)
            files[i] = Path.GetFileNameWithoutExtension(files[i]);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        return files;
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Trim();
    }
}

// Example usage:
//
// Save current settings
// settings.SaveToJsonFile("MyPreset");
//
// Load (overwrite current instance fields)
// settings.LoadFromJsonFileOverwrite("MyPreset");