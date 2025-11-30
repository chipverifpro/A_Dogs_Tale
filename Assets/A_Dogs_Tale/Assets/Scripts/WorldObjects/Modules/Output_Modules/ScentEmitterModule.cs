using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DurationScentSource    // scent fades over duration
{
    public ScentSource scentSource;
    public float duration;
    public float time_remaining;
}

public class ScentEmitterModule : WorldModule
{
    public ScentSource normalScentSource;       // constant scent
    public ScentSource onDemandScentSource;     // mark territory
    public List<DurationScentSource> durationScentSources;  // from contact: skunk spray, muddy
    public float deposit_time_left;

    protected override void Awake()
    {
        base.Awake();
        deposit_time_left = 0f;
    }

    protected override void Update()
    {
        float dt = Time.deltaTime;

        // deposit normal scent
        normalScentSource.Emit(worldObject.locationModule.cell, dt, decayed: 1.0f);

        // emit all temporary scents
        DurationScentSource dss;
        // deposit and update durations remaining for durationScents
        if (durationScentSources == null || durationScentSources.Count == 0)
            return;
        for (int dss_index = durationScentSources.Count-1; dss_index >= 0; dss_index--)
        {
            dss = durationScentSources[dss_index];
            if (dss==null) continue; // should never happen
            float decayed = dss.time_remaining / dss.duration; // scent decays over time
            dss.scentSource.Emit(worldObject.locationModule.cell, dt, decayed: decayed);
            dss.time_remaining -= dt;
            if (dss.duration <= dss.time_remaining) // faded away
            {
                durationScentSources.RemoveAt(dss_index);
            }
        }
        
        if ((onDemandScentSource!=null) && (deposit_time_left>0f))
        {
            onDemandScentSource.Emit(worldObject.locationModule.cell, dt, decayed: 1.0f);
        }
    }

    public bool EmitOnDemandScent(float duration)
    {
        if (onDemandScentSource==null) return false;

        deposit_time_left = duration;
        return true;
    }
}