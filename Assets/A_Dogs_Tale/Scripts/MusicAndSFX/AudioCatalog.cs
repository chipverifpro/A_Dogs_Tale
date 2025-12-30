using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/*
in AudioCatalog.cs...
--AudioClipCfg (data class) holds all data and options about an audio track.
    no functions

--AudioCatalog holds the master list "clipCfgList" of all known audio tracks that contain:
  name, audio file, location or object, mixer group, special effects, and repeat modes.
  AddClipToCatalog() builds an entry. You only need to specify parameters that are not defaults.
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

    // List inside each catalog entry with one task listed per currently playing instance of the audio file.
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

    [Header("Audio Catalog Object References")]
    public AudioMixerGroups audioMixerGroups;           // mixer channels
    public AudioPlayer audioPlayer;                     // play controls
    public Directory dir;

    // call this from AudioPlayer.Awake()
    public void StartAudioCatalog()
    {
        // Initialize class references...
        if (dir == null) dir = FindFirstObjectByType<Directory>(FindObjectsInactive.Include);
        if (dir == null)
        {
            Debug.LogError($"AudioCatalog: ObjectDirectory not found");
            return;
        }
        if (audioMixerGroups == null) audioMixerGroups = dir.audioMixerGroups;
        if (audioPlayer == null) audioPlayer = dir.audioPlayer;
    }

    // these should move to someplace more relevant to each.  Left here as an example.
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

        clipCfg = new()  // assign all the easy fields first...
        {
            name = name,
            filename = filename,
            subtitle = subtitle,
            volumeRange = volumeRange,
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

        // --- Override startAfterInterval if required by repeat modes ---
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
                Debug.LogWarning($"Warning: Failed non-preload {channel} file existence check: {clipCfg.name}, filename: {clipCfg.filename}");
            }
        }

        // new clipCfg is ready to add to catalog, but first make sure it is unique...
        // --- Determine if clipCfg already existed ---
        // Find if the clip is already in the catalog (by name)
        AudioClipCfg clipCfg_old;
        
        // A new clip completely replaces an old one of the same name.  Just in case more than one entry exists, loop through all.
        while ((clipCfg_old = clipCfgList.Find(e => e.name == name)) != null)
        {
            Debug.LogWarning($"Warning: Replacing old clipCfg '{clipCfg_old.name}' which has {clipCfg_old.running_Tasks.Count} running tasks which will be stopped.");
            UnloadClipCfg(clipCfg_old, fadeOut: 0f); // spawns coroutines to stop tasks and unload audio.
            clipCfgList.Remove(clipCfg_old);  // we can remove the old entry now (even if tasks are still stopping)
        }
        // --- Add clipCfg to master list
        clipCfgList.Add(clipCfg);

        return true;
    }

    // helper function
    public string GetPathToAudioClip(AudioClipCfg clipCfg)
    {
        return $"Audio/{clipCfg.channel}/{clipCfg.filename}";
    }

    // Note: UnloadClipCfg() does NOT remove the clipCfg from the catalog list, do that separately.
    // UnloadClipCfg() stops any running tasks, unloads audio data, and cleans up references.
    // Note that much of this is done as coroutines which will complete over time.  No need to wait for any of that to finish.
    public void UnloadClipCfg(AudioClipCfg clipCfg, float fadeOut)
    {
        if (clipCfg.running_Tasks.Count != 0)
        {
            audioPlayer.StopClips(trackName: clipCfg.name, fadeOut: fadeOut);
            StartCoroutine(UnloadAudioClipWhenAllTasksDone(clipCfg));
        } else {
            if (clipCfg.clip != null) UnloadClip(clipCfg);
        }
    }

    // Launch one of these when deleting a clipCfg if there were tasks still running.  Frees remaining memory when done.
    public IEnumerator UnloadAudioClipWhenAllTasksDone(AudioClipCfg clipCfg)
    {
        // Wait until all tasks for this clipCfg are done
        yield return new WaitUntil(() => clipCfg.running_Tasks.Count == 0);
        // Unload the clip
        if (clipCfg.clip != null)
        {
            UnloadClip(clipCfg);
        }
        Debug.Log($"[DeleteAudioClipWhenAllTasksDone] Unloaded clip '{clipCfg.name}' after all tasks done.");

    }

    // UnloadClip() just unloads the audio data, leaving the small AudioCatalog entry
    // which is able to be reloaded with LoadClip() if needed again.
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

    // Loads audio clip & assigns AudioMixerGroup based on 'channel' and 'filename'.
    // Returns true if clip was successfully set.
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
