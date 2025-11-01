using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/*. -- OLD DESCRIPTIONS --
in AudioCatalog.cs...
--SFX_AudioGroups is just a holder for the AudioMixerGroup controls.
--SFX_Entry holds all data and options about a audio track.
--SFX_Catalog is the master list of all known audio tracks.


in AudioPlayer.cs...
--AudioPlayTracking holds data needed to interrupt currently playing tracks.
--AudioPlayer has a pool of AudioSource structures intended to be reused.
*/


// Track all active audio tasks for easy cancel/cleanup
public class AudioPlayTracking
{
    public AudioSource src;         // src
    public GameObject go;           // go
    public bool isTempGO;           // singleton
    public Coroutine loopCo;        // loop/play control
    public bool isPlaying = false;
    public bool stopRepeating = false; // stop at end of this track

    //see comment in SFX_Catalog.LookupAndStopAudioPlaying() for 'fadeout' options
    public IEnumerator Abort_SFX(AudioClipCfg entry, float fadeout)
    {
        // kill the driving loop coroutine
        if (loopCo != null)
        {
            AudioPlayer.Instance.StopCoroutine(loopCo);
            loopCo = null;
        }

        if (fadeout < 0) // allow the current play to complete.
        {
            src.loop = false;
            yield return (src.isPlaying == false);
            src.Stop(); // not really necessary, it is done.
        }
        else    // don't let it complete, stop or fade out now.
        {
            // fade out and stop the audio
            if (src != null)
            {
                float startVol = src.volume;
                float t = 0;
                while ((t < fadeout) && (src != null))
                {
                    t += Time.deltaTime;
                    src.volume = Mathf.Lerp(startVol, 0f, t / fadeout);
                    yield return null;
                }
                if (src != null)
                {
                    src.volume = 0f;
                    src.Stop();
                    //src.volume = startVol;  //???
                    src = null;
                }
            }

            // destroy the game object if it is temporary
            if ((isTempGO == true) && (go != null))
            {
                Object.Destroy(go);
                go = null;
            }
        }
    }
}


public partial class AudioPlayer : MonoBehaviour
{
    public AudioCatalog catalog;

    // PlayClip() finds the audio clip in the master catalog (clipCfgList),
    //  configures everything, and then launches the Coroutine
    //  PlayWithInterval() which starts the play and follows
    //  it through it's lifecycle.
    // Each entry in the master catalog (a unique "clipCfg") maintains
    //  a list of currently playing copies so they can be ended
    //  anytime needed.
    bool PlayClip(string name)
    {
        // 1. Find entry
        AudioClipCfg clipCfg = catalog.clipCfgList.Find(e => e.name == name);

        if (clipCfg == null)
        {
            Debug.LogWarning($"[SFX_Catalog] SFX '{name}' not found in clipCfgList.");
            return false;
        }

        // 2. Ensure clip loaded
        if (clipCfg.clip == null)
        {
            if (!catalog.LoadClip(clipCfg)) // Load clip
            {
                Debug.LogError($"Failed to load {clipCfg.channel} entry: {clipCfg.name} with file {clipCfg.filename}.");
                return false;
            }
        }

        // 2.1. Ensure channel is valid
        if (clipCfg.group == null)
        {
            Debug.LogError($"{clipCfg.channel} was not matched to a group entry: {clipCfg.name}.");
            return false;
        }

        // 2.2. Register this task so it can be terminated if needed.
        AudioPlayTracking taskInfo = new();
        clipCfg.running_Tasks.Add(taskInfo);

        // 3. Get playback object (GameObject) and source (AudioSource) 
        AudioSource src = null;
        GameObject go = null;

        if (clipCfg.sourceObject != null)
        {
            // a real object will be used as the source location of the sound
            go = clipCfg.sourceObject as GameObject;
            src = go.GetComponent<AudioSource>();
            if (src == null) src = go.AddComponent<AudioSource>();
            taskInfo.go = go;
            taskInfo.src = src;
            taskInfo.isTempGO = false; // a real object is used
        }
        else
        {
            // a temporary object will be used as the source of the sound
            go = new GameObject($"{clipCfg.channel}_{name}_Temp");
            src = go.AddComponent<AudioSource>();
            // if audioLocation is specified, use it, otherwise use 2D sound for non-localized sound
            if (clipCfg.audioLocation == null)
            {
                // set sound location by moving the temporary GameObject
                go.transform.position = clipCfg.audioLocation ?? Vector3.zero;
            }
            else
            {
                // no sound location specified
                src.spatialBlend = 0f; // 2D sound for non-localized sounds like UI
            }
            taskInfo.go = go;
            taskInfo.src = src;
            taskInfo.isTempGO = true; // a new GameObject was created so make ready to delete it when done.
        }

        // 4. Mixer group
        src.outputAudioMixerGroup = clipCfg.group;

        // 5. Volume and pitch
        src.volume = clipCfg.relative_volume;
        float pitch_min = (clipCfg.pitchRange?.x) ?? 1f;
        float pitch_max = (clipCfg.pitchRange?.y) ?? 1f;
        src.pitch = UnityEngine.Random.Range(pitch_min, pitch_max);

        // 6. Set clip
        src.clip = clipCfg.clip;
        src.loop = (clipCfg.interval == Vector2.zero); // Only use Unity's loop feature if interval is zero.

        // 7. Play once or looped
        // Use coroutine looping with fixed/random intervals, or one-time playback
        Coroutine co;
        co = AudioPlayer.Instance.StartCoroutine(PlayWithInterval(src, clipCfg, taskInfo));
        taskInfo.loopCo = co; // save coroutine ID for manual kills

        return true;    // successfully started playing.
    }

    // PlayWithInterval() will handle all cases of plaing a track.
    //   whether single time, continuous repeat, or
    //   repeat forever with fixed or random pauses between.
    // A TaskInfo structure is maintained so that tasks and audio 
    //   can be stopped if needed (ie. scene change, sound source disappears, etc)
    IEnumerator PlayWithInterval(AudioSource src, AudioClipCfg clipCfg, AudioPlayTracking taskInfo)
    {
        yield return null;  // avoid race condition with caller function

        while (!taskInfo.stopRepeating)
        {
            // Play the sound
            src.Play();
            taskInfo.isPlaying = true;

            if (src.loop)
            {
                // a continuously looping track will never end,
                //  so go ahead and exit this task.  The taskInfo
                //  will remain as a means of manually stopping it.
                taskInfo.loopCo = null;
                yield break;
            }
            // Wait for the clip to end
            float clipLength = clipCfg.clip.length / Mathf.Abs(src.pitch);
            yield return new WaitForSeconds(clipLength);

            // done playing]
            src.Stop();     // not necessary, it should already stop itself.
            taskInfo.isPlaying = false;

            // if we were requested to stop at end of track, then cleanup and end this coroutine
            if (taskInfo.stopRepeating)
                break;

            float interval_min = (clipCfg.pitchRange?.x) ?? 0f;
            float interval_max = (clipCfg.pitchRange?.y) ?? 0f;
            // wait for interval before restarting
            if (interval_min <= interval_max)
            {
                // Delay random interval before beginning to play again.

                float delay = UnityEngine.Random.Range(interval_min, interval_max);
                yield return new WaitForSeconds(delay);
            }
            else    // (interval_min > interval_max) is a special code that means no repeat
            {
                break;    // played once, now stop looping
            }
        } // end while true

        // DONE
        // cleanup temporary GameObject if we created one.
        if (taskInfo.isTempGO && taskInfo.go)
            Object.Destroy(taskInfo.go);

        // remove us from the running_Tasks list
        taskInfo.loopCo = null; // We are exiting, so don't need to keep this coroutine id anymore.  Not necessary, whole structure will be released on the next line.
        clipCfg.running_Tasks.Remove(taskInfo);
    }

    // StopAudioPlaying has several options...
    //  Stop all
    //  Stop all from a GameObject
    //  Stop all with a particular name
    //  Stop all on a particular channel (ie Music, UI, SFX, ...)
    //  Stop all from all temporary GameObjects
    // Can end in one of these ways:
    //  Immediate cutoff of playing. (fadeOut = 0)
    //  Fade out and then stop.      (fadeOut = # seconds)
    //  Allow to finish in-progress track, but don't start again. (fadeOut < 0)
    public void LookupAndStopAudioPlaying(GameObject go = null, bool tempGO = false, string trackName = null, string channelName = null, float fadeOut = 0f)
    {
        // decide what to identify tracks to stop...
        bool stop_by_go = go != null;
        bool stop_by_temp_go = tempGO;
        bool stop_by_name = !string.IsNullOrEmpty(trackName);
        bool stop_by_channel = !string.IsNullOrEmpty(channelName);
        bool stop_everything = !(stop_by_go || stop_by_temp_go || stop_by_name || stop_by_channel);

        // check all playing audio tracks
        for (int snum = catalog.clipCfgList.Count; snum >= 0; snum--)
        {
            AudioClipCfg clipCfg = catalog.clipCfgList[snum];
            for (int tnum = clipCfg.running_Tasks.Count; tnum >= 0; tnum--)
            {
                AudioPlayTracking task = clipCfg.running_Tasks[tnum];

                // Should we stop this task?  Does it meet any criteria?
                bool stop_it =
                    stop_everything ||
                    (stop_by_go && task.go == go) ||
                    (stop_by_temp_go && task.isTempGO) ||
                    (stop_by_name && clipCfg.name == trackName) ||
                    (stop_by_channel && clipCfg.channel == channelName);

                if (stop_it)
                {
                    // Stop the task
                    StartCoroutine(task.Abort_SFX(clipCfg, fadeOut));

                    // Remove task from the running tasks list
                    clipCfg.running_Tasks.Remove(task);
                }
            }
        }
    }
}

// same class... continued.  The below code may still need some work.

// optimized to keep a pool of audio sources to minimize garbage collection.
public partial class AudioPlayer : MonoBehaviour
{
    [SerializeField] int initialSize = 8;
    [SerializeField] AudioMixerGroup mixerGroup;

    readonly List<AudioSource> pool = new();

    public static AudioPlayer Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // prevent duplicates
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // optional: keep across scenes

        for (int i = 0; i < initialSize; i++) pool.Add(NewSource());
    }

    public AudioClip NewClip(string filename)
    {
        // Load new audio from Resources/Audio/
        AudioClip clip = Resources.Load<AudioClip>(filename);
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

    public IEnumerator RandomRepeatSFX(string filename, float minVol = 0.05f, float maxVol = 0.15f, float MinTime = 5f, float MaxTime = 15f)
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