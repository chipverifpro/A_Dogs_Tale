using UnityEngine;

[DisallowMultipleComponent]
public class HearingModule : WorldModule
{
    public override void Tick(float deltaTime)
    {
        Debug.Log($"HearingModule {worldObject.DisplayName}: Tick {deltaTime}");
    }
}
