#nullable enable
using System.Collections.Generic;
using DogGame.LLM;
using DogGame.Modules;
using UnityEngine;

namespace DogGame.Tasks
{
    public enum ScentFollowState
    {
        Idle,
        AcquireScent,
        FollowTrail,
        Backtrack,
        CastSearch,
        TargetFound,
        Failed,
        Cancelled
    }

    public sealed partial class Task_ScentFollow : IAgentTask
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

        // When "stuck", we allow mild downhill to escape peaks.
        private readonly int stuckStepsToExplore = 4;
        private readonly float improvementEpsilon = 0.01f;

        // ---- Scoring weights ----
        private float wStrength = 1.00f;
        private float wImprovement = 1.50f;
        private float wIncreaseBonus = 0.25f;
        private float wDirection = 0.10f;
        private float wExploredPenalty = 0.20f;
        private float wVisitPenalty = 0.10f;
        private float wRecentVisitPenalty = 0.35f;
        private float wStalePenalty = 0.25f;
        private float immediateBacktrackPenalty = 0.50f;
        private float wTurnPenalty = 0.10f;
        private float wDeadEndPenalty = 0.75f;
        private float wRiseBonus = 0.06f;
        private float riseScale = 0.02f;
        private float minDetectableScent01 = 0.05f;
        private float smallImprovementThreshold01 = 0.03f;

        // Time constants.
        private float recentVisitWindowSeconds = 2.0f;
        private float staleMemorySeconds = 15.0f;

        // ---- Runtime state ----
        private bool started;
        private float startedTime;
        private float nextStepTime;
        private int stepsTaken;

        private Vector2Int lastCellPos;
        private Vector2Int prevCellPos;
        private Vector2Int? previousMoveDirection;
        private float lastChosenScore;
        private int stuckCounter;
        private bool prevExploring;
        private readonly Queue<Vector2Int> recentCells = new();
        private readonly int recentCellsCapacity = 6;

        private ScentFollowState currentState = ScentFollowState.Idle;
        private Vector2Int? lastStrongScentLocation;
        private float lastStrongScentValue;
        private float lastStrongScentTime;
        private float trailConfidence;
        private Vector2Int? activeBacktrackTarget;
        private Vector2Int castSearchAnchor;
        private int currentCastRadius;

        private readonly float trailFollowThreshold01 = 0.15f;
        private readonly float lostScentThreshold01 = 0.08f;
        private readonly float strongScentThreshold01 = 0.40f;
        private readonly float lowTrailConfidenceThreshold01 = 0.25f;
        private readonly int maxCastRadius = 5;

        public DirFlags lastBestDir = DirFlags.None;
        public Vector2Int lastBestPos;
        public float lastBestStrength;

        public Task_MoveToCell? moveToCell;
        public ScentFollowState CurrentState => currentState;

        private Dir dir => Dir.Instance;

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
            prevExploring = false;
            previousMoveDirection = null;
            recentCells.Clear();
            currentState = ScentFollowState.Idle;
            lastStrongScentLocation = null;
            lastStrongScentValue = 0f;
            lastStrongScentTime = -1f;
            trailConfidence = 0.35f;
            activeBacktrackTarget = null;
            castSearchAnchor = default;
            currentCastRadius = 1;

            ClearScentMemory();

            if (context.Agent != null && context.Agent.locationModule != null)
            {
                lastCellPos = context.Agent.locationModule.cell.pos;
                prevCellPos = lastCellPos;

                NoteVisit(lastCellPos);
                TryNoteScentAt(context, lastCellPos);
                UpdateLastStrongScent(lastCellPos);

                WorldObject otherAgent = context.Agent;
                context.Agent.scentPerceptionModule?.IdentifyScent(
                    scentKey: $"agent:{otherAgent.ObjectId}",
                    displayName: otherAgent.DisplayName,
                    agentId: otherAgent.ObjectId);
            }

            EnterState(ScentFollowState.AcquireScent, "start");
            Debug.Log("Task_ScentFollow.Start");
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            if (!started)
                Start(context);

            TaskTickResult? moveTick = TickCurrentMove(context, deltaTimeSeconds);
            if (moveTick.HasValue)
                return moveTick.Value;

            if (!TryGetReadyContext(context, out ScentPerceptionModule scentModule, out Cell cell, out TaskTickResult notReadyResult))
                return notReadyResult;

            TaskTickResult? stopResult = CheckStopLimits();
            if (stopResult.HasValue)
                return stopResult.Value;

