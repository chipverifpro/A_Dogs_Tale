using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MissingScriptRuntimeDetector : MonoBehaviour
{
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ScanNextFrame(scene));
    }

    IEnumerator ScanNextFrame(Scene scene)
    {
        // Wait one frame so the scene hierarchy is fully realized
        yield return null;

        int total = 0;
        foreach (var root in scene.GetRootGameObjects())
            total += ReportMissingRecursive(root, root.name);

        if (total > 0)
            Debug.LogWarning($"[MissingScriptDetector] Scene '{scene.name}' has {total} missing script component(s).");
    }

    int ReportMissingRecursive(GameObject go, string path)
    {
        int missing = 0;
        var mbs = go.GetComponents<MonoBehaviour>(); // null entries indicate missing scripts
        foreach (var mb in mbs)
        {
            if (mb == null)
            {
                missing++;
                Debug.LogWarning($"[MissingScriptDetector] Missing script on '{path}'", go);
            }
        }
        for (int i = 0; i < go.transform.childCount; i++)
        {
            var child = go.transform.GetChild(i).gameObject;
            missing += ReportMissingRecursive(child, path + "/" + child.name);
        }
        return missing;
    }
}