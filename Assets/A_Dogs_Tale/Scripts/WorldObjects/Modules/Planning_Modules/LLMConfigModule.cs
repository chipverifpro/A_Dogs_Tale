#nullable enable
using System;
using System.Collections.Generic;
using DogGame.LLM.Core;
using DogGame.LLM.Personality;
using DogGame.LLM.Policy;
using DogGame.LLM.Prompting;
using DogGame.Modules;
using UnityEngine;
using static DogGame.LLM.Core.LLMClientBase;

namespace DogGame.LLM.Agent
{
    /// <summary>
    /// Static/per-agent configuration for LLM behavior.
    /// Sections declared in LLMConfigModuleSections.cs.
    /// Intended to be designer-tweaked. Pairs with LLMWorldStateModule (dynamic context).
    /// </summary>
    public sealed class LLMConfigModule : WorldModule
    {
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
            worldState.AddContextBlocks(contextBlocks);

            systemBlocks.AddRange(contextBlocks);

            Debug.Log($"[LLMConfig] toolsChars={instructions.BuildToolDefinitionsJson().Length} schemaChars={instructions.BuildResponseSchemaJson().Length}");

            // ========== Tools Section ==========
            string toolsJson = instructions.BuildToolDefinitionsJson();
            
            // ========== Schema Section ==========
            string schemaJson = instructions.BuildResponseSchemaJson();

            var req = new LLMRequest
            {
                requestId = requestId,
                profile = profile,
                userPrompt = userTaskPrompt.Trim(),
                systemBlocks = systemBlocks,

                // Legacy fields still filled (for compatibility)
                toolDefinitionsJson = toolsJson,
                responseSchemaJson = schemaJson,

                metadata = new Dictionary<string, string>
                {
                    { "agentId", agentId },
                    { "sophistication", tier.ToString() }
                }
            };

            // NEW: parse into structured objects once (preferred path)
            if (LLMPacketJsonPrinter.TryParseObject(toolsJson, out var toolsObj, out _))
                req.toolDefinitions = toolsObj;

            if (LLMPacketJsonPrinter.TryParseObject(schemaJson, out var schemaObj, out _))
                req.responseSchema = schemaObj;

            return req;
        }
    }
}