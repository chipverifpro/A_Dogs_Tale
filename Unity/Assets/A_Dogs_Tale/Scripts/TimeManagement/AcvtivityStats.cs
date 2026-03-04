using System.Collections;
using UnityEngine;

// This class gives quick feedback on activity statistics
public class AcvtivityStats : MonoBehaviour
{
    [Header("Enable activity statistics collection.")]
    public bool EnableStatistics = true;
    public bool reset = false;
    [SerializeField] float smoothing = 0.1f; // 0..1, higher = more responsive

    [Header("Measured values")]
    public float FPS = 60f;
    public float FPS_Interval = 1f/60f; // measured FPS interval
    public float startTime = 0f;
    public float runtimeSinceStartup = 0f;
    public int frameCount = 0;


    [Header("Per function statistics")]
    [Header("Scent physics updates")]
    public float physics_cumulative = 0f;
    public int physics_calls = 0;
    public float physics_avg_per_second = 0;
    public int cellsProcessed = 0;
    public int scentsProcessed = 0;



    [Header("Object updates")]
    public float BuildNewInstances_cumulative = 0f;
    public int BuildNewInstances_calls = 0;
    public float BuildNewInstances_avg_per_second = 0;
    public float ApplyUpdates_cumulative = 0f;
    public int ApplyUpdates_calls = 0;
    public float ApplyUpdates_avg_per_second = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        reset = true;   // will trigger an initial reset on first Update
    }

    // Update is called once per frame
    void Update()
    {
        if (!EnableStatistics)
            return;
        GetRawStats();
        CalcStats();
    }

    void GetRawStats()
    {
        if (reset)
        {
            reset = false;
            startTime = Time.realtimeSinceStartup;
            FPS_Interval = Time.deltaTime;
            FPS = 1f / FPS_Interval;
            frameCount = 0;

            // reset per-function stats
            physics_cumulative = 0f;
            physics_calls = 0;
            BuildNewInstances_cumulative = 0f;
            BuildNewInstances_calls = 0;
            ApplyUpdates_cumulative = 0f;
            ApplyUpdates_calls = 0;
        }

        runtimeSinceStartup = Mathf.Max(Time.realtimeSinceStartup-startTime, 0.0001f); // avoid div by zero
        frameCount += 1;
    }

    void CalcStats()
    {
        float dt = Time.unscaledDeltaTime;
        float current = 1f / Mathf.Max(dt, 0.0001f);
        FPS = Mathf.Lerp(FPS, current, smoothing);
        FPS_Interval = 1f / FPS;
        //FPS_Interval = Time.deltaTime;
        //FPS = 1f / Mathf.Max(FPS_Interval, 0.0001f);
        physics_avg_per_second = (physics_cumulative / physics_calls);
        BuildNewInstances_avg_per_second = (BuildNewInstances_cumulative / BuildNewInstances_calls);
        ApplyUpdates_avg_per_second = (ApplyUpdates_cumulative / ApplyUpdates_calls);
    }
}
