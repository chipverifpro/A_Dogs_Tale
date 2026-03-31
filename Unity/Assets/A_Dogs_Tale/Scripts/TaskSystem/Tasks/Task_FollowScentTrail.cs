#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    /// <summary>
    /// Follows an already-chosen scent trail (by agentId) using a local 3x3 neighborhood gradient.
    /// Does NOT choose the scent; it only tracks the given scent id.
    /// </summary>
    public sealed class Task_FollowScentTrail : IAgentTask
    {
        public Dir? dir;
        private readonly int trackedScentAgentId;
        private readonly int queryThreshold;

        private readonly float airWeight;
        private readonly float groundWeight;

        private readonly float inertiaBonus;
        private readonly float reversePenalty;
        private readonly float dropPenaltyScale;

        private readonly int stuckAllowReverseAfterTicks;
        private readonly int maxSteps;                 // safety to avoid infinite wandering
        private readonly float repickIntervalSeconds;  // how often to re-evaluate the next step

        private int stepsTaken;
        private float nextRepickTime;

        private Vector2Int lastPos;
        private Vector2Int lastDir;
        private int stuckTicks;
        private float bestRecentStrength;

        private Cell? currentTargetCell;

        public string DebugName => $"Task_FollowScentTrail(agentId={trackedScentAgentId})";

        public Task_FollowScentTrail(
            int trackedScentAgentId,
            float stopRadius = 0.25f,
            int queryThreshold = 50,
            float airWeight = 0.5f,
            float groundWeight = 1.0f,
            float inertiaBonus = 0.10f,
            float reversePenalty = 0.35f,
            float dropPenaltyScale = 0.50f,
            int stuckAllowReverseAfterTicks = 6,
            int maxSteps = 60,
            float repickIntervalSeconds = 0.2f)
        {
            this.trackedScentAgentId = trackedScentAgentId;
            this.queryThreshold = queryThreshold;

            this.airWeight = airWeight;
            this.groundWeight = groundWeight;

            this.inertiaBonus = inertiaBonus;
            this.reversePenalty = reversePenalty;
            this.dropPenaltyScale = dropPenaltyScale;

            this.stuckAllowReverseAfterTicks = stuckAllowReverseAfterTicks;
            this.maxSteps = maxSteps;
            this.repickIntervalSeconds = repickIntervalSeconds;
            Debug.Log(DebugName);
        }

        public void Awake()
        {
            if (dir == null) dir = Object.FindFirstObjectByType<Dir>();
        }

        public void Start(TaskContext context)
        {
            stepsTaken = 0;
            nextRepickTime = 0f;

            lastPos = context.CurrentCellPos;  // requires context.CurrentCell
            lastDir = Vector2Int.zero;
            stuckTicks = 0;
            bestRecentStrength = GetScentStrengthOrZero(context.Agent.locationModule.cell, trackedScentAgentId, airWeight, groundWeight);

            currentTargetCell = null;
        }

        private int debugDoubleTick = -1;
        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            if (stepsTaken >= maxSteps)
            {
                context.Motion.StopMoving();
                return TaskTickResult.Succeeded();
            }

            // If we’re already moving to a target cell, see if we’re there.
            if (currentTargetCell != null)
            {
                Vector3 targetWorld = CenterOfCellWorld(currentTargetCell);
                if (!context.Motion.IsAt(targetWorld))
                {
                    // Keep heading toward it.
                    context.Motion.SetMoveTarget(targetWorld);
                    return TaskTickResult.Running();
                }

                // Arrived at target.
                currentTargetCell = null;
            }

            // Re-evaluate at a controlled cadence (prevents jitter)
            if (Time.time < nextRepickTime)
                return TaskTickResult.Running();

            nextRepickTime = Time.time + repickIntervalSeconds;

            Cell centerCell = context.Agent.locationModule.cell;

            // Build 3x3 accessible neighborhood (your function)
            Cell[,] neighbors = dir!.gen.GetEightNeighborCells(centerCell, queryThreshold);

            Cell? nextCell = ChooseNextCellScentTrail(
                neighbors,
                trackedScentAgentId,
                centerCell,
                ref lastPos,
                ref lastDir,
                ref stuckTicks,
                ref bestRecentStrength,
                airWeight,
                groundWeight,
                inertiaBonus,
                reversePenalty,
                dropPenaltyScale,
                stuckAllowReverseAfterTicks);

            if (nextCell == null)
            {
                // Lost trail: no meaningful direction. Stop and succeed for now.
                // Later: you can switch to “spiral sniff search” or LLM request.
                context.Motion.StopMoving();
                return TaskTickResult.Succeeded();
            }

            currentTargetCell = nextCell;
            stepsTaken++;

            Vector3 nextWorld = CenterOfCellWorld(nextCell);
            context.Motion.SetMoveTarget(nextWorld);
            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
            context.Motion.StopMoving();
        }

        private static Vector3 CenterOfCellWorld(Cell cell)
        {
            return cell.center3d_f;
        }

        private static float GetScentStrengthOrZero(Cell cell, int targetAgentId, float airW, float groundW)
        {
            if (cell?.scents == null) return 0f;

            for (int i = 0; i < cell.scents.Count; i++)
            {
                var s = cell.scents[i];
                if (s.agentId != targetAgentId) continue;
                return s.airIntensity * airW + s.groundIntensity * groundW;
            }

            return 0f;
        }

        private static Cell? ChooseNextCellScentTrail(
            Cell[,] neighbors,
            int targetAgentId,
            Cell centerCell,
            ref Vector2Int lastPos,
            ref Vector2Int lastDir,
            ref int stuckTicks,
            ref float bestRecentStrength,
            float airW,
            float groundW,
            float inertiaBonus,
            float reversePenalty,
            float dropPenaltyScale,
            int stuckAllowReverseAfterTicks)
        {
            float centerStrength = GetScentStrengthOrZero(centerCell, targetAgentId, airW, groundW);
            Vector2Int centerPos = centerCell.pos;

            bool allowReverse = stuckTicks >= stuckAllowReverseAfterTicks;

            Cell? best = null;
            float bestScore = float.NegativeInfinity;

            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                Cell cand = neighbors[1 + dx, 1 + dy];
                if (cand == null) continue;

                float candStrength = GetScentStrengthOrZero(cand, targetAgentId, airW, groundW);

                float score = candStrength;

                // Penalize big drops compared to current cell.
                float drop = Mathf.Max(0f, centerStrength - candStrength);
                score -= drop * dropPenaltyScale;

                // Inertia bonus for continuing direction.
                Vector2Int candDir = cand.pos - centerPos;
                if (lastDir != Vector2Int.zero && candDir == lastDir)
                    score += inertiaBonus;

                // Avoid immediate reversal unless stuck.
                if (!allowReverse && cand.pos == lastPos)
                    score -= reversePenalty;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = cand;
                }
            }

            // Update "stuck" heuristic based on the strength of the chosen best neighbor (if any).
            if (best != null)
            {
                float bestNeighborStrength = GetScentStrengthOrZero(best, targetAgentId, airW, groundW);
                const float progressEpsilon = 0.01f;

                if (bestNeighborStrength > bestRecentStrength + progressEpsilon)
                {
                    bestRecentStrength = bestNeighborStrength;
                    stuckTicks = 0;
                }
                else
                {
                    stuckTicks++;
                }

                // Update direction history
                lastPos = centerPos;
                lastDir = best.pos - centerPos;
            }

            return best;
        }
    }
}
