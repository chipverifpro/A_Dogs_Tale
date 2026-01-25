using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DogGame.LLM.Personality
{
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
    }
    
    public sealed class PersonalitySection
    {
        [Header("Species")]
        public List<PersonalityOption> species = new();

        [Header("Roles")]
        public int numRoles = 1;
        public List<PersonalityOption> roles = new();

        [Header("Quirks")]
        public int numQuirks = 2;
        public List<PersonalityOption> quirks = new();

        [Header("Complications")]
        public int numComplications = 2;
        public List<PersonalityOption> complications = new();
    

        // each random choice contains a name, weight, and some text to describe the behavior to the LLM.

        public List<PersonalityOption> SpeciesChoices = new() {
            new("Dog",   "- Strong scent + hearing, weaker vision.\n" +
                        "- Communicates via body language, barks, movement.\n" +
                        "- Safety, curiosity, pack bonds matter."),
            new("Human", "Can use tools and communicate."),
            new("Cat",   "Independent and ignores commands.")
        };

        public List<PersonalityOption> RoleChoices = new() {
            new("Guard",    "Protect"),
            new("Scout",    "Explore"),
            new("Mentor",   "Lead"),
            new("Tickster", "Cause mischief"),
            new("Trainer",  "Teach"),
            new("Healer",   "Help")
        };

        public List<PersonalityOption> QuirksChoices = new() {
            new("Impulsive",      "Acts without thinking"),
            new("Curious",        "Investigates new things"),
            new("Proud",          "Refuses help from others"),
            new("Loyal",          "Stays close to allies"),
            new("Anxious",        "Startles at loud noises"),
            new("Playful",        "Treats serious situations like a game"),
            new("Stubborn",       "Resists changing plans once decided"),
            new("Protective",     "Instinctively guards weaker allies"),
            new("Distractible",   "Loses focus when something interesting appears"),
            new("Cautious",       "Hesitates before taking risks"),
            new("Greedy",         "Tries to claim more than their share"),
            new("Affectionate",   "Seeks closeness and approval from others"),
            new("Suspicious",     "Assumes unknown actors may be hostile"),
            new("Competitive",    "Turns cooperation into a contest"),
            new("Observant",      "Notices small details others miss")
        };

        public List<PersonalityOption> ComplicationChoices = new() {
            new("InjuredPaw",         "Moves slower than usual"),
            new("AfraidOfThunder",    "Hides during storms"),
            new("DistrustsCats",      "Will not cooperate with cats"),
            new("EasilyDistracted",   "Stops moving to look at butterflies"),
            new("SensitiveNose",      "Overreacts to strong or unpleasant smells"),
            new("OldInjury",          "Avoids strenuous actions that might reopen wounds"),
            new("FoodObsessed",       "Will abandon tasks to pursue food scents"),
            new("Territorial",        "Becomes hostile when others enter claimed areas"),
            new("FearOfHeights",      "Refuses to cross high or unstable ground"),
            new("PoorNightVision",    "Struggles to perceive details in low light"),
            new("Overprotective",     "Intervenes unnecessarily to defend allies"),
            new("ShortAttentionSpan", "Forgets goals when interrupted"),
            new("NoiseSensitive",     "Flinches or freezes at sudden sounds"),
            new("SeparationAnxiety",  "Performs poorly when isolated from the pack")
        };
        
        public PersonalityOption GetRandomChoice(List<PersonalityOption> sourceList)
        {
            int totalWeight = sourceList.Sum(x => x.Weight);    // Linq
            int randomNumber = UnityEngine.Random.Range(0, totalWeight);

            // Iterate through items and subtract weight until reaching 0
            foreach (var entry in sourceList)
            {
                if (randomNumber < entry.Weight)
                {
                    return entry;
                }
                randomNumber -= entry.Weight;
            }
            return sourceList.First();
        }
    
        public void RandomizePersonality()
        {
            PersonalityOption selection;
            while (species.Count<1)
            {
                selection = GetRandomChoice(SpeciesChoices);
                species.Add(selection);
            }

            while (roles.Count<numRoles)
            {
                selection = GetRandomChoice(RoleChoices);
                roles.Add(selection);
            }

            while (quirks.Count<numQuirks)
            {
                selection = GetRandomChoice(QuirksChoices);
                quirks.Add(selection);
            }

            while (complications.Count<numComplications)
            {
                selection = GetRandomChoice(ComplicationChoices);
                complications.Add(selection);
            }
        }

        public string PersonalityToString ()
        {
            StringBuilder sb = new();
            
            sb.Append("PERSONALITY: ");
            foreach(PersonalityOption opt in species)
                sb.Append(opt.Name + " => " + opt.Notes + "; ");
            foreach(PersonalityOption opt in roles)
                sb.Append(opt.Name + " => " + opt.Notes + "; ");
            foreach(PersonalityOption opt in quirks)
                sb.Append(opt.Name + " => " + opt.Notes + "; ");
            foreach(PersonalityOption opt in complications)
                sb.Append(opt.Name + " => " + opt.Notes + "; ");
        
            string result = sb.ToString();
            Debug.Log("PersonalityToString: " + result);
            return result;
        }
    }
}