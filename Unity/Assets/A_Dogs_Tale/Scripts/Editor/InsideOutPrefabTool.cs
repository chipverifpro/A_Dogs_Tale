using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

public static class InsideOutPrefabTool
{
    private const string MenuPath = "Tools/A Dog's Tale/Turn Selection Inside Out";
    private const string GeneratedMeshFolder = "Assets/A_Dogs_Tale/Generated/InsideOutMeshes";

    [MenuItem(MenuPath, false, 2100)]
    private static void TurnSelectionInsideOut()
    {
        var prefabPaths = new HashSet<string>();
        var sceneRoots = new List<GameObject>();

        foreach (UnityEngine.Object selectedObject in Selection.objects)
        {
            if (selectedObject is not GameObject gameObject)
                continue;

            string assetPath = AssetDatabase.GetAssetPath(gameObject);
            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                prefabPaths.Add(assetPath);
            else if (!EditorUtility.IsPersistent(gameObject))
                sceneRoots.Add(gameObject);
        }

        if (prefabPaths.Count == 0 && sceneRoots.Count == 0)
            return;

        EnsureGeneratedMeshFolderExists();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Turn Selection Inside Out");
        int convertedMeshCount = 0;

        try
        {
            foreach (string prefabPath in prefabPaths)
                convertedMeshCount += ProcessPrefabAsset(prefabPath);

            foreach (GameObject sceneRoot in RemoveNestedSelections(sceneRoots))
                convertedMeshCount += ProcessHierarchy(sceneRoot, true, sceneRoot.name);

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"Turned {convertedMeshCount} mesh object(s) inside out.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Turn Selection Inside Out", exception.Message, "OK");
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateTurnSelectionInsideOut()
    {
        foreach (UnityEngine.Object selectedObject in Selection.objects)
        {
            if (selectedObject is GameObject gameObject &&
                gameObject.GetComponentInChildren<MeshFilter>(true) != null)
                return true;
        }

        return false;
    }

    private static int ProcessPrefabAsset(string prefabPath)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            int count = ProcessHierarchy(prefabRoot, false, Path.GetFileNameWithoutExtension(prefabPath));
            if (count > 0)
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            return count;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static int ProcessHierarchy(GameObject root, bool useUndo, string assetNamePrefix)
    {
        int count = 0;
        foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh sourceMesh = meshFilter.sharedMesh;
            ProBuilderMesh proBuilderMesh = meshFilter.GetComponent<ProBuilderMesh>();

            // Recover a scene prefab instance left in a half-converted state by an
            // earlier failed run. Its source prefab still retains the original mesh.
            if (sourceMesh == null && proBuilderMesh != null && proBuilderMesh.vertexCount == 0)
            {
                MeshFilter sourceFilter = PrefabUtility.GetCorrespondingObjectFromSource(meshFilter);
                sourceMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
                DestroyProBuilderComponent(proBuilderMesh, useUndo);
                proBuilderMesh = null;
                meshFilter.sharedMesh = sourceMesh;
            }

            if (sourceMesh == null)
            {
                Debug.LogWarning($"Skipped '{meshFilter.name}' because it has no source mesh.", meshFilter);
                continue;
            }

            MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.isPartOfStaticBatch)
            {
                Debug.LogWarning($"Skipped static-batched object '{meshFilter.name}'.", meshFilter);
                continue;
            }

            if (proBuilderMesh == null)
            {
                // Adding ProBuilderMesh may immediately clear MeshFilter.sharedMesh,
                // so the importer must use the source reference captured above.
                proBuilderMesh = useUndo
                    ? Undo.AddComponent<ProBuilderMesh>(meshFilter.gameObject)
                    : meshFilter.gameObject.AddComponent<ProBuilderMesh>();

                try
                {
                    var importer = new MeshImporter(
                        sourceMesh,
                        meshRenderer != null ? meshRenderer.sharedMaterials : null,
                        proBuilderMesh);
                    importer.Import(new MeshImportSettings());
                }
                catch
                {
                    DestroyProBuilderComponent(proBuilderMesh, useUndo);
                    meshFilter.sharedMesh = sourceMesh;
                    throw;
                }
            }
            else if (useUndo)
            {
                Undo.RecordObject(proBuilderMesh, "Flip Object Normals");
            }

            foreach (Face face in proBuilderMesh.faces)
                face.Reverse();

            proBuilderMesh.ToMesh();
            proBuilderMesh.Refresh();

            // ToMesh writes the ProBuilder result back to the object's MeshFilter.
            // ProBuilderMesh.mesh and Optimize/Rebuild are internal in ProBuilder 6.
            Mesh bakedMesh = UnityEngine.Object.Instantiate(meshFilter.sharedMesh);
            bakedMesh.name = $"{assetNamePrefix}_{SanitizeFileName(meshFilter.name)}_InsideOut";
            string meshAssetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{GeneratedMeshFolder}/{bakedMesh.name}.asset");
            AssetDatabase.CreateAsset(bakedMesh, meshAssetPath);

            if (useUndo)
                Undo.RecordObject(meshFilter, "Strip ProBuilder Scripts");
            meshFilter.sharedMesh = bakedMesh;

            foreach (MeshCollider collider in meshFilter.GetComponents<MeshCollider>())
            {
                if (useUndo)
                    Undo.RecordObject(collider, "Strip ProBuilder Scripts");
                collider.sharedMesh = bakedMesh;
            }

            DestroyProBuilderComponent(proBuilderMesh, useUndo);

            EditorUtility.SetDirty(meshFilter.gameObject);
            count++;
        }

        return count;
    }

    private static void DestroyProBuilderComponent(ProBuilderMesh proBuilderMesh, bool useUndo)
    {
        if (proBuilderMesh == null)
            return;

        if (useUndo)
            Undo.DestroyObjectImmediate(proBuilderMesh);
        else
            UnityEngine.Object.DestroyImmediate(proBuilderMesh);
    }

    private static List<GameObject> RemoveNestedSelections(List<GameObject> selections)
    {
        var selectionSet = new HashSet<GameObject>(selections);
        return selections.FindAll(gameObject =>
        {
            for (Transform parent = gameObject.transform.parent; parent != null; parent = parent.parent)
            {
                if (selectionSet.Contains(parent.gameObject))
                    return false;
            }
            return true;
        });
    }

    private static void EnsureGeneratedMeshFolderExists()
    {
        string currentPath = "Assets";
        foreach (string folderName in GeneratedMeshFolder.Substring("Assets/".Length).Split('/'))
        {
            string nextPath = $"{currentPath}/{folderName}";
            if (!AssetDatabase.IsValidFolder(nextPath))
                AssetDatabase.CreateFolder(currentPath, folderName);
            currentPath = nextPath;
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidCharacter, '_');
        return value;
    }
}
