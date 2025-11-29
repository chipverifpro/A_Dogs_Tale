#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

//using DogGame.World;       // WorldObject, LocationModule, MotionModule, BlackboardModule, etc.
using DogGame.AI;          // AgentModule, AgentMovementModule, AgentSenseModule, AgentPackMemberModule, AgentBlackboardView
//using DogGame.Sensory;     // VisionModule, HearingModule, SmellModule, ScentEmitterModule, SoundEmitterModule
// using DogGame.Visuals;  // If you have a VisualModule namespace

public static class AgentPrefabHelper
{
    [MenuItem("Tools/DogGame/Setup Agent On Selected %#a")]
    private static void SetupAgentOnSelected()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogWarning("No GameObject selected. Select a prefab or scene object to set up as an agent.");
            return;
        }

        // If it's part of a prefab asset, work on the root
        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(go) ?? go;

        Undo.RegisterFullObjectHierarchyUndo(root, "Setup Agent Modules");

        int addedCount = 0;

        // ---- REQUIRED CORE WORLD MODULES ----
        addedCount += EnsureComponent<WorldObject>(root);
        addedCount += EnsureComponent<LocationModule>(root);
        addedCount += EnsureComponent<MotionModule>(root);
        addedCount += EnsureComponent<BlackboardModule>(root);

        // ---- AGENT BRAIN & INTERFACES ----
        addedCount += EnsureComponent<AgentModule>(root);
        addedCount += EnsureComponent<AgentMovementModule>(root);
        addedCount += EnsureComponent<AgentSensesModule>(root);
        addedCount += EnsureComponent<AgentPackMemberModule>(root);
        // AgentBlackboardView is likely a pure C# class, not a MonoBehaviour.
        // If you've made it a component, uncomment this:
        // addedCount += EnsureComponent<AgentBlackboardView>(root);

        // ---- SENSORY MODULES ----
        addedCount += EnsureComponent<VisionModule>(root);
        //addedCount += EnsureComponent<HearingModule>(root);
        //addedCount += EnsureComponent<SmellModule>(root);
        addedCount += EnsureComponent<ScentEmitterModule>(root);
        //addedCount += EnsureComponent<SoundEmitterModule>(root);

        // ---- OPTIONAL / NICE-TO-HAVE MODULES ----
        // If you have them implemented, you can add them here:
        // addedCount += EnsureComponent<EatModule>(root);
        // addedCount += EnsureComponent<InventoryModule>(root);

        // ---- Tag hint (optional) ----
        // If you want, you can auto-tag these as "Agent"
        // if (root.CompareTag("Untagged"))
        // {
        //     Undo.RecordObject(root, "Tag Agent");
        //     root.tag = "Agent";
        // }

        if (addedCount == 0)
        {
            Debug.Log($"[AgentPrefabHelper] '{root.name}' already has all the expected agent modules.");
        }
        else
        {
            Debug.Log($"[AgentPrefabHelper] Added {addedCount} missing agent modules to '{root.name}'.");
        }

        EditorUtility.SetDirty(root);
    }

    private static int EnsureComponent<T>(GameObject go) where T : Component
    {
        if (go.GetComponent<T>() != null)
            return 0;

        Undo.AddComponent<T>(go);
        return 1;
    }

    [MenuItem("Tools/DogGame/Setup Agent On Selected %#a", true)]
    private static bool ValidateSetupAgentOnSelected()
    {
        return Selection.activeGameObject != null;
    }
}
#endif