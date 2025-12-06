using UnityEngine;

[DisallowMultipleComponent]
public class FetchQuestModule : QuestModuleBase
{
    public override void Tick(float deltaTime)
    {
        Debug.Log($"FetchQuestModule {worldObject.DisplayName}: Tick {deltaTime}");
    }
}
