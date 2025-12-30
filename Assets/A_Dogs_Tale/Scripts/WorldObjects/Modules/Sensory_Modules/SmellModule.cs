using UnityEngine;

namespace DogGame.Modules
{
    [DisallowMultipleComponent]
    public class SmellModule : WorldModule
    {
        public bool debugMode = false;

        public override void Tick(float deltaTime)
        {
            if (debugMode) Debug.Log($"SmellModule {worldObject.DisplayName}: Tick {deltaTime}");
        }
    }
}