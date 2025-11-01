using UnityEngine;
using System.Collections;
using System;

//
// Obsolete file, replaced by AudioPlayer and AudioCatalog
//

[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    public string titleAudioFileName = "Music/Curious Whispers";
    public string exploreAudioFileName = "Music/Through the Windowpane";
    public float maxVolume = 0.1f;
    public float fadeInDuration = 2f;
    public float fadeOutDuration = 2f;

    private AudioSource audioSource;

    void Start()
    {
        StartCoroutine(StartMusicCoroutine(titleAudioFileName, fadeOut:false, fadeIn:false));
    }

    public void StartMusic(String musicFilename, bool fadeOut = false, bool fadeIn = false)
    {
        StartCoroutine(StartMusicCoroutine(musicFilename, fadeOut, fadeIn));
    }

    public IEnumerator StartMusicCoroutine(String musicFilename, bool fadeOut = false, bool fadeIn = false)
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        Debug.Log($"StartMusic({musicFilename})");

        // Optionally fade out old music
        if (fadeOut)
        {
            yield return StartCoroutine(FadeOutMusic(fadeOutDuration));
        }
        else
        {
            audioSource.Stop();
        }

        // Load new music from Resources/Audio/
        AudioClip clip = Resources.Load<AudioClip>("Audio/Music/" + musicFilename);
        if (clip == null)
        {
            Debug.LogError($"[TitleMusicPlayer] Could not find audio file: Resources/Audio/Music/{musicFilename}");
            yield break;
        }

        audioSource.clip = clip;
        audioSource.Play();
        if (fadeIn)
        {
            audioSource.volume = 0;
            yield return StartCoroutine(FadeInMusic(maxVolume, fadeInDuration));
        }
        audioSource.volume = maxVolume;
    }

    IEnumerator FadeInMusic(float targetVolume, float duration)
    {
        Debug.Log($"FadeInMusic({targetVolume}, {duration})");

        float startTime = Time.time;
        while (audioSource.volume < targetVolume)
        {
            audioSource.volume = Mathf.Lerp(0f, targetVolume, (Time.time - startTime) / duration);
            yield return null;
        }
        audioSource.volume = targetVolume;
    }

    IEnumerator FadeOutMusic(float duration)
    {
        float startVolume = audioSource.volume;
        float startTime = Time.time;

        while (audioSource.volume > 0f)
        {
            audioSource.volume = Mathf.Lerp(startVolume, 0f, (Time.time - startTime) / duration);
            yield return null;
        }

        audioSource.Stop();
        audioSource.volume = 0f;
    }
}