using UnityEngine;

namespace DogGame.Modules
{
    [DisallowMultipleComponent]
    public class HearingModule : WorldModule
    {
        public bool debugMode = false;

        public override void Tick(float deltaTime)
        {
            if (debugMode) Debug.Log($"HearingModule {worldObject.DisplayName}: Tick {deltaTime}");
        }
    }
}