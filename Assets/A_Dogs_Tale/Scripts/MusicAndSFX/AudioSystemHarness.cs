// Assets/Audio/Runtime/AudioSystemHarness.cs
// Drop this MonoBehaviour into any scene and press Play.
// It will exercise AudioCatalog + AudioPlayer end-to-end without the Test Runner.
//
// What it does
// - Creates a default AudioMixer and assigns AudioMixerGroups.* channels
// - Spawns an AudioPlayer and wires its mixer group via reflection
// - Builds several AudioClipCfg entries via AddClipToCatalog (mirroring your Awake() use cases)
// - "Loads" entries by assigning a synthetic tone clip and resolving the output mixer group
// - Plays localized (temp GO and real object) and non-localized clips
// - Aborts with: immediate stop (fadeout 0) and stop-when-finished (fadeout -1)
// - Shows a tiny OnGUI panel with buttons + status so you can re-run each step quickly
//
// Notes
// - Uses a tiny synthetic sine tone so it runs in any project (no asset dependencies)
// - Uses reflection for optional/private fields on AudioPlayer (mixerGroup, initialSize)
// - If AudioPlayTracking is not accessible, we fall back to a simple abort shim
//
using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Audio;

public class AudioSystemHarness : MonoBehaviour
{
    [Header("Auto Run")]
    public bool autoRunOnStart = true;
    public float stepDelay = 0.15f;

    [Header("Debug UI")]
    public bool showPanel = true;
    public Rect panelRect = new Rect(10, 10, 380, 340);

    private StringBuilder _log = new StringBuilder(1024);
    private Vector2 _scroll;
    private bool _isRunning;
    private AudioMixerGroup _group;
    private AudioPlayer _player;
    private AudioCatalog _catalog;

    // reflection caches
    private FieldInfo _apMixerGroup;
    private FieldInfo _apInitialSize;
    private MethodInfo _apPlayNonLocalized; // PlayNonLocalized(AudioClip clip, float volume=1f)
    private Type _typeAudioPlayTracking;
    private MethodInfo _mAbortSFX; // IEnumerator Abort_SFX(AudioClipCfg entry, float fadeout)

    void Awake()
    {
        EnsureMixerAndStatics();
        SpawnPlayer();
        NewCatalog();
        CacheOptionalAPis();
        Log("Harness ready.");
    }

    void Start()
    {
        if (autoRunOnStart)
            StartCoroutine(RunAll());
    }

    void EnsureMixerAndStatics()
    {
        // Try to get any valid AudioMixerGroup from a temporary AudioSource.
        var temp = new GameObject("TempAudioSrcForGroup").AddComponent<AudioSource>();
        _group = temp.outputAudioMixerGroup;

        if (_group == null)
        {
            Debug.LogWarning("No AudioMixerGroup found on temp AudioSource; tests will use null group.");
        }
        else
        {
            Debug.Log("Using AudioMixerGroup: " + _group.name);
        }

        // Assign all known channels (null is acceptable; just suppresses routing)
        try { AudioMixerGroups.SFX = _group; } catch {}
        try { AudioMixerGroups.UI = _group; } catch {}
        try { AudioMixerGroups.Music = _group; } catch {}
        try { AudioMixerGroups.Voices = _group; } catch {}
        try { AudioMixerGroups.Ambient = _group; } catch {}

        Destroy(temp.gameObject);
    }

