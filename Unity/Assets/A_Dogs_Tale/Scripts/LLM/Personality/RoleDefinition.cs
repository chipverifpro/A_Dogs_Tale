#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.LLM.Personality
{
    [Serializable]
    public sealed class RoleDefinition
    {
        public string id = "guard";
        public int weight = 1;

        [TextArea(2, 10)]
        public string roleBlock =
            "ROLE: Guard\n" +
            "- Protect occupants and territory.\n" +
            "- Prefer observe -> warn -> intercept -> alarm.";

        public List<string> defaultGoals = new();
    }
}