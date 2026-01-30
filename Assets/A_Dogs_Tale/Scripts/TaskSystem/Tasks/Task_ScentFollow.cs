#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using DogGame.Modules;
using static DungeonGenerator;
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_ScentFollow : IAgentTask
    {
        public string DebugName => $"ScentFollow({scentKey},{medium})";

        private readonly string scentKey;
        private readonly ScentMedium medium;

        // ---- Tuning knobs ----
        private readonly float minStrengthToContinue01;
        private readonly float stepCooldownSeconds;
        private readonly int maxSteps;
        private readonly float maxSeconds;

        // Scoring weights
        private readonly float wScent = 1.0f;
        private readonly float wNovel = 0.35f;
        private readonly float wVisit = 0.75f;
        private readonly float wBacktrack = 0.50f;

        // How strongly we avoid very recent cells
        private readonly int tabooLength = 4;

        // When "stuck", we allow mild downhill to escape peaks
        private readonly int stuckStepsToExplore = 4;
        private readonly float improvementEpsilon = 0.01f;

        // ---- Runtime state ----
        private bool started;
        private float startedTime;
        private float nextStepTime;
        private int stepsTaken;

        private Vector2Int lastCellPos;
        private Vector2Int prevCellPos;  // for immediate backtrack penalty
        private float lastChosenScore;
        private int stuckCounter;

        // Optional “result” fields (for debug/UI)
        public DirFlags lastBestDir = DirFlags.None;
        public Vector2Int lastBestPos;
        public float lastBestStrength;

        public Task_MoveToCell? moveToCell;

        // ---- Track memory ----
        private struct CellTrackInfo
        {
            public float bestStrength01;     // best observed so far for tracked scent (for this medium)
            public float lastSeenTime;
            public int visitCount;
            public float lastVisitTime;
        }

        private readonly Dictionary<Vector2Int, CellTrackInfo> memory = new();
        private readonly Queue<Vector2Int> tabooQueue = new();        // recent visited cells
        private readonly HashSet<Vector2Int> tabooSet = new();        // fast lookup

        public Task_ScentFollow(
            string scentKey,
            ScentMedium medium,
            float minStrengthToContinue01 = 0.0002f,
            float stepCooldownSeconds = 0.20f,
            int maxSteps = 50,
            float maxSeconds = 60f)
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
            tabooQueue.Clear();
            tabooSet.Clear();

            if (context.Agent != null && context.Agent.locationModule != null)
            {
                lastCellPos = context.Agent.locationModule.cell.pos;
                prevCellPos = lastCellPos;

                NoteVisit(lastCellPos);
                // prime memory for current cell if possible
                TryNoteScentAt(context, lastCellPos);
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
                return TaskTickResult.Succeeded();
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

        private void UpdateMemoryFromLocalSniff(
            TaskContext context,
            ScentPerceptionModule scentModule,
            Vector2Int centerPos,
            int height)
        {
            // Current cell
            TryNoteScentAt(context, centerPos);

            // Neighbors
            foreach (DirFlags dir in DirFlagsEx.All8)
            {
                Vector2Int p = centerPos + dir.ToVector2Int();
                TryNoteScentAt(context, p);
            }
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

        private void NoteVisit(Vector2Int pos)
        {
            float now = Time.time;

            if (!memory.TryGetValue(pos, out var info))
                info = default;

            info.visitCount++;
            info.lastVisitTime = now;
            memory[pos] = info;

            // taboo maintenance
            if (!tabooSet.Contains(pos))
            {
                tabooQueue.Enqueue(pos);
                tabooSet.Add(pos);

                while (tabooQueue.Count > tabooLength)
                {
                    var old = tabooQueue.Dequeue();
                    tabooSet.Remove(old);
                }
            }
            else
            {
                // if we re-visit a taboo cell, we still keep it taboo; no reorder needed
            }
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

            // Evaluate each neighbor
            foreach (DirFlags dir in DirFlagsEx.All8)
            {
                Vector2Int p = centerPos + dir.ToVector2Int();

                // Optional hard block: avoid immediate bounce unless truly necessary
                bool isImmediateBacktrack = (p == prevCellPos);

                float strength01 = GetKnownStrength(p);

                // If we don't know strength from memory, try a direct query (optional but helpful)
                if (strength01 <= 0f && scentModule.TryGetScentStrengthAtCell(scentKey, p, height, medium, out float sLive))
                    strength01 = sLive;

                if (strength01 <= 0f)
                    continue;

                // If we are NOT exploring, don't go downhill too much.
                // This is the key: when you hit a peak, you’ll flip into exploring mode and allow it.
                if (!exploring)
                {
                    float currentStrength = GetKnownStrength(centerPos);
                    if (strength01 + 0.0001f < currentStrength) // small tolerance
                        continue;
                }

                //float score = ScoreNeighbor(p, strength01, isImmediateBacktrack);
                float score = ScoreNeighbor(p, strength01, false);

                // taboo is a strong discouragement, but not an absolute ban
                if (tabooSet.Contains(p))
                    score -= 1.25f;

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

        private float GetKnownStrength(Vector2Int pos)
        {
            if (memory.TryGetValue(pos, out var info))
                return info.bestStrength01;
            return 0f;
        }

        private float ScoreNeighbor(Vector2Int pos, float strength01, bool immediateBacktrack)
        {
            memory.TryGetValue(pos, out var info);

            // Novelty: 1 if never visited, decreasing with visit count
            float novelty01 = (info.visitCount <= 0) ? 1f : 1f / (1f + info.visitCount);

            // Visit penalty: based on recency + frequency
            float now = Time.time;
            float secondsSinceVisit = (info.lastVisitTime <= 0f) ? 999f : (now - info.lastVisitTime);

            // recency penalty fades over ~5 seconds
            float recencyPenalty01 = Mathf.Clamp01(1f - (secondsSinceVisit / 5f));
            float frequencyPenalty01 = Mathf.Clamp01(info.visitCount / 6f);

            float visitPenalty01 = Mathf.Clamp01(0.6f * recencyPenalty01 + 0.4f * frequencyPenalty01);

            float backtrackPenalty01 = immediateBacktrack ? 1f : 0f;

            float score =
                (wScent * strength01) +
                (wNovel * novelty01) -
                (wVisit * visitPenalty01) -
                (wBacktrack * backtrackPenalty01);

            return score;
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
    }
}