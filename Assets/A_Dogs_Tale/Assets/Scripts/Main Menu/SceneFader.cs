using System.Collections;
using UnityEngine;

public class SceneFader : MonoBehaviour
{
    public ObjectDirectory dir;
    //public AudioPlayer sfx;
    public AudioPlayer audioPlayer;
    public AudioCatalog audioCatalog;
    public static SceneFader Instance;

    [Header("UI (optional)")]
    public CanvasGroup splashCanvasGroup;
    public CanvasGroup menuCanvasGroup;

    [Header("Timing")]
    public float minSplashSeconds = 1.5f;  // brief pause
    public float fadeDuration = 5f;       // cross fade duration

    [Header("Debug/UX")]
    public bool allowSkip = true;          // press any key / click to skip after min time


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (audioPlayer == null)
        {
            audioPlayer = FindFirstObjectByType<AudioPlayer>();
            if (audioPlayer == null)
            {
                Debug.LogError($"SceneFader.Start: AudioPlayer not found");
            }
            else
            {
                audioCatalog = audioPlayer.audioCatalog;
            }
        }

        SetupTitleSFX();    // configure music and SFX

        StartCoroutine(CrossFade());
        
        //if (audioPlayer) audioPlayer.PlayClip("Bark_GermanShepherd");
        
        //if (!sfx) sfx = FindFirstObjectByType<AudioPlayer>();
        //if (sfx) sfx.RandomRepeatSFX("German_shepherd_bark",minVol:0.05f, maxVol:0.15f, MinTime:5f, MaxTime: 15f));
    }

    void SetupTitleSFX()
    {
        audioCatalog.AddClipToCatalog(
            name: "Button-Click",
            filename: "Button-Click",
            channel: "UI",
            preload: true
        );

        audioCatalog.AddClipToCatalog(
            name: "Opening Title",
            filename: "Curious Whispers",
            subtitle: "[Music Playing: Curious Whispers]",
            channel: "Music",
            intervalRange: new(0, 0),     // continuous repeat
            preload: true
        );

        audioCatalog.AddClipToCatalog(
            name: "Mission Home Sweet Home",
            filename: "Through the Windowpane",
            subtitle: "[Music Playing: Through the Windowpane]",
            channel: "Music",
            intervalRange: new(0, 0),     // continuous repeat
            preload: false
        );

        audioCatalog.AddClipToCatalog(
            name: "Bark_GS_repeat",
            filename: "Bark_GermanShepherd",
            subtitle: "[Bark (German Shepherd)]",
            channel: "SFX",     // Ambient ?
            pitchRange: new(.95f, 1.05f),
            intervalRange: new(5f, 10f),
            startAfterInterval: true,
            preload: true
        );
    }
    private IEnumerator CrossFade()
    {
        yield return null;      // let things settle out before beginning this.
        BottomBanner.Show("🐾 Welcome, Pup! Sniffing out treasures...");

        // Display just the splash screen.
        splashCanvasGroup.alpha = 1;
        menuCanvasGroup.alpha = 0;

        // Start background Music and SFX
        audioPlayer.PlayClip("Opening Title");
        audioPlayer.PlayClip("Bark_GS_repeat");

        // display splash screen for a bit.  Press any key/mouse button to skip.
        yield return StartCoroutine(WaitAllowSkip(minSplashSeconds));

        // Fade out splash
        StartCoroutine(Fade(splashCanvasGroup, 1f, 0f));
        // Simultaneously fade in menu...
        yield return StartCoroutine(Fade(menuCanvasGroup, 0f, 1f));
    }


    public IEnumerator FadeToGame()
    {
        BottomBanner.Show("🐾 Welcome, Pup! On the way to Adventure...");

        // LEGACY:
        //MusicPlayer musicPlayer = FindFirstObjectByType<MusicPlayer>();
        //if (musicPlayer != null)
        //    musicPlayer.StartMusic(musicPlayer.exploreAudioFileName, fadeOut:true, fadeIn:false);

        // Display just the menu screen.
        //splashCanvasGroup.alpha = 0;
        menuCanvasGroup.alpha = 1;

        // display splash screen for a bit.  Press any key to skip.
        //yield return StartCoroutine(WaitAllowSkip(minSplashSeconds));

        // Fade out title music and SFX
        audioPlayer.StopClips(trackName: "Opening Title", fadeOut: 1f);
        audioPlayer.StopClips(trackName: "Bark_GS_repeat", fadeOut: -1f); // fadeOut: -1 means finish clip

        // Fade out Main Menu
        yield return StartCoroutine(Fade(menuCanvasGroup, 1f, 0f));

        GameObject splashObject;
        splashObject = GameObject.Find("Splash");
        Debug.Log($"Disabling object Splash, enabling object GeneratorCanvas.");
        dir.gen.EnableGeneratorCanvas(true);
        splashObject.SetActive(false);

        // Let generator know the main menu closed.  It will start it's music, among other things.
        dir.gen.MainMenuClosed();
    }

    public IEnumerator WaitAllowSkip(float minSplashSeconds)
    {
        float t = 0f;
        while (t < minSplashSeconds)
        {
            t += Time.deltaTime;
            // Optional: allow skip after min time
            if (allowSkip && t >= minSplashSeconds && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
            {
                break;  // skip remaining initial title time and begin crossfade
            }
            yield return null;
        }
        yield return null;
    }

    private IEnumerator Fade(CanvasGroup canvasGroup, float startAlpha, float targetAlpha)
    {
        canvasGroup.blocksRaycasts = true; // prevent clicks during fade

        float fadePct = 0f;

        while (fadePct < 1f)
        {
            fadePct += Time.deltaTime / fadeDuration;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, fadePct);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;    // done, make sure fade is complete.
        canvasGroup.blocksRaycasts = (targetAlpha != 0f);
        yield break;
    }
}