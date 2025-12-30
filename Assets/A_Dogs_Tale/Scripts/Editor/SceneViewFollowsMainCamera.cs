#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SceneViewFollowsMainCamera
{
    private static bool followEnabled;
    private static SceneView targetSceneView;

    static SceneViewFollowsMainCamera()
    {
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/Scene View/Toggle Follow Main Camera %#g")]
    private static void ToggleFollow()
    {
        followEnabled = !followEnabled;

        // Try to remember which SceneView to drive
        targetSceneView = SceneView.lastActiveSceneView;
        if (targetSceneView == null && SceneView.sceneViews.Count > 0)
        {
            targetSceneView = (SceneView)SceneView.sceneViews[0];
        }

        string viewName = targetSceneView != null ? targetSceneView.titleContent.text : "none";
        Debug.Log($"[SceneViewFollowMainCamera] Follow: {(followEnabled ? "ON" : "OFF")} (target view: {viewName})");
    }

    private static void Update()
    {
        if (!followEnabled) return;
        if (!Application.isPlaying) return;

        // Make sure we have a SceneView to drive
        if (targetSceneView == null)
        {
            if (SceneView.sceneViews.Count == 0) return;
            targetSceneView = (SceneView)SceneView.sceneViews[0];
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            // Uncomment if you want spam:
            // Debug.LogWarning("[SceneViewFollowMainCamera] No Camera.main found.");
            return;
        }

        // Ensure we're in 3D mode
        targetSceneView.in2DMode = false;

        // Match SceneView to the main camera
        Transform ct = cam.transform;
        targetSceneView.LookAt(
            ct.position,
            ct.rotation,
            targetSceneView.size
        );

        // Force the SceneView to redraw
        targetSceneView.Repaint();
    }
}
#endif