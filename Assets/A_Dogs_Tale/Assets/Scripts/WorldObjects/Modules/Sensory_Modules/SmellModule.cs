using UnityEngine;

[DisallowMultipleComponent]
public class SmellModule : WorldModule
{
    public override void Tick(float deltaTime)
    {
        Debug.Log($"SmellModule {worldObject.DisplayName}: Tick {deltaTime}");
    }
}
