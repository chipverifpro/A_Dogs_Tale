using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/*
in AudioCatalog.cs...
--AudioClipCfg holds all data and options about an audio track.
    no functions
    
--AudioCatalog is the master list of all known audio tracks.


in AudioPlayer.cs...
--AudioPlayTracking holds data needed to interrupt currently playing tracks.
--SFXPlayer has a pool of AudioSource structures intended to be reused.
*/

public class AudioClipCfg
{
    public string name;
    public string filename;
    public string subtitle;
    public Vector2? volumeRange = null;
    public Object sourceObject = null;
    public Vector3? audioLocation = null; // source of the sound if not attached to a sourceObject
    public string channel = "SFX";
    public Vector2? intervalRange = new(999f, 0f); // no looping (x>y).
                                              // (0,0)           : continuous loop
                                              // (x,y) where x<y : wait between x and y seconds before repeat
                                              // (x,y) where x=y : wait exactly x seconds before repeat         
                                              // (x,y) where x>y : no looping, play once

    public Vector2? pitchRange = Vector2.one; // random from .x=min to .y=max
    public AudioClip clip = null;
    public AudioMixerGroup mixerGroup = null;    // set from channel string above
    public bool deleteWhenDone = false;
    public bool startAfterInterval = true;      // when false, will start with interval wait before first play.

    // List to manage interrupting play.  One entry per currently playing instance.
    public List<AudioPlayTracking> running_Tasks = new();

    // Helpers for decoding interval
    public bool IsPlayOnce()
    {
        float interval_min = (intervalRange?.x) ?? 0f;
        float interval_max = (intervalRange?.y) ?? 0f;
        return (interval_min > interval_max);
    }
    public bool IsContinuousLoop()
    {
        float interval_min = (intervalRange?.x) ?? 0f;
        float interval_max = (intervalRange?.y) ?? 0f;
        return (interval_min == 0f && interval_max == 0f);
    }
}

// class AudioCatalog contains a master list of all audio available
//  to the game.  All that is needed is to call PlayClip() with
//  the name of the track, and it handles everything else.
// It will eventually import this from some configuration file,
//  maybe a .csv, a .json, or from the directory itself.
public class AudioCatalog : MonoBehaviour
{
    public List<AudioClipCfg> clipCfgList = new(10);    // catalog of sounds

    [Header("Object References")]
    public AudioMixerGroups audioMixerGroups;           // mixer channels
    public AudioPlayer audioPlayer;                     // play controls
    public ObjectDirectory dir;

    // call this from AudioPlayer.Awake()
    public void StartAudioCatalog()
    {
        // Initialize class references...
        if (dir == null) dir = FindFirstObjectByType<ObjectDirectory>(FindObjectsInactive.Include);
        if (dir == null)
        {
            Debug.LogError($"AudioCatalog: ObjectDirectory not found");
            return;
        }
        if (audioMixerGroups == null) audioMixerGroups = dir.audioMixerGroups;
        if (audioPlayer == null) audioPlayer = dir.audioPlayer;
    }

    // these should move to someplace more relevant to each.
    public void AddSomeClipsToTheCatalog()
    {
        // add some clips to the catalog...
        AddClipToCatalog(
            name: "Bark_GermanShepherd",
            filename: "Bark_GermanShepherd",
            subtitle: "[Bark (German Shepherd)]",
            sourceObjectName: "AgentGermanShepherd",
            pitchRange: new(.95f, 1.05f)
        );

        AddClipToCatalog(
            name: "Button-Click",
            filename: "Button-Click",
            subtitle: "[Button Click]",
            channel: "UI"
        );

        AddClipToCatalog(
            name: "Opening Title",
            filename: "Curious Whispers",
            subtitle: "[Music Playing]",
            channel: "Music"
        );

        AddClipToCatalog(
            name: "Mission 01",
            filename: "Through the Windowpane",
            subtitle: "[Music Playing]",
            channel: "Music"
        );

    }

