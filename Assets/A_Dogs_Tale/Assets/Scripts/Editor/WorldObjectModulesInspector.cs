// Assets/A_Dogs_Tale/Assets/Editor/WorldObjectModulesInspector.cs

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DogGame.AI;           // adjust to your namespaces
// using DogGame.World;    // if WorldObject is in another namespace

[CustomEditor(typeof(WorldObject), true)]
public class WorldObjectModulesInspector : Editor
{
    private bool showSenses       = true;
    private bool showAgents       = true;
    private bool showAgentControl = true;
    private bool showBaseControl  = true;

    private int sensesAddIndex       = 0;
    private int agentsAddIndex       = 0;
    private int agentControlAddIndex = 0;
    private int baseControlAddIndex  = 0;

    // Instead of string names + reflection, use direct Type references.
    // Comment out types you don't have yet or add your own here.

    private static readonly Type[] SensesModuleTypes =
    {
        typeof(VisionModule),    // if you don't have it yet, comment this out
        typeof(HearingModule),   // same here
        typeof(SmellModule)      // and here
    };

    private static readonly Type[] AgentDecisionModuleTypes =
    {
        typeof(PlayerDecisionModule),
        typeof(WandererDecisionModule),
        typeof(FollowerDecisionModule),
        // typeof(CombatDecisionModule),
        // typeof(FormationDecisionModule),
    };

    private static readonly Type[] AgentControlModuleTypes =
    {
        typeof(AgentMovementModule),
        typeof(AgentPackMemberModule),
        // typeof(AgentActionModule),
        // typeof(AgentAnimationModule),
    };

    private static readonly Type[] BaseControlModuleTypes =
    {
        typeof(MotionModule),
        typeof(LocationModule),
        typeof(AgentModule),
    };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("WorldObject Modules", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Modules are grouped by conceptual execution order. " +
            "Actual Tick order is handled by AgentUpdateDriver.",
            MessageType.Info);

        var worldObject = (WorldObject)target;
        var go = worldObject.gameObject;

        EditorGUILayout.Space();

        DrawModuleCategory(
            "Senses",
            go,
            ref showSenses,
            SensesModuleTypes,
            ref sensesAddIndex);

        DrawModuleCategory(
            "Agent Decisions",
            go,
            ref showAgents,
            AgentDecisionModuleTypes,
            ref agentsAddIndex);

        DrawModuleCategory(
            "Agent Controls (movement, actions)",
            go,
            ref showAgentControl,
            AgentControlModuleTypes,
            ref agentControlAddIndex);

        DrawModuleCategory(
            "Base Controls (motion, location, core agent)",
            go,
            ref showBaseControl,
            BaseControlModuleTypes,
            ref baseControlAddIndex);
    }

    private void DrawModuleCategory(
        string label,
        GameObject go,
        ref bool foldout,
        Type[] moduleTypes,
        ref int addIndex)
    {
        EditorGUILayout.Space();
        var headerStyle = new GUIStyle(EditorStyles.foldout)
        {
            fontStyle = FontStyle.Bold
        };

        foldout = EditorGUILayout.Foldout(foldout, label, true, headerStyle);
        if (!foldout)
            return;

        EditorGUI.indentLevel++;

        // Collect present/missing based on direct GetComponent(Type)
        var presentComponents = new List<Component>();
        var missingTypes      = new List<Type>();

        foreach (var type in moduleTypes)
        {
            if (type == null)
                continue;

            var comp = go.GetComponent(type);
            if (comp != null)
                presentComponents.Add(comp);
            else
                missingTypes.Add(type);
        }

        // Present modules
        EditorGUILayout.LabelField("Present:", EditorStyles.miniBoldLabel);
        if (presentComponents.Count > 0)
        {
            foreach (var comp in presentComponents)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(
                        comp.GetType().Name,
                        comp,
                        comp.GetType(),
                        true);
                }
            }
        }
        else
        {
            EditorGUILayout.LabelField("  (none)");
        }

        EditorGUILayout.Space(2);

        // Add-module popup for missing ones
        EditorGUILayout.LabelField("Add Module:", EditorStyles.miniBoldLabel);
        if (missingTypes.Count > 0)
        {
            var options = new string[missingTypes.Count + 1];
            options[0] = "-- Select --";
            for (int i = 0; i < missingTypes.Count; i++)
                options[i + 1] = missingTypes[i].Name;

            addIndex = EditorGUILayout.Popup(addIndex, options);

            if (addIndex > 0)
            {
                var typeToAdd = missingTypes[addIndex - 1];

                Undo.RecordObject(go, "Add Module");  // lighter than full hierarchy undo
                go.AddComponent(typeToAdd);

                Debug.Log($"[WorldObjectModulesInspector] Added module {typeToAdd.Name} to {go.name}.");

                addIndex = 0;

                // Important: mark object dirty so changes persist
                EditorUtility.SetDirty(go);
            }
        }
        else
        {
            EditorGUILayout.LabelField("  (all known modules present)");
        }

        EditorGUI.indentLevel--;
    }
}