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

    [Serializable]
    public sealed class MixedPersonality
    {
        public string archetypeId;
        public List<string> quirkIds = new();
        public string complicationId;

        public string personaBlock;             // final composed persona
        public List<string> goals = new();
    }
}