#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.LLM.Personality
{
    [Serializable]
    public sealed class SpeciesDefinition
    {
        public string id = "dog";
        public int weight = 1;

        [TextArea(2, 10)]
        public string speciesBlock =
            "SPECIES: Dog\n" +
            "- Strong scent + hearing, weaker vision.\n" +
            "- Communicates via body language, barks, movement.\n" +
            "- Safety, curiosity, pack bonds matter.";

        public List<string> defaultGoals = new();
    }
}