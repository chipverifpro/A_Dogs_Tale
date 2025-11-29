#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TeleportToSelectionEditor
{
    // Menu item with hotkey: Tools/Teleport Player To Selection  (Ctrl/Cmd+Alt+T)
    [MenuItem("Tools/Teleport Player To Selection %#t")]
    private static void TeleportPlayerToSelection()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Teleport only works in Play Mode.");
            return;
        }

        Transform selected = Selection.activeTransform;
        if (selected == null)
        {
            Debug.LogWarning("No object selected in the Hierarchy.");
            return;
        }

        // Find the player – customize this to your setup.
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("No GameObject with tag 'Player' found.");
            return;
        }

        // Optional offset so you don't spawn *inside* objects
        Vector3 offset = Vector3.up * 0.5f;

        Undo.RecordObject(player.transform, "Teleport Player To Selection");
        player.transform.position = selected.position + offset;

        Debug.Log($"Teleported Player to {selected.name} at {selected.position}.");
    }
}
#endif