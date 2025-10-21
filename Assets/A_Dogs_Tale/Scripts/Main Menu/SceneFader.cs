using System.Collections;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.SceneManagement;

public class SceneFader : MonoBehaviour
{
    public ObjectDirectory dir;

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
        StartCoroutine(CrossFade());
    }

    private IEnumerator CrossFade()
    {
        BottomBanner.Show("🐾 Welcome, Pup! Sniffing out treasures...");

        // Display just the splash screen.
        splashCanvasGroup.alpha = 1;
        menuCanvasGroup.alpha = 0;

        // display splash screen for a bit.  Press any key to skip.
        yield return StartCoroutine(WaitAllowSkip(minSplashSeconds));

        // Fade out splash
        StartCoroutine(Fade(splashCanvasGroup, 1f, 0f));
        // Simultaneously fade in menu...
        yield return StartCoroutine(Fade(menuCanvasGroup, 0f, 1f));
    }

    public IEnumerator FadeToGame()
    {
        BottomBanner.Show("🐾 Welcome, Pup! On the way to Adventure...");

        // Display just the menu screen.
        //splashCanvasGroup.alpha = 0;
        menuCanvasGroup.alpha = 1;

        // display splash screen for a bit.  Press any key to skip.
        //yield return StartCoroutine(WaitAllowSkip(minSplashSeconds));

        // Fade out splash
        //StartCoroutine(Fade(splashCanvasGroup, 1f, 0f));
        // Simultaneously fade in menu...
        yield return StartCoroutine(Fade(menuCanvasGroup, 1f, 0f));

        GameObject splashObject;
        splashObject = GameObject.Find("Splash");
        Debug.Log($"Disabling object Splash, enabling object GeneratorCanvas.");
        dir.gen.EnableGeneratorCanvas(true);
        splashObject.SetActive(false);
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