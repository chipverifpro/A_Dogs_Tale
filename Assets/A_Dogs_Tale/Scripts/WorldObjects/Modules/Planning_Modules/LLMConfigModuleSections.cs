#nullable enable
using System;
using System.Collections.Generic;
using DogGame.LLM.Core;
using DogGame.LLM.Personality;
using DogGame.LLM.Policy;
using DogGame.LLM.Prompting;
using UnityEngine;

// These classes are subsets of LLMConfigModule
// each section is:
//	•	[Serializable]
//	•	Responsible for one concern
//	•	Easy to fold/unfold in the Inspector
// LLMConfigModule (MonoBehaviour)
//  ├─ IdentitySection
//  ├─ PersonalitySection
//  ├─ InstructionSection
//  ├─ ModelOverrideSection
//  └─ ToolPermissionSection


// ================ 1. Identity Section ===============
[Serializable]
public sealed class IdentitySection
{
    [Tooltip("Optional stable ID override. If empty, falls back to GameObject name.")]
    public string agentIdOverride = "";

    [Tooltip("Boss NPCs may get higher sophistication and more context.")]
    public bool isBoss = false;

    [Tooltip("Simple creatures are clamped to lower sophistication tiers.")]
    public bool isSimpleCreature = false;

    [Tooltip("What species this agent is (dog, human, wolf, etc.).")]
    public string species = "dog";

    [Tooltip("What job/role this agent has (guard dog, merchant, villager, etc.).")]
    public string job = "guard dog";

    public string ResolveAgentId(GameObject owner)
    {
        if (!string.IsNullOrWhiteSpace(agentIdOverride))
            return agentIdOverride.Trim();

        return owner.name;
    }
}

// ================ 2. Personality Section ===============

[Serializable]
public sealed class PersonalitySection
{
    public bool allowRandomPersonality = true;
    public bool lockPersonalityAfterSpawn = true;

    [Tooltip("Optional manual species override (dog/human/etc). If null, mixer may pick randomly.")]
    public SpeciesDefinition? manualSpecies;

    [Tooltip("Optional manual role override (guard/pet/etc). If null, mixer may pick randomly.")]
    public RoleDefinition? manualRole;

    public PersonalityDatabase? personalityDatabase;

    [Tooltip("Optional manual archetype override.")]
    public ArchetypeDefinition? manualArchetype;

    [Tooltip("Forced quirks. If non-empty, these replace random quirks.")]
    public List<QuirkDefinition> forcedQuirks = new();

    [Tooltip("Optional manual complication override.")]
    public ComplicationDefinition? manualComplication;

    [Range(0, 5)]
    public int randomQuirkCount = 2;

    [NonSerialized]
    private MixedPersonality? cachedPersonality;

    public MixedPersonality BuildOrGetPersonality(string stableSeed)
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
                forcedQuirks.Count > 0 ? forcedQuirks : null;

            bool shouldMix =
                allowRandomPersonality ||
                manualArchetype != null ||
                manualQuirkOverrides != null ||
                manualComplication != null;

            result = shouldMix
                ? mixer.Build(
                    stableSeedString: stableSeed,
                    manualSpeciesOverride: manualSpecies,
                    manualRoleOverride: manualRole,
                    manualArchetypeOverride: manualArchetype, // legacy fallback still supported
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
}

// ================ 3 Instruction Section ===============

[Serializable]
public sealed class InstructionSection
{
    [TextArea(3, 12)]
    [Tooltip("Extra system-level instructions specific to this agent.")]
    public string extraSystemInstructions = "";

    public void AddSystemBlocks(List<string> systemBlocks)
    {
        if (!string.IsNullOrWhiteSpace(extraSystemInstructions))
            systemBlocks.Add(extraSystemInstructions.Trim());
    }
}

// ================ 4 Model Override Section ===============

[Serializable]
public sealed class ModelOverrideSection
{
    [Tooltip("Optional vendor override (OpenAI, Gemini, etc.).")]
    public string vendorOverride = "";

    public string modelLow = "";
    public string modelMedium = "";
    public string modelHigh = "";

    public LLMProfile ApplyOverrides(Sophistication tier, LLMProfile defaults)
    {
        var profile = new LLMProfile
        {
            vendor = string.IsNullOrWhiteSpace(vendorOverride)
                ? defaults.vendor
                : vendorOverride.Trim(),

            model = defaults.model,
            level = tier,

            maxOutputTokens = defaults.maxOutputTokens,
            temperature = defaults.temperature,
            allowTools = defaults.allowTools,
            contextDetail = defaults.contextDetail,
            planningDepth = defaults.planningDepth,
            minSecondsBetweenCalls = defaults.minSecondsBetweenCalls
        };

        string modelOverride = tier switch
        {
            Sophistication.Low => modelLow,
            Sophistication.Medium => modelMedium,
            _ => modelHigh
        };

        if (!string.IsNullOrWhiteSpace(modelOverride))
            profile.model = modelOverride.Trim();

        return profile;
    }
}

// ================ 5 Tool Permission Section ===============

[Serializable]
public sealed class ToolPermissionSection
{
    [Tooltip(
        "If enabled, the LLM may issue structured commands (\"tools\") at LOW sophistication.\n" +
        "Disable for ambient NPCs or simple creatures that should only react, not act.")]
    public bool allowToolsLow = false;

    [Tooltip(
        "If enabled, the LLM may issue structured commands (\"tools\") at MEDIUM sophistication.\n" +
        "Typical for standard NPCs that can interact with the world in limited ways.")]
    public bool allowToolsMedium = true;

    [Tooltip(
        "If enabled, the LLM may issue structured commands (\"tools\") at HIGH sophistication.\n" +
        "Recommended for bosses, quest givers, or planners that may initiate complex actions.")]
    public bool allowToolsHigh = true;

    public bool AllowTools(Sophistication tier)
    {
        return tier switch
        {
            Sophistication.Low => allowToolsLow,
            Sophistication.Medium => allowToolsMedium,
            _ => allowToolsHigh
        };
    }

    public static string IdentityBlock(string species, string job)
    {
        species = (species ?? "").Trim();
        job = (job ?? "").Trim();

        if (string.IsNullOrEmpty(species) && string.IsNullOrEmpty(job))
            return "IDENTITY: Unknown.";

        if (!string.IsNullOrEmpty(species) && !string.IsNullOrEmpty(job))
            return $"IDENTITY: Species={species}. Job={job}.";

        if (!string.IsNullOrEmpty(species))
            return $"IDENTITY: Species={species}.";

        return $"IDENTITY: Job={job}.";
    }
}

// ================ Identity Section ===============