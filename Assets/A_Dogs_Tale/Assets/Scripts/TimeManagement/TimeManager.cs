using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TimeManager : MonoBehaviour
{
    // --- Config ---
    public Directory dir;
    public DungeonSettings cfg;
    [Range(0f, 100f)] public float budgetPercent = 0.95f;   // portion of each frame we allow work to consume
    public bool useUnscaledTime = true;                    // ignore Time.timeScale
    [Tooltip("Smoothing half-life (in frames) for per-frame budget readout. 0 = no smoothing.")]
    public int smoothingHalfLifeFrames = 0;

    public TimeTask BeginTask(string name) => new TimeTask(this, name);

    // --- Singleton ---
    static TimeManager _instance;
    public static TimeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject(nameof(TimeManager));
                _instance = go.AddComponent<TimeManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // --- Frame budget bookkeeping ---
    float _lastFrameDt;               // seconds
    float _budgetThisFrame;           // seconds allowed this frame
    float _remainingBudget;           // seconds remaining for cooperative work
    float _usedThisFrame;             // actual seconds used (reported by tasks)
    int _activeCount;               // tasks currently active

    // Optional smoothing of dt → budget
    float _smoothedDt;
    float _alphaDt;                   // EWMA alpha derived from half-life

    // --- Global stats ---
    public struct GlobalStats
    {
        public int frames;
        public float totalBudget;
        public float totalUsed;
        public float maxUtilization;      // max used/budget in a frame
        public int maxConcurrentTasks;
        public int totalTasksCompleted;
        public long totalIfYieldCalls;
    }
    GlobalStats _g;

    // Aggregated per-name stats across all task lifetimes
    public class NameStats
    {
        public string name;
        public double totalTime;      // sum of measured chunks
        public long ifYieldCalls;
        public float avgInterval;     // EWMA of chunk durations
        public float maxInterval;
        public float avgPctOfFrame;   // average of (chunk / frameDt)
        public float maxPctOfFrame;
        public long yieldsTaken;
        public long sessions;         // how many TimeTask instances of this name
    }
    readonly Dictionary<string, NameStats> _byName = new();

    // Active task registry (no allocs during IfYield)
    readonly HashSet<TimeTask> _active = new();

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        //DontDestroyOnLoad(gameObject);

        if (smoothingHalfLifeFrames > 0)
        {
            // alpha = ln(2) / half-life (per-frame EWMA)
            _alphaDt = Mathf.Log(2f) / Mathf.Max(1, smoothingHalfLifeFrames);
        }
        else _alphaDt = 1f; // no smoothing → directly follow dt

        InitializeConnections();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (_instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InitializeConnections();
    }

    void InitializeConnections()
    {
        // Try to find DungeonSettings if not assigned
        if (!cfg)
        {
            cfg = FindFirstObjectByType<DungeonSettings>();
            if (!cfg)
            {
                cfg = Resources.Load<DungeonSettings>("DungeonSettings");
                if (!cfg)
                    Debug.LogWarning("[TimeManager] No DungeonSettings found in scene or Resources!");
                else
                    Debug.Log("[TimeManager] Loaded DungeonSettings from Resources.");
            }
            else
            {
                Debug.Log($"[TimeManager] Connected to DungeonSettings: {cfg.name}");
            }
        }
    }

    void Update()
    {
        _lastFrameDt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        // Smooth dt (optional)
        if (_g.frames == 0) _smoothedDt = _lastFrameDt;
        else _smoothedDt += _alphaDt * (_lastFrameDt - _smoothedDt);

        // Set frame budget and reset counters
        _budgetThisFrame = Mathf.Max(0f, _smoothedDt) * Mathf.Clamp01(budgetPercent);
        _remainingBudget = _budgetThisFrame;
        _usedThisFrame = 0f;
        _activeCount = _active.Count;
        _g.maxConcurrentTasks = Mathf.Max(_g.maxConcurrentTasks, _activeCount);
    }

    void LateUpdate()
    {
        // End-of-frame accounting
        _g.frames++;
        _g.totalBudget += _budgetThisFrame;
        _g.totalUsed += Mathf.Min(_usedThisFrame, _budgetThisFrame);

        float util = (_budgetThisFrame > 1e-6f) ? (_usedThisFrame / _budgetThisFrame) : 0f;
        if (util > _g.maxUtilization) _g.maxUtilization = util;
    }

    // --- Called by tasks ---

    internal float Now => useUnscaledTime ? Time.realtimeSinceStartup : Time.time;
    internal float LastFrameDt => Mathf.Max(_lastFrameDt, 1e-6f);

    internal void Register(TimeTask task)
    {
        task.cfg = cfg;     // TODO: Is this the right place to set?
        if (_active.Add(task))
        {
            _activeCount = _active.Count;
            // bump per-name session count
            var ns = GetNameStats(task.Name);
            ns.sessions++;
        }
    }

    internal void Unregister(TimeTask task)
    {
        if (_active.Remove(task))
        {
            _activeCount = _active.Count;
            _g.totalTasksCompleted++;
        }
    }

    internal NameStats GetNameStats(string name)
    {
        if (!_byName.TryGetValue(name, out var ns))
        {
            ns = new NameStats { name = name };
            _byName[name] = ns;
        }
        return ns;
    }

    // Called by task after measuring a finished chunk
    internal void ReportChunk(string name, float chunkSeconds)
    {
        _usedThisFrame += Mathf.Max(0f, chunkSeconds);

        var ns = GetNameStats(name);
        ns.totalTime += chunkSeconds;
        if (chunkSeconds > ns.maxInterval) ns.maxInterval = chunkSeconds;

        // Update avg interval (EWMA with gentle alpha)
        const float a = 0.15f;
        ns.avgInterval = (ns.avgInterval == 0f)
            ? chunkSeconds
            : ns.avgInterval + a * (chunkSeconds - ns.avgInterval);

        // Percent-of-frame stats for this chunk
        float pct = chunkSeconds / LastFrameDt;
        // Clamp only for stat sanity (could exceed 1 if chunk > frame)
        pct = Mathf.Clamp(pct, 0f, 10f);

        // Running average (Kahan not necessary here)
        ns.avgPctOfFrame = (ns.avgPctOfFrame == 0f)
            ? pct
            : ns.avgPctOfFrame + 0.15f * (pct - ns.avgPctOfFrame);
        if (pct > ns.maxPctOfFrame) ns.maxPctOfFrame = pct;

        _byName[name] = ns;
    }

    internal void CountIfYield(string name, bool yielded)
    {
        _g.totalIfYieldCalls++;
        var ns = GetNameStats(name);
        ns.ifYieldCalls++;
        if (yielded) ns.yieldsTaken++;
        _byName[name] = ns;
    }

    // Reservation before doing the next chunk (prospective)
    internal bool TryReserveForNextChunk(float predictedSeconds)
    {
        if (predictedSeconds <= 0f) return true;
        if (_remainingBudget <= 0f) return false;

        // soft fairness guard: don't allow reserve if predicted is wildly larger than what's left
        if (predictedSeconds > _remainingBudget) return false;

        _remainingBudget -= predictedSeconds;
        return true;
    }

    internal float SoftCapPerTask()
    {
        int n = Mathf.Max(1, _activeCount);
        return _remainingBudget / n;
    }

    // --- Report ---

    public void DumpStats()
    {
        Debug.Log("=== TimeManager Stats ===");
        Debug.Log($"Frames:          {_g.frames}");
        Debug.Log($"Budget%:         {budgetPercent:P0} (unscaled:{useUnscaledTime})");
        Debug.Log($"Total Budget:    {_g.totalBudget:F4}s");
        Debug.Log($"Total Used:      {_g.totalUsed:F4}s");
        Debug.Log($"Max Utilization: {_g.maxUtilization:P1}");
        Debug.Log($"Max Concurrency: {_g.maxConcurrentTasks}");
        Debug.Log($"Tasks Completed: {_g.totalTasksCompleted}");
        Debug.Log($"IfYield Calls:   {_g.totalIfYieldCalls}");

        Debug.Log("---- Per-Task (by name) ----");
        foreach (var kv in _byName)
        {
            var ns = kv.Value;
            Debug.Log(
                $"{ns.name}  (sessions:{ns.sessions})\n" +
                $"  totalTime={ns.totalTime:F4}s, calls={ns.ifYieldCalls}, yields={ns.yieldsTaken}\n" +
                $"  avgInterval={ns.avgInterval * 1000f:F2}ms, maxInterval={ns.maxInterval * 1000f:F2}ms\n" +
                $"  avg%Frame={ns.avgPctOfFrame * 100f:F2}%, max%Frame={ns.maxPctOfFrame * 100f:F2}%"
            );
        }
    }
}

