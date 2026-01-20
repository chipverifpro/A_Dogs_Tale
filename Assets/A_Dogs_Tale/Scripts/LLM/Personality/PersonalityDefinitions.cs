using System;
using System.Collections.Generic;

namespace DogGame.LLM.Personality
{
    [Serializable]
    public sealed class ArchetypeDefinition
    {
        public string id;
        public int weight = 1;
        public string personaBlock;             // larger chunk
        public List<string> defaultGoals = new();
    }

    [Serializable]
    public sealed class QuirkDefinition
    {
        public string id;
        public int weight = 1;
        public string personaLine;              // short spice line
        public List<string> doDontRules = new(); // optional
        public List<string> conflictsWith = new();
    }

    [Serializable]
    public sealed class ComplicationDefinition
    {
        public string id;
        public int weight = 1;
        public string constraintText;
    }

#nullable enable
    [Serializable]
    public sealed class MixedPersonality
    {
        public string? speciesId;
        public string? roleId;

        // legacy (optional)
        public string? archetypeId;

        public string? complicationId;

        public List<string> quirkIds = new();
        public List<string> goals = new();

        public string personaBlock = "";

        public string DebugSummary()
        {
            string species = string.IsNullOrWhiteSpace(speciesId) ? "none" : speciesId;
            string role = string.IsNullOrWhiteSpace(roleId) ? "none" : roleId;
            string legacy = string.IsNullOrWhiteSpace(archetypeId) ? "" : $" legacyArchetype={archetypeId}";
            string complication = string.IsNullOrWhiteSpace(complicationId) ? "none" : complicationId;

            return $"species={species} role={role}{legacy} complication={complication} quirks={quirkIds.Count} goals={goals.Count}";
        }
    }
}