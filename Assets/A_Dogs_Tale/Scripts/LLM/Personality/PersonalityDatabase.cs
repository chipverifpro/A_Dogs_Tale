using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.LLM.Personality
{
    [CreateAssetMenu(menuName = "A Dogs Tale/LLM/Personality Database")]
    public sealed class PersonalityDatabase : ScriptableObject
    {
        public List<ArchetypeDefinition> archetypes = new();
        public List<QuirkDefinition> quirks = new();
        public List<ComplicationDefinition> complications = new();
    }
}