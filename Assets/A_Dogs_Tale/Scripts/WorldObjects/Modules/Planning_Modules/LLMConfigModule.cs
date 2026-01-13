#nullable enable
using System.Collections.Generic;
using DogGame.LLM.Core;
using DogGame.LLM.Personality;
using DogGame.LLM.Policy;
using DogGame.LLM.Prompting;
using UnityEngine;

namespace DogGame.LLM.Agent
{
    /// <summary>
    /// Static/per-agent configuration for LLM behavior.
    /// Sections declared in LLMConfigModuleSections.cs.
    /// Intended to be designer-tweaked. Pairs with LLMWorldStateModule (dynamic context).
    /// </summary>
    public sealed class LLMConfigModule : MonoBehaviour
    {
        private readonly SophisticationPolicy sophisticationPolicy = new();

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
            string userTaskPrompt,
            LLMProfile low,
            LLMProfile medium,
            LLMProfile high)
        {
            string agentId = identity.ResolveAgentId(gameObject);

            var inputs = worldState.BuildSophisticationInputs(identity.isBoss);
            Sophistication tier = sophisticationPolicy.Evaluate(inputs);
            tier = sophisticationPolicy.ClampByNpcType(tier, identity.isSimpleCreature);

            LLMProfile defaults = tier switch
            {
                Sophistication.Low => low,
                Sophistication.Medium => medium,
                _ => high
            };

            LLMProfile profile = modelOverrides.ApplyOverrides(tier, defaults);
            profile.allowTools = tools.AllowTools(tier);

            MixedPersonality persona = personality.BuildOrGetPersonality(agentId);

            var systemBlocks = new List<string>
            {
                PromptBlocks.GlobalRulesBlock(),
                PromptBlocks.PlanningGuidanceBlock(profile.planningDepth),
                persona.personaBlock
            };

            instructions.AddSystemBlocks(systemBlocks);

            var contextBlocks = new List<string>();
            worldState.AddContextBlocks(contextBlocks);
            systemBlocks.AddRange(contextBlocks);

            return new LLMRequest
            {
                requestId = requestId,
                profile = profile,
                userPrompt = userTaskPrompt.Trim(),
                systemBlocks = systemBlocks,
                metadata = new Dictionary<string, string>
                {
                    { "agentId", agentId },
                    { "sophistication", tier.ToString() }
                }
            };
        }
    }
}