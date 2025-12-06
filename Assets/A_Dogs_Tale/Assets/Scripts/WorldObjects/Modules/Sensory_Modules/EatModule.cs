using UnityEngine;

[DisallowMultipleComponent]
public class EatModule : WorldModule
{
    public override void Tick(float deltaTime)
    {
        Debug.Log($"EatModule {worldObject.DisplayName}: Tick {deltaTime}");
    }
}
