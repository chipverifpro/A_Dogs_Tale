// Assets/Editor/FindPrefabsWithModule.cs
using UnityEngine;
using UnityEditor;

public class FindPrefabsWithModule : EditorWindow
{
    private string moduleTypeName = "AgentMovementModule";

    [MenuItem("Tools/Find Prefabs With Module")]
    public static void ShowWindow()
    {
        GetWindow<FindPrefabsWithModule>("Find Prefabs With Module");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Search Prefabs for Module", EditorStyles.boldLabel);
        moduleTypeName = EditorGUILayout.TextField("Module Type Name", moduleTypeName);

        if (GUILayout.Button("Search"))
        {
            SearchPrefabs(moduleTypeName);
        }
    }

    private static void SearchPrefabs(string typeName)
    {
        System.Type t = FindTypeInProject(typeName);
        if (t == null)
        {
            Debug.LogError($"Type '{typeName}' not found in assemblies.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        Debug.Log($"Searching {guids.Length} prefabs for module {typeName}...");

        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null && prefab.GetComponentInChildren(t, true) != null)
            {
                Debug.Log($"FOUND in prefab: {path}", prefab);
                count++;
            }
        }

        Debug.Log($"Search complete. Found {count} prefabs with module '{typeName}'.");
    }

    private static System.Type FindTypeInProject(string typeName)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = asm.GetType(typeName);
            if (type != null)
                return type;
        }
        return null;
    }
}