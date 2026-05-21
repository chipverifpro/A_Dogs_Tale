// Assets/A_Dogs_Tale/Assets/Editor/WorldObjectModulesInspector.cs

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DogGame.Modules;           // adjust to your namespaces
using DogGame.AI;
using DogGame.LLM;
using DogGame.UI.InteractionWheel;
using DogGame.World;
using DogGame.LLM.Agent;

[CustomEditor(typeof(WorldObject), true)]
public class WorldObjectModulesInspector : Editor
{
    private bool showSensory        = true;
    private bool showAgents         = true;
    private bool showAgentInterface = true;
    private bool showOutput         = true;
    private bool showAbility        = true;
    private bool showPlanning       = true;
    private bool showData           = true;
    private bool showThing          = true;
    private bool showQuest          = true;

    private int sensoryAddIndex      = 0;
    private int agentsAddIndex       = 0;
    private int agentInterfaceAddIndex = 0;
    private int planningAddIndex     = 0;
    private int abilityAddIndex      = 0;
    private int outputAddIndex       = 0;
    private int dataAddIndex         = 0;
    private int thingAddIndex        = 0;
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
        typeof(ExploreDecisionModule),
        typeof(WandererDecisionModule),
        typeof(FollowerDecisionModule),
        typeof(HerdDecisionModule),
        typeof(ImmobileDecisionModule),
        typeof(TaskFollowerDecisionModule),
    };

    private static readonly Type[] AgentInterfaceModuleTypes =
    {
        typeof(AgentModule),            // switches DecisionModules
        typeof(AgentMovementModule),    // Movement intent
        typeof(PackMemberModule),       // Membership, leader, formation
        typeof(MotivationModule),       // Combination of senses
        typeof(LLMThinkModule),         // LLM Interface: request, collect responses, convert to tasks and reactions
        typeof(ReactionModule),         // detects conditions and trigger response scripts
    };

    private static readonly Type[] AbilityModuleTypes =
    {
        typeof(ActivatorModule),        // Click on, step on, use, ...
        typeof(InteractionModule),      // Interaction Wheel Commands
        typeof(MotionModule),           // Low level movement
    };

    private static readonly Type[] OutputModuleTypes =
    {
        typeof(AppearanceModule),       // Animation, SFX
        typeof(ScentEmitterModule),     // Emit scent including on-demand
        typeof(NoiseMakerModule),       // Emit noise: bark, run, etc.
    };

    private static readonly Type[] DataModuleTypes =
    {
        typeof(BlackboardModule),   // Generic data storage
        typeof(AgentStateModule),   // Conditions (hungry, tired, alert, training)
        typeof(TaskListModule),     // Current list of tasks to perform (STUB)
    };

    private static readonly Type[] ThingModuleTypes =
    {
        typeof(KineticModule), // Impulse-driven movement for thrown, rolled, or kicked items
        typeof(PlacementModule),    // Furniture placement definitions (ONLY RANDOM OBJECTS)
        typeof(DoorModule),         // Can open and close (doors, chests, holes)
        typeof(ContainerModule),    // Can hold an item, inventory management
    };

    private static readonly Type[] QuestModuleTypes =
    {
        typeof(FetchQuestModule),   // simple parameterized quest
    };

    private static readonly Type[] PlanningModuleTypes =
    {
        typeof(LLMConfigModule),     // This is the per-agent LLM request builder. It picks a sophistication tier,
                                     //   randomizes identity/personality, injects world-state observations, and 
                                     //   builds tool/schema JSON for the scheduler. 
                                     //   It is consumed by LLMThinkModule.cs (line 54) and
                                     //   dispatched by LLMWorldScheduler.cs (line 409). 
        typeof(LLMWorldStateModule), // This is the dynamic context provider for LLM agents. Today it mainly
                                     //   contributes leash text, position/room/door context, vision summaries, 
                                     //   and queued task observations. 
                                     //   It is used by LLMConfigModule, 
                                     //   by the sidecar /world_state endpoint in UnitySidecarInboundServer.cs (line 204), 
                                     //   by exploration logic for door discovery in ExploreDecisionModule.cs (line 466), 
                                     //   and by TaskExecutor.cs (line 100) to store task reports as observations.
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
            "Agent Decision Modules",
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
            "LLM Planning",
            go,
            ref showPlanning,
            PlanningModuleTypes,
            ref planningAddIndex);

        DrawModuleCategory(
            "Senses",
            go,
            ref showSensory,
            SensoryModuleTypes,
            ref sensoryAddIndex);

        DrawModuleCategory(
            "Ability (motion, activators, interaction wheel commands, container)",
            go,
            ref showAbility,
            AbilityModuleTypes,
            ref abilityAddIndex);

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
            "ThingModules (applies to objects, not agents)",
            go,
            ref showThing,
            ThingModuleTypes,
            ref thingAddIndex);

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
