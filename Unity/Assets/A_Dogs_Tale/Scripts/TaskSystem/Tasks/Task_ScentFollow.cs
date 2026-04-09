#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using DogGame.Modules;
using DogGame.LLM;
using static DungeonGenerator;
using NUnit.Framework;

namespace DogGame.Tasks
{
    public sealed class Task_ScentFollow : IAgentTask
    {
        public string DebugName => $"ScentFollow({scentKey},{medium})";
        public string Description = "Tracks a specific scent across nearby cells by maintaining local scent memory, choosing promising next steps, and moving cell by cell until the trail is found, exhausted, or graceful limits are reached.";

        private readonly string scentKey;
        private readonly ScentMedium medium;

        // ---- Tuning knobs ----
        private readonly float minStrengthToContinue01;
        private readonly float stepCooldownSeconds;
        private readonly int maxSteps;
        private readonly float maxSeconds;

        // Scoring weights
        //private readonly float wScent = 1.0f;
        //private readonly float wNovel = 0.35f;
        //private readonly float wVisit = 0.75f;
        //private readonly float wBacktrack = 0.50f;

        // How strongly we avoid very recent cells
        //private readonly int tabooLength = 4;

        // When "stuck", we allow mild downhill to escape peaks
        private readonly int stuckStepsToExplore = 4;
        private readonly float improvementEpsilon = 0.01f;

        // ---- More Scoring weights ----
        private float wStrength = 1.00f;
        private float wVisitPenalty = 0.35f;      // discourages repeated cells
        private float wRecentVisitPenalty = 0.45f; // discourages oscillation
        private float wStalePenalty = 0.25f;      // discourages far-old cells on JumpToHighestScore
        private float immediateBacktrackPenalty = 0.60f;
        private float wRiseBonus = 0.06f;      // max bonus added
        private float riseScale  = 0.02f;      // delta that maps to full bonus

        // Time constants
        private float recentVisitWindowSeconds = 2.0f;   // "I was just here"
        private float staleMemorySeconds = 15.0f;        // memory older than this gets penalized in jumping
        
        // ---- Runtime state ----
        private bool started;
        private float startedTime;
        private float nextStepTime;
        private int stepsTaken;

        private Vector2Int lastCellPos;
        private Vector2Int prevCellPos;  // for immediate backtrack penalty
        private float lastChosenScore;
        private int stuckCounter;

        private bool prev_exploring=false;
        // Optional “result” fields (for debug/UI)
        public DirFlags lastBestDir = DirFlags.None;
        public Vector2Int lastBestPos;
        public float lastBestStrength;

        public Task_MoveToCell? moveToCell;

        Dir dir => Dir.Instance;
        
        // ---- Track memory ----
        private struct CellTrackInfo
        {
            public float bestStrength01;     // best ever (useful for “where was it strongest historically”)
            public float prevStrength01;
            public float lastStrength01;     // last observed (useful for “what is it like now”)
            public float lastSeenTime;
            public int visitCount;
            public float lastVisitTime;
        }

        private readonly Dictionary<Vector2Int, CellTrackInfo> memory = new();
        //private readonly Queue<Vector2Int> tabooQueue = new();        // recent visited cells
        //private readonly HashSet<Vector2Int> tabooSet = new();        // fast lookup

        public Task_ScentFollow(
            string scentKey,
            ScentMedium medium,
            float minStrengthToContinue01 = 0.0002f,
            float stepCooldownSeconds = 0.20f,
            int maxSteps = 100,
            float maxSeconds = 120f)
        {
            this.scentKey = scentKey ?? "";
            this.medium = medium;
            this.minStrengthToContinue01 = Mathf.Clamp01(minStrengthToContinue01);
            this.stepCooldownSeconds = Mathf.Clamp(stepCooldownSeconds, 0.05f, 5f);
            this.maxSteps = Mathf.Clamp(maxSteps, 1, 500);
            this.maxSeconds = Mathf.Clamp(maxSeconds, 0.5f, 120f);
        }

