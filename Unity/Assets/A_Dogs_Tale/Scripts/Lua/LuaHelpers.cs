#nullable enable
using System;
using MoonSharp.Interpreter;
using UnityEngine;
using DogGame.Reactions;
using DogGame.Tasks;
using DogGame.LLM;
using DogGame.Modules;

namespace DogGame.Lua
{
    public interface ILuaTaskSink
    {
        void EnqueueTask(IAgentTask task, int priority, string tag);
    }

    public sealed class TaskControllerLuaTaskSink : ILuaTaskSink
    {
        private readonly TaskController taskController;

        public TaskControllerLuaTaskSink(TaskController taskController)
        {
            this.taskController = taskController;
        }

        public void EnqueueTask(IAgentTask task, int priority, string tag)
        {
            taskController.EnqueueTask(
                task: task,
                priority: priority,
                source: TaskSource.Lua,
                canInterrupt: true,
                resumePrevious: false,
                clearStackOnStart: false,
                tag: tag,
                front: false);
        }
    }

    public sealed class LocalLuaTaskSink : ILuaTaskSink
    {
        private readonly TaskQueue taskQueue;

        public LocalLuaTaskSink(TaskQueue taskQueue)
        {
            this.taskQueue = taskQueue;
        }

        public void EnqueueTask(IAgentTask task, int priority, string tag)
        {
            taskQueue.Enqueue(new TaskRequest(
                task: task,
                priority: priority,
                source: TaskSource.Lua,
                canInterrupt: true,
                resumePrevious: false,
                clearStackOnStart: false,
                tag: tag));
        }
    }

    public sealed class LuaTaskEnvironment
    {
        private static bool knownMapCellRegistered;

        private readonly TaskQueue taskQueue = new();
        private readonly TaskExecutor taskExecutor;
        private readonly string scentKey;
        private readonly ScentMedium scentMedium;
        private readonly float minThreshold;
        private readonly bool visitRoomCenterBeforeBacktracking;

        private TaskContext? currentContext;
        private Script? script;
        private int trackedAgentId = -1;
        private bool taskSucceeded;
        private bool taskFailed;
        private string failureReason = "lua_failed";

        public LuaTaskEnvironment(
            string scentKey,
            ScentMedium scentMedium,
            float minThreshold,
            bool visitRoomCenterBeforeBacktracking)
        {
            this.scentKey = string.IsNullOrWhiteSpace(scentKey) ? string.Empty : scentKey.Trim();
            this.scentMedium = scentMedium;
            this.minThreshold = Mathf.Clamp01(minThreshold);
            this.visitRoomCenterBeforeBacktracking = visitRoomCenterBeforeBacktracking;
            taskExecutor = new TaskExecutor(taskQueue);
            trackedAgentId = TryParseTrackedAgentId(this.scentKey);
            EnsureLuaTypesRegistered();
        }

        public ILuaTaskSink CreateTaskSink()
        {
            return new LocalLuaTaskSink(taskQueue);
        }

        public void AttachScript(Script script)
        {
            this.script = script;
        }

        public void SetCurrentContext(TaskContext context)
        {
            currentContext = context;
        }

        public void UpdateGlobals(Script script, TaskContext context)
        {
            currentContext = context;
            this.script = script;

            Vector2Int currentCell = context.CurrentCellPos;
            script.Globals["CurrentX"] = currentCell.x;
            script.Globals["CurrentY"] = currentCell.y;
            script.Globals["MoveInProgress"] = IsDriving;
            script.Globals["IsAdjacentToScentSource"] = IsAdjacentToScentSource();
            script.Globals["MinThreshold"] = minThreshold;
            script.Globals["ScentKey"] = scentKey;
            script.Globals["ScentMedium"] = scentMedium.ToString().ToLowerInvariant();
            script.Globals["VisitRoomCenterBeforeBacktracking"] = visitRoomCenterBeforeBacktracking;
        }

        public bool IsDriving => taskExecutor.HasTask || taskQueue.Count > 0 || taskExecutor.SuspendedCount > 0;

        public void TickChildTasks(TaskContext context, float deltaTimeSeconds)
        {
            currentContext = context;

            if (!IsDriving)
                return;

            taskExecutor.Tick(context, deltaTimeSeconds);
        }

        public bool TryConsumeCompletion(out TaskTickResult result)
        {
            if (taskSucceeded)
            {
                taskSucceeded = false;
                result = TaskTickResult.Succeeded();
                return true;
            }

            if (taskFailed)
            {
                taskFailed = false;
                result = TaskTickResult.Failed(failureReason);
                return true;
            }

            result = default;
            return false;
        }

        public void Stop(TaskContext context)
        {
            taskExecutor.ClearAll(context);
        }

        public Table GetMiniSniff(int x, int y)
        {
            return BuildSniffTable(
                new[]
                {
                    new Vector2Int(0, 0),
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 0),
                    new Vector2Int(0, -1),
                    new Vector2Int(-1, 0)
                },
                x,
                y,
                includeBlocked: false);
        }

        public Table GetSniff(int x, int y)
        {
            return BuildSniffTable(
                new[]
                {
                    new Vector2Int(0, 0),
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 0),
                    new Vector2Int(0, -1),
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, 2),
                    new Vector2Int(2, 0),
                    new Vector2Int(0, -2),
                    new Vector2Int(-2, 0),
                    new Vector2Int(1, 1),
                    new Vector2Int(1, -1),
                    new Vector2Int(-1, 1),
                    new Vector2Int(-1, -1)
                },
                x,
                y,
                includeBlocked: false);
        }

        public void MoveToXYwithMiniSniff(int x, int y)
        {
            if (currentContext == null)
                return;

            Vector2Int current = currentContext.CurrentCellPos;
            if (current.x == x && current.y == y)
                return;

            taskQueue.Enqueue(new TaskRequest(
                task: new Task_MoveToCell(x, y, 0.25f),
                priority: 58,
                source: TaskSource.Lua,
                canInterrupt: true,
                resumePrevious: false,
                clearStackOnStart: false,
                tag: $"Lua:MoveToCell:{x}:{y}"));
        }

        public void ResponseLostScent()
        {
            if (currentContext != null)
                currentContext.Blackboard.SetString("scent.follow.response", "lost_scent");

            taskFailed = true;
            failureReason = "lost_scent";
        }

        public void ResponseFoundScentTarget()
        {
            if (currentContext != null)
                currentContext.Blackboard.SetString("scent.follow.response", "found_scent_target");

            taskSucceeded = true;
        }

        public void SucceedTask()
        {
            taskSucceeded = true;
        }

        public void FailTask(string reason)
        {
            taskFailed = true;
            failureReason = string.IsNullOrWhiteSpace(reason) ? "lua_failed" : reason.Trim();
        }

        private static void EnsureLuaTypesRegistered()
        {
            if (knownMapCellRegistered)
                return;

            UserData.RegisterType<KnownMapCell>();
            knownMapCellRegistered = true;
        }

        private Table BuildSniffTable(Vector2Int[] offsets, int x, int y, bool includeBlocked)
        {
            Script ownerScript = script ?? new Script();
            Table result = new Table(ownerScript);
            Vector2Int origin = new Vector2Int(x, y);
            int outIndex = 1;

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2Int target = origin + offsets[i];
                if (!TryGetCellAt(target, out Cell? targetCell))
                    continue;

                if (!includeBlocked && !CanSniffWithoutCrossingWallsOrDoors(origin, target))
                    continue;

                float strength = GetCellScentStrength(targetCell!);
                KnownMapCell knownMapCell = new KnownMapCell
                {
                    x = target.x,
                    y = target.y,
                    scentStrength = strength,
                    timestamp = Time.time
                };

                result.Set(outIndex++, DynValue.FromObject(ownerScript, knownMapCell));
            }

            return result;
        }

        private bool IsAdjacentToScentSource()
        {
            if (currentContext == null || trackedAgentId <= 0)
                return false;

            if (WorldObjectRegistry.Instance == null)
                return false;

            if (!WorldObjectRegistry.Instance.TryGet(trackedAgentId, out WorldObject target) || target == null || target.locationModule == null)
                return false;

            Vector2Int here = currentContext.CurrentCellPos;
            Vector2Int there = target.locationModule.cell.pos;
            int manhattanDistance = Mathf.Abs(here.x - there.x) + Mathf.Abs(here.y - there.y);
            return manhattanDistance == 1;
        }

        private float GetCellScentStrength(Cell cell)
        {
            if (currentContext?.Agent?.scentPerceptionModule == null)
                return 0f;

            ScentPerceptionModule scentModule = currentContext.Agent.scentPerceptionModule;
            int height = cell.height;
            ScentMedium requestedMedium = scentMedium;

            if (requestedMedium == ScentMedium.Air)
            {
                scentModule.TryGetScentStrengthAtCell(scentKey, cell.pos, height, ScentMedium.Air, out float airStrength);
                return airStrength;
            }

            if (requestedMedium == ScentMedium.Ground)
            {
                scentModule.TryGetScentStrengthAtCell(scentKey, cell.pos, height, ScentMedium.Ground, out float groundStrength);
                return groundStrength;
            }

            scentModule.TryGetScentStrengthAtCell(scentKey, cell.pos, height, ScentMedium.Ground, out float groundFallback);
            scentModule.TryGetScentStrengthAtCell(scentKey, cell.pos, height, ScentMedium.Air, out float airFallback);
            return Mathf.Max(groundFallback, airFallback);
        }

        private bool TryGetCellAt(Vector2Int pos, out Cell? cell)
        {
            cell = null;

            if (currentContext?.Agent?.dir?.gen?.cellGrid == null)
                return false;

            if (pos.x < 0 || pos.y < 0 || pos.x >= currentContext.Agent.dir.cfg.mapWidth || pos.y >= currentContext.Agent.dir.cfg.mapHeight)
                return false;

            cell = currentContext.Agent.dir.gen.cellGrid[pos.x, pos.y];
            return cell != null;
        }

        private bool CanSniffWithoutCrossingWallsOrDoors(Vector2Int origin, Vector2Int target)
        {
            if (origin == target)
                return true;

            Vector2Int delta = target - origin;

            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
                return CanTraverseWithoutWallsOrDoors(origin, DeltaToCardinal(delta));

            if (delta.x == 0 && Mathf.Abs(delta.y) == 2)
            {
                DirFlags step = delta.y > 0 ? DirFlags.N : DirFlags.S;
                Vector2Int mid = origin + step.ToVector2Int();
                return CanTraverseWithoutWallsOrDoors(origin, step) && CanTraverseWithoutWallsOrDoors(mid, step);
            }

            if (delta.y == 0 && Mathf.Abs(delta.x) == 2)
            {
                DirFlags step = delta.x > 0 ? DirFlags.E : DirFlags.W;
                Vector2Int mid = origin + step.ToVector2Int();
                return CanTraverseWithoutWallsOrDoors(origin, step) && CanTraverseWithoutWallsOrDoors(mid, step);
            }

            if (Mathf.Abs(delta.x) == 1 && Mathf.Abs(delta.y) == 1)
            {
                DirFlags xStep = delta.x > 0 ? DirFlags.E : DirFlags.W;
                DirFlags yStep = delta.y > 0 ? DirFlags.N : DirFlags.S;
                Vector2Int xMid = origin + xStep.ToVector2Int();
                Vector2Int yMid = origin + yStep.ToVector2Int();

                bool xThenY = CanTraverseWithoutWallsOrDoors(origin, xStep) && CanTraverseWithoutWallsOrDoors(xMid, yStep);
                bool yThenX = CanTraverseWithoutWallsOrDoors(origin, yStep) && CanTraverseWithoutWallsOrDoors(yMid, xStep);
                return xThenY || yThenX;
            }

            return false;
        }

        private bool CanTraverseWithoutWallsOrDoors(Vector2Int from, DirFlags direction)
        {
            if (!TryGetCellAt(from, out Cell? fromCell))
                return false;

            Vector2Int to = from + direction.ToVector2Int();
            if (!TryGetCellAt(to, out Cell? toCell))
                return false;

            DirFlags opposite = direction.Opposite();

            if ((fromCell!.walls & direction) != 0 || (fromCell.doors & direction) != 0)
                return false;

            if ((toCell!.walls & opposite) != 0 || (toCell.doors & opposite) != 0)
                return false;

            return true;
        }

        private static DirFlags DeltaToCardinal(Vector2Int delta)
        {
            if (delta == new Vector2Int(0, 1)) return DirFlags.N;
            if (delta == new Vector2Int(1, 0)) return DirFlags.E;
            if (delta == new Vector2Int(0, -1)) return DirFlags.S;
            if (delta == new Vector2Int(-1, 0)) return DirFlags.W;
            return DirFlags.None;
        }

        private static int TryParseTrackedAgentId(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("agent:", StringComparison.OrdinalIgnoreCase))
                return -1;

            return int.TryParse(key.Substring("agent:".Length), out int agentId) ? agentId : -1;
        }
    }

    public class LuaHelpers
    {
        private readonly ILuaTaskSink taskSink;
        private readonly WorldObject observer;
        private readonly LuaTaskEnvironment? taskEnvironment;
        private PerceptionEvent perceptionEvent;
        private Script? script;

        public LuaHelpers(
            ILuaTaskSink taskSink,
            WorldObject observer,
            PerceptionEvent perceptionEvent,
            LuaTaskEnvironment? taskEnvironment = null)
        {
            this.taskSink = taskSink;
            this.observer = observer;
            this.perceptionEvent = perceptionEvent;
            this.taskEnvironment = taskEnvironment;
        }

        public string ObserverDisplayName => observer != null ? observer.DisplayName : "<unknown>";

        public void Register(Script script)
        {
            this.script = script;
            taskEnvironment?.AttachScript(script);

            script.Globals["Bark"] = (Action<int>)Bark;
            script.Globals["MoveToEvent"] = (Action<float>)MoveToEvent;
            script.Globals["MoveToTarget"] = (Action<float>)MoveToTarget;
            script.Globals["FaceEventTarget"] = (Action<float, float>)FaceEventTarget;
            script.Globals["MoveUntilEventSeen"] = (Action<float, float>)MoveUntilEventSeen;
            script.Globals["MoveToEventSound"] = (Action<float>)MoveToEventSound;
            script.Globals["Sniff"] = (Action<float>)Sniff;
            script.Globals["FollowScent"] = (Action<string, string>)FollowScent;
            script.Globals["FollowEventScent"] = (Action)FollowEventScent;
            script.Globals["FollowEventScentAir"] = (Action)FollowEventScentAir;
            script.Globals["GoThroughDoor"] = (Action<int>)GoThroughDoor;
            script.Globals["GoToRoomCenter"] = (Action)GoToRoomCenter;
            script.Globals["getMiniSniff"] = (Func<int, int, Table>)GetMiniSniff;
            script.Globals["getSniff"] = (Func<int, int, Table>)GetSniff;
            script.Globals["moveToXYwithMiniSniff"] = (Action<int, int>)MoveToXYwithMiniSniff;
            script.Globals["Response_LostScent"] = (Action)ResponseLostScent;
            script.Globals["Response_FoundScentTarget"] = (Action)ResponseFoundScentTarget;
            script.Globals["TaskSucceed"] = (Action)TaskSucceed;
            script.Globals["TaskFail"] = (Action<string>)TaskFail;
        }

        public void SetPerceptionEvent(PerceptionEvent perceptionEvent)
        {
            this.perceptionEvent = perceptionEvent;
        }

        public void Bark(int times)
        {
            int barkCount = Mathf.Clamp(times, 1, 5);

            for (int barkIndex = 0; barkIndex < barkCount; barkIndex++)
            {
                Enqueue(
                    taskSpec: TS.Bark(volume10: 6),
                    priority: 60,
                    tag: "Lua:Bark");
            }
        }

        public void MoveToEvent(float stopRadius)
        {
            Enqueue(
                taskSpec: TS.MoveToEvent(stopRadius),
                priority: 55,
                tag: $"Lua:MoveToEvent:{stopRadius:0.##}");
        }

        public void MoveToTarget(float stopRadius)
        {
            Enqueue(
                taskSpec: TS.MoveToTarget(stopRadius),
                priority: 55,
                tag: $"Lua:MoveToTarget:{stopRadius:0.##}");
        }

        public void FaceEventTarget(float toleranceDeg, float maxSeconds)
        {
            Enqueue(
                taskSpec: TS.FaceTarget(toleranceDeg, maxSeconds),
                priority: 56,
                tag: $"Lua:FaceEventTarget:{toleranceDeg:0.##}:{maxSeconds:0.##}");
        }

        public void MoveUntilEventSeen(float stopRadius, float maxSeconds)
        {
            Enqueue(
                taskSpec: TS.MoveUntilSeen(
                    stopRadius: stopRadius,
                    maxSeconds: maxSeconds),
                priority: 56,
                tag: $"Lua:MoveUntilEventSeen:{stopRadius:0.##}:{maxSeconds:0.##}");
        }

        public void MoveToEventSound(float stopRadius)
        {
            if (!perceptionEvent.Sound.HasValue)
            {
                Debug.LogError("[LuaHelpers] MoveToEventSound called but current event has no sound payload.");
                return;
            }

            Enqueue(
                taskSpec: TS.MoveToEvent(stopRadius),
                priority: 56,
                tag: $"Lua:MoveToEventSound:{stopRadius:0.##}");
        }

        public void Sniff(float seconds)
        {
            float clampedSeconds = Mathf.Clamp(seconds, 0.05f, 10f);

            Enqueue(
                taskSpec: TS.Sniff(clampedSeconds),
                priority: 56,
                tag: $"Lua:Sniff:{clampedSeconds:0.##}");
        }

        public void FollowScent(string scentKey, string medium)
        {
            if (string.IsNullOrWhiteSpace(scentKey))
            {
                Debug.LogError("[LuaHelpers] FollowScent requires a non-empty scentKey.");
                return;
            }

            ScentMedium scentMedium = ParseScentMedium(medium);

            EnqueueTask(
                task: new Task_ScentFollowLua(scentKey.Trim(), scentMedium),
                priority: 58,
                tag: $"Lua:FollowScentLua:{scentMedium}:{scentKey.Trim()}");
        }

        public void FollowEventScent()
        {
            FollowEventScentInternal(ScentMedium.Ground);
        }

        public void FollowEventScentAir()
        {
            FollowEventScentInternal(ScentMedium.Air);
        }

        public void GoThroughDoor(int doorId)
        {
            EnqueueTask(
                task: new Task_GoThroughDoor(doorId),
                priority: 58,
                tag: $"Lua:GoThroughDoor:{doorId}");
        }

        public void GoToRoomCenter()
        {
            EnqueueTask(
                task: new Task_GoToRoomCenter(),
                priority: 57,
                tag: "Lua:GoToRoomCenter");
        }

        public Table GetMiniSniff(int x, int y)
        {
            if (taskEnvironment == null)
                return EmptyTable();

            return taskEnvironment.GetMiniSniff(x, y);
        }

        public Table GetSniff(int x, int y)
        {
            if (taskEnvironment == null)
                return EmptyTable();

            return taskEnvironment.GetSniff(x, y);
        }

        public void MoveToXYwithMiniSniff(int x, int y)
        {
            if (taskEnvironment == null)
            {
                Debug.LogError("[LuaHelpers] moveToXYwithMiniSniff is only available inside Task_RunLua.");
                return;
            }

            taskEnvironment.MoveToXYwithMiniSniff(x, y);
        }

        public void ResponseLostScent()
        {
            if (taskEnvironment == null)
            {
                Debug.LogError("[LuaHelpers] Response_LostScent is only available inside Task_RunLua.");
                return;
            }

            taskEnvironment.ResponseLostScent();
        }

        public void ResponseFoundScentTarget()
        {
            if (taskEnvironment == null)
            {
                Debug.LogError("[LuaHelpers] Response_FoundScentTarget is only available inside Task_RunLua.");
                return;
            }

            taskEnvironment.ResponseFoundScentTarget();
        }

        public void TaskSucceed()
        {
            if (taskEnvironment == null)
            {
                Debug.LogError("[LuaHelpers] TaskSucceed is only available inside Task_RunLua.");
                return;
            }

            taskEnvironment.SucceedTask();
        }

        public void TaskFail(string reason)
        {
            if (taskEnvironment == null)
            {
                Debug.LogError("[LuaHelpers] TaskFail is only available inside Task_RunLua.");
                return;
            }

            taskEnvironment.FailTask(reason);
        }

        private void FollowEventScentInternal(ScentMedium medium)
        {
            if (!perceptionEvent.Scent.HasValue)
            {
                Debug.LogError("[LuaHelpers] FollowEventScent called but current event has no scent payload.");
                return;
            }

            string scentKey = perceptionEvent.Scent.Value.ScentKey;
            if (string.IsNullOrWhiteSpace(scentKey))
            {
                Debug.LogError("[LuaHelpers] FollowEventScent could not resolve scent key from event.");
                return;
            }

            EnqueueTask(
                task: new Task_ScentFollowLua(scentKey, medium),
                priority: 58,
                tag: $"Lua:FollowEventScentLua:{medium}:{scentKey}");
        }

        private Table EmptyTable()
        {
            return new Table(script ?? new Script());
        }

        private static ScentMedium ParseScentMedium(string medium)
        {
            if (string.Equals(medium, "air", StringComparison.OrdinalIgnoreCase))
                return ScentMedium.Air;

            return ScentMedium.Ground;
        }

        private void Enqueue(TaskSpec taskSpec, int priority, string tag)
        {
            if (!CanEnqueue())
                return;

            if (!TaskSpecFactory.TryBuildTask(
                    spec: taskSpec,
                    observer: observer,
                    e: perceptionEvent,
                    task: out IAgentTask? builtTask,
                    error: out string? error))
            {
                Debug.LogError($"[LuaHelpers] Failed to build task for '{taskSpec.Name}': {error}");
                return;
            }

            if (builtTask == null)
            {
                Debug.LogError($"[LuaHelpers] TryBuildTask succeeded but returned null for '{taskSpec.Name}'.");
                return;
            }

            EnqueueTask(builtTask, priority, tag);
        }

        private void EnqueueTask(IAgentTask task, int priority, string tag)
        {
            if (!CanEnqueue())
                return;

            taskSink.EnqueueTask(task, priority, tag);
        }

        private bool CanEnqueue()
        {
            if (taskSink == null)
            {
                Debug.LogError("[LuaHelpers] taskSink is null.");
                return false;
            }

            if (observer == null)
            {
                Debug.LogError("[LuaHelpers] observer is null.");
                return false;
            }

            return true;
        }
    }
}
