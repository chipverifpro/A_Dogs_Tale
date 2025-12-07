using UnityEngine;

// ----- ABSTRACT BASE CLASS -----

namespace DogGame.Modules
{
    public abstract class QuestModuleBase : WorldModule
    {
        public override void Tick(float deltaTime)
        {
            Debug.Log($"QuestModuleBase {worldObject.DisplayName}: Tick {deltaTime}");
        }

    }
}