        public void Start(TaskContext context)
        {
            started = true;
            startedTime = Time.time;
            nextStepTime = Time.time;
            stepsTaken = 0;

            lastBestDir = DirFlags.None;
            lastBestStrength = 0f;
            lastBestPos = default;

            lastChosenScore = float.NegativeInfinity;
            stuckCounter = 0;

            memory.Clear();
            //tabooQueue.Clear();
            //tabooSet.Clear();

            if (context.Agent != null && context.Agent.locationModule != null)
            {
                lastCellPos = context.Agent.locationModule.cell.pos;
                prevCellPos = lastCellPos;

                NoteVisit(lastCellPos);
                // prime memory for current cell if possible
                TryNoteScentAt(context, lastCellPos);

                WorldObject otherAgent = context.Agent;
                // as soon as we commit to tracking, mark as Scented
                //context.Agent?.scentPerceptionModule?.PromoteScentFamiliarity(scentKey, ScentFamiliarity.Scented);
                context.Agent?.scentPerceptionModule?.IdentifyScent(
                    scentKey: $"agent:{otherAgent.ObjectId}",
                    displayName: otherAgent.DisplayName,
                    agentId: otherAgent.ObjectId);
            }

            Debug.Log("Task_ScentFollow.Start");
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            if (!started)
                Start(context);

            // 0) If we are already moving, keep driving that task
            if (moveToCell != null)
            {
                var moveResult = moveToCell.Tick(context, deltaTimeSeconds);
                if (moveResult.Status == TaskStatus.Running || moveResult.Status == TaskStatus.Failed || moveResult.Status == TaskStatus.NotStarted)
                    return moveResult;

                if (moveResult.Status == TaskStatus.Succeeded)
                {
                    moveToCell = null;

                    // arrival bookkeeping
                    if (context.Agent?.locationModule != null)
                    {
                        prevCellPos = lastCellPos;
                        lastCellPos = context.Agent.locationModule.cell.pos;
                        NoteVisit(lastCellPos);
                        TryNoteScentAt(context, lastCellPos);

                        DebugPrintScentMemory();
                    }

                    if (IsTargetReached(context))
                    {
                        Debug.Log($"[ScentFollow] Found target for {scentKey}");
                        return TaskTickResult.Succeeded();
                    }

                    // TODO: this should only be used If you’re tracking a category (Food/Human/etc.) and don’t have a specific agent id.
                    if (IsHeuristicFound(lastCellPos))
                    {
                        Debug.Log($"[ScentFollow] Found target using IsHeuristicFound for {scentKey}");
                        return TaskTickResult.Succeeded();
                    }
                }
            }

            if (context.Agent == null)
                return TaskTickResult.Failed("missing_agent");

            var scentModule = context.Agent.scentPerceptionModule;
            if (scentModule == null)
                return TaskTickResult.Failed("missing_scent_perception_module");

            if (context.Agent.locationModule == null)
                return TaskTickResult.Failed("missing_location_module");

            // 1) Global stop conditions
            float elapsed = Time.time - startedTime;
            if (elapsed > maxSeconds)
            {
                Debug.Log($"Timed out gracefully ({elapsed:0.00}s).");
                return TaskTickResult.Succeeded();
            }

            if (stepsTaken >= maxSteps)
            {
                Debug.Log($"Step limit reached gracefully ({stepsTaken}).");
                return TaskTickResult.Succeeded();
            }

            if (Time.time < nextStepTime)
                return TaskTickResult.Running();

            nextStepTime = Time.time + stepCooldownSeconds;

            // 2) Sniff current + neighbors -> update memory
            Cell cell = context.Agent.locationModule.cell;
            Vector2Int centerPos = cell.pos;
            int height = cell.height;

            UpdateMemoryFromLocalSniff(context, scentModule, centerPos, height);

            // 3) Choose next step using memory + taboo + explore logic
            bool exploring = stuckCounter >= stuckStepsToExplore;
            if(exploring != prev_exploring)
            {
                Debug.Log($"exploring = {exploring}");
                prev_exploring = exploring;
            }

            if (!TryPickNextStep(
                    scentModule: scentModule,
                    centerPos: centerPos,
                    height: height,
                    exploring: exploring,
                    out DirFlags chosenDir,
                    out Vector2Int chosenPos,
                    out float chosenStrength,
                    out float chosenScore))
            {
                Debug.Log("No viable next step (likely boxed in or no scent).");
                //Experiment...
                chosenPos = JumpToHighestScore(centerPos);
                Debug.Log($"JumpToHighestScore {chosenPos}");
                if (centerPos==chosenPos)
                {
                    Debug.Log($"Already at best score.");
                    return TaskTickResult.Succeeded();
                }
                moveToCell = new Task_MoveToCell(chosenPos.x, chosenPos.y, 0.25f);
                moveToCell.Start(context);
                stepsTaken++;
                return TaskTickResult.Running();
                //return TaskTickResult.Succeeded();
            }

            lastBestDir = chosenDir;
            lastBestPos = chosenPos;
            lastBestStrength = chosenStrength;

            if (chosenStrength < minStrengthToContinue01)
            {
                Debug.Log("Scent too weak to continue.");
                return TaskTickResult.Succeeded();
            }

            // 4) Stuck detection: if we aren’t improving, start exploring
            if (chosenScore <= lastChosenScore + improvementEpsilon)
                stuckCounter++;
            else
                stuckCounter = 0;

            lastChosenScore = chosenScore;

            // 5) Issue move one step
            moveToCell = new Task_MoveToCell(chosenPos.x, chosenPos.y, 0.25f);
            moveToCell.Start(context);

            stepsTaken++;
            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
        }

