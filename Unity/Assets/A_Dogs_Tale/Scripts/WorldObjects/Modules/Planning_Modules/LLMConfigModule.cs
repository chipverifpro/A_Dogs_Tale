#nullable enable
using System;
using System.Collections.Generic;
using DogGame.LLM.Core;
using DogGame.LLM.Personality;
using DogGame.LLM.Policy;
using DogGame.LLM.Prompting;
using DogGame.Modules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static DogGame.LLM.Core.LLMClientBase;
using InspectorTools;

namespace DogGame.LLM.Agent
{
    /// <summary>
    /// Static/per-agent configuration for LLM behavior.
    /// Sections declared in LLMConfigModuleSections.cs.
    /// Intended to be designer-tweaked. Pairs with LLMWorldStateModule (dynamic context).
    /// </summary>
    [InspectorNote("Planning_Modules/LLM Config Module", "Static per-agent configuration for LLM.  Sections declared in LLMConfigModuleSections.cs.  Intended to be designer-tweaked. Pairs with LLMWorldStateModule (dynamic context).")]
    [DisallowMultipleComponent]
    public sealed class LLMConfigModule : WorldModule
    {
        [Serializable]
        public sealed class SaveData
        {
            public List<PersonalityOptionSaveData> identitySpecies = new();
            public List<PersonalityOptionSaveData> identityRoles = new();
            public string cachedIdentity = "";
            public List<PersonalityOptionSaveData> personalityQuirks = new();
            public List<PersonalityOptionSaveData> personalityComplications = new();
            public string cachedPersonality = "";
        }

        [Serializable]
        public sealed class PersonalityOptionSaveData
        {
            public string name = "";
            public string notes = "";
            public string options = "";
            public int weight;

            public static PersonalityOptionSaveData FromOption(global::PersonalityOption option)
            {
                if (option == null)
                    return new PersonalityOptionSaveData();

                return new PersonalityOptionSaveData
                {
                    name = option.Name,
                    notes = option.Notes,
                    options = option.Options,
                    weight = option.Weight
                };
            }

            public global::PersonalityOption ToOption()
            {
                return new global::PersonalityOption(name, notes, options, weight);
            }
        }

        private readonly SophisticationPolicy sophisticationPolicy = new();

        public LLMProfile lowProfile = new();
        public LLMProfile mediumProfile = new();
        public LLMProfile highProfile = new();

        //[Header("Identity")]
        public IdentitySection identity = new();

        //[Header("Personality")]
        public PersonalitySection personality = new();

        //[Header("Instructions")]
        public InstructionSection instructions = new();

        //[Header("Model Overrides")]
        public ModelOverrideSection modelOverrides = new();

        //[Header("Tools")]
        public ToolPermissionSection tools = new();

        public LLMRequest BuildLLMRequest(
            LLMWorldStateModule worldState,
            string requestId,
            string agentId,
            string userTaskPrompt
            )
        {
            List<string> systemBlocks = new();

            var inputs = worldState.BuildSophisticationInputs(identity.isBoss);
            Sophistication tier = sophisticationPolicy.Evaluate(inputs);
            tier = sophisticationPolicy.ClampByNpcType(tier, identity.isSimpleCreature);

            LLMProfile defaults = tier switch
            {
                Sophistication.Low => lowProfile,
                Sophistication.Medium => mediumProfile,
                _ => highProfile
            };

            LLMProfile profile = modelOverrides.ApplyOverrides(tier, defaults);
            profile.allowTools = tools.AllowTools(tier);

            // ========== 2 Personality Section ==========
            //MixedPersonality persona = personality.BuildOrGetPersonality(agentId);
            //
            //systemBlocks = new List<string>
            //{
            //    PromptBlocks.GlobalRulesBlock(),
            //    PromptBlocks.PlanningGuidanceBlock(profile.planningDepth),
            //    persona.personaBlock
            //};

            personality.RandomizePersonality(); // if already run before, won't do anything
            
            string personatext = personality.PersonalityToString();
            systemBlocks = new List<string>
            {
                PromptBlocks.GlobalRulesBlock(),
                PromptBlocks.PlanningGuidanceBlock(profile.planningDepth),
                personatext
            };

            // ========== 1. Identity Section ==========
            identity.RandomizeIdentity();

            string identitytext = identity.IdentityToString();

            systemBlocks.Insert(1, identitytext);

            // ========== 3. Instructions Section ==========
            instructions.AddSystemBlocks(systemBlocks);

            var contextBlocks = new List<string>();
            //worldObject.llmWorldStateModule.AddContextBlocks(contextBlocks);
            // ========== World State Section ==========
            var observations = worldState.ConsumeObservations();
            foreach (var obs in observations)
            {
                systemBlocks.Add("OBSERVATION: " + obs);
            }

            worldState.AddContextBlocks(contextBlocks);

            systemBlocks.AddRange(contextBlocks);

            //Debug.Log($"[LLMConfig] toolsChars={instructions.BuildToolDefinitionsJson().Length} schemaChars={instructions.BuildResponseSchemaJson().Length}");

            // ========== Tools Section ==========
            JObject toolsJson = instructions.BuildToolDefinitionsJson();
            
            // ========== Schema Section ==========
            JObject schemaJson = instructions.BuildResponseSchemaJson();

            var req = new LLMRequest
            {
                requestId = requestId,
                profile = profile,
                userPrompt = userTaskPrompt.Trim(),
                systemBlocks = systemBlocks,

                // Legacy fields still filled (for compatibility)
                toolDefinitions = toolsJson,
                responseSchema = schemaJson,

                metadata = new Dictionary<string, string>
                {
                    { "agentId", agentId },
                    { "sophistication", tier.ToString() }
                }
            };

            // NEW: parse into structured objects once (preferred path)
            //if (LLMPacketJsonPrinter.TryParseObject(toolsJson, out var toolsObj, out _))
            req.toolDefinitions = toolsJson;

            //if (LLMPacketJsonPrinter.TryParseObject(schemaJson, out var schemaObj, out _))
            req.responseSchema = schemaJson;

            return req;
        }

        public SaveData CaptureSaveData()
        {
            return new SaveData
            {
                identitySpecies = CaptureOptions(identity.species),
                identityRoles = CaptureOptions(identity.roles),
                cachedIdentity = identity.cachedIdentity,
                personalityQuirks = CaptureOptions(personality.quirks),
                personalityComplications = CaptureOptions(personality.complications),
                cachedPersonality = personality.cachedPersonality
            };
        }

        public void RestoreSaveData(SaveData data)
        {
            if (data == null)
                return;

            identity.species = RestoreOptions(data.identitySpecies);
            identity.roles = RestoreOptions(data.identityRoles);
            identity.cachedIdentity = data.cachedIdentity ?? "";
            personality.quirks = RestoreOptions(data.personalityQuirks);
            personality.complications = RestoreOptions(data.personalityComplications);
            personality.cachedPersonality = data.cachedPersonality ?? "";
        }

        private static List<PersonalityOptionSaveData> CaptureOptions(List<global::PersonalityOption> options)
        {
            List<PersonalityOptionSaveData> savedOptions = new();
            if (options == null)
                return savedOptions;

            foreach (global::PersonalityOption option in options)
                savedOptions.Add(PersonalityOptionSaveData.FromOption(option));

            return savedOptions;
        }

        private static List<global::PersonalityOption> RestoreOptions(List<PersonalityOptionSaveData> savedOptions)
        {
            List<global::PersonalityOption> options = new();
            if (savedOptions == null)
                return options;

            foreach (PersonalityOptionSaveData savedOption in savedOptions)
            {
                if (savedOption != null)
                    options.Add(savedOption.ToOption());
            }

            return options;
        }
    }
}
