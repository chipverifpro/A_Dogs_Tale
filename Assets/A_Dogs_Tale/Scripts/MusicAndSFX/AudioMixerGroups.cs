using UnityEngine;
using UnityEngine.Audio;

public class AudioMixerGroups : MonoBehaviour
{
    [Header("Object References")]
    // Register your groups here (drag in via Inspector)
    public AudioMixerGroup SFX;
    public AudioMixerGroup UI;
    public AudioMixerGroup Music;
    public AudioMixerGroup Voices;
    public AudioMixerGroup Ambient;

    // You can extend this with more categories
    public AudioMixerGroup GetMixerGroup(string channel)
    {
        switch (channel.ToUpperInvariant())
        {
            case "MUSIC": return Music;
            case "UI": return UI;
            case "VOICES": return Voices;
            case "AMBIENT": return Ambient;
            default: return SFX; // default/fallback
        }
    }
}