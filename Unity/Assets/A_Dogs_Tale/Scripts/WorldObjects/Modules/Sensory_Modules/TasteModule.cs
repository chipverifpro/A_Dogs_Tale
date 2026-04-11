using UnityEngine;
using InspectorTools;

namespace DogGame.Modules
{
    [DisallowMultipleComponent]
    [InspectorNote("Sensory_Modules/Taste Module", "PLACEHOLDER ONLY.",UnityEditor.MessageType.Warning)]
    public class TasteModule : WorldModule
    {
        public bool debugMode = false;

        private int debugDoubleTick = -1;
        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            if (debugMode) Debug.Log($"TasteModule {worldObject.DisplayName}: Tick {deltaTime}");
        }
    }
}