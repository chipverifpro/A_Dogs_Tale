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
    public AudioMixerGroup group = null;    // set from channel string above
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
    public List<AudioClipCfg> clipCfgList = new(32);    // catalog of sounds
    public AudioMixerGroups audioMixerGroups;           // mixer channels

    void StartTheCatalog()
    {
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
            string channel = "SFX",
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

        // --- Assign mixer group ---
        if (clipCfg.group == null)
        {
            clipCfg.group = audioMixerGroups.GetGroup(clipCfg.channel);
            if (clipCfg.group == null)
            {
                Debug.LogWarning($"[clipCfg]: {clipCfg.name}] No AudioMixerGroup found for channel '{clipCfg.channel}', using fallback SFX.");
                clipCfg.group = audioMixerGroups.GetGroup("SFX");
            }
        }

        // --- load audio now or later ---
        if (preload)    // load audio now
        {
            // Attempt load + group setup
            if (!LoadClip(clipCfg))
            {
                Debug.LogError($"Failed to load {channel} entry: {clipCfg.name}, filename: {clipCfg.filename}");
                return false;
            }
            else
            {
                clipCfgList.Add(clipCfg);
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
                clipCfgList.Add(clipCfg);
            }
            if (!exists)
            {
                // File doesnt exist: not an error, just a warning because we don't need it immediately.
                // Later attempts to play it will just skip the play request with a clip missing warning.
                Debug.LogWarning($"Warning: Failed non-preload {channel} file existance check: {clipCfg.name}, filename: {clipCfg.filename}");
            }
        }

        return true;
    }

    public string GetPathToAudioClip (AudioClipCfg clipCfg)
    {
        return $"Audio/{clipCfg.channel}/{clipCfg.filename}";
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
    /// Returns true if both clip and group were successfully set.
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
}

