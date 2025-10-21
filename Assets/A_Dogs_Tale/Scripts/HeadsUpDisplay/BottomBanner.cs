using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class BottomBanner : MonoBehaviour
{
    public static BottomBanner Instance { get; private set; }

    [Header("Style")]
    [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.5f);
    [SerializeField] Color textColor = Color.white;
    [SerializeField] int fontSize = 96;
    [SerializeField] float height = 200f;           // banner height in pixels
    [SerializeField] float sidePadding = 24f;      // left/right padding
    [SerializeField] bool useSafeArea = true;      // respect phone notches, etc.

    public Canvas BottomBannerCanvas;              // Optional
    GameObject panel;
    RectTransform panelRT;
    TextMeshProUGUI label;
    Coroutine hideRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        //DontDestroyOnLoad(gameObject);

        BuildUIIfNeeded();
    }

    void BuildUIIfNeeded()
    {
        // Canvas (Screen Space - Overlay)
        if (BottomBannerCanvas == null)
        {
            GameObject c = new GameObject("BottomBannerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            c.transform.SetParent(transform, false);
            BottomBannerCanvas = c.GetComponent<Canvas>();

            //  BottomBannerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            //  var scaler = c.GetComponent<CanvasScaler>();
            //  scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            //  scaler.referenceResolution = new Vector2(800, 800);
        }
        //else
        //{
        //canvas = go.GetComponent<Canvas>();
        BottomBannerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        BottomBannerCanvas.overrideSorting = true;
        BottomBannerCanvas.sortingOrder = 5000;     // high so it sits on top

        var scaler = BottomBannerCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 800);
        //}

        // Panel background
        panel = new GameObject("BannerPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(BottomBannerCanvas.transform, false);
        var img = panel.GetComponent<Image>();
        img.color = backgroundColor;
        panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0, 0.05f);   // bottom stretch
        panelRT.anchorMax = new Vector2(1, 0.05f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.sizeDelta = new Vector2(0, height);
        // set panelRT into the safe area
        var safe = Screen.safeArea;
        // Position panel at the bottom; add bottom inset if safe area eats into it
        float bottomInset = Mathf.Max(0, safe.y);
        panelRT.offsetMin = new Vector2(0, bottomInset);   // left,bottom
        panelRT.offsetMax = new Vector2(0, 0);             // right,top

        // Label (TMP)
        GameObject textGO = new GameObject("BannerText", typeof(TextMeshProUGUI));
        textGO.transform.SetParent(panel.transform, false);
        label = textGO.GetComponent<TextMeshProUGUI>();
        label.text = "";
        label.color = textColor;
        label.fontSize = fontSize;
        //label.enableWordWrapping = false; // obsolete
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        var tRT = label.rectTransform;
        tRT.anchorMin = new Vector2(0, 0);
        tRT.anchorMax = new Vector2(1, 1);
        tRT.offsetMin = new Vector2(sidePadding, 0);
        tRT.offsetMax = new Vector2(-sidePadding, 0);

        //if (useSafeArea) ApplySafeArea();
    }

    void OnRectTransformDimensionsChange()
    {
        if (useSafeArea && (panel != null)) ApplySafeArea();
    }

    void ApplySafeArea()
    {
        panelRT = panel.GetComponent<RectTransform>();
#if UNITY_EDITOR || UNITY_STANDALONE
        var safe = new Rect(0, 0, Screen.width, Screen.height);
#else
        var safe = Screen.safeArea;
#endif
        // Position panel at the bottom; add bottom inset if safe area eats into it
        float bottomInset = Mathf.Max(0, safe.y);
        panelRT.offsetMin = new Vector2(0, bottomInset);   // left,bottom
        panelRT.offsetMax = new Vector2(0, 0);             // right,top
    }

    // ----- Public API -----

    /// <summary>Show message until replaced or cleared.</summary>
    public static void Show(string message)
    {
        if (Instance == null) CreateSingleton();
        Instance._Show(message);
    }

    /// <summary>Show message for a duration, then clear.</summary>
    public static void ShowFor(string message, float seconds)
    {
        if (Instance == null) CreateSingleton();
        Instance._ShowFor(message, seconds);
    }

    /// <summary>Clear the banner.</summary>
    public static void Clear()
    {
        if (Instance == null) return;
        Instance._Clear();
    }

    // ----- Implementation -----

    void _Show(string message)
    {
        if (hideRoutine != null) { StopCoroutine(hideRoutine); hideRoutine = null; }
        if (label == null) BuildUIIfNeeded();
        label.text = message ?? "";
        panelRT.gameObject.SetActive(!string.IsNullOrEmpty(label.text));
    }

    void _Clear()
    {
        if (hideRoutine != null) { StopCoroutine(hideRoutine); hideRoutine = null; }
        if (label == null) return;
        label.text = "";
        panelRT.gameObject.SetActive(false);
    }

    void _ShowFor(string message, float seconds)
    {
        _Show(message);
        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfter(seconds));
    }

    IEnumerator HideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _Clear();
    }

    static void CreateSingleton()
    {
        var go = new GameObject("BottomBanner");
        go.AddComponent<BottomBanner>(); // Awake builds the UI
    }

}