            if (Time.time < nextStepTime)
                return TaskTickResult.Running();

            nextStepTime = Time.time + stepCooldownSeconds;

            Vector2Int centerPos = cell.pos;
            int height = cell.height;

            UpdateMemoryFromLocalSniff(context, scentModule, centerPos, height);
            UpdateLastStrongScent(centerPos);

            bool exploring = stuckCounter >= stuckStepsToExplore;
            if (exploring != prevExploring)
            {
                Debug.Log($"exploring = {exploring}");
                prevExploring = exploring;
            }

            switch (currentState)
            {
                case ScentFollowState.AcquireScent:
                    return TickAcquireScent(context, scentModule, centerPos, height, exploring);

                case ScentFollowState.FollowTrail:
                    return TickFollowTrail(context, scentModule, centerPos, height, exploring);

                case ScentFollowState.Backtrack:
                    return TickBacktrack(context, scentModule, centerPos, height);

                case ScentFollowState.CastSearch:
                    return TickCastSearch(context, scentModule, centerPos, height);

                case ScentFollowState.TargetFound:
                    return TaskTickResult.Succeeded();

                case ScentFollowState.Failed:
                    return TaskTickResult.Failed("scent_follow_failed");

                case ScentFollowState.Cancelled:
                    return TaskTickResult.Failed("scent_follow_cancelled");

                case ScentFollowState.Idle:
                default:
                    EnterState(ScentFollowState.AcquireScent, "tick_from_idle");
                    return TaskTickResult.Running();
            }
        }

        public void Stop(TaskContext context)
        {
            moveToCell = null;
            EnterState(ScentFollowState.Cancelled, "stop");
        }

        private TaskTickResult TickAcquireScent(
            TaskContext context,
            ScentPerceptionModule scentModule,
            Vector2Int centerPos,
            int height,
            bool exploring)
        {
            if (ConfirmTargetFound(context, centerPos))
                return TaskTickResult.Succeeded();

            if (!TryPickNextStep(scentModule, centerPos, height, exploring, out DirFlags chosenDir, out Vector2Int chosenPos, out float chosenStrength, out float chosenScore) ||
                chosenStrength < minStrengthToContinue01)
            {
                castSearchAnchor = lastStrongScentLocation ?? centerPos;
                currentCastRadius = 1;
                return EnterBacktrackOrCastSearch(scentModule, centerPos, height, "no_acquired_scent");
            }

            EnterState(chosenStrength >= trailFollowThreshold01 ? ScentFollowState.FollowTrail : ScentFollowState.AcquireScent, "candidate_acquired");
            return CommitChosenMove(context, centerPos, chosenDir, chosenPos, chosenStrength, chosenScore);
        }

        private TaskTickResult TickFollowTrail(
            TaskContext context,
            ScentPerceptionModule scentModule,
            Vector2Int centerPos,
            int height,
            bool exploring)
        {
            if (ConfirmTargetFound(context, centerPos))
                return TaskTickResult.Succeeded();

            float currentStrength = GetLastStrength(centerPos);
            if ((currentStrength > 0f && currentStrength < lostScentThreshold01) ||
                trailConfidence <= lowTrailConfidenceThreshold01)
            {
                return EnterBacktrackOrCastSearch(scentModule, centerPos, height, "current_scent_lost");
            }

            if (!TryPickNextStep(scentModule, centerPos, height, exploring, out DirFlags chosenDir, out Vector2Int chosenPos, out float chosenStrength, out float chosenScore))
            {
                return EnterBacktrackOrCastSearch(scentModule, centerPos, height, "no_follow_candidate");
            }

            if (chosenStrength < minStrengthToContinue01)
            {
                return EnterBacktrackOrCastSearch(scentModule, centerPos, height, "candidate_too_weak");
            }

            return CommitChosenMove(context, centerPos, chosenDir, chosenPos, chosenStrength, chosenScore);
        }

        private TaskTickResult TickBacktrack(
            TaskContext context,
            ScentPerceptionModule scentModule,
            Vector2Int centerPos,
            int height)
        {
            if (ConfirmTargetFound(context, centerPos))
                return TaskTickResult.Succeeded();

            float currentStrength = GetLastStrength(centerPos);
            if (currentStrength >= trailFollowThreshold01)
            {
                activeBacktrackTarget = null;
                RewardTrailProgress(currentStrength, improved: true);
                EnterState(ScentFollowState.FollowTrail, "trail_reacquired_during_backtrack");
                return TaskTickResult.Running();
            }

            if (!activeBacktrackTarget.HasValue ||
                activeBacktrackTarget.Value == centerPos ||
                IsDeadEnd(activeBacktrackTarget.Value))
            {
                if (!TryFindBestFrontier(scentModule, centerPos, height, out Vector2Int frontier, out float frontierScore))
                {
                    activeBacktrackTarget = null;
                    castSearchAnchor = lastStrongScentLocation ?? centerPos;
                    currentCastRadius = 1;
                    EnterState(ScentFollowState.CastSearch, "no_frontier_for_backtrack");
                    return TaskTickResult.Running();
                }

                activeBacktrackTarget = frontier;
                Debug.Log($"[ScentFollow] Backtracking to frontier {frontier} score={frontierScore:0.000}");
            }

            SetCameFrom(activeBacktrackTarget.Value, centerPos);
            return BeginMove(context, activeBacktrackTarget.Value);
        }

        private TaskTickResult TickCastSearch(
            TaskContext context,
            ScentPerceptionModule scentModule,
            Vector2Int centerPos,
            int height)
        {
            if (ConfirmTargetFound(context, centerPos))
                return TaskTickResult.Succeeded();

            if (GetLastStrength(centerPos) >= trailFollowThreshold01)
            {
                EnterState(ScentFollowState.FollowTrail, "trail_reacquired_at_current_cell");
                return TaskTickResult.Running();
            }

            if (currentCastRadius > maxCastRadius)
            {
                EnterState(ScentFollowState.Failed, "cast_radius_exhausted");
                return TaskTickResult.Failed("scent_cast_search_exhausted");
            }

            if (TryPickNextStep(scentModule, centerPos, height, exploring: true, out DirFlags chosenDir, out Vector2Int chosenPos, out float chosenStrength, out float chosenScore) &&
                IsWithinCastRadius(chosenPos))
            {
                if (chosenStrength >= trailFollowThreshold01)
                    EnterState(ScentFollowState.FollowTrail, "trail_reacquired_by_candidate");

                AdvanceCastRadiusIfNeeded(chosenPos);
                return CommitChosenMove(context, centerPos, chosenDir, chosenPos, chosenStrength, chosenScore);
            }

            Vector2Int rememberedBest = JumpToHighestScore(centerPos);
            if (rememberedBest != centerPos && IsWithinCastRadius(rememberedBest))
            {
                AdvanceCastRadiusIfNeeded(rememberedBest);
                SetCameFrom(rememberedBest, centerPos);
                return BeginMove(context, rememberedBest);
            }

            currentCastRadius++;
            Debug.Log($"[ScentFollow] Expanding cast search to radius {currentCastRadius} around {castSearchAnchor}");
            return TaskTickResult.Running();
        }

        private TaskTickResult? TickCurrentMove(TaskContext context, float deltaTimeSeconds)
        {
            if (moveToCell == null)
                return null;

            var moveResult = moveToCell.Tick(context, deltaTimeSeconds);
            if (moveResult.Status == TaskStatus.Running ||
                moveResult.Status == TaskStatus.NotStarted)
            {
                return moveResult;
            }

            if (moveResult.Status == TaskStatus.Failed)
            {
                moveToCell = null;
                EnterState(ScentFollowState.Failed, moveResult.FailureReason ?? "move_failed");
                return moveResult;
            }

            moveToCell = null;
            NoteArrival(context);

            if (ConfirmTargetFound(context, lastCellPos))
                return TaskTickResult.Succeeded();

            return null;
        }

        private bool TryGetReadyContext(
            TaskContext context,
            out ScentPerceptionModule scentModule,
            out Cell cell,
            out TaskTickResult failureResult)
        {
            scentModule = null!;
            cell = null!;
            failureResult = TaskTickResult.Running();

            if (context.Agent == null)
            {
                EnterState(ScentFollowState.Failed, "missing_agent");
                failureResult = TaskTickResult.Failed("missing_agent");
                return false;
            }

            scentModule = context.Agent.scentPerceptionModule;
            if (scentModule == null)
            {
                EnterState(ScentFollowState.Failed, "missing_scent_perception_module");
                failureResult = TaskTickResult.Failed("missing_scent_perception_module");
                return false;
            }

            if (context.Agent.locationModule == null)
            {
                EnterState(ScentFollowState.Failed, "missing_location_module");
                failureResult = TaskTickResult.Failed("missing_location_module");
                return false;
            }

            cell = context.Agent.locationModule.cell;
            return true;
        }

        private TaskTickResult? CheckStopLimits()
        {
            float elapsed = Time.time - startedTime;
            if (elapsed > maxSeconds)
            {
                Debug.Log($"[ScentFollow] Timed out after {elapsed:0.00}s.");
                EnterState(ScentFollowState.Failed, "timeout");
                return TaskTickResult.Failed("scent_follow_timeout");
            }

            if (stepsTaken >= maxSteps)
            {
                Debug.Log($"[ScentFollow] Step limit reached ({stepsTaken}).");
                EnterState(ScentFollowState.Failed, "step_limit");
                return TaskTickResult.Failed("scent_follow_step_limit");
            }

            return null;
        }

        private void NoteArrival(TaskContext context)
        {
            if (context.Agent?.locationModule == null)
                return;

            prevCellPos = lastCellPos;
            lastCellPos = context.Agent.locationModule.cell.pos;
            NoteVisit(lastCellPos);
            TryNoteScentAt(context, lastCellPos);
            UpdateLastStrongScent(lastCellPos);
            DebugPrintScentMemory();
        }

        private TaskTickResult CommitChosenMove(
            TaskContext context,
            Vector2Int fromPos,
            DirFlags chosenDir,
            Vector2Int chosenPos,
            float chosenStrength,
            float chosenScore)
        {
            lastBestDir = chosenDir;
            lastBestPos = chosenPos;
            lastBestStrength = chosenStrength;

            bool improved = chosenScore > lastChosenScore + improvementEpsilon;
            if (!improved)
                stuckCounter++;
            else
                stuckCounter = 0;

            lastChosenScore = chosenScore;
            previousMoveDirection = ClampStep(chosenPos - fromPos);
            SetCameFrom(chosenPos, fromPos);
            RewardTrailProgress(chosenStrength, improved);

            return BeginMove(context, chosenPos);
        }

        private TaskTickResult BeginMove(TaskContext context, Vector2Int targetPos)
        {
            moveToCell = new Task_MoveToCell(targetPos.x, targetPos.y, 0.25f);
            moveToCell.Start(context);
            stepsTaken++;
            return TaskTickResult.Running();
        }

        private bool ConfirmTargetFound(TaskContext context, Vector2Int pos)
        {
            if (!IsTargetReached(context) && !IsHeuristicFound(pos))
                return false;

            MarkTargetDetected(pos);
            EnterState(ScentFollowState.TargetFound, "target_confirmed");
            Debug.Log($"[ScentFollow] Found target for {scentKey}");
            return true;
        }

        private void UpdateLastStrongScent(Vector2Int pos)
        {
            float strength = GetLastStrength(pos);
            if (strength <= 0f)
                return;

            if (strength < strongScentThreshold01 && strength <= lastStrongScentValue)
                return;

            lastStrongScentLocation = pos;
            lastStrongScentValue = strength;
            lastStrongScentTime = Time.time;

            if (currentState == ScentFollowState.CastSearch)
                castSearchAnchor = pos;
        }

        private void EnterState(ScentFollowState nextState, string reason)
        {
            if (currentState == nextState)
                return;

            Debug.Log($"[ScentFollow] State {currentState} -> {nextState} ({reason})");
            currentState = nextState;
        }

        private bool IsWithinCastRadius(Vector2Int pos)
        {
            int chebyshev = Mathf.Max(Mathf.Abs(pos.x - castSearchAnchor.x), Mathf.Abs(pos.y - castSearchAnchor.y));
            return chebyshev <= currentCastRadius;
        }

        private void AdvanceCastRadiusIfNeeded(Vector2Int chosenPos)
        {
            int chebyshev = Mathf.Max(Mathf.Abs(chosenPos.x - castSearchAnchor.x), Mathf.Abs(chosenPos.y - castSearchAnchor.y));
            if (chebyshev >= currentCastRadius)
                currentCastRadius = Mathf.Min(currentCastRadius + 1, maxCastRadius + 1);
        }

        private void RememberRecentCell(Vector2Int pos)
        {
            recentCells.Enqueue(pos);

            while (recentCells.Count > recentCellsCapacity)
                recentCells.Dequeue();
        }

        private bool IsRecentCell(Vector2Int pos)
        {
            foreach (Vector2Int recentCell in recentCells)
            {
                if (recentCell == pos)
                    return true;
            }

            return false;
        }

        private static Vector2Int ClampStep(Vector2Int delta)
        {
            return new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        }
    }
}
