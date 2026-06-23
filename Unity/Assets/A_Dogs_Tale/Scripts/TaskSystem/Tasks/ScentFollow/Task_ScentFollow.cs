#nullable enable
using DogGame.LLM;
using DogGame.Modules;
using UnityEngine;

namespace DogGame.Tasks
{
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
        private float wVisitPenalty = 0.35f;
        private float wRecentVisitPenalty = 0.45f;
        private float wStalePenalty = 0.25f;
        private float immediateBacktrackPenalty = 0.60f;
        private float wRiseBonus = 0.06f;
        private float riseScale = 0.02f;

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
        private float lastChosenScore;
        private int stuckCounter;
        private bool prevExploring;

        public DirFlags lastBestDir = DirFlags.None;
        public Vector2Int lastBestPos;
        public float lastBestStrength;

        public Task_MoveToCell? moveToCell;

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

            ClearScentMemory();

            if (context.Agent != null && context.Agent.locationModule != null)
            {
                lastCellPos = context.Agent.locationModule.cell.pos;
                prevCellPos = lastCellPos;

                NoteVisit(lastCellPos);
                TryNoteScentAt(context, lastCellPos);

                WorldObject otherAgent = context.Agent;
                context.Agent.scentPerceptionModule?.IdentifyScent(
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

            // 0) If we are already moving, keep driving that task.
            if (moveToCell != null)
            {
                var moveResult = moveToCell.Tick(context, deltaTimeSeconds);
                if (moveResult.Status == TaskStatus.Running ||
                    moveResult.Status == TaskStatus.Failed ||
                    moveResult.Status == TaskStatus.NotStarted)
                {
                    return moveResult;
                }

                if (moveResult.Status == TaskStatus.Succeeded)
                {
                    moveToCell = null;

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

            // 1) Global stop conditions.
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

            // 2) Sniff current + neighbors -> update memory.
            Cell cell = context.Agent.locationModule.cell;
            Vector2Int centerPos = cell.pos;
            int height = cell.height;

            UpdateMemoryFromLocalSniff(context, scentModule, centerPos, height);

            // 3) Choose next step using memory + explore logic.
            bool exploring = stuckCounter >= stuckStepsToExplore;
            if (exploring != prevExploring)
            {
                Debug.Log($"exploring = {exploring}");
                prevExploring = exploring;
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
                chosenPos = JumpToHighestScore(centerPos);
                Debug.Log($"JumpToHighestScore {chosenPos}");
                if (centerPos == chosenPos)
                {
                    Debug.Log("Already at best score.");
                    return TaskTickResult.Succeeded();
                }

                moveToCell = new Task_MoveToCell(chosenPos.x, chosenPos.y, 0.25f);
                moveToCell.Start(context);
                stepsTaken++;
                return TaskTickResult.Running();
            }

            lastBestDir = chosenDir;
            lastBestPos = chosenPos;
            lastBestStrength = chosenStrength;

            if (chosenStrength < minStrengthToContinue01)
            {
                Debug.Log("Scent too weak to continue.");
                return TaskTickResult.Succeeded();
            }

            // 4) Stuck detection: if we are not improving, start exploring.
            if (chosenScore <= lastChosenScore + improvementEpsilon)
                stuckCounter++;
            else
                stuckCounter = 0;

            lastChosenScore = chosenScore;

            // 5) Issue move one step.
            moveToCell = new Task_MoveToCell(chosenPos.x, chosenPos.y, 0.25f);
            moveToCell.Start(context);

            stepsTaken++;
            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
        }
    }
}