    public bool AddClipToCatalog(
            string name,                // REQUIRED
            string filename,            // REQUIRED
            string subtitle = null,
            Vector2? volumeRange = null,
            Vector3? audioLocation = null,
            string sourceObjectName = null,
            string channel = null,
            Vector2? intervalRange = null,
            // intervalRange(0,0)           : continuous loop, no gap (FYI: loop is managed by Unity automagically)
            // intervalRange(x,y) where x<y : wait between x and y seconds before repeat
            // intervalRange(x,y) where x=y : wait exactly x seconds before repeat         
            // intervalRange(x,y) where x>y : no looping, play once (DEFAULT if null)
            bool startAfterInterval = true, // when true, will start with interval wait (if any) before first play.
            Vector2? pitchRange = null, // random from .x=min to .y=max (DEFAULT is (1,1) if null)
            bool deleteWhenDone = false,
            bool preload = false
            )
    {
        AudioClipCfg clipCfg;
        clipCfg = new()
        {
            name = name,
            filename = filename,
            subtitle = subtitle,
            volumeRange = volumeRange,
            // sourceObject = GameObject.Find(sourceObjectName),  // initialized below...
            audioLocation = audioLocation,
            channel = channel,
            intervalRange = intervalRange,
            startAfterInterval = startAfterInterval,
            pitchRange = pitchRange,
            deleteWhenDone = deleteWhenDone
        };

        // --- sanity checks ---
        if (string.IsNullOrEmpty(filename))
        {
            Debug.LogError($"Error: clip filenamename is blank.");
            return false;
        }
        if (string.IsNullOrEmpty(name))
        {
            clipCfg.name = filename;
            Debug.LogWarning($"clip name is blank, falling back to filename {clipCfg.name}");
        }
        // --- set default values for vecor (range) fields ---
        if (pitchRange == null) clipCfg.pitchRange = new(1, 1);     // null default pitch=1
        if (intervalRange == null) clipCfg.intervalRange = new(999f, 0f);     // null default play-once (.x > .y)
        if (volumeRange == null) clipCfg.volumeRange = new(1f, 1f); // null default volume=1

        // --- Force startAfterInterval ---
        if (clipCfg.IsPlayOnce() || clipCfg.IsContinuousLoop())
            clipCfg.startAfterInterval = false;     // override this setting if there is no time interval

        // --- lookup sourceObject ---
        if (sourceObjectName != null && sourceObjectName.Length != 0)
        {
            bool objectFindResult = clipCfg.sourceObject = GameObject.Find(sourceObjectName);
            if (objectFindResult == false)
            {
                Debug.LogError($"Failed to find source object {sourceObjectName} in {channel} entry: {clipCfg.name}.");
                clipCfg.sourceObject = null; // failover is sound not attached to any object.
            }
        }
        else
        {
            clipCfg.sourceObject = null;    // sound intentionally not attached to any object, example: UI, Music, etc.
        }

        // -- check for position and source object, warning ---
        if ((clipCfg.sourceObject != null) && (clipCfg.audioLocation != null))
            Debug.LogWarning($"Warning: in {clipCfg.name} an audioLocation was specified {clipCfg.audioLocation} in addition to a source object {sourceObjectName}.  audioLocation will be ignored.");

        // --- Assign channel and mixer group ---
        if (string.IsNullOrEmpty(clipCfg.channel)) clipCfg.channel = "SFX";

        if (clipCfg.mixerGroup == null)
        {
            clipCfg.mixerGroup = audioMixerGroups.GetMixerGroup(clipCfg.channel);
            if (clipCfg.mixerGroup == null)
            {
                Debug.LogWarning($"[clipCfg]: {clipCfg.name}] No AudioMixerGroup found for channel '{clipCfg.channel}', using fallback SFX.");
                clipCfg.mixerGroup = audioMixerGroups.GetMixerGroup("SFX");
            }
        }

        // --- load audio now or later ---
        if (preload)    // load audio now
        {
            // Attempt clip load
            if (!LoadClip(clipCfg))
            {
                Debug.LogError($"Failed to load {channel} entry: {clipCfg.name}, filename: {clipCfg.filename}");
                return false;
            }
        }
        else // not preload, load audio later
        {
            // Verify is the file is valid without loading it.
            // This does technically instantiate the AudioClip object but doesn’t decode or keep PCM data
            //   in memory if you unload immediately.  For small validation runs at startup, this is perfectly
            //   fine — it’s cheap and reliable.
            var clip = Resources.Load<AudioClip>(GetPathToAudioClip(clipCfg));
            bool exists = (clip != null);
            if (exists)
            {
                Resources.UnloadAsset(clip); // release memory immediately
                clip = null;
            }
            if (!exists)
            {
                // File doesnt exist: not an error, just a warning because we don't need it immediately.
                // Later attempts to play it will just skip the play request with a clip missing warning.
                Debug.LogWarning($"Warning: Failed non-preload {channel} file existance check: {clipCfg.name}, filename: {clipCfg.filename}");
            }
        }

        // --- Determine if clipCfg already existed ---
        // Find entry
        AudioClipCfg clipCfg_old = clipCfgList.Find(e => e.name == name);

        // M2. Merge if it exists
        if (clipCfg_old != null)
        {
            bool success = MergeClipCfg(clipCfg_old, ref clipCfg,
                                        name,
                                        filename,
                                        subtitle,
                                        volumeRange,
                                        audioLocation,
                                        sourceObjectName,
                                        channel,
                                        intervalRange,
                                        startAfterInterval,
                                        pitchRange,
                                        deleteWhenDone,
                                        preload );
            if (success == true)
            {
                UnloadClip(clipCfg_old); // free up memory if any allocated
                clipCfgList.Remove(clipCfg_old);
            }
            else
            {
                return false;
            }
        }

        // --- Add clipCfg to master list
        clipCfgList.Add(clipCfg);

        return true;
    }

    public string GetPathToAudioClip(AudioClipCfg clipCfg)
    {
        return $"Audio/{clipCfg.channel}/{clipCfg.filename}";
    }

    public IEnumerator UnloadClipCfg(AudioClipCfg clipCfg, float fadeOut)
    {
        yield return null;
        if (clipCfg.running_Tasks.Count != 0)
        {
            audioPlayer.StopClips(trackName: clipCfg.name, fadeOut: fadeOut);
            clipCfgList.Remove(clipCfg);    // remove entry from master list (but keep local reference here)
            yield return new WaitUntil(() => clipCfg.running_Tasks.Count == 0);
        }
        if (clipCfg.clip != null) Resources.UnloadAsset(clipCfg.clip);
        clipCfg.clip = null; // break references so GC can clean up
    }

    public bool UnloadClip(AudioClipCfg clipCfg)
    {
        if (clipCfg.clip != null)
        {
            Resources.UnloadAsset(clipCfg.clip);
            clipCfg.clip = null; // break references so GC can clean up
            return true;
        }
        return false;
    }

    /// <summary>
    /// Loads audio clip & assigns AudioMixerGroup based on 'channel'.
    /// Returns true if clip was successfully set.
    /// </summary>
    public bool LoadClip(AudioClipCfg clipCfg)
    {
        // --- Load clip if missing ---
        if (clipCfg.clip == null)
        {
            clipCfg.clip = Resources.Load<AudioClip>(GetPathToAudioClip(clipCfg));
            if (clipCfg.clip == null)
            {
                Debug.LogError($"[SFX_Entry: {clipCfg.name}] Clip not found at Resources/Audio/{clipCfg.channel}/{clipCfg.filename}");
                return false;
            }
        }

        return true;
    }

    // MergeClipCfg(old, ref new):
    //   If any fields are not specifically set by non-default parameters,
    //   then copy relevant values from clipCfg_old to clipCfg_new.
    // Exceptions:
    //   Not allowed to change:        name
    //   Old value ignored, keep new:  filename, startAfterInterval, deleteWhenDone, preload
    //   
    // Commentary: This routine has been difficult to get right, and has no known necessary usage case.
    //             I'd recommend if the fields change, this breaks or needs update, just scrap it instead.
    public bool MergeClipCfg(AudioClipCfg clipCfg_old, ref AudioClipCfg clipCfg,
            string name,                // equivalent or this function wouldn't be relevant
            string filename,            // keep new
            string subtitle,
            Vector2? volumeRange,
            Vector3? audioLocation,
            string sourceObjectName,
            string channel,
            Vector2? intervalRange,
            bool startAfterInterval,    // old ignored, keep new
            Vector2? pitchRange,
            bool deleteWhenDone,        // old ignored, keep new
            bool preload)               // old ignored, keep new
    {
        //
        if (!string.IsNullOrEmpty(subtitle)) clipCfg.subtitle = clipCfg_old.subtitle;
        if (volumeRange != null) clipCfg.volumeRange = clipCfg_old.volumeRange;
        if (audioLocation != null) clipCfg.audioLocation = clipCfg_old.audioLocation;
        if (!string.IsNullOrEmpty(sourceObjectName)) clipCfg.sourceObject = clipCfg_old.sourceObject;
        if (!string.IsNullOrEmpty(channel)) clipCfg.channel = clipCfg_old.channel;
        if (intervalRange != null) clipCfg.intervalRange = clipCfg_old.intervalRange;
        // startAfterInterval -> keep new
        if (pitchRange != null) clipCfg.pitchRange = clipCfg_old.pitchRange;
        // deleteWhenDone -> keep new
        if ((preload == true) && (clipCfg.clip == null) && (clipCfg.filename != clipCfg_old.filename)) clipCfg.clip = clipCfg_old.clip;
        if (!string.IsNullOrEmpty(channel)) clipCfg.mixerGroup = clipCfg_old.mixerGroup;
        // transfer any old running_Tasks...
        clipCfg.running_Tasks = clipCfg_old.running_Tasks;
        if ((preload == true) && (clipCfg.filename != clipCfg_old.filename))
            UnloadClip(clipCfg_old);

        Debug.Log($"[MergeClip] '{clipCfg.name}' complete.");
        return true;
    }
}
