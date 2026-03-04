#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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


// PersonalityOption defines a property of the Agent.
// only Name is required, all others are optional.
// Weight can be used to adjust randomization in the
//   TryGetRandomChoice function.
public class PersonalityOption
{
    public string Name { get; set; }
    public string Notes { get; set; }
    public string Options { get; set; }
    public int Weight { get; set; }

    public PersonalityOption(string name, string notes = "", string options = "", int weight=10)
    {
        Name = name;
        Notes = notes;      // guidance to LLM
        Options = options;
        Weight = weight;
    }

    public static bool TryGetRandomChoice(List<PersonalityOption> choices, out PersonalityOption selection)
    {
        if (choices.Count==0) 
        {
            selection = new("");
            return false;
        }

        int totalWeight = choices.Sum(x => x.Weight);    // Linq
        int randomNumber = UnityEngine.Random.Range(0, totalWeight);

        // Iterate through items and subtract weight until reaching 0
        foreach (var entry in choices)
        {
            if (randomNumber < entry.Weight)
            {
                selection = entry;
                return true;
            }
            randomNumber -= entry.Weight;
        }
        selection = choices.First();
        return true; 
    }

    public string OptionToString()
    {
        StringBuilder sb = new();

        sb.Append(Name);
        if (!string.IsNullOrEmpty(Notes))
            sb.Append(" = " + Notes + " ");
        if (!string.IsNullOrEmpty(Options))
            sb.Append("("+Options+") ");
        return sb.ToString();
    }
}



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

    [Header("Species")]
    public List<PersonalityOption> species = new();

    [Header("Roles")]
    public int numRoles = 1;
    public List<PersonalityOption> roles = new();


    public List<PersonalityOption> SpeciesChoices = new() {
        new("Dog",   "- Strong scent + hearing, weaker vision.\n" +
                     "- Communicates via body language, barks, movement.\n" +
                     "- Safety, curiosity, pack bonds matter."),
        new("Human", "Can use tools and communicate."),
        new("Cat",   "Independent and ignores commands.")
    };

    public List<PersonalityOption> RoleChoices = new() {
        new("Guard",    "Protects a location."),
        new("Bodyguard","Protects a character."),
        new("Scout",    "Explores the world."),
        new("Mentor",   "Guides and helps others."),
        new("Tickster", "Causes mischief."),
        new("Trainer",  "Teaches commands, abilities"),
        new("Healer",   "Helps injured characters.")
    };
    
    public string cachedIdentity = "";

    public string ResolveAgentId(GameObject owner)
    {
        if (!string.IsNullOrWhiteSpace(agentIdOverride))
            return agentIdOverride.Trim();

        return owner.name;
    }

    public void RandomizeIdentity()
    {
        PersonalityOption selection;
        while (species.Count<1)
        {
            PersonalityOption.TryGetRandomChoice(SpeciesChoices, out selection);
            species.Add(selection);
        }

        while (roles.Count<numRoles)
        {
            PersonalityOption.TryGetRandomChoice(RoleChoices, out selection);
            roles.Add(selection);
        }

        // empty the cached string
        if (!string.IsNullOrEmpty(cachedIdentity))
            cachedIdentity="";
    }

    public string IdentityToString ()
    {
        if (string.IsNullOrEmpty(cachedIdentity))
        {
            StringBuilder sb = new();
            
            sb.Append("Identity: ");
            foreach(PersonalityOption opt in species)
                sb.Append(opt.OptionToString());
            foreach(PersonalityOption opt in roles)
                sb.Append(opt.OptionToString());
        
            cachedIdentity = sb.ToString();
            Debug.Log("IdentityToString: " + cachedIdentity);
        }
        return cachedIdentity;
    }
}

// ================ 2. Personality Section ===============

[Serializable]
public sealed class PersonalitySection
{
    [Header("Quirks")]
    public int numQuirks = 2;
    public List<PersonalityOption> quirks = new();

    [Header("Complications")]
    public int numComplications = 2;
    public List<PersonalityOption> complications = new();


    public List<PersonalityOption> QuirksChoices = new() {
        new("Impulsive",      "Acts without thinking."),
        new("Curious",        "Investigates new things."),
        new("Proud",          "Refuses help from others."),
        new("Loyal",          "Stays close to allies."),
        new("Anxious",        "Startles at loud noises."),
        new("Playful",        "Treats serious situations like a game."),
        new("Stubborn",       "Resists changing plans once decided."),
        new("Protective",     "Instinctively guards weaker allies."),
        new("Distractible",   "Loses focus when something interesting appears."),
        new("Cautious",       "Hesitates before taking risks."),
        new("Greedy",         "Tries to claim more than their share."),
        new("Affectionate",   "Seeks closeness and approval from others."),
        new("Suspicious",     "Assumes unknown actors may be hostile."),
        new("Competitive",    "Turns cooperation into a contest."),
        new("Observant",      "Notices small details others miss.")
    };

// TODO: add option field that will be parsed to modify character capabilities.

    public List<PersonalityOption> ComplicationChoices = new() {
        new("InjuredPaw",         "Moves slower than usual."),
        new("AfraidOfThunder",    "Hides during storms."),
        new("DistrustsCats",      "Will not cooperate with cats."),
        new("EasilyDistracted",   "Stops moving to look at butterflies."),
        new("SensitiveNose",      "Overreacts to strong or unpleasant smells."),
        new("OldInjury",          "Avoids strenuous actions that might reopen wounds."),
        new("FoodObsessed",       "Will abandon tasks to pursue food scents."),
        new("Territorial",        "Becomes hostile when others enter claimed areas."),
        new("FearOfHeights",      "Refuses to cross high or unstable ground."),
        new("PoorNightVision",    "Struggles to perceive details in low light."),
        new("Overprotective",     "Intervenes unnecessarily to defend allies."),
        new("ShortAttentionSpan", "Forgets goals when interrupted."),
        new("NoiseSensitive",     "Flinches or freezes at sudden sounds."),
        new("SeparationAnxiety",  "Performs poorly when isolated from the pack.")
    };
        
    public string cachedPersonality = "";


    // Note: does nothing if there are already enough entries in each.
    public void RandomizePersonality()
    {
        PersonalityOption selection;

        while (quirks.Count<numQuirks)
        {
            PersonalityOption.TryGetRandomChoice(QuirksChoices, out selection);
            quirks.Add(selection);
        }

        while (complications.Count<numComplications)
        {
            PersonalityOption.TryGetRandomChoice(ComplicationChoices, out selection);
            complications.Add(selection);
        }

        // empty the cached string
        if (!string.IsNullOrEmpty(cachedPersonality))
            cachedPersonality="";
    }

    public string PersonalityToString ()
    {
        if (string.IsNullOrEmpty(cachedPersonality))
        {
            StringBuilder sb = new();
            
            sb.Append("PERSONALITY: ");
            foreach(PersonalityOption opt in quirks)
                sb.Append(opt.OptionToString());
            foreach(PersonalityOption opt in complications)
                sb.Append(opt.OptionToString());
        
            cachedPersonality = sb.ToString();
            Debug.Log("PersonalityToString: " + cachedPersonality);
        }
        return cachedPersonality;
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
}