        // =========================================================
        // Memory + scoring
        // =========================================================


        /// <summary>
        /// Pull tracked scent strength at a given cell from the dungeon cell scents.
        /// This matches your scentKey convention "agent:<id>".
        /// </summary>
        private float GetScentStrengthAtCell(ScentPerceptionModule scentModule, Vector2Int pos, int height)
        {
            // Uses the same underlying data you already use in TryFindStrongestNeighborForScent.
            var dir = scentModule.dir;
            if (dir == null || dir.gen == null || dir.gen.hf == null)
                return 0f;

            dir.gen.hf.TryQueryAt(pos.x, pos.y, height, 50, out NeighborMatch match);
            if (match.roomId < 0 || match.cellId < 0)
                return 0f;

            Cell cell = dir.gen.rooms[match.roomId].cells[match.cellId];
            if (cell.scents == null)
                return 0f;

            float best = 0f;

            foreach (var s in cell.scents)
            {
                string cellKey = $"agent:{s.agentId}";
                if (cellKey != scentKey)
                    continue;

                float v = (medium == ScentMedium.Ground) ? s.groundIntensity : s.airIntensity;
                if (v > best) best = v;
            }

            return best;
        }

        private void TryNoteScentAt(TaskContext context, Vector2Int pos)
        {
            var agent = context.Agent;
            if (agent == null) return;

            var scentModule = agent.scentPerceptionModule;
            if (scentModule == null) return;

            int height = agent.locationModule.cell.height;

            // You implement this in ScentPerceptionModule
            if (!scentModule.TryGetScentStrengthAtCell(scentKey, pos, height, medium, out float s01))
                return;

            if (s01 <= 0f)
                return;

            float now = Time.time;

            if (!memory.TryGetValue(pos, out var info))
                info = default;

            if (s01 > info.bestStrength01)
                info.bestStrength01 = s01;

            info.lastSeenTime = now;

            memory[pos] = info;
        }

        private bool TryPickNextStep(
            ScentPerceptionModule scentModule,
            Vector2Int centerPos,
            int height,
            bool exploring,
            out DirFlags bestDir,
            out Vector2Int bestPos,
            out float bestStrength,
            out float bestScore)
        {
            bestDir = DirFlags.None;
            bestPos = default;
            bestStrength = 0f;
            bestScore = float.NegativeInfinity;

            float currentStrength = GetKnownStrength(centerPos);

            foreach (DirFlags dir in DirFlagsEx.All8)
            {
                Vector2Int p = centerPos + dir.ToVector2Int();
                bool isImmediateBacktrack = (p == prevCellPos);

                float strength01 = GetLastStrength(p);

                // If we don't know strength from memory, try a live query
                if (strength01 <= 0f && scentModule.TryGetScentStrengthAtCell(scentKey, p, height, medium, out float sLive))
                    strength01 = sLive;

                if (strength01 <= 0f)
                    continue;

                // Non-exploring mode: still mostly hill-climb,
                // but allow a small downhill step if it reduces repetition/looping.
                if (!exploring)
                {
                    // Allow mild downhill if it helps escape loops (tolerance depends on your scent scale)
                    float downhillTolerance = 0.02f; // you used 0.01; bump slightly for stability
                    if (strength01 + downhillTolerance < currentStrength)
                    {
                        // If it’s a backtrack AND downhill, skip it (usually useless)
                        if (isImmediateBacktrack)
                            continue;

                        // Otherwise allow it, score will decide
                    }
                }

                // Use rise delta from memory (works even though sniff just ran)
                float delta01 = GetRiseDelta(p);
                float score = ScoreNeighbor(p, strength01, false, exploring, delta01);
                
                //if (tabooSet.Contains(p))
                //    score -= 1.25f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = dir;
                    bestPos = p;
                    bestStrength = strength01;
                }
            }

            return bestDir != DirFlags.None;
        }

        private float ScoreNeighbor(Vector2Int pos, float strength01, bool isImmediateBacktrack, bool exploring, float delta01)
        {
            // Base: stronger is better
            float score = wStrength * strength01;

            // Rising scent bonus (small, capped).
            // Helps escape plateaus and interpret “getting warmer”.
            // Negative delta gets no bonus (don’t punish—staleness & visit penalties already handle it).
            const float maxRiseBonus = 0.25f;        // tune: 0.1..0.4
            const float riseScale = 0.15f;           // tune: how much delta counts
            if (delta01 > 0f)
                score += Mathf.Clamp(delta01 / riseScale, 0f, 1f) * maxRiseBonus;

            // Penalize revisits (soft curve)
            int visits = GetVisitCount(pos);
            float visitPenalty = Mathf.Log(1f + visits);
            score -= wVisitPenalty * visitPenalty;

            // Penalize very recent revisits (stops oscillation)
            float lastVisit = GetLastVisitTime(pos);
            float dtVisit = Time.time - lastVisit;
            if (dtVisit >= 0f && dtVisit < recentVisitWindowSeconds)
            {
                float t = 1f - (dtVisit / recentVisitWindowSeconds);
                score -= wRecentVisitPenalty * t;
            }

            if (isImmediateBacktrack)
                score -= immediateBacktrackPenalty;

            // Exploration mode: novelty bonus
            if (exploring)
            {
                float noveltyBonus = 0.0f;

                if (visits == 0) noveltyBonus += 0.35f;
                else noveltyBonus += 0.15f / (1f + visits);

                if (dtVisit > recentVisitWindowSeconds)
                    noveltyBonus += 0.10f;

                score += noveltyBonus;
            }

            // Staleness penalty (already good)
            float seenAge = GetSeenAgeSeconds(pos);
            if (seenAge > staleMemorySeconds)
            {
                float extra = (seenAge - staleMemorySeconds) / staleMemorySeconds;
                score -= wStalePenalty * Mathf.Clamp01(extra);
            }

            if (seenAge < 2.0f) // < 1.0 or < 2.0f depending on step rate
                score += RiseBonus01(delta01);

            return score;
        }

        public Vector2Int JumpToHighestScore(Vector2Int currentPos)
        {
            float hiScore = float.NegativeInfinity;
            Vector2Int bestPos = currentPos;

            foreach (var kvp in memory)
            {
                Vector2Int pos = kvp.Key;
                var info = kvp.Value;

                float strength01 = info.bestStrength01;
                if (strength01 <= 0f)
                    continue;

                // Base score uses the same function (treat as exploring)
                float delta01 = GetStrengthDelta(pos, strength01);
                float score = ScoreNeighbor(pos, strength01, isImmediateBacktrack: false, exploring: true, delta01);

                // Penalize stale cells so we don't jump back across the world.
                float seenAge = Time.time - info.lastSeenTime;
                if (seenAge > staleMemorySeconds)
                {
                    // linear penalty after the staleness window
                    float extra = (seenAge - staleMemorySeconds) / staleMemorySeconds; // 0..1..2...
                    score -= wStalePenalty * Mathf.Clamp01(extra);
                }

                // Optional: small distance penalty (keeps you local)
                int manhattan = Mathf.Abs(pos.x - currentPos.x) + Mathf.Abs(pos.y - currentPos.y);
                score -= 0.01f * manhattan; // tiny: only matters when scores are close

                if (score > hiScore)
                {
                    hiScore = score;
                    bestPos = pos;
                }
            }

            return bestPos;
        }

        private float RiseBonus01(float delta01)
        {
            if (delta01 <= 0f) return 0f;
            float t = Mathf.Clamp01(delta01 / riseScale); // 0..1
            return wRiseBonus * t;
        }

        private void DebugPrintScentMemory()
        {
            if (memory == null || memory.Count == 0)
            {
                Debug.Log("[ScentFollow] Memory is empty");
                return;
            }

            System.Text.StringBuilder sb = new();
            sb.AppendLine($"[ScentFollow] Memory ({memory.Count} cells):");

            foreach (var kvp in memory)
            {
                Vector2Int pos = kvp.Key;
                var cell = kvp.Value;

                sb.AppendLine(
                    $"  {pos.x,3},{pos.y,3} | " +
                    $"strength={cell.bestStrength01:0.000} " +
                    $"visits={cell.visitCount} " +
                    $"lastSeen={Time.time - cell.lastSeenTime:0.0}s ago"
                );
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Samples scent field around centerPos (radius 1..2 recommended) and estimates a gradient direction.
        /// Returns a bestDir and confidence [0..1].
        ///
        /// - Does NOT "teleport" knowledge: it only looks at nearby cells you can query.
        /// - Works for both Air/Ground using ScentInCell intensities.
        /// </summary>
        public bool TryEstimateScentGradient(
            string scentKey,
            Vector2Int centerPos,
            int height,
            ScentMedium medium,
            int radius,
            out DirFlags bestDir,
            out float confidence01)
        {
            bestDir = DirFlags.None;
            confidence01 = 0f;

            if (string.IsNullOrWhiteSpace(scentKey))
                return false;

            // Safety
            if (dir == null || dir.gen == null || dir.gen.hf == null)
                return false;

            radius = Mathf.Clamp(radius, 1, 3); // keep sane
            NeighborMatch match;

            // We'll estimate a gradient vector by comparing sampled strength at offsets.
            // For radius=2, we sample all offsets in the square [-2..2] excluding [0,0].
            // We weight by:
            //   - distance (1.0 for dist=1, 0.5 for dist=2, 0.33 for dist=3)
            //   - optional mild diagonal penalty (diagonals are farther)
            Vector2 gradient = Vector2.zero;
            float totalWeight = 0f;

            // Sample strength at center to allow "is it improving?" logic if you want it later.
            float centerStrength = GetScentStrengthAtCellUnsafe(scentKey, centerPos, height, medium);

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int manhattan = Mathf.Abs(dx) + Mathf.Abs(dy);
                    int chebyshev = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)); // 1 or 2 typically

                    // Prefer nearer samples; keep it simple.
                    // For radius=2: dist1 weight=1.0, dist2 weight=0.5.
                    float distWeight = chebyshev switch
                    {
                        1 => 1.0f,
                        2 => 0.5f,
                        3 => 0.33f,
                        _ => 0.0f
                    };
                    if (distWeight <= 0f)
                        continue;

                    // Mild diagonal penalty so (1,1) doesn't overvote (1,0)+(0,1).
                    bool isDiagonal = (dx != 0 && dy != 0);
                    float diagPenalty = isDiagonal ? 0.85f : 1.0f;

                    Vector2Int pos = new Vector2Int(centerPos.x + dx, centerPos.y + dy);

                    // Query that cell
                    dir.gen.hf.TryQueryAt(pos.x, pos.y, height, 50, out match);
                    if (match.roomId < 0 || match.cellId < 0)
                        continue;

                    Cell cell = dir.gen.rooms[match.roomId].cells[match.cellId];
                    if (cell.scents == null)
                        continue;

                    float strength = 0f;
                    if (!TryGetScentStrengthInCell(cell, scentKey, medium, out strength))
                        continue;

                    // If you want realism/noise for air scent, you can add a tiny jitter:
                    // if (medium == ScentMedium.Air) strength *= UnityEngine.Random.Range(0.95f, 1.05f);

                    // Convert sample into "push" away from center:
                    // Use deltaStrength so a uniform pool doesn't create a fake direction.
                    float delta = Mathf.Max(0f, strength - centerStrength);

                    float w = distWeight * diagPenalty;

                    // Unit direction toward sample
                    Vector2 dirVec = new Vector2(dx, dy);
                    float mag = dirVec.magnitude;
                    if (mag > 0.0001f)
                        dirVec /= mag;

                    gradient += dirVec * (delta * w);
                    totalWeight += (delta * w);
                }
            }

            if (totalWeight <= 0f || gradient.sqrMagnitude <= 0.000001f)
            {
                // No meaningful gradient (pool or nothing detected nearby)
                bestDir = DirFlags.None;
                confidence01 = 0f;
                return false;
            }

            // Convert gradient vector to one of 8 DirFlags
            bestDir = DirFlagsEx.FromVector2(gradient);

            // Confidence: how strong is the gradient relative to weights
            // Clamp into [0..1] for easy gating.
            confidence01 = Mathf.Clamp01(totalWeight * 2.0f);

            return bestDir != DirFlags.None;
        }

        /// <summary>
        /// Helper: Find strength for scentKey in an already-known cell.
        /// </summary>
        private static bool TryGetScentStrengthInCell(Cell cell, string scentKey, ScentMedium medium, out float strength)
        {
            strength = 0f;

            if (cell.scents == null || cell.scents.Count == 0)
                return false;

            for (int i = 0; i < cell.scents.Count; i++)
            {
                ScentInCell s = cell.scents[i];
                string cellKey = $"agent:{s.agentId}"; // matches your agent scentKey convention

                if (!string.Equals(cellKey, scentKey, StringComparison.Ordinal))
                    continue;

                strength = (medium == ScentMedium.Ground) ? s.groundIntensity : s.airIntensity;
                return strength > 0f;
            }

            return false;
        }

        /// <summary>
        /// Debug/utility: get scent strength at a position (returns 0 if not present).
        /// Keeps it "unsafe" (no exceptions) and minimal allocations.
        /// </summary>
        private float GetScentStrengthAtCellUnsafe(string scentKey, Vector2Int pos, int height, ScentMedium medium)
        {
            if (dir == null || dir.gen == null || dir.gen.hf == null)
                return 0f;

            NeighborMatch match;
            dir.gen.hf.TryQueryAt(pos.x, pos.y, height, 50, out match);
            if (match.roomId < 0 || match.cellId < 0)
                return 0f;

            Cell cell = dir.gen.rooms[match.roomId].cells[match.cellId];
            if (cell.scents == null)
                return 0f;

            float strength;
            return TryGetScentStrengthInCell(cell, scentKey, medium, out strength) ? strength : 0f;
        }

        private void UpdateMemoryFromLocalSniff(
            TaskContext context,
            ScentPerceptionModule scentModule,
            Vector2Int centerPos,
            int height)
        {
            // Smell within radius 2 (5x5). Helps detect gradients and reduces oscillation.
            const int radius = 2;

            if (context.Agent == null)
                return;

            // Sample neighborhood and update bestStrength01 per cell (do NOT count as "visited")
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    // Optional circle instead of square:
                    if (dx * dx + dy * dy > radius * radius) continue;

                    Vector2Int pos = new(centerPos.x + dx, centerPos.y + dy);

                    if (!TryIsValidCellAt(scentModule, pos, height))
                        continue;

                    float strength = GetTrackedScentStrengthAtCell(scentModule, pos, height);
                    if (strength > 0f)
                        NoteScentObserved(pos, strength);
                }
            }

            // Ensure center cell gets updated too (usually redundant, but safe)
            float centerStrength = GetTrackedScentStrengthAtCell(scentModule, centerPos, height);
            NoteScentObserved(centerPos, centerStrength);
        }

        private void NoteScentObserved(Vector2Int pos, float strength01)
        {
            float now = Time.time;

            if (!memory.TryGetValue(pos, out CellTrackInfo info))
            {
                info = new CellTrackInfo
                {
                    bestStrength01 = strength01,
                    prevStrength01 = strength01,   // init same
                    lastStrength01 = strength01,
                    lastSeenTime = now,
                    visitCount = 0,
                    lastVisitTime = -1f
                };
                memory[pos] = info;
                return;
            }

            // best ever
            if (strength01 > info.bestStrength01)
                info.bestStrength01 = strength01;

            // shift last -> prev, then write new last
            info.prevStrength01 = info.lastStrength01;
            info.lastStrength01 = strength01;

            info.lastSeenTime = now;
            memory[pos] = info;
        }

        // Call this ONLY when the agent actually arrives at a cell (you already do this on move success)
        private void NoteVisit(Vector2Int pos)
        {
            float now = Time.time;

            if (!memory.TryGetValue(pos, out CellTrackInfo info))
            {
                info = new CellTrackInfo
                {
                    bestStrength01 = 0f,
                    lastStrength01 = 0f,
                    lastSeenTime = -1f,
                    visitCount = 1,
                    lastVisitTime = now
                };
                memory[pos] = info;
                return;
            }

            info.visitCount += 1;
            info.lastVisitTime = now;
            memory[pos] = info;
        }

        private static bool TryIsValidCellAt(ScentPerceptionModule scentModule, Vector2Int pos, int height)
        {
            var dir = scentModule.dir;
            if (dir == null || dir.gen == null || dir.gen.hf == null)
                return false;

            dir.gen.hf.TryQueryAt(pos.x, pos.y, height, 50, out NeighborMatch match);
            return match.roomId >= 0 && match.cellId >= 0;
        }

        private float GetTrackedScentStrengthAtCell(ScentPerceptionModule scentModule, Vector2Int pos, int height)
        {
            var dir = scentModule.dir;
            if (dir == null || dir.gen == null || dir.gen.hf == null)
                return 0f;

            dir.gen.hf.TryQueryAt(pos.x, pos.y, height, 50, out NeighborMatch match);
            if (match.roomId < 0 || match.cellId < 0)
                return 0f;

            Cell cell = dir.gen.rooms[match.roomId].cells[match.cellId];
            if (cell.scents == null)
                return 0f;

            float best = 0f;

            foreach (ScentInCell s in cell.scents)
            {
                // You currently track agent scents like: "agent:<id>"
                string cellKey = $"agent:{s.agentId}";
                if (!string.Equals(cellKey, scentKey, StringComparison.Ordinal))
                    continue;

                float v = (medium == ScentMedium.Ground) ? s.groundIntensity : s.airIntensity;
                if (v > best) best = v;
            }

            return best;
        }

        private float GetKnownStrength(Vector2Int pos)
        {
            if (memory.TryGetValue(pos, out var info))
                return info.bestStrength01;
            return 0f;
        }

        private int GetVisitCount(Vector2Int pos)
        {
            if (memory.TryGetValue(pos, out var info))
                return info.visitCount;
            return 0;
        }

        private float GetLastVisitTime(Vector2Int pos)
        {
            if (memory.TryGetValue(pos, out var info))
                return info.lastVisitTime;
            return -999f;
        }

        private float GetLastSeenTime(Vector2Int pos)
        {
            if (memory.TryGetValue(pos, out var info))
                return info.lastSeenTime;
            return -999f;
        }

        private float GetLastStrength(Vector2Int pos)
        {
            return memory.TryGetValue(pos, out var info) ? info.lastStrength01 : 0f;
        }

        private float GetStrengthDelta(Vector2Int pos, float currentStrength)
        {
            if (!memory.TryGetValue(pos, out var info))
                return 0f;

            // delta vs last observed (positive means increasing)
            return currentStrength - info.lastStrength01;
        }

        private float GetRiseDelta(Vector2Int pos)
        {
            if (!memory.TryGetValue(pos, out var info))
                return 0f;

            return info.lastStrength01 - info.prevStrength01;
        }

        private float GetSeenAgeSeconds(Vector2Int pos)
        {
            return memory.TryGetValue(pos, out var info) ? (Time.time - info.lastSeenTime) : 999f;
        }

        private bool IsTargetReached(TaskContext context)
        {
            if (context.Agent == null) return false;

            // only works when scentKey is agent:<id>
            if (!TryParseAgentId(scentKey, out int targetId)) 
                return false;

            if (!WorldObjectRegistry.Instance.TryGet(targetId, out var target) || target == null)
                return false;

            // Cell-based proximity (recommended for your grid world)
            Vector2Int a = context.Agent.locationModule.cell.pos;
            Vector2Int b = target.locationModule.cell.pos;

            int manhattan = Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
            return manhattan <= 1; // same or adjacent
        }

        private static bool TryParseAgentId(string key, out int id)
        {
            id = -1;
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (!key.StartsWith("agent:", StringComparison.Ordinal)) return false;
            return int.TryParse(key.Substring("agent:".Length), out id);
        }

        private readonly float foundStrengthThreshold01 = 0.20f; // tune to your diffusion
        private readonly float plateauEpsilon = 0.005f;
        private float lastHeuristicStrength01 = 0f;
        private Vector2Int lastHeuristicPos;
        private bool hasLastHeuristicSample = false;
        private int plateauTicks = 0;

        // Use IsHeuristicFound only for “category-only” or “named non-agent sources”.
        private bool IsHeuristicFound(Vector2Int pos)
        {
            float current = GetLastStrength(pos); // from memory[pos].lastStrength01
            if (current < foundStrengthThreshold01)
            {
                plateauTicks = 0;
                hasLastHeuristicSample = false;
                return false;
            }

            // Only count plateau if we're sampling the same cell consecutively.
            if (hasLastHeuristicSample && pos == lastHeuristicPos)
            {
                if (current <= lastHeuristicStrength01 + plateauEpsilon)
                    plateauTicks++;
                else
                    plateauTicks = 0;
            }
            else
            {
                // New cell sample resets plateau tracking
                plateauTicks = 0;
                hasLastHeuristicSample = true;
            }

            lastHeuristicPos = pos;
            lastHeuristicStrength01 = current;

            return plateauTicks >= 3; // ~3 cycles “we’re basically at source”
        }

    }
}
