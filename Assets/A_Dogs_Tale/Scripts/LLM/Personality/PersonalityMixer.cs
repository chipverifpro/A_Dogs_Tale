using System;
using System.Collections.Generic;
using System.Text;

namespace DogGame.LLM.Personality
{
    public sealed class PersonalityMixer
    {
        private readonly PersonalityDatabase database;

        public PersonalityMixer(PersonalityDatabase database)
        {
            this.database = database;
        }

        public MixedPersonality Build(
            string stableSeedString,
            ArchetypeDefinition manualArchetypeOverride = null,
            List<QuirkDefinition> manualQuirkOverrides = null,
            ComplicationDefinition manualComplicationOverride = null,
            int randomQuirkCount = 2)
        {
            if (database == null)
                throw new InvalidOperationException("PersonalityMixer requires a PersonalityDatabase.");

            var rng = new Random(StableHash(stableSeedString));

            ArchetypeDefinition archetype = manualArchetypeOverride ?? WeightedPick(database.archetypes, rng);
            ComplicationDefinition complication = manualComplicationOverride ?? WeightedPick(database.complications, rng);

            List<QuirkDefinition> quirks = new();
            if (manualQuirkOverrides != null && manualQuirkOverrides.Count > 0)
            {
                quirks.AddRange(manualQuirkOverrides);
            }
            else
            {
                quirks = PickNonConflictingQuirks(database.quirks, rng, randomQuirkCount);
            }

            var result = new MixedPersonality
            {
                archetypeId = archetype?.id,
                complicationId = complication?.id
            };

            foreach (var quirk in quirks)
                result.quirkIds.Add(quirk.id);

            // Compose persona text
            result.personaBlock = ComposePersona(archetype, quirks, complication);

            // Goals
            if (archetype != null && archetype.defaultGoals != null)
                result.goals.AddRange(archetype.defaultGoals);

            return result;
        }

        private static string ComposePersona(
            ArchetypeDefinition archetype,
            List<QuirkDefinition> quirks,
            ComplicationDefinition complication)
        {
            var builder = new StringBuilder(512);

            builder.AppendLine("CHARACTER PERSONA:");
            if (!string.IsNullOrWhiteSpace(archetype?.personaBlock))
            {
                builder.AppendLine(archetype.personaBlock.Trim());
            }

            if (quirks != null && quirks.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("QUIRKS:");
                foreach (var quirk in quirks)
                {
                    if (!string.IsNullOrWhiteSpace(quirk?.personaLine))
                        builder.AppendLine($"- {quirk.personaLine.Trim()}");
                }
            }

            if (!string.IsNullOrWhiteSpace(complication?.constraintText))
            {
                builder.AppendLine();
                builder.AppendLine("COMPLICATION:");
                builder.AppendLine(complication.constraintText.Trim());
            }

            return builder.ToString().Trim();
        }

        private static List<QuirkDefinition> PickNonConflictingQuirks(
            List<QuirkDefinition> candidates,
            Random rng,
            int count)
        {
            var selected = new List<QuirkDefinition>(count);
            if (candidates == null || candidates.Count == 0 || count <= 0)
                return selected;

            // simple attempt loop: try picking until we fill or we give up
            int attemptsRemaining = 50;

            while (selected.Count < count && attemptsRemaining-- > 0)
            {
                QuirkDefinition pick = WeightedPick(candidates, rng);
                if (pick == null) break;

                bool conflicts = false;

                foreach (var existing in selected)
                {
                    if (existing == null) continue;

                    if (existing.conflictsWith != null && existing.conflictsWith.Contains(pick.id))
                        conflicts = true;

                    if (pick.conflictsWith != null && pick.conflictsWith.Contains(existing.id))
                        conflicts = true;

                    if (conflicts) break;
                }

                if (!conflicts && !selected.Contains(pick))
                    selected.Add(pick);
            }

            return selected;
        }

        private static T WeightedPick<T>(List<T> items, Random rng) where T : class
        {
            if (items == null || items.Count == 0) return null;

            int totalWeight = 0;
            foreach (var item in items)
            {
                int weight = GetWeight(item);
                if (weight > 0) totalWeight += weight;
            }

            if (totalWeight <= 0)
                return items[0];

            int roll = rng.Next(0, totalWeight);
            int running = 0;

            foreach (var item in items)
            {
                int weight = GetWeight(item);
                if (weight <= 0) continue;

                running += weight;
                if (roll < running)
                    return item;
            }

            return items[0];
        }

        private static int GetWeight<T>(T item)
        {
            switch (item)
            {
                case ArchetypeDefinition archetype: return archetype.weight;
                case QuirkDefinition quirk: return quirk.weight;
                case ComplicationDefinition complication: return complication.weight;
                default: return 1;
            }
        }

        private static int StableHash(string text)
        {
            // deterministic hash across runs/platforms
            unchecked
            {
                int hash = 23;
                if (text == null) return hash;

                for (int i = 0; i < text.Length; i++)
                    hash = (hash * 31) + text[i];

                return hash;
            }
        }
    }
}