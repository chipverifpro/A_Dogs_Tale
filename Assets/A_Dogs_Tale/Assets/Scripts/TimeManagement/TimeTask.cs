using System.Collections;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

/// <summary>
/// Per-coroutine cooperative time task.
/// Create via TimeManager.Instance.BeginTask(name) → call IfYield() inside your loop.
/// </summary>
public sealed class TimeTask
{
    public ObjectDirectory dir;
    public DungeonSettings cfg;
    public string Name { get; }
    readonly TimeManager _mgr;
    bool _active;
    float _lastStamp;          // last decision time (seconds)
    bool _hasStamp;

    // Local stats (quick access; aggregated by name in manager)
    float _avgInterval;        // EWMA inside this task instance
    float _maxInterval;
    long  _calls;

    // Prediction parameters
    const float SeedInterval   = 0.0005f;  // 0.5 ms seed until we measure
    const float MinInterval    = 0.0001f;  // lower floor to avoid zero
    const float EWMA_Alpha     = 0.15f;

    internal TimeTask(TimeManager mgr, string name)
    {
        _mgr  = mgr;
        Name  = string.IsNullOrEmpty(name) ? "Task" : name;
        _mgr.Register(this);
        _active = true;
    }

    /// <summary>
    /// Decide whether to yield now. Call this at the end of each work chunk.
    /// Returns true → you should `yield return null`; false → continue working this frame.
    /// </summary>
    public bool IfYield()
    {
        // return false; // DEBUG
        float now = _mgr.Now;
        float chunk = 0f;
        // Measure the chunk we just finished (time since last decision)
        if (_hasStamp)
        {
            chunk = Mathf.Max(0f, now - _lastStamp);

            // local EWMA + max
            if (_avgInterval == 0f) _avgInterval = chunk;
            else _avgInterval += EWMA_Alpha * (chunk - _avgInterval);

            if (chunk > _maxInterval) _maxInterval = chunk;

            // report to manager (adds to frame usage & name-based stats)
            _mgr.ReportChunk(Name, chunk);
        }

        // Prepare for next round
        _lastStamp = now;
        _hasStamp  = true;
        _calls++;
        // Predict next chunk from EWMA (seeded)
        float predicted = Mathf.Max(MinInterval, (_avgInterval > 0f ? _avgInterval : SeedInterval));

        // Fair-share soft cap
        float softCap = _mgr.SoftCapPerTask();

        softCap = Mathf.Max(MinInterval, softCap); // avoid zero

        // Let task continue if within its fair share and we can reserve
        bool allowContinue = (predicted <= softCap * 1.05f) // slight slack
                             && _mgr.TryReserveForNextChunk(predicted);

        _mgr.CountIfYield(Name, yielded: !allowContinue);
        //if (chunk / _mgr.budgetPercent > 1f) Debug.Log(Name + ": chunk/softCap " + chunk/softCap);  //DEBUG
        return !allowContinue;
    }

    /// <summary>
    /// Either does a timed delay (if cfg.showBuildProcess = true),
    /// or behaves like IfYield() (if false).
    /// </summary>
    public IEnumerator YieldOrDelay(float seconds, bool realtime = false)
    {
        if (cfg.showBuildProcess) // global flag
        {
            // End current chunk measurement so stats are correct
            if (IfYield()) yield return null;

            if (realtime)
                yield return new WaitForSecondsRealtime(seconds);
            else
                yield return new WaitForSeconds(seconds);

            // reset stamp after delay so next chunk starts fresh
            _hasStamp = false;
        }
        else
        {
            if (IfYield()) yield return null;
        }
    }

    /// <summary> End this task (always call, e.g. in finally). </summary>
    public void End()
    {
        if (!_active) return;
        _active = false;
        _mgr.Unregister(this);
    }
}