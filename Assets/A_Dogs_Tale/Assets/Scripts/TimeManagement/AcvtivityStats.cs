using System.Collections;
using UnityEngine;

// This class gives quick feedback on activity statistics
public class AcvtivityStats : MonoBehaviour
{
    [Header("Enable activity statistics collection.")]
    public bool EnableStatistics = true;
    public bool reset = false;

    [Header("Measured values")]
    public float FPS = 60f;
    public float FPS_Interval = 1f/60f; // measured FPS interval
    public float startTime = 0f;
    public float runtimeSinceStartup = 0f;
    public int frameCount = 0;


    [Header("Per function statistics")]
    [Header("Scent physics updates")]
    public float scentPhysicsStepOnce_cumulative = 0f;
    public int scentPhysicsStepOnce_calls = 0;
    public float scentPhysicsStepOnce_avg_per_second = 0;


    [Header("Object updates")]
    public float BuildNewInstancesForLayer_cumulative = 0f;
    public int BuildNewInstancesForLayer_calls = 0;
    public float BuildNewInstancesForLayer_avg_per_second = 0;
    public float ApplyPendingUpdates_cumulative = 0f;
    public int ApplyPendingUpdates_calls = 0;
    public float ApplyPendingUpdates_avg_per_second = 0;



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
            scentPhysicsStepOnce_cumulative = 0f;
            scentPhysicsStepOnce_calls = 0;
            BuildNewInstancesForLayer_cumulative = 0f;
            BuildNewInstancesForLayer_calls = 0;
            ApplyPendingUpdates_cumulative = 0f;
            ApplyPendingUpdates_calls = 0;
        }

        runtimeSinceStartup = Mathf.Min(Time.realtimeSinceStartup-startTime, 0.0001f); // avoid div by zero
        frameCount += 1;
    }

    void CalcStats()
    {
        FPS_Interval = runtimeSinceStartup / frameCount;
        FPS = 1f / FPS_Interval;
        scentPhysicsStepOnce_avg_per_second = (scentPhysicsStepOnce_cumulative / scentPhysicsStepOnce_calls);
        BuildNewInstancesForLayer_avg_per_second = (BuildNewInstancesForLayer_cumulative / BuildNewInstancesForLayer_calls);
        ApplyPendingUpdates_avg_per_second = (ApplyPendingUpdates_cumulative / ApplyPendingUpdates_calls);
    }
}
