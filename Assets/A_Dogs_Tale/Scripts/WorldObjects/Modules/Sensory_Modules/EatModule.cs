using UnityEngine;

namespace DogGame.Modules
{
    [DisallowMultipleComponent]
    public class EatModule : WorldModule
    {
        public bool debugMode = false;

        public override void Tick(float deltaTime)
        {
            if (debugMode) Debug.Log($"EatModule {worldObject.DisplayName}: Tick {deltaTime}");
        }
    }
}