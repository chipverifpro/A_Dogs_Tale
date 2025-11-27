using System.Collections.Generic;
using UnityEngine;

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

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Update()
    {
        float dt = Time.deltaTime;
        
        // deposit normal scent
        normalScentSource.Emit(worldObject.Location.cell, dt, decayed: 1.0f);

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
            dss.scentSource.Emit(worldObject.Location.cell, dt, decayed: 1.0f);
            dss.time_remaining -= dt;
            if (dss.duration <= dss.time_remaining) // faded away
            {
                durationScentSources.RemoveAt(dss_index);
            }
        }
    }

    public bool MarkScent(float dt)
    {
        // deposit onDemandScentSource
        if (onDemandScentSource==null) return false;
        onDemandScentSource.Emit(worldObject.Location.cell, dt, decayed: 1.0f);    // TODO: make this a coroutine so action will consume time.
        return true;
    }

}