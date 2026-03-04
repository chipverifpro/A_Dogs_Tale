using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class MissingScriptTools
{
    [MenuItem("Tools/Missing Scripts/Report In Active Scene")]
    static void ReportInScene()
    {
        int total = 0, objs = 0;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            total += ReportRecursive(root, ref objs);

        Debug.Log($"[Missing Scripts] Scene: {objs} GameObjects scanned, {total} missing components.");
    }

    [MenuItem("Tools/Missing Scripts/Remove In Active Scene")]
    static void RemoveInScene()
    {
        int total = 0, objs = 0;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            total += RemoveRecursive(root, ref objs);

        if (total > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[Missing Scripts] REMOVED {total} missing components from {objs} GameObjects in scene.");
    }

    [MenuItem("Tools/Missing Scripts/Report On Selected Prefabs")]
    static void ReportOnSelectedPrefabs()
    {
        int total = 0, objs = 0;
        foreach (var obj in Selection.objects)
        {
            var go = obj as GameObject;
            if (!go) continue;
            total += ReportRecursive(go, ref objs);
        }
        Debug.Log($"[Missing Scripts] Selected prefabs: {objs} GameObjects, {total} missing components.");
    }

    [MenuItem("Tools/Missing Scripts/Remove On Selected Prefabs")]
    static void RemoveOnSelectedPrefabs()
    {
        int total = 0, objs = 0;
        foreach (var obj in Selection.objects)
        {
            var go = obj as GameObject;
            if (!go) continue;
            Undo.RegisterFullObjectHierarchyUndo(go, "Remove Missing Scripts");
            total += RemoveRecursive(go, ref objs);
            EditorUtility.SetDirty(go);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Missing Scripts] REMOVED {total} missing components from {objs} GameObjects (selected prefabs).");
    }

    static int ReportRecursive(GameObject go, ref int objCount)
    {
        int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
        if (missing > 0)
            Debug.LogWarning($"[Missing Scripts] '{GetPath(go)}' has {missing} missing script(s).", go);
        objCount++;
        foreach (Transform t in go.transform) missing += ReportRecursive(t.gameObject, ref objCount);
        return missing;
    }

    static int RemoveRecursive(GameObject go, ref int objCount)
    {
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
        if (count > 0) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        objCount++;
        foreach (Transform t in go.transform) count += RemoveRecursive(t.gameObject, ref objCount);
        return count;
    }

    static string GetPath(GameObject go)
    {
        string path = go.name;
        while (go.transform.parent) { go = go.transform.parent.gameObject; path = go.name + "/" + path; }
        return path;
    }
}
