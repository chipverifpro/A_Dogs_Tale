#nullable enable
using System;
using UnityEditor;
using UnityEngine;

public static class FindManagedReferenceType
{
    [MenuItem("Tools/Debug/Find SerializeReference ProBuilder Stairs")]
    public static void Find()
    {
        const string needle = "UnityEngine.ProBuilder.Shapes.Stairs";

        string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
        int hits = 0;

        foreach (string path in allAssetPaths)
        {
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                continue;

            UnityEngine.Object[] assetsAtPath = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assetsAtPath == null || assetsAtPath.Length == 0)
                continue;

            foreach (var asset in assetsAtPath)
            {
                if (asset == null) continue;

                SerializedObject so;
                try
                {
                    so = new SerializedObject(asset);
                }
                catch
                {
                    continue; // some assets can't be serialized this way
                }

                SerializedProperty it = so.GetIterator();
                bool enterChildren = true;

                while (it.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    if (it.propertyType != SerializedPropertyType.ManagedReference)
                        continue;

                    // ManagedReferenceFullTypename contains the concrete runtime type.
                    string fullTypeName = it.managedReferenceFullTypename;
                    if (string.IsNullOrEmpty(fullTypeName))
                        continue;

                    if (fullTypeName.Contains(needle, StringComparison.Ordinal))
                    {
                        hits++;
                        Debug.Log(
                            $"[SerializeReference HIT] Asset='{path}' Object='{asset.name}' " +
                            $"Property='{it.propertyPath}' Type='{fullTypeName}'",
                            asset);
                        break; // one hit is enough to flag the asset
                    }
                }
            }
        }

        Debug.Log($"Find complete. Hits: {hits}");
    }
}