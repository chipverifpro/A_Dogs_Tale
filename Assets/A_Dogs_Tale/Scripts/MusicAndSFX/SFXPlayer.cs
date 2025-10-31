using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public static class SFX_AudioGroups
{
    // Register your groups here (drag in via Inspector)
    public static AudioMixerGroup SFX;
    public static AudioMixerGroup UI;
    public static AudioMixerGroup Music;
    public static AudioMixerGroup Voices;
    public static AudioMixerGroup Ambient;

    // You can extend this with more categories
    public static AudioMixerGroup GetGroup(string channel)
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

// Track all active audio tasks for easy cancel/cleanup
public class SFX_Tasks
{
    //public SFX_Entry entry;       // sfx (now parent of this)
    public AudioSource src;         // src
    public GameObject go;           // go
    public bool isTempGO;           // singleton
    public Coroutine loopCo;        // loop/play control
    public bool isPlaying = false;
    public bool stopRepeating = false; // stop at end of this track

    //see comment in SFX_Catalog.LookupAndStopAudioPlaying() for 'fadeout' options
    public IEnumerator Abort_SFX(SFX_Entry entry, float fadeout)
    {
        // kill the driving loop coroutine
        if (loopCo != null)
        {
            SFXPlayer.Instance.StopCoroutine(loopCo);
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


public class SFX_Entry
{
    public string name;
    public string filename;
    public string subtitle;
    public float relative_volume = 1.0f;
    public Object sourceObject = null;
    public string channel = "SFX";
    public Vector2 interval = new(999f, 0f); // no looping (x>y).
                                             // (0,0)           : continuous loop
                                             // (x,y) where x<y : wait between x and y seconds before repeat
                                             // (x,y) where x=y : wait exactly x seconds before repeat         
                                             // (x,y) where x>y : no looping, play once

    public Vector2 pitchRange = Vector2.one; // random from .x=min to .y=max
    public AudioClip clip = null;
    public AudioMixerGroup group = null;

    // Lists to manage interrupting play.
    public List<SFX_Tasks> running_Tasks = new();
    //public List<Coroutine> running_Coroutines = new();
    //public List<AudioSource> running_AudioSources = new();
    //public List<GameObject> running_GameObjects = new();

    public bool localized => sourceObject != null;

    /// <summary>
    /// Loads audio clip & assigns AudioMixerGroup based on 'channel'.
    /// Returns true if both clip and group were successfully set.
    /// </summary>
    public bool SetClipAndGroup()
    {
        // --- Load clip if missing ---
        if (clip == null)
        {
            clip = Resources.Load<AudioClip>($"Audio/{channel}/{filename}");
            if (clip == null)
            {
                Debug.LogError($"[SFX_Entry: {name}] Clip not found at Resources/Audio/{channel}/{filename}");
                return false;
            }
        }

        // --- Assign mixer group ---
        if (group == null)
        {
            group = SFX_AudioGroups.GetGroup(channel);
            if (group == null)
            {
                Debug.LogWarning($"[SFX_Entry: {name}] No AudioMixerGroup found for channel '{channel}', using fallback.");
                return clip != null; // clip still valid even if group missing
            }
        }

        return true;
    }

    public bool LoadClip()
    {
        if (clip == null)
            clip = Resources.Load<AudioClip>($"Audio/{channel}/{filename}");
        return (clip != null);
    }
}


// class SFX_Catalog contains a master list of all audio available
//  to the game.  All that is needed is to call PlayClip() with
//  the name of the track, and it handles everything else.
public class SFX_Catalog : MonoBehaviour
{
    public List<SFX_Entry> sfx_List;    // catalog of sounds
 
    void Awake()
    {
        SFX_Entry sfx;
        // --------------------
        sfx = new()
        {
            name = "Bark_GermanShepherd",
            filename = "Bark_GermanShepherd",
            subtitle = "[Bark (German Shepherd)]",
            sourceObject = GameObject.Find("AgentGermanShepherd"),
            pitchRange = new Vector2(.95f, 1.05f)
        };
        // Attempt load + group setup
        if (!sfx.SetClipAndGroup())
            Debug.LogError($"Failed to initialize SFX entry: {sfx.name}");
        else
            sfx_List.Add(sfx);
        // --------------------
        sfx = new()
        {
            name = "Button-Click",
            filename = "Button-Click",
            subtitle = "[Button Click]",
            channel = "UI"
        };
        // Attempt load + group setup
        if (!sfx.SetClipAndGroup())
            Debug.LogError($"Failed to initialize SFX entry: {sfx.name}");
        else
            sfx_List.Add(sfx);
        // --------------------
        sfx = new()
        {
            name = "Opening Title",
            filename = "Curious Whispers",
            subtitle = "[Music Playing]",
            channel = "Music"
        };
        // Attempt load + group setup
        if (!sfx.SetClipAndGroup())
            Debug.LogError($"Failed to initialize SFX entry: {sfx.name}");
        else
            sfx_List.Add(sfx);
        // --------------------
        sfx = new()
        {
            name = "Mission 01",
            filename = "Through the Windowpane",
            subtitle = "[Music Playing]",
            channel = "Music"
        };
        // Attempt load + group setup
        if (!sfx.SetClipAndGroup())
            Debug.LogError($"Failed to initialize SFX entry: {sfx.name}");
        else
            sfx_List.Add(sfx);
        // --------------------
    }

    // PlayClip() finds the audio clip in the master catalog (sfx_List),
    //  configures everything, and then launches the Coroutine
    //  PlayWithInterval() which starts the play and follows
    //  it through it's lifecycle.
    // Each entry in the master catalog (a unique "sfx") maintains
    //  a list of currently playing copies so they can be ended
    //  anytime needed.
    bool PlayClip(string name)
    {
        // 1. Find entry
        SFX_Entry sfx = sfx_List.Find(e => e.name == name);

        if (sfx == null)
        {
            Debug.LogWarning($"[SFX_Catalog] SFX '{name}' not found in sfx_List.");
            return false;
        }

        // 2. Ensure clip and mixer group loaded
        if ((sfx.clip == null) || (sfx.group == null))
        {
            if (!sfx.SetClipAndGroup()) // Load clip and assign mixer group
            {
                Debug.LogError($"[SFX_Catalog] Failed to initialize SFX entry: {sfx.name}");
                return false;
            }
        }

        // 2.1. Register this task so it can be terminated if needed.
        SFX_Tasks taskInfo = new();
        sfx.running_Tasks.Add(taskInfo);

        // 3. Determine playback object
        AudioSource src = null;
        GameObject go = null;

        if (sfx.localized && sfx.sourceObject != null)
        {
            go = sfx.sourceObject as GameObject;
            src = go.GetComponent<AudioSource>();
            if (src == null) src = go.AddComponent<AudioSource>();
            taskInfo.go = go;
            taskInfo.src = src;
            taskInfo.isTempGO = false; // a real object is used
        }
        else
        {
            go = new GameObject($"{sfx.channel}_{name}_Temp");
            src = go.AddComponent<AudioSource>();
            go.transform.position = Vector3.zero;
            src.spatialBlend = 0f; // 2D sound for non-localized
            taskInfo.go = go;
            taskInfo.src = src;
            taskInfo.isTempGO = true; // a new object was created
        }

        // 4. Mixer group
        src.outputAudioMixerGroup = sfx.group;

        // 5. Volume and pitch
        src.volume = sfx.relative_volume;
        src.pitch = UnityEngine.Random.Range(sfx.pitchRange.x, sfx.pitchRange.y);

        // 6. Set clip
        src.clip = sfx.clip;
        src.loop = (sfx.interval == Vector2.zero); // Only use Unity's loop feature if interval is zero.

        // 7. Play once or looped
        // Use coroutine looping with fixed/random intervals, or one-time playback
        Coroutine co;
        co = SFXPlayer.Instance.StartCoroutine(PlayWithInterval(src, sfx, taskInfo));
        taskInfo.loopCo = co; // save coroutine ID for manual kills

        return true;    // successfully started playing.
    }

    // PlayWithInterval() will handle all cases of plaing a track.
    //   whether single time, continuous repeat, or
    //   repeat forever with fixed or random pauses between.
    // A TaskInfo structure is maintained so that tasks and audio 
    //   can be stopped if needed (ie. scene change, sound source disappears, etc)
    IEnumerator PlayWithInterval(AudioSource src, SFX_Entry sfx, SFX_Tasks taskInfo)
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
            float clipLength = sfx.clip.length / Mathf.Abs(src.pitch);
            yield return new WaitForSeconds(clipLength);

            // done playing]
            src.Stop();     // not necessary, it should already stop itself.
            taskInfo.isPlaying = false;

            // if we were requested to stop at end of track, then cleanup and end this coroutine
            if (taskInfo.stopRepeating)
                break;

            // wait for interval before restarting
            if (sfx.interval.x <= sfx.interval.y)
            {
                // Delay random interval before beginning to play again.
                float delay = UnityEngine.Random.Range(sfx.interval.x, sfx.interval.y);
                yield return new WaitForSeconds(delay);
            }
            else    // (interval.x > interval.y) is a special code that means no repeat
            {
                break;    // played once, now stop
            }
        } // end while true

        // DONE
        // cleanup temporary GameObject if we created one.
        if (taskInfo.isTempGO && taskInfo.go)
            Object.Destroy(taskInfo.go);

        // remove us from the running_Tasks list
        taskInfo.loopCo = null; // We are exiting, so don't need to keep this coroutine id anymore.  Not necessary, whole structure will be released on the next line.
        sfx.running_Tasks.Remove(taskInfo);
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
        for (int snum = sfx_List.Count; snum >= 0; snum--)
        {
            SFX_Entry sfx = sfx_List[snum];
            for (int tnum = sfx.running_Tasks.Count; tnum >= 0; tnum--)
            {
                SFX_Tasks task = sfx.running_Tasks[tnum];

                // Should we stop this task?  Does it meet any criteria?
                bool stop_it =
                    stop_everything ||
                    (stop_by_go && task.go == go) ||
                    (stop_by_temp_go && task.isTempGO) ||
                    (stop_by_name && sfx.name == trackName) ||
                    (stop_by_channel && sfx.channel == channelName);

                if (stop_it)
                {
                    // Stop the task
                    StartCoroutine(task.Abort_SFX(sfx, fadeOut));

                    // Remove task from the running tasks list
                    sfx.running_Tasks.Remove(task);
                }
            }
        }
    }

}



// optimized to keep a pool of audio sources to minimize garbage collection.
public class SFXPlayer : MonoBehaviour
{
    [SerializeField] int initialSize = 8;
    [SerializeField] AudioMixerGroup mixerGroup;

    readonly List<AudioSource> pool = new();

    public static SFXPlayer Instance { get; private set; }

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