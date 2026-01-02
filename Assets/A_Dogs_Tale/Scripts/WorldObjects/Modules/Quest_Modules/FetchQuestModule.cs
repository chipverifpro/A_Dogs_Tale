using UnityEngine;

namespace DogGame.Modules
{
    [DisallowMultipleComponent]
    public class FetchQuestModule : QuestModuleBase
    {
        private int debugDoubleTick = -1;
        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            Debug.Log($"FetchQuestModule {worldObject.DisplayName}: Tick {deltaTime}");
        }
    }
}