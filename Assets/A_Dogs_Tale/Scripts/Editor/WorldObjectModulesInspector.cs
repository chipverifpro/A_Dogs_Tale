// Assets/A_Dogs_Tale/Assets/Editor/WorldObjectModulesInspector.cs

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DogGame.Modules;           // adjust to your namespaces
using DogGame.AI;

[CustomEditor(typeof(WorldObject), true)]
public class WorldObjectModulesInspector : Editor
{
    private bool showSensory        = true;
    private bool showAgents         = true;
    private bool showAgentInterface = true;
    private bool showOutput         = true;
    private bool showData           = true;
    private bool showQuest          = true;

    private int sensoryAddIndex      = 0;
    private int agentsAddIndex       = 0;
    private int agentInterfaceAddIndex = 0;
    private int outputAddIndex       = 0;
    private int dataAddIndex         = 0;
    private int questAddIndex        = 0;


    // Instead of string names + reflection, use direct Type references.
    // Comment out types you don't have yet or add your own here.

    private static readonly Type[] SensoryModuleTypes =
    {
        typeof(LocationModule),         // World location, orientation
        typeof(VisionPerceptionModule),           // What can be seen
        typeof(HearingModule),          // What can be heard
        typeof(TasteModule),              // Response to eating/tasting
        typeof(ScentPerceptionModule),  // What can be smelled
    };

    private static readonly Type[] AgentDecisionModuleTypes =
    {
        typeof(PlayerDecisionModule),
        typeof(WandererDecisionModule),
        typeof(FollowerDecisionModule),
        typeof(ImmobileDecisionModule),
        typeof(TaskFollowerDecisionModule),
    };

    private static readonly Type[] AgentInterfaceModuleTypes =
    {
        typeof(AgentModule),            // switches DecisionModules
        typeof(AgentMovementModule),    // Movement intent
        typeof(PackMemberModule),       // Membership, leader, formation
        typeof(MotivationModule),       // Combination of senses
        typeof(LLMRequestResponseModule),   // LLM Interface: request, collect responses, convert to tasks and reactions
        typeof(ReactionModule),         // detects conditions and trigger response scripts
    };

    private static readonly Type[] AbilityTypes =
    {
        typeof(ActivatorModule),        // Click on, step on, use, ...
        typeof(InteractionModule),      // Dialog
    };

    private static readonly Type[] OutputModuleTypes =
    {
        typeof(MotionModule),           // Actual movement
        typeof(AppearanceModule),       // Animation, SFX
        typeof(ScentEmitterModule),     // Emit scent including on-demand
        typeof(NoiseMakerModule),       // Emit noise: bark, run, etc.
    };

    private static readonly Type[] DataModuleTypes =
    {
        typeof(BlackboardModule),   // Generic data storage
        typeof(PlacementModule),    // Furniture placement definitions
        typeof(StatusModule),       // Conditions (hungry, tired, alert, training)
        typeof(TaskListModule),     // Current list of tasks to perform
        typeof(ContainerModule),    // Inventory management
    };

    private static readonly Type[] QuestModuleTypes =
    {
        typeof(FetchQuestModule),   // simple parameterized quest
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
            ref showSensory,
            SensoryModuleTypes,
            ref sensoryAddIndex);

        DrawModuleCategory(
            "Agent Decisions",
            go,
            ref showAgents,
            AgentDecisionModuleTypes,
            ref agentsAddIndex);

        DrawModuleCategory(
            "Agent Controls (movement, actions)",
            go,
            ref showAgentInterface,
            AgentInterfaceModuleTypes,
            ref agentInterfaceAddIndex);

        DrawModuleCategory(
            "Ability (click on, step on, use, talk)",
            go,
            ref showOutput,
            OutputModuleTypes,
            ref outputAddIndex);

        DrawModuleCategory(
            "Outputs (motion, location, core agent)",
            go,
            ref showOutput,
            OutputModuleTypes,
            ref outputAddIndex);

        DrawModuleCategory(
            "DataModules",
            go,
            ref showData,
            DataModuleTypes,
            ref dataAddIndex);

        DrawModuleCategory(
            "QuestModules",
            go,
            ref showQuest,
            QuestModuleTypes,
            ref questAddIndex);
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