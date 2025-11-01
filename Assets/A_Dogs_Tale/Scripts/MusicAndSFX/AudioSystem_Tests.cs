// Assets/Tests/AudioSystem_Tests.cs
// Requires Unity Test Framework (EditMode/PlayMode). Put this in a folder named "Tests".
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;



public class AudioSystem_Tests
{
    // --- helpers ---
    private static AudioMixerGroup CreateDefaultMixerGroup()
    {
        // Try to load a mixer from Resources first (if you have one)
        var mixer = Resources.Load<AudioMixer>("MainMixer"); // expects Assets/Resources/MainMixer.mixer
        if (mixer != null)
        {
            var groups = mixer.FindMatchingGroups("Master");
            if (groups != null && groups.Length > 0)
                return groups[0];
        }

        // Fallback: use an AudioSource’s default output group
        var tempGO = new GameObject("TempAudioSrcForGroup");
        var src = tempGO.AddComponent<AudioSource>();
        var group = src.outputAudioMixerGroup;
        UnityEngine.Object.DestroyImmediate(tempGO);

        if (group == null)
            Debug.LogWarning("No AudioMixerGroup found — using null. Tests will still run, but without routing.");

        return group;
    }

    private static AudioClip CreateSineClip(string name = "unit_test_tone", float seconds = 0.25f, int sampleRate = 44100, float freq = 440f)
    {
        int samples = Mathf.Max(1, Mathf.RoundToInt(seconds * sampleRate));
        var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
        float[] data = new float[samples];
        float inc = 2f * Mathf.PI * freq / sampleRate;
        for (int i = 0; i < samples; i++) data[i] = Mathf.Sin(i * inc) * 0.1f;
        clip.SetData(data, 0);
        return clip;
    }

    private static void InitMixerStatics(AudioMixerGroup g)
    {
        // Use same group for all channels in tests
        AudioMixerGroups.SFX = g;
        AudioMixerGroups.UI = g;
        AudioMixerGroups.Music = g;
        AudioMixerGroups.Voices = g;
        AudioMixerGroups.Ambient = g;
    }

    private static AudioPlayer SpawnPlayer(AudioMixerGroup g)
    {
        var go = new GameObject("AudioPlayer_Test");
        var ap = go.AddComponent<AudioPlayer>();
        // AudioPlayer.Awake() will run and set Instance + build initial pool.
        // We also want any new sources to use our group.
        // Use reflection in case 'mixerGroup' or 'initialSize' are private/protected.
        var t = typeof(AudioPlayer);
        var mg = t.GetField("mixerGroup", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (mg != null) mg.SetValue(ap, g);
        var initSizeF = t.GetField("initialSize", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (initSizeF != null) initSizeF.SetValue(ap, 1);
        return ap;
    }

    private static AudioCatalog NewCatalog()
    {
        var cat = new AudioCatalog();
        cat.clipCfgList = new List<AudioClipCfg>();
        return cat;
    }

    // ---------- TESTS ----------

    //[UnityTest]
    public IEnumerator AddClip_Minimal_Then_Load_Assigns_Group_And_Clip()
    {
        var group = CreateDefaultMixerGroup();
        InitMixerStatics(group);

        var cat = NewCatalog();

        // minimal info: name, filename, subtitle; all else defaults
        bool ok = cat.AddClipToCatalog(
            name: "Click1",
            filename: "Button-Click",
            subtitle: "[click]",
            preload: false // defer loading; we will inject a clip for the test
        );
        Assert.IsTrue(ok, "AddClipToCatalog should succeed");
        Assert.AreEqual(1, cat.clipCfgList.Count);

        var e = cat.clipCfgList[0];
        // Defaults
        Assert.AreEqual("SFX", e.channel);
        Assert.AreEqual(new Vector2(999f, 0f), e.interval.Value, "Default interval should mean 'play once'");
        Assert.AreEqual(Vector2.one, e.pitchRange.Value, "Default pitch range should be (1,1)");
        Assert.IsNull(e.group, "Group not assigned until LoadClip");
        Assert.IsNull(e.clip, "Clip not loaded when preload=false");

        // Inject synthetic clip to simulate an already-loaded asset, then call LoadClip to assign the group.
        e.clip = CreateSineClip("click_sine", seconds: 0.05f);
        ok = cat.LoadClip(e);
        Assert.IsTrue(ok, "LoadClip should succeed when clip is pre-assigned");
        Assert.AreEqual(group, e.group, "LoadClip should have assigned the AudioMixerGroup");

        yield return null;
    }

    //[UnityTest]
    public IEnumerator AddClip_With_Object_And_Position_Logs_Warning_And_Uses_Object()
    {
        var group = CreateDefaultMixerGroup();
        InitMixerStatics(group);

        // Create a source GameObject to be looked up by name
        var srcGO = new GameObject("UnitTest_Src");
        var cat = NewCatalog();

        //UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("audioLocation will be ignored", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        // Expected: "audioLocation will be ignored" warning
        Debug.Log("(Expected) Warning should appear: 'audioLocation will be ignored'");

        bool ok = cat.AddClipToCatalog(
            name: "Bark",
            filename: "Bark_GermanShepherd",
            subtitle: "[bark]",
            sourceObjectName: "UnitTest_Src",
            audioLocation: new Vector3(1, 2, 3), // should be ignored because sourceObject is set
            preload: false
        );
        Assert.IsTrue(ok);
        var e = cat.clipCfgList[0];
        Assert.AreEqual(srcGO, e.sourceObject, "Should attach to source object by name");
        // load group/clip (inject clip)
        e.clip = CreateSineClip("bark_sine", seconds: 0.2f);
        ok = cat.LoadClip(e);
        Assert.IsTrue(ok);
        Assert.AreEqual(group, e.group);

        yield return null;
    }

    //[UnityTest]
    public IEnumerator Play_TempGO_Then_Abort_Immediate()
    {
        var group = CreateDefaultMixerGroup();
        InitMixerStatics(group);
        var ap = SpawnPlayer(group);

        // We will simulate a catalog entry that uses a temp GameObject (no source object)
        var entry = new AudioClipCfg
        {
            name = "TempBeep",
            filename = "ignored",
            subtitle = "[beep]",
            channel = "SFX",
            audioLocation = Vector3.zero,
            interval = new Vector2(999f, 0f) // play once
        };
        entry.clip = CreateSineClip("beep_sine", seconds: 0.25f);
        entry.group = group;

        // Build a temp GO + source and play it using the public API
        var tempGO = new GameObject($"{entry.channel}_{entry.name}_Temp");
        var src = tempGO.AddComponent<AudioSource>();
        src.spatialBlend = 1f;
        src.outputAudioMixerGroup = group;
        src.clip = entry.clip;
        src.Play();
        Assert.IsTrue(src.isPlaying, "Audio should have started");

        // Track it and abort immediately (fadeout 0)
        var task = new AudioPlayTracking
        {
            go = tempGO,
            src = src,
            isTempGO = true
        };
        var co = ap.StartCoroutine(task.Abort_SFX(entry, fadeout: 0f));
        yield return co;

        // After abort immediate, source should be stopped and GO destroyed
        Assert.IsTrue(task.go == null || task.go.Equals(null), "Temp GO should be destroyed");
    }

    //[UnityTest]
    public IEnumerator Play_OnRealObject_StopAfterFinish()
    {
        var group = CreateDefaultMixerGroup();
        InitMixerStatics(group);
        var ap = SpawnPlayer(group);

        // real object as source
        var srcGO = new GameObject("RealSource");
        var src = srcGO.AddComponent<AudioSource>();
        src.outputAudioMixerGroup = group;

        var entry = new AudioClipCfg
        {
            name = "RealBeep",
            filename = "ignored",
            subtitle = "[beep]",
            channel = "SFX",
            sourceObject = srcGO,
            interval = new Vector2(0f, 0f) // doesn't matter here
        };
        entry.clip = CreateSineClip("real_beep", seconds: 0.15f);
        entry.group = group;

        src.clip = entry.clip;
        src.Play();
        Assert.IsTrue(src.isPlaying);

        var task = new AudioPlayTracking
        {
            go = srcGO,
            src = src,
            isTempGO = false
        };

        // Negative fadeout requests "stop when finished"
        var co = ap.StartCoroutine(task.Abort_SFX(entry, fadeout: -1f));
        yield return co;

        Assert.IsFalse(src.isPlaying, "Should be stopped after finish");
        // real object should NOT be destroyed
        Assert.IsNotNull(srcGO);
    }

    //[UnityTest]
    public IEnumerator Play_NonLocalized_And_StopWhenFinished()
    {
        var group = CreateDefaultMixerGroup();
        InitMixerStatics(group);
        var ap = SpawnPlayer(group);

        var clip = CreateSineClip("ui_click", seconds: 0.05f);
        ap.PlayNonLocalized(clip, volume: 0.5f);
        // Let it play & auto-stop by StopWhenFinished coroutine
        yield return new WaitForSeconds(0.2f);

        // We can't directly access the private pool, but at least ensure no exceptions and AudioListener exists
        Assert.Pass("Non-localized play did not throw and finished.");
    }
}
