using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// optimized to keep a pool of audio sources to minimize garbage collection.
public class SFXPlayer : MonoBehaviour
{
    [SerializeField] int initialSize = 8;
    [SerializeField] AudioMixerGroup mixerGroup;

    readonly List<AudioSource> pool = new();

    void Awake()
    {
        for (int i = 0; i < initialSize; i++) pool.Add(NewSource());
    }

    public AudioClip NewClip(string filename)
    {
        // Load new audio from Resources/Audio/
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + filename);
        if (clip == null)
        {
            Debug.LogError($"[NewClip] Could not find audio file: Resources/Audio/{filename}");
            return clip;
        }
        return clip;
    }

    AudioSource NewSource()
    {
        var go = new GameObject("SFX_Source");
        go.transform.SetParent(transform);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;
        s.outputAudioMixerGroup = mixerGroup;
        s.spatialBlend = 1f; // 3D
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 2f; s.maxDistance = 25f;
        return s;
    }

    AudioSource GetFree()
    {
        foreach (var s in pool) if (!s.isPlaying) return s;
        var extra = NewSource(); pool.Add(extra); return extra; // grow if needed
    }

    public void PlayAt(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f)
    {
        var s = GetFree();
        s.transform.position = pos;
        s.pitch = pitch;
        s.volume = volume;
        s.clip = clip;

        // precise scheduling avoids clicks when stacking many sounds
        double t = AudioSettings.dspTime + 0.005;
        s.SetScheduledStartTime(t);
        s.PlayScheduled(t);

        // optional: stop when done (if you need to guarantee release timing)
        StartCoroutine(StopWhenFinished(s));
    }

    // not sure if this is correct...
    public void PlayNonLocalized(AudioClip clip, float volume = 1f)
    {
        var s = GetFree();
        s.PlayOneShot(clip, volume);
    }


    public IEnumerator StopWhenFinished(AudioSource s)
    {
        // small pad for pitch variations
        yield return new WaitForSeconds((s.clip.length / Mathf.Max(0.01f, s.pitch)) + 0.05f);
        s.Stop();
    }

    public IEnumerator RandomRepeatSFX(string filename, float minVol=0.05f, float maxVol=0.15f, float MinTime = 5f, float MaxTime = 15f)
    {
        AudioClip clip;
        clip = NewClip(filename);
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(MinTime, MaxTime));
            PlayNonLocalized(clip, Random.Range(minVol, maxVol));
        }
    }
}

/*
USAGE: 

// e.g., when dog barks at position:
SFXPlayer.PlayAt(barkClip, dog.transform.position, volume:0.9f, pitch:Random.Range(0.95f,1.05f));

// for non-localized sounds (GUI clicks, etc):
uiAudioSource.PlayOneShot(clickClip, 0.8f);

*/