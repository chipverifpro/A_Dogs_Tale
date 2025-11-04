using System.Collections;
using UnityEngine;

/*.
in AudioPlayer.cs...
--AudioPlayTracking (data class) holds data needed to cleanly shut down audio tracks playing or repeating.

--AudioPlayer class has
    -task PlayClip() usess all the information in the Audio Catalog to configure and launch an audio track,
     coroutine PlayWithInterval() is started by PlayClip() to monitor the track until it is complete.
    -task StopTasks() identifies which tasks to stop, kill's their monitor coroutine, and then replaces it
     with it's own coroutine AudioTaskStop()
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
}


public partial class AudioPlayer : MonoBehaviour
{
    [Header("Object References")]
    public ObjectDirectory dir;
    public AudioCatalog audioCatalog;

    public static AudioPlayer Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // prevent duplicates
            return;
        }

        Instance = this;
        audioCatalog.StartAudioCatalog();
        //DontDestroyOnLoad(gameObject); // optional: keep across scenes
    }

    // -------------------------------- PLAY CLIPS -------------------------------

    // PlayClip() finds the audio clip in the master catalog (clipCfgList),
    //  configures everything, and then launches the Coroutine
    //  PlayWithInterval() which starts the play and follows
    //  it through it's lifecycle.
    // Each entry in the master catalog (a unique "clipCfg") maintains
    //  a list of currently playing copies so they can be ended
    //  anytime needed.
    public bool PlayClip(string name)
    {
        Debug.Log($"[PlayClip] '{name}' requested.");
            
        // 1. Find entry
        AudioClipCfg clipCfg = audioCatalog.clipCfgList.Find(e => e.name == name);

        if (clipCfg == null)
        {
            Debug.LogWarning($"[PlayClip] Audio definition '{name}' not found in clipCfgList. PlayClip aborting.");
            return false;
        }

        // 2. Ensure clip loaded
        if (clipCfg.clip == null)
        {
            if (!audioCatalog.LoadClip(clipCfg)) // Load clip
            {
                Debug.LogError($"Failed to load {clipCfg.channel} entry: {clipCfg.name} with file {clipCfg.filename}.");
                return false;
            }
        }

        // 2.1. Ensure channel is valid
        if (clipCfg.mixerGroup == null)
        {
            Debug.LogError($"{clipCfg.channel} was not matched to a mixerGroup entry: {clipCfg.name}.");
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
        src.outputAudioMixerGroup = clipCfg.mixerGroup;

        // 5. Volume and pitch
        float volume_min = (clipCfg.volumeRange?.x) ?? 1f;
        float volume_max = (clipCfg.volumeRange?.y) ?? 1f;
        src.volume = UnityEngine.Random.Range(volume_min, volume_max);

        float pitch_min = (clipCfg.pitchRange?.x) ?? 1f;
        float pitch_max = (clipCfg.pitchRange?.y) ?? 1f;
        src.pitch = UnityEngine.Random.Range(pitch_min, pitch_max);
        Debug.Log($"[PlayClip] {name}: volume = {src.volume}, pitch = {src.pitch}");

        // 6. Set clip
        src.clip = clipCfg.clip;
        src.loop = clipCfg.IsContinuousLoop(); // Only use Unity's loop feature if interval is zero.

        // 7. Play once or looped
        // Use this same coroutine for looping with fixed/random intervals, or one-time playback
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
        int timesPlayed = 0;

        Debug.Log($"[PlayWithInterval] '{clipCfg.name}' begins.");

        while (!taskInfo.stopRepeating) // repeat forever, unless abort requested at end of track
        {
            if ((timesPlayed != 0) || (clipCfg.startAfterInterval == false)) // skip playing before waiting interval if requested.
            {
                // Play the sound
                src.Play();
                taskInfo.isPlaying = true;
                //Debug.Log($"[PlayWithInterval] '{clipCfg.name}' started playing.");

                if (src.loop)
                {
                    // a continuously looping track will never end,
                    //  so go ahead and exit this task.  The taskInfo
                    //  will remain as a means of manually stopping it.
                    //Debug.Log($"[PlayWithInterval] '{clipCfg.name}' monitoring task ends since auto loop playing is active.");
                    taskInfo.loopCo = null;
                    yield break;    // exit coroutine without doing cleanup
                }
                // Wait for the clip to end
                float clipLength = clipCfg.clip.length / Mathf.Abs(src.pitch);
                yield return new WaitForSeconds(clipLength);

                // done playing
                src.Stop();     // probably not necessary, it should already stop itself.
                taskInfo.isPlaying = false;
                //Debug.Log($"[PlayWithInterval] '{clipCfg.name}' finished playing.");
            }

            timesPlayed++;

            // if we were requested to stop at end of track, then cleanup and end this coroutine
            if (taskInfo.stopRepeating)
            {
                //Debug.Log($"PlayWithInterval: {clipCfg.name} stopRepeating flag observed, stopping.");
                break;  // loop abort requested, so don't delay, exit the while loop and do cleanup
            }
            if (clipCfg.IsPlayOnce())
            {
                //Debug.Log($"PlayWithInterval: {clipCfg.name} isPlayOnce, stopping. interval = {clipCfg.intervalRange}");
                break;  // played once, now stop looping, exit the while loop and do cleanup
            }

            // Delay random interval before beginning to play again.
            float interval_min = (clipCfg.intervalRange?.x) ?? 0f;
            float interval_max = (clipCfg.intervalRange?.y) ?? 0f;
            float delay = UnityEngine.Random.Range(interval_min, interval_max);
            //Debug.Log($"[PlayWithInterval] '{clipCfg.name}' waiting {delay} between plays. intervalRange {clipCfg.intervalRange}");
            yield return new WaitForSeconds(delay);

        } // end while loop

        if (taskInfo.stopRepeating)
        {
            src.Stop();
            taskInfo.isPlaying = false;
            //Debug.Log($"PlayWithInterval: stopped {clipCfg.name} due to stopRepeating flag.");
        }
        // DONE
        // cleanup temporary GameObject if we created one.
        if (taskInfo.isTempGO && taskInfo.go)
            Object.Destroy(taskInfo.go);

        // remove us from the running_Tasks list
        taskInfo.loopCo = null; // We are exiting, so don't need to keep this coroutine id anymore.  Not necessary, whole structure will be released on the next line.
        clipCfg.running_Tasks.Remove(taskInfo);
    }

    // -------------------------------- STOP CLIPS -------------------------------

    // StopClips() has several options...
    //  Stop everything
    //  Stop all with a particular name
    //  Stop all on a particular channel (ie Music, UI, SFX, ...)
    //  Stop all from a GameObject
    //  Stop all from all temporary GameObjects

    // Can end in one of these styles:
    //  Immediate cutoff of playing. (fadeOut = 0)
    //  Fade out and then stop.      (fadeOut = # seconds)
    //  Allow to finish in-progress track, but don't start again. (fadeOut < 0)
    public void StopClips(bool stop_everything = false, string trackName = null, string channelName = null, GameObject go = null, bool tempGO = false, float fadeOut = 0f)
    {
        // decide what to identify tracks to stop...
        bool stop_by_go = go != null;
        bool stop_by_temp_go = tempGO;
        bool stop_by_name = !string.IsNullOrEmpty(trackName);
        bool stop_by_channel = !string.IsNullOrEmpty(channelName);

        // check all playing audio tracks
        for (int snum = audioCatalog.clipCfgList.Count - 1; snum >= 0; snum--)
        {
            AudioClipCfg clipCfg = audioCatalog.clipCfgList[snum];
            for (int tnum = clipCfg.running_Tasks.Count - 1; tnum >= 0; tnum--)
            {
                AudioPlayTracking task = clipCfg.running_Tasks[tnum];

                // Should we stop this task?  Does it meet any criteria?
                bool stop_it =
                    stop_everything ||
                    (stop_by_go && task.go == go && go != null) ||
                    (stop_by_temp_go && task.isTempGO) ||
                    (stop_by_name && clipCfg.name == trackName) ||
                    (stop_by_channel && clipCfg.channel == channelName);

                if (stop_it)
                {
                    // Stop the task
                    StartCoroutine(AudioTaskStop(task, fadeOut));

                    // Remove task from the running tasks list
                    clipCfg.running_Tasks.Remove(task);
                }
            }
        }
    }
    
    // Don't call coroutine AudioTaskStop() directly, use StopClips() above
    //   which applies a lookup filter to select tasks and remove them from the active
    //   list, and send control of them here to be managed until they are done.
    // task.AudioTaskStop():  Corourtine that stops managed track by stopping existing driver
    //   coroutine, then it takes over managing the track until it has stopped. Finally
    //   it destroys the GameObject if it is a temporary one created just for this audio.
    // Can end in one of these styles:
    //   Immediate cutoff of playing. (fadeOut = 0)
    //   Fade out and then stop.      (fadeOut = # seconds)
    //   Allow to finish in-progress track, but don't start again. (fadeOut < 0)
    public IEnumerator AudioTaskStop (AudioPlayTracking track, float fadeout)
    {
        // kill the driving loop coroutine
        if (track.loopCo != null)
        {
            AudioPlayer.Instance.StopCoroutine(track.loopCo);
            track.loopCo = null;
        }

        if (fadeout < 0) // allow the current play to complete.
        {
            track.src.loop = false;
            yield return new WaitUntil(() => track.src.isPlaying == false);
            track.src.Stop(); // not really necessary, it should already have stopped.
            track.src = null;  // release the pointer.
        }
        else    // don't let it complete, stop or fade out now.
        {
            // fade out and stop the audio
            if (track.src != null)
            {
                float startVol = track.src.volume;
                float t = 0;
                while ((t < fadeout) && (track.src != null))
                {
                    t += Time.deltaTime;
                    track.src.volume = Mathf.Lerp(startVol, 0f, t / fadeout);
                    yield return null;
                }
                if (track.src != null)
                {
                    track.src.volume = 0f;
                    track.src.Stop();
                    track.src = null;
                }
            }

            // destroy the game object if it is temporary
            if ((track.isTempGO == true) && (track.go != null))
            {
                Object.Destroy(track.go);
                track.go = null;
            }
        }
        // When this coroutine ends, the last reference to 'track' will be gone
        // and remaining memory used will be garbage collected.
    }
}
