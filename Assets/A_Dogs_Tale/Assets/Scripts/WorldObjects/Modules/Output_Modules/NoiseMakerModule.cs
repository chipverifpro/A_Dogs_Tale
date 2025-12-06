using UnityEngine;

[DisallowMultipleComponent]
public class NoiseMakerModule : WorldModule
{
    public void Bark()
    {
        dir.audioPlayer.PlayClip("Bark_GS_once");
    }
}
