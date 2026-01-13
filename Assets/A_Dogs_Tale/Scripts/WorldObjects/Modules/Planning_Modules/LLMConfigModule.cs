#nullable enable
using System;
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
    /// Intended to be designer-tweaked. Pairs with LLMWorldStateModule (dynamic context).
    /// </summary>
    public sealed class LLMConfigModule : MonoBehaviour
    {
        private readonly SophisticationPolicy sophisticationPolicy = new();

        [Header("Identity")]
        [Tooltip("Optional stable ID override. If empty, falls back to GameObject name.")]
        [SerializeField] private string agentIdOverride = "";

        [Tooltip("Boss NPCs may get higher sophistication and more context.")]
        [SerializeField] private bool isBoss = false;

        [Tooltip("Simple creatures can be clamped to avoid expensive 'High' behavior.")]
        [SerializeField] private bool isSimpleCreature = false;

        [Header("Personality")]
        [Tooltip("If true, will generate personality via weighted random when manual overrides aren't set.")]
        [SerializeField] private bool allowRandomPersonality = true;

        [Tooltip("If true, personality is generated once and cached for the life of this instance.")]
        [SerializeField] private bool lockPersonalityAfterSpawn = true;

        [Tooltip("Database containing archetypes/quirks/complications.")]
        [SerializeField] private PersonalityDatabase? personalityDatabase;

        [Tooltip("Optional manual archetype override (if set, it will be used).")]
        [SerializeField] private ArchetypeDefinition? manualArchetype;

        [Tooltip("Optional forced quirks. If non-empty, these are used instead of random quirks.")]
        [SerializeField] private List<QuirkDefinition> forcedQuirks = new();

        [Tooltip("Optional manual complication override.")]
        [SerializeField] private ComplicationDefinition? manualComplication;

        [Tooltip("How many quirks to randomly add when no forced quirks are provided.")]
        [Range(0, 5)]
        [SerializeField] private int randomQuirkCount = 2;

        [Header("Instructions (Static per agent)")]
        [TextArea(3, 12)]
        [Tooltip("Extra system-level instructions specific to this agent (added after persona).")]
        [SerializeField] private string extraSystemInstructions = "";

        [Header("Model selection overrides (optional)")]
        [Tooltip("If set, overrides the vendor for this agent (e.g. OpenAI/Gemini). If empty, uses defaults passed to BuildLLMRequest.")]
        [SerializeField] private string vendorOverride = "";

        [Tooltip("Optional model override for Low sophistication.")]
        [SerializeField] private string modelLow = "";

        [Tooltip("Optional model override for Medium sophistication.")]
        [SerializeField] private string modelMedium = "";

        [Tooltip("Optional model override for High sophistication.")]
        [SerializeField] private string modelHigh = "";

        [Header("Tools (optional)")]
        [SerializeField] private bool allowToolsLow = false;
        [SerializeField] private bool allowToolsMedium = true;
        [SerializeField] private bool allowToolsHigh = true;

        private MixedPersonality? cachedPersonality;

        // -----------------------------
        // Public accessors (used by other systems if needed)
        // -----------------------------
        public bool IsBoss => isBoss;
        public bool IsSimpleCreature => isSimpleCreature;
        public string ExtraSystemInstructions => extraSystemInstructions;

        /// <summary>
        /// Returns a stable agent id. Prefer setting agentIdOverride or wiring it from your own GUID system.
        /// </summary>
        public string ResolveAgentId()
        {
            if (!string.IsNullOrWhiteSpace(agentIdOverride))
                return agentIdOverride.Trim();

            // Reasonable fallback if you don't yet have a GUID system.
            return gameObject.name;
        }

        /// <summary>
        /// Select a profile for the given sophistication tier by taking provided defaults
        /// and applying per-agent overrides (vendor/model/tools).
        /// </summary>
        public LLMProfile SelectProfile(Sophistication sophistication, LLMProfile defaults)
        {
            if (defaults == null) throw new ArgumentNullException(nameof(defaults));

            var profile = new LLMProfile
            {
                vendor = string.IsNullOrWhiteSpace(vendorOverride) ? defaults.vendor : vendorOverride.Trim(),
                model = defaults.model,
                level = sophistication,

                maxOutputTokens = defaults.maxOutputTokens,
                temperature = defaults.temperature,

                allowTools = defaults.allowTools,
                contextDetail = defaults.contextDetail,
                planningDepth = defaults.planningDepth,

                minSecondsBetweenCalls = defaults.minSecondsBetweenCalls
            };

            // Model override by tier if provided.
            string modelOverride = sophistication switch
            {
                Sophistication.Low => modelLow,
                Sophistication.Medium => modelMedium,
                _ => modelHigh
            };

            if (!string.IsNullOrWhiteSpace(modelOverride))
                profile.model = modelOverride.Trim();

            // Tool permission override by tier (designer control).
            profile.allowTools = sophistication switch
            {
                Sophistication.Low => allowToolsLow,
                Sophistication.Medium => allowToolsMedium,
                _ => allowToolsHigh
            };

            return profile;
        }

        /// <summary>
        /// Build or return cached MixedPersonality for this agent.
        /// </summary>
        public MixedPersonality BuildOrGetPersonality()
        {
            if (lockPersonalityAfterSpawn && cachedPersonality != null)
                return cachedPersonality;

            MixedPersonality result;

            if (personalityDatabase == null)
            {
                result = new MixedPersonality
                {
                    personaBlock = "CHARACTER PERSONA:\n- Act as a game NPC."
                };
            }
            else
            {
                var mixer = new PersonalityMixer(personalityDatabase);

                List<QuirkDefinition>? manualQuirkOverrides =
                    (forcedQuirks != null && forcedQuirks.Count > 0) ? forcedQuirks : null;

                bool shouldMix =
                    allowRandomPersonality ||
                    manualArchetype != null ||
                    manualQuirkOverrides != null ||
                    manualComplication != null;

                result = shouldMix
                    ? mixer.Build(
                        stableSeedString: ResolveAgentId(),
                        manualArchetypeOverride: manualArchetype,
                        manualQuirkOverrides: manualQuirkOverrides,
                        manualComplicationOverride: manualComplication,
                        randomQuirkCount: randomQuirkCount)
                    : new MixedPersonality
                    {
                        personaBlock = "CHARACTER PERSONA:\n- Act as a game NPC."
                    };
            }

            if (lockPersonalityAfterSpawn)
                cachedPersonality = result;

            return result;
        }

        /// <summary>
        /// Build a fully-populated LLMRequest for this agent using config + dynamic world state.
        /// </summary>
        public LLMRequest BuildLLMRequest(
            LLMWorldStateModule worldState,
            string requestId,
            string userTaskPrompt,
            LLMProfile defaultLow,
            LLMProfile defaultMedium,
            LLMProfile defaultHigh,
            string? toolDefinitionsJson = null,
            string? responseSchemaJson = null)
        {
            if (worldState == null) throw new ArgumentNullException(nameof(worldState));
            if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("requestId is required.", nameof(requestId));
            if (string.IsNullOrWhiteSpace(userTaskPrompt)) throw new ArgumentException("userTaskPrompt is required.", nameof(userTaskPrompt));
            if (defaultLow == null) throw new ArgumentNullException(nameof(defaultLow));
            if (defaultMedium == null) throw new ArgumentNullException(nameof(defaultMedium));
            if (defaultHigh == null) throw new ArgumentNullException(nameof(defaultHigh));

            // 1) Identify agent
            string resolvedAgentId = ResolveAgentId();

            // 2) Compute sophistication from dynamic state
            SophisticationPolicy.Inputs sophisticationInputs = worldState.BuildSophisticationInputs(isBoss: isBoss);

            Sophistication desired = sophisticationPolicy.Evaluate(sophisticationInputs);
            desired = sophisticationPolicy.ClampByNpcType(desired, isSimpleCreature);

            // 3) Select defaults for tier, then apply per-agent overrides
            LLMProfile defaultsForTier = desired switch
            {
                Sophistication.Low => defaultLow,
                Sophistication.Medium => defaultMedium,
                _ => defaultHigh
            };

            LLMProfile profile = SelectProfile(desired, defaultsForTier);

            // 4) Personality
            MixedPersonality mixedPersonality = BuildOrGetPersonality();

            // 5) Context blocks from world state (dynamic)
            var contextBlocks = new List<string>(capacity: 8);
            worldState.AddContextBlocks(contextBlocks);

            // 6) System blocks
            var systemBlocks = new List<string>(capacity: 16)
            {
                PromptBlocks.GlobalRulesBlock(),
                PromptBlocks.PlanningGuidanceBlock(profile.planningDepth),
            };

            if (!string.IsNullOrWhiteSpace(mixedPersonality.personaBlock))
                systemBlocks.Add(mixedPersonality.personaBlock.Trim());

            if (!string.IsNullOrWhiteSpace(extraSystemInstructions))
                systemBlocks.Add(extraSystemInstructions.Trim());

            if (contextBlocks.Count > 0)
                systemBlocks.AddRange(contextBlocks);

            // 7) Metadata
            var metadata = new Dictionary<string, string>(capacity: 10)
            {
                { "agentId", resolvedAgentId },
                { "sophistication", desired.ToString() },
                { "vendor", profile.vendor ?? "" },
                { "model", profile.model ?? "" }
            };

            // Include a few dynamic signals for debugging / analytics
            metadata["distanceToPlayerMeters"] = worldState.distanceToPlayerMeters.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            metadata["isInCombat"] = worldState.isInCombat ? "true" : "false";
            metadata["nearbyEntityCount"] = worldState.nearbyEntityCount.ToString();

            // 8) Build request
            var request = new LLMRequest
            {
                requestId = requestId,
                profile = profile,
                userPrompt = userTaskPrompt.Trim(),
                toolDefinitionsJson = toolDefinitionsJson ?? "",
                responseSchemaJson = responseSchemaJson ?? "",
                metadata = metadata
            };

            request.systemBlocks.AddRange(systemBlocks);

            return request;
        }
    }
}