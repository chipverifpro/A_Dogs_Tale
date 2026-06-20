#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DogGame.Modules;

public static class WorldObjectPrefabTools
{
    [MenuItem("Tools/DogGame/Setup WorldObject + Modules On Selected Prefabs")]
    public static void SetupWorldObjectsOnSelectedPrefabs()
    {
        var selected = Selection.objects;

        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("WorldObjectPrefabTools: No assets selected. Select one or more prefabs in the Project window.");
            return;
        }

        int processed = 0;
        int modified  = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var obj in selected)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                // Only process prefab assets
                var prefabType = PrefabUtility.GetPrefabAssetType(obj);
                if (prefabType == PrefabAssetType.NotAPrefab)
                    continue;

                if (!path.EndsWith(".prefab"))
                    continue;

                processed++;

                // Load the prefab contents into a temporary scene
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                    continue;

                bool changed = SetupOnePrefab(root);
                changed |= EnsureSavePrefabId(root, path);

                if (changed)
                {
                    modified++;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }

                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"WorldObjectPrefabTools: Processed {processed} prefab(s), modified {modified}.");
    }

    [MenuItem("Tools/DogGame/Save Prefabs/Fill SavePrefabId On Selected Prefabs")]
    public static void FillSavePrefabIdOnSelectedPrefabs()
    {
        FillSavePrefabIds(GetPrefabPathsFromSelection(includeFolders: false));
    }

    [MenuItem("Tools/DogGame/Save Prefabs/Repair SavePrefabId On Selected Prefabs Or Folders")]
    public static void RepairSavePrefabIdOnSelectedPrefabsOrFolders()
    {
        FillSavePrefabIds(GetPrefabPathsFromSelection(includeFolders: true));
    }

    [MenuItem("Tools/DogGame/Save Prefabs/Repair SavePrefabId On Resources Prefabs")]
    public static void RepairSavePrefabIdOnResourcesPrefabs()
    {
        FillSavePrefabIds(GetPrefabPathsInFolders("Assets/A_Dogs_Tale/Resources/Prefabs"));
    }

    [MenuItem("Tools/DogGame/Save Prefabs/Fill SavePrefabId On All Project Prefabs")]
    public static void FillSavePrefabIdOnAllProjectPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        FillSavePrefabIds(prefabGuids.Select(AssetDatabase.GUIDToAssetPath));
    }

    private static string[] GetPrefabPathsFromSelection(bool includeFolders)
    {
        Object[] selected = Selection.objects;
        if (selected == null || selected.Length == 0)
            return System.Array.Empty<string>();

        List<string> paths = new();
        foreach (Object selectedObject in selected)
        {
            string path = AssetDatabase.GetAssetPath(selectedObject);
            if (string.IsNullOrWhiteSpace(path))
                continue;

            if (includeFolders && AssetDatabase.IsValidFolder(path))
            {
                paths.AddRange(GetPrefabPathsInFolders(path));
                continue;
            }

            if (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                paths.Add(path);
        }

        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct()
            .ToArray();
    }

    private static string[] GetPrefabPathsInFolders(params string[] folderPaths)
    {
        if (folderPaths == null || folderPaths.Length == 0)
            return System.Array.Empty<string>();

        string[] validFolders = folderPaths
            .Where(path => !string.IsNullOrWhiteSpace(path) && AssetDatabase.IsValidFolder(path))
            .Distinct()
            .ToArray();

        if (validFolders.Length == 0)
            return System.Array.Empty<string>();

        return AssetDatabase.FindAssets("t:Prefab", validFolders)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrWhiteSpace(path) && path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();
    }

    private static void FillSavePrefabIds(IEnumerable<string> prefabPaths)
    {
        string[] paths = prefabPaths?
            .Where(path => !string.IsNullOrWhiteSpace(path) && path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToArray() ?? System.Array.Empty<string>();

        if (paths.Length == 0)
        {
            Debug.LogWarning("WorldObjectPrefabTools: No prefabs selected.");
            return;
        }

        int processed = 0;
        int modified = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string path in paths)
            {
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabAsset == null)
                    continue;

                if (PrefabUtility.GetPrefabAssetType(prefabAsset) == PrefabAssetType.NotAPrefab)
                    continue;

                processed++;

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                    continue;

                bool changed = EnsureSavePrefabId(root, path);
                if (changed)
                {
                    modified++;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }

                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"WorldObjectPrefabTools: SavePrefabId processed {processed} prefab(s), modified {modified}.");
    }

    /// <summary>
    /// Ensure WorldObject + key modules exist on the root, and initialize PlacementModule defaults.
    /// Returns true if anything was changed.
    /// </summary>
    private static bool SetupOnePrefab(GameObject root)
    {
        bool changed = false;

        // --- Ensure WorldObject on root ---
        var worldObject = root.GetComponent<WorldObject>();
        if (worldObject == null)
        {
            worldObject = root.AddComponent<WorldObject>();
            changed = true;
        }

        // Optional: set displayName if empty
        var displayNameField = typeof(WorldObject).GetField("displayName");
        if (displayNameField != null)
        {
            string current = (string)displayNameField.GetValue(worldObject);
            if (string.IsNullOrEmpty(current))
            {
                displayNameField.SetValue(worldObject, root.name);
                changed = true;
            }
        }

        // --- Ensure LocationModule on root ---
        var location = root.GetComponent<LocationModule>();
        if (location == null)
        {
            location = root.AddComponent<LocationModule>();
            changed = true;
        }

        // --- Ensure VisualModule on root (or child) ---
        var appearance = root.GetComponent<AppearanceModule>();
        if (appearance == null)
        {
            appearance = root.AddComponent<AppearanceModule>();
            changed = true;
        }

        // We can optionally assign mainRenderer here if null
        if (appearance != null && appearance.mainRenderer == null)
        {
            var renderer = root.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                appearance.mainRenderer = renderer;
                changed = true;
            }
        }

        // --- Ensure PlacementModule on root ---
        var placement = root.GetComponent<PlacementModule>();
        if (placement == null)
        {
            placement = root.AddComponent<PlacementModule>();
            changed = true;
        }

        // Initialize PlacementModule defaults based on prefab name
        if (placement != null)
        {
            if (SetupPlacementDefaults(root.name, placement))
                changed = true;

            // Auto-size from mesh, if not done yet
            if (placement.autoSizeFromMesh)
            {
                placement.AutoComputeSizeFromMesh();
                changed = true;
            }
        }

        if (changed)
            EditorUtility.SetDirty(root);

        return changed;
    }

    private static bool EnsureSavePrefabId(GameObject root, string assetPath)
    {
        bool changed = false;
        SavePrefabId savePrefabId = root.GetComponent<SavePrefabId>();
        if (savePrefabId == null)
        {
            savePrefabId = root.AddComponent<SavePrefabId>();
            changed = true;
        }

        string resourcesPath = ToResourcesPath(assetPath);
        string prefabId = !string.IsNullOrWhiteSpace(resourcesPath)
            ? $"resources:{resourcesPath}"
            : $"asset:{assetPath}";

        if (savePrefabId.PrefabId != prefabId ||
            savePrefabId.ResourcesPath != resourcesPath ||
            savePrefabId.AssetPath != assetPath)
        {
            savePrefabId.SetPrefabIdentity(prefabId, resourcesPath, assetPath);
            changed = true;
        }

        return changed;
    }

    private static string ToResourcesPath(string assetPath)
    {
        const string resourcesSegment = "/Resources/";
        int resourcesIndex = assetPath.IndexOf(resourcesSegment, System.StringComparison.Ordinal);
        if (resourcesIndex < 0)
            return "";

        string pathAfterResources = assetPath.Substring(resourcesIndex + resourcesSegment.Length);
        return System.IO.Path.ChangeExtension(pathAfterResources, null);
    }

    /// <summary>
    /// Very simple heuristics based on prefab name.
    /// You can tweak this list as you add more furniture types.
    /// </summary>
    private static bool SetupPlacementDefaults(string prefabName, PlacementModule placement)
    {
        bool changed = false;
        string nameLower = prefabName.ToLowerInvariant();

        // Start with generic defaults
        var allowed = PlacementRoomTypeFlags.Generic;
        var edge    = EdgeHint.Free;
        var rot     = RotationRule.Snap90;

        // Bedroom-ish
        if (nameLower.Contains("bed"))
        {
            allowed = PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Generic;
            edge    = EdgeHint.AgainstWall;
            rot     = RotationRule.FaceAwayFromWall;
        }
        else if (nameLower.Contains("sofa") || nameLower.Contains("couch"))
        {
            allowed = PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Generic;
            edge    = EdgeHint.NearWall;
            rot     = RotationRule.FaceAwayFromWall;
        }
        else if (nameLower.Contains("chair"))
        {
            allowed = PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Generic;
            edge    = EdgeHint.Free;
            rot     = RotationRule.Snap90;
        }
        else if (nameLower.Contains("table") || nameLower.Contains("desk"))
        {
            allowed = PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Kitchen | PlacementRoomTypeFlags.Generic;
            edge    = EdgeHint.Free;
            rot     = RotationRule.Snap90;
        }
        else if (nameLower.Contains("lamp"))
        {
            allowed = PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Generic;
            edge    = EdgeHint.NearWall;
            rot     = RotationRule.Free;
        }
        else if (nameLower.Contains("toilet") || nameLower.Contains("bath") || nameLower.Contains("sink"))
        {
            allowed = PlacementRoomTypeFlags.Bathroom;
            edge    = EdgeHint.AgainstWall;
            rot     = RotationRule.FaceAwayFromWall;
        }
        else if (nameLower.Contains("wardrobe") || nameLower.Contains("cabinet") || nameLower.Contains("shelf"))
        {
            allowed = PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Utility | PlacementRoomTypeFlags.Generic;
            edge    = EdgeHint.AgainstWall;
            rot     = RotationRule.FaceAwayFromWall;
        }
        else if (nameLower.Contains("tree") || nameLower.Contains("bush") || nameLower.Contains("rock"))
        {
            allowed = PlacementRoomTypeFlags.Outdoor | PlacementRoomTypeFlags.Generic;
            edge    = EdgeHint.Free;
            rot     = RotationRule.Free;
        }

        // Apply only if different from current
        if (placement.allowedRooms != allowed)
        {
            placement.allowedRooms = allowed;
            changed = true;
        }

        if (placement.edgeHint != edge)
        {
            placement.edgeHint = edge;
            changed = true;
        }

        if (placement.rotationRule != rot)
        {
            placement.rotationRule = rot;
            changed = true;
        }

        return changed;
    }
}
#endif