/*
TimeManager.cs was generated initially by ChatGPT(v5) from the following prompt:

I want to add a class TimeManagement to deal with time management and coroutine
yield calls.  The class will be instantiated in each coroutine, be initialized
with a text string name for identification.  It will measure how long on average
between calls to a if_yield call, and include that duration in its decision
whether to yield until the next frame or continue working.  That instance will
be killed or idled when the job is complete.  A global time manager will keep
track of how many time-management tasks are currently using the if_yield protocol,
and divide up the available frame-to-frame interval (combination of all tasks
should work about 95 percent of that period, as set by a configuration parameter)
between the various tasks in process.  I'd like to save the statistics for each
named task (include total time, total number of if_yield calls, average if_yield
interval, max if_yield interval).  It should also record average and max percent
of the frame-to-frame interval has been used, along with some total numbers.  At
end of run, a function can be called to display all the statistics collected.
Start by explaining your proposed implementation, and wait until I approve the
plan before writing the code.


Awesome idea. Here’s a clean, Unity-friendly plan that gives you cooperative frame budgeting across many coroutines, plus solid stats.

⸻

Goals you set
	•	Each coroutine creates a time task with a name (for stats/debug).
	•	Inside the loop, you call an IfYield() that decides whether to yield return null this frame, based on:
	•	how long your last work chunk took (measured),
	•	how much frame budget remains globally this frame,
	•	how many other tasks are active (fair share).
	•	A global manager shares ~95% of the last frame’s duration across active tasks (configurable).
	•	Collect per-task and global stats (totals, counts, averages, maxes, % of frame used).
	•	Dump a readable report at end.

⸻

High-level design

1) TimeManager (singleton MonoBehaviour)

Responsibilities
	•	Tracks per-frame budget and remaining budget.
	•	Knows the active task count.
	•	Decides if a task should yield now.
	•	Records global stats and can dump a report.

How it computes budget
	•	On every Update():
	•	Measure the last frame’s duration: frameDt = useUnscaled ? Time.unscaledDeltaTime : Time.deltaTime.
	•	totalBudgetThisFrame = frameDt * budgetPercent (e.g., 0.95).
	•	Reset remainingBudget = totalBudgetThisFrame.
	•	Optionally smooth the budget with an EWMA to avoid jitter (configurable “half life in frames”).

Fair sharing
	•	Each active task gets a soft cap this frame:
softCapPerTask = remainingBudget / activeTaskCount.
	•	Manager will let a task keep working if either:
	•	task’s predicted next chunk (task.predictedInterval) ≤ softCapPerTask, and we still have global remainingBudget, or
	•	there are very few tasks and we’re still well under the budget (simple guard to avoid under-utilization).

Config
	•	budgetPercent (default 0.95f).
	•	useUnscaledTime (true if you don’t want timescale to affect scheduling).
	•	smoothingHalfLifeFrames (0 to disable).
	•	Optional: priorities/weights per task later (not in v1).

Stats
	•	Global:
	•	frames observed, average budget per frame, average utilization (% used of available), max utilization, max concurrent tasks.
	•	Exposed method DumpStats() prints a tidy table.

⸻

2) TimeTask (per-coroutine instance)

Lifecycle
	•	Create with TimeManager.BeginTask("CarveRooms") (returns a TimeTask).
	•	Call task.IfYield() at the end of each work chunk in your loop.
	•	It measures elapsed since last call (chunk duration).
	•	Updates its EWMA average and max.
	•	Asks the manager if it should yield now.
	•	If yes: increments counters, updates stats, returns true (you yield return null).
	•	If no: deduct predicted cost from manager’s remainingBudget, returns false.
	•	Call task.End() at completion (or wrap in using/try/finally so it always deregisters).

What it tracks
	•	name
	•	isActive
	•	lastStamp (last tick time, from Time.realtimeSinceStartup or unscaled)
	•	avgInterval (EWMA of time per chunk)
	•	maxInterval
	•	totalInterval (sum of all measured chunk times)
	•	ifYieldCalls (how many times we hit the decision point)
	•	yieldsTaken
	•	% of frame used (per frame: chunkTime / frameDt; track avg & max)

Prediction
	•	predictedInterval = max(minFloor, avgInterval); start with a small seed (e.g., 0.0005s) until we have data.

Zero-alloc
	•	No LINQ, no GC per call; reuse strings as keys.

⸻

API sketch (how you’ll use it)

Setup once
	•	Add TimeManager component to a bootstrap GameObject (or let it FindOrCreate itself).
	•	Configure in Inspector:
	•	Budget Percent = 0.95
	•	Use Unscaled Time = true
	•	Smoothing Half Life = 8 frames (example)

In a coroutine
IEnumerator RegenerateDungeon()
{
    var task = TimeManager.Instance is null
        ? new TimeTask(TimeManager.Instance, "RegenerateDungeon") // instance will autocreate
        : TimeManager.Instance.BeginTask("RegenerateDungeon");    // see helper below
    try
    {
        for (int i = 0; i < steps; i++)
        {
            DoOneChunk(i); // your work

            if (task.IfYield())
                yield return null;
        }
    }
    finally { task.End(); }
}

Add this helper method to TimeManager for convenience:
public TimeTask BeginTask(string name) => new TimeTask(this, name);


Dump stats at end (or on key)
TimeManager.Instance.DumpStats();


⸻

Decision logic (simple & effective)

Inside TimeTask.IfYield():
	1.	Measure chunk = now - lastStamp (update stats: total, avg EWMA, max).
	2.	Compute predicted = max(minFloor, avgInterval).
	3.	Ask manager:
	•	softCap = remainingBudget / max(1, activeCount).
	•	If predicted <= softCap and remainingBudget >= predicted * 0.5 → continue (deduct predicted, return false).
	•	Else → yield (return true).
	4.	Update % of frame stats for this chunk:
pct = chunk / lastFrameDt (clamp 0..1), track avg & max.

Manager’s Update() each frame:
	•	Rebuild budgets, track active count, track global utilization from last frame (sum of chunks reported in that frame / available budget), update global stats.

This gives fair, order-independent cooperation without hard preemption. If 5 tasks are active, each tends to take ~1/5 of 95% of the frame; if one finishes, the others naturally get a larger share.

⸻

Nice extras (v2, optional)
	•	Task weights (e.g., map gen gets 2x of loot decoration).
	•	Hard cap: if remainingBudget drops below a small floor (e.g., 0.1 ms), force yield.
	•	Overrun accounting: if a task just used more than predicted, temporarily throttle that task next decision.
	•	Hot reload resilience: detect editor domain reload (OnDisable), auto-reset safely.
	•	On-screen HUD: toggleable overlay that shows per-task % usage bars this frame.

⸻

Why this will be deterministic and smooth
	•	All timing uses (un)scaled deltaTime and realtimeSinceStartup on the main thread.
	•	No reliance on call order: every task makes a local decision against the current shared budget and active task count.
	•	EWMA smooths noisy chunk durations, so tasks don’t oscillate between yield/continue.

⸻

What I’ll deliver after your 👍
	1.	TimeManager.cs (singleton MonoBehaviour)
	•	Config, per-frame budget, active set, global stats, DumpStats().
	2.	TimeTask.cs
	•	Created by manager, holds per-task state, IfYield() logic, stats.
	3.	Usage examples in your coroutines (one snippet for tight loops; one for nested loops).
	4.	A concise Console report (aligned columns) and optional on-screen HUD toggle.

Want me to proceed with code for the v1 (no priorities, with EWMA and stats + report)?
*/