    void SpawnPlayer()
    {
        var go = new GameObject("AudioPlayer_RuntimeHarness");
        _player = go.AddComponent<AudioPlayer>();

        // wire private fields if they exist
        _apMixerGroup = typeof(AudioPlayer).GetField("mixerGroup", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        _apInitialSize = typeof(AudioPlayer).GetField("initialSize", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if (_apMixerGroup != null) _apMixerGroup.SetValue(_player, _group);
        if (_apInitialSize != null) _apInitialSize.SetValue(_player, 2);
    }

    void NewCatalog()
    {
        _catalog = new AudioCatalog();
        _catalog.clipCfgList = new System.Collections.Generic.List<AudioClipCfg>();
    }

    void CacheOptionalAPis()
    {
        _apPlayNonLocalized = typeof(AudioPlayer).GetMethod("PlayNonLocalized", BindingFlags.Public|BindingFlags.Instance);
        _typeAudioPlayTracking = Type.GetType("AudioPlayTracking");
        if (_typeAudioPlayTracking == null)
        {
            // try to get it from the AudioPlayer assembly-qualified name if nested/other namespace
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                _typeAudioPlayTracking = asm.GetType("AudioPlayTracking");
                if (_typeAudioPlayTracking != null) break;
            }
        }
        if (_typeAudioPlayTracking != null)
        {
            _mAbortSFX = _typeAudioPlayTracking.GetMethod("Abort_SFX", BindingFlags.Public|BindingFlags.Instance);
        }
    }

    // ---------- UI ----------
    void OnGUI()
    {
        if (!showPanel) return;
        panelRect = GUILayout.Window(GetInstanceID(), panelRect, DrawWindow, "Audio Harness");
    }

    void DrawWindow(int id)
    {
        GUI.enabled = !_isRunning;
        if (GUILayout.Button("Run All")) { StartCoroutine(RunAll()); }
        GUILayout.Space(6);
        if (GUILayout.Button("1) AddClip Minimal + Load")) { StartCoroutine(Step_AddClip_Minimal_Then_Load()); }
        if (GUILayout.Button("2) AddClip with SourceObjectName + Position")) { StartCoroutine(Step_AddClip_Object_And_Position()); }
        if (GUILayout.Button("3) Play TempGO → Abort Immediate")) { StartCoroutine(Step_Play_TempGO_AbortImmediate()); }
        if (GUILayout.Button("4) Play Real Object → Stop When Finished")) { StartCoroutine(Step_Play_RealObject_StopWhenFinished()); }
        if (GUILayout.Button("5) Play Non-Localized")) { StartCoroutine(Step_Play_NonLocalized()); }
        GUI.enabled = true;

        GUILayout.Space(8);
        if (GUILayout.Button("Clear Log")) { _log.Length = 0; }
        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(170));
        GUILayout.Label(_log.ToString());
        GUILayout.EndScrollView();

        GUI.DragWindow(new Rect(0,0, 10000, 20));
    }

    void Log(string msg) { Debug.Log(msg); _log.AppendLine(msg); }

    // ---------- Steps ----------

    IEnumerator RunAll()
    {
        if (_isRunning) yield break;
        _isRunning = true;
        _log.Length = 0;
        Log("[RunAll] started");

        yield return Step_AddClip_Minimal_Then_Load();
        yield return Wait(stepDelay);

        yield return Step_AddClip_Object_And_Position();
        yield return Wait(stepDelay);

        yield return Step_Play_TempGO_AbortImmediate();
        yield return Wait(stepDelay);

        yield return Step_Play_RealObject_StopWhenFinished();
        yield return Wait(stepDelay);

        yield return Step_Play_NonLocalized();
        Log("[RunAll] complete");
        _isRunning = false;
    }

    IEnumerator Step_AddClip_Minimal_Then_Load()
    {
        Log("[1] AddClip minimal");
        NewCatalog();

        bool ok = _catalog.AddClipToCatalog(
            name: "Click1",
            filename: "Button-Click",
            subtitle: "[click]",
            preload: false
        );
        Log(" AddClipToCatalog => " + ok);

        if (_catalog.clipCfgList.Count == 0)
        {
            Log(" FAILED: no entries in catalog"); yield break;
        }

        var e = _catalog.clipCfgList[0];
        Log($" Entry defaults: channel={e.channel}, interval={e.interval}, pitchRange={e.pitchRange}, " +
            $"group={(e.group != null ? e.group.name : "null")}, clip={(e.clip != null ? e.clip.name : "null")}");
    
        // Pre-inject a synthetic clip so LoadClip resolves mixer group using your logic
        e.clip = MakeSineClip("click_sine", 0.05f);
        ok = _catalog.LoadClip(e);
        Log(" LoadClip => " + ok + " ; group now = " + (e.group != null ? e.group.name : "null"));

        yield return null;
    }

    IEnumerator Step_AddClip_Object_And_Position()
    {
        Log("[2] AddClip with sourceObjectName + audioLocation");
        NewCatalog();
        var srcGO = new GameObject("Harness_Src");
        bool ok = _catalog.AddClipToCatalog(
            name: "Bark",
            filename: "Bark_GermanShepherd",
            subtitle: "[bark]",
            sourceObjectName: "Harness_Src",
            audioLocation: new Vector3(1,2,3),
            preload: false
        );
        Log(" AddClipToCatalog => " + ok);
        var e = _catalog.clipCfgList.Count>0 ? _catalog.clipCfgList[0] : null;
        if (e == null) { Log(" FAILED: no entry created"); yield break; }
        Log("  sourceObject=" + (e.sourceObject != null ? e.sourceObject.name : "null") + " ; audioLocation=" + (e.audioLocation.HasValue? e.audioLocation.Value.ToString() : "null"));

        e.clip = MakeSineClip("bark_sine", 0.2f);
        ok = _catalog.LoadClip(e);
        Log(" LoadClip => " + ok + " ; group=" + (e.group != null ? e.group.name : "null"));
        yield return null;
    }

    IEnumerator Step_Play_TempGO_AbortImmediate()
    {
        Log("[3] Play temp GO → abort immediate");
        var entry = new AudioClipCfg
        {
            name = "TempBeep",
            filename = "ignored",
            subtitle = "[beep]",
            channel = "SFX",
            audioLocation = Vector3.zero,
            interval = new Vector2(999f, 0f) // play once
        };
        entry.group = _group;
        entry.clip = MakeSineClip("beep_sine", 0.25f);

        var tempGO = new GameObject($"{entry.channel}_{entry.name}_Temp");
        var src = tempGO.AddComponent<AudioSource>();
        src.outputAudioMixerGroup = _group;
        src.spatialBlend = 1f;
        src.clip = entry.clip;
        src.Play();
        Log("  started temp source");

        yield return Wait(0.05f);
        yield return AbortSFX(entry, src, tempGO, fadeout: 0f); // immediate
        Log("  aborted immediate; temp destroyed? " + (tempGO==null || tempGO.Equals(null)));
    }

    IEnumerator Step_Play_RealObject_StopWhenFinished()
    {
        Log("[4] Play on real object → stop when finished");
        var srcGO = new GameObject("Harness_RealSource");
        var src = srcGO.AddComponent<AudioSource>();
        src.outputAudioMixerGroup = _group;

        var entry = new AudioClipCfg
        {
            name = "RealBeep",
            filename = "ignored",
            subtitle = "[beep]",
            channel = "SFX",
            sourceObject = srcGO,
            interval = Vector2.zero
        };
        entry.group = _group;
        entry.clip = MakeSineClip("real_beep", 0.15f);

        src.clip = entry.clip;
        src.Play();
        Log("  started real source");
        yield return AbortSFX(entry, src, srcGO, fadeout: -1f);
        Log("  finished; src.isPlaying=" + src.isPlaying);
    }

    IEnumerator Step_Play_NonLocalized()
    {
        Log("[5] Play non-localized");
        var clip = MakeSineClip("ui_click", 0.06f);

        if (_apPlayNonLocalized != null)
        {
            _apPlayNonLocalized.Invoke(_player, new object[]{clip, 0.5f});
            Log("  AudioPlayer.PlayNonLocalized invoked");
            yield return Wait(0.2f);
        }
        else
        {
            // Fallback: quick one-shot on a pooled source
            var go = new GameObject("Harness_NonLocalized");
            var src = go.AddComponent<AudioSource>();
            src.outputAudioMixerGroup = _group;
            src.PlayOneShot(clip, 0.5f);
            Log("  Fallback PlayOneShot");
            yield return Wait(0.2f);
            Destroy(go);
        }
    }

    // ---------- Helpers ----------

    IEnumerator AbortSFX(AudioClipCfg entry, AudioSource src, GameObject hostGO, float fadeout)
    {
        if (_typeAudioPlayTracking != null && _mAbortSFX != null)
        {
            // Use the project-provided implementation if present
            var tracking = Activator.CreateInstance(_typeAudioPlayTracking);
            _typeAudioPlayTracking.GetField("go", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.SetValue(tracking, hostGO);
            _typeAudioPlayTracking.GetField("src", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.SetValue(tracking, src);
            _typeAudioPlayTracking.GetField("isTempGO", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.SetValue(tracking, hostGO != null && hostGO.name.Contains("_Temp"));

            var enumerator = _mAbortSFX.Invoke(tracking, new object[]{ entry, fadeout }) as IEnumerator;
            if (enumerator != null) { yield return StartCoroutine(enumerator); }
            else { Log("  WARN: Abort_SFX returned null IEnumerator; falling back to shim"); yield return AbortShim(src, hostGO, fadeout); }
        }
        else
        {
            // Fallback shim
            yield return AbortShim(src, hostGO, fadeout);
        }
    }

    IEnumerator AbortShim(AudioSource src, GameObject hostGO, float fadeout)
    {
        if (src == null) yield break;

        if (fadeout > 0f)
        {
            float t = 0f;
            float start = src.volume;
            while (t < fadeout && src != null)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(t / fadeout);
                src.volume = start * k;
                yield return null;
            }
            if (src != null) src.Stop();
        }
        else if (fadeout == 0f)
        {
            src.Stop();
        }
        else // < 0 -> stop when finished
        {
            while (src != null && src.isPlaying) yield return null;
        }

        if (hostGO != null && (hostGO.name.Contains("_Temp") || hostGO.name.StartsWith("Harness_")))
        {
            Destroy(hostGO);
        }
    }

    static AudioClip MakeSineClip(string name, float seconds, int sampleRate = 44100, float freq = 440f)
    {
        int samples = Mathf.Max(1, Mathf.RoundToInt(seconds * sampleRate));
        var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
        var data = new float[samples];
        float inc = 2f * Mathf.PI * freq / sampleRate;
        for (int i = 0; i < samples; i++) data[i] = Mathf.Sin(i * inc) * 0.1f;
        clip.SetData(data, 0);
        return clip;
    }

    static WaitForSecondsRealtime Wait(float t) => new WaitForSecondsRealtime(t);
}
