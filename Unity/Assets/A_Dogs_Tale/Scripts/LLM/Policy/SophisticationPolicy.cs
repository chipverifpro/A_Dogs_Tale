using UnityEngine;
using DogGame.LLM.Core;

namespace DogGame.LLM.Policy
{
    public sealed class SophisticationPolicy
    {
        public sealed class Inputs
        {
            public float distanceToPlayerMeters;
            public bool isInCombat;
            public bool isQuestCritical;
            public bool isBoss;
            public bool isPlayerFocusingThisNpc; // e.g. targeted, looked-at, selected
            public int nearbyEntityCount;        // rough crowd/complexity signal
        }

        public Sophistication Evaluate(Inputs inputs)
        {
            if (inputs == null) return Sophistication.Low;

            int score = 0;

            // Proximity: closer -> higher
            if (inputs.distanceToPlayerMeters <= 3f) score += 4;
            else if (inputs.distanceToPlayerMeters <= 10f) score += 2;
            else if (inputs.distanceToPlayerMeters <= 25f) score += 1;

            if (inputs.isPlayerFocusingThisNpc) score += 2;
            if (inputs.isInCombat) score += 3;

            if (inputs.isQuestCritical) score += 3;
            if (inputs.isBoss) score += 4;

            // Complexity bump
            score += Mathf.Clamp(inputs.nearbyEntityCount / 4, 0, 3);

            if (score >= 8) return Sophistication.High;
            if (score >= 4) return Sophistication.Medium;
            return Sophistication.Low;
        }

        public Sophistication ClampByNpcType(Sophistication desired, bool isSimpleCreature)
        {
            // Example: simple creatures never exceed Medium
            if (isSimpleCreature && desired == Sophistication.High)
                return Sophistication.Medium;
            return desired;
        }
    }
}