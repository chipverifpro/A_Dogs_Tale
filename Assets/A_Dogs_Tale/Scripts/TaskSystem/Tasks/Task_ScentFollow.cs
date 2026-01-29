#nullable enable
using System;
using System.Reflection;
using UnityEngine;
using DogGame.LLM;
using DogGame.Modules;
using static DungeonGenerator;
//using System.Threading.Tasks;

namespace DogGame.Tasks
{
    /// <summary>
    /// Automatic scent-following task (no LLM loop required).
    /// Each cycle:
    ///  1) sniff neighbor gradient for scentKey (air or ground)
    ///  2) choose best neighbor direction
    ///  3) move one step
    /// Stop conditions:
    ///  - scent lost (below threshold)
    ///  - peak reached (no better neighbor)
    ///  - timeout / max steps
    /// </summary>
    public sealed class Task_ScentFollow : IAgentTask
    {
        public string DebugName => $"ScentFollow({scentKey},{medium})";

        private readonly string scentKey;
        private readonly ScentMedium medium;

        // Tuning knobs
        private readonly float minStrengthToContinue01;
        private readonly float stepCooldownSeconds;
        private readonly int maxSteps;
        private readonly float maxSeconds;

        // Runtime state
        private bool started;
        private float startedTime;
        private float nextStepTime;
        private int stepsTaken;

        private Vector2Int lastCellPos;
        private float lastStrength;

        // Optional “result” fields (for debug/UI)
        public DirFlags lastBestDir = DirFlags.None;
        public Vector2Int lastBestPos;
        public float lastBestStrength;

        public Task_MoveToCell? moveToCell;

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

            lastStrength = 0f;
            lastBestDir = DirFlags.None;
            lastBestStrength = 0f;
            lastBestPos = default;

            if (context.Agent != null && context.Agent.locationModule != null)
                lastCellPos = context.Agent.locationModule.cell.pos;
            Debug.Log("Task_ScentFollow.Start");
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            //Debug.Log("Task_ScentFollow.Tick");
            if (!started)
                Start(context);

            // if we are already moving to a cell, do that insteaqd of follow.
            TaskTickResult moveResult;
            if (moveToCell != null)
            {
                moveResult = moveToCell.Tick(context,deltaTimeSeconds);
                if (moveResult.Status == TaskStatus.Running) 
                    return moveResult;
                if (moveResult.Status == TaskStatus.Failed)
                    return moveResult;
                if (moveResult.Status == TaskStatus.NotStarted)
                    return moveResult;
                if (moveResult.Status == TaskStatus.Succeeded)
                {
                    Debug.Log($"moveToCell success.  Arrived at {context.Agent.locationModule.pos2} ({context.Agent.locationModule.pos2_f})");
                    moveToCell = null;
                }
            }

            if (context.Agent == null)
                return TaskTickResult.Failed("missing_agent");

            var scentModule = context.Agent.scentPerceptionModule;
            if (scentModule == null)
                return TaskTickResult.Failed("missing_scent_perception_module");

            if (context.Agent.locationModule == null)
                return TaskTickResult.Failed("missing_location_module");

            // Global stop conditions
            float elapsed = Time.time - startedTime;
            if (elapsed > maxSeconds)
            {
                Debug.Log($"Timed out gracefully ({elapsed}).");
                return TaskTickResult.Succeeded(); // timed out gracefully
            }

            if (stepsTaken >= maxSteps)
            {
                Debug.Log($"Step limit reached gracefully ({stepsTaken}).");
                return TaskTickResult.Succeeded(); // limit reached gracefully
            }

            //if (Time.time < nextStepTime)
            //{
            //    return TaskTickResult.Running();
            //}

            nextStepTime = Time.time + stepCooldownSeconds;



            Cell cell = context.Agent.locationModule.cell;
            Vector2Int centerPos = cell.pos;
            int height = cell.height;

            // Find strongest neighbor for this scent
            if (!scentModule.TryFindStrongestNeighborForScent(
                    scentKey: scentKey,
                    centerPos: centerPos,
                    height: height,
                    medium: medium,
                    out DirFlags bestDir,
                    out Vector2Int bestPos,
                    out float bestStrength))
            {
                Debug.Log("Lost scent.");
                return TaskTickResult.Succeeded(); // lost scent
            }

            lastBestDir = bestDir;
            lastBestPos = bestPos;
            lastBestStrength = bestStrength;

            if (bestStrength < minStrengthToContinue01)
            {
                Debug.Log("Too weak to bother");
                return TaskTickResult.Succeeded(); // too weak to bother
            }

            // Peak heuristic:
            // If we did not move since last tick and direction isn't improving, stop.
            //if (centerPos == lastCellPos && bestStrength <= lastStrength + 0.0001f)
            //{
            //    Debug.Log("did not move since last tick and direction isn't improving");
            //    return TaskTickResult.Succeeded();
            //}

            // Move one cell toward bestPos
            moveToCell = new Task_MoveToCell(bestPos.x, bestPos.y, 0.25f);
            moveToCell.Start(context);


            //bool moveIssued = TryIssueMoveToCell(context, bestPos);
            //if (!moveIssued)
            //    return TaskTickResult.Failed("no_move_to_cell_api");

            // Update state; we’ll re-sniff after movement advances a bit
            stepsTaken++;
            lastCellPos = bestPos;
            lastStrength = bestStrength;

            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
            // No special cleanup. If you want to cancel movement, you could do it here.
        }

        // ------------------------------------------------------------
        // Movement issuing (reflection so it compiles against your codebase)
        // ------------------------------------------------------------
        private static bool TryIssueMoveToCell(TaskContext context, Vector2Int targetCell)
        {
            if (context.Agent == null)
                return false;

            // Common places movement might live
            object? mover =
                (object?)context.Agent.agentMovementModule ??
                (object?)context.Agent.motionModule ??
                (object?)context.Agent;

            if (mover == null)
                return false;

            // Try some likely method signatures in order.
            // If any exists, call it.
            return
                TryInvoke(mover, "MoveToCell", targetCell) ||
                TryInvoke(mover, "RequestMoveToCell", targetCell) ||
                TryInvoke(mover, "SetDestinationCell", targetCell) ||
                TryInvoke(mover, "GoToCell", targetCell) ||
                TryInvoke(mover, "MoveToCell", targetCell, 0.5f) ||
                TryInvoke(mover, "RequestMoveToCell", targetCell, 0.5f) ||
                TryInvoke(mover, "SetDestinationCell", targetCell, 0.5f);
        }

        private static bool TryInvoke(object instance, string methodName, params object[] args)
        {
            try
            {
                Type t = instance.GetType();
                MethodInfo? m = t.GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    types: Array.ConvertAll(args, a => a.GetType()),
                    modifiers: null);

                if (m == null)
                    return false;

                m.Invoke(instance, args);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}