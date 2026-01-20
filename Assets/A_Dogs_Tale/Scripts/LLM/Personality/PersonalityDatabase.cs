using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.LLM.Personality
{
    [CreateAssetMenu(menuName = "A Dogs Tale/LLM/Personality Database")]
    public sealed class PersonalityDatabase : ScriptableObject
    {
        [Header("Species / Roles")]
        public List<SpeciesDefinition> species = new();
        public List<RoleDefinition> roles = new();

        [Header("Legacy / Optional")]
        public List<ArchetypeDefinition> archetypes = new();

        [Header("Mix-ins")]
        public List<QuirkDefinition> quirks = new();
        public List<ComplicationDefinition> complications = new();
    }
}