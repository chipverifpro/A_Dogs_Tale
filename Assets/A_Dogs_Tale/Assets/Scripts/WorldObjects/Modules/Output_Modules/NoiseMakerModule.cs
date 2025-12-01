using UnityEngine;

public class NoiseMakerModule : WorldModule
{
    public void Bark()
    {
        dir.audioPlayer.PlayClip("Bark_GS_once");
    }
}
