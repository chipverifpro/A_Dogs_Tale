#nullable enable
using System;
using MoonSharp.Interpreter;
using UnityEngine;
using DogGame.LLM;
using DogGame.Lua;
using DogGame.Modules;

namespace DogGame.Tasks
{
    public sealed class Task_ScentFollowLua : IAgentTask
    {
        private const string DefaultLuaScript = @"
state = {
    initialized = false,
    knownMap = {},
    lastCellKey = nil,
    lastHeadingX = 0,
    lastHeadingY = 0
}

local STRENGTH_IMPROVEMENT_THRESHOLD = 0.02
local INERTIA_EPSILON = 0.0001

local function log(message)
    print('[ScentFollowLua] ' .. message)
end

local function key(x, y)
    return tostring(x) .. ',' .. tostring(y)
end

local function sign(v)
    if v > 0 then return 1 end
    if v < 0 then return -1 end
    return 0
end

local function mergeCells(cells)
    local changed = false
    if cells == nil then
        return false
    end

    for _, cell in ipairs(cells) do
        local k = key(cell.x, cell.y)
        local existing = state.knownMap[k]

        if existing == nil then
            state.knownMap[k] = {
                x = cell.x,
                y = cell.y,
                scentStrength = cell.scentStrength,
                timestamp = cell.timestamp
            }
            changed = true
        else
            local previousTimestamp = existing.timestamp

            if cell.scentStrength > existing.scentStrength + STRENGTH_IMPROVEMENT_THRESHOLD then
                existing.scentStrength = cell.scentStrength
                existing.timestamp = cell.timestamp

                local neighbors = {
                    { x = cell.x, y = cell.y + 1 },
                    { x = cell.x + 1, y = cell.y },
                    { x = cell.x, y = cell.y - 1 },
                    { x = cell.x - 1, y = cell.y }
                }

                for _, neighbor in ipairs(neighbors) do
                    local nk = key(neighbor.x, neighbor.y)
                    local adjacent = state.knownMap[nk]
                    if adjacent ~= nil and adjacent.timestamp < previousTimestamp then
                        state.knownMap[nk] = nil
                    end
                end

                changed = true
            elseif cell.scentStrength < existing.scentStrength then
                existing.scentStrength = cell.scentStrength
                existing.timestamp = cell.timestamp
            elseif cell.timestamp > existing.timestamp then
                existing.timestamp = cell.timestamp
            end
        end
    end

    return changed
end

local function mergeMiniSniffForCurrentCell()
    local currentKey = key(CurrentX, CurrentY)
    if state.lastCellKey == currentKey then
        return false
    end

    state.lastCellKey = currentKey
    return mergeCells(getMiniSniff(CurrentX, CurrentY))
end

local function isPerimeterCell(cell)
    local neighbors = {
        key(cell.x, cell.y + 1),
        key(cell.x + 1, cell.y),
        key(cell.x, cell.y - 1),
        key(cell.x - 1, cell.y)
    }

    for _, neighborKey in ipairs(neighbors) do
        if state.knownMap[neighborKey] == nil then
            return false
        end
    end

    return true
end

local function scoreCell(cell)
    local dx = cell.x - CurrentX
    local dy = cell.y - CurrentY
    local distance = math.sqrt((dx * dx) + (dy * dy))
    local distanceWeight = 1.0 / math.max((distance / 3.0) - 1.0, 1.0)
    local score = cell.scentStrength * distanceWeight

    local headingX = sign(dx)
    local headingY = sign(dy)
    if headingX == state.lastHeadingX and headingY == state.lastHeadingY then
        score = score + INERTIA_EPSILON
    end

    return score
end

local function chooseBestPerimeterCell()
    local bestCell = nil
    local bestScore = -1.0

    for _, cell in pairs(state.knownMap) do
        if not (cell.x == CurrentX and cell.y == CurrentY) and isPerimeterCell(cell) then
            local score = scoreCell(cell)
            if score > bestScore then
                bestScore = score
                bestCell = cell
            end
        end
    end

    return bestCell, bestScore
end

function tick()
    if not state.initialized then
        state.initialized = true
        state.lastCellKey = key(CurrentX, CurrentY)
        mergeCells(getSniff(CurrentX, CurrentY))
        log('initialized knownMap at ' .. tostring(CurrentX) .. ',' .. tostring(CurrentY))
    end

    mergeMiniSniffForCurrentCell()

    if IsAdjacentToScentSource then
        log('adjacent to scent source')
        Response_FoundScentTarget()
        return
    end

    if MoveInProgress then
        return
    end

    local bestCell, bestScore = chooseBestPerimeterCell()
    if bestCell ~= nil and bestScore > MinThreshold then
        state.lastHeadingX = sign(bestCell.x - CurrentX)
        state.lastHeadingY = sign(bestCell.y - CurrentY)
        log('move to perimeter ' .. tostring(bestCell.x) .. ',' .. tostring(bestCell.y) .. ' score=' .. tostring(bestScore))
        moveToXYwithMiniSniff(bestCell.x, bestCell.y)
        return
    end

    local foundNewTrail = mergeCells(getSniff(CurrentX, CurrentY))
    if not foundNewTrail then
        log('lost scent after sniff')
        Response_LostScent()
        return
    end

    bestCell, bestScore = chooseBestPerimeterCell()
    if bestCell == nil or bestScore <= MinThreshold then
        log('no perimeter cell above threshold after sniff')
        Response_LostScent()
        return
    end

    state.lastHeadingX = sign(bestCell.x - CurrentX)
    state.lastHeadingY = sign(bestCell.y - CurrentY)
    log('move after sniff to ' .. tostring(bestCell.x) .. ',' .. tostring(bestCell.y) .. ' score=' .. tostring(bestScore))
    moveToXYwithMiniSniff(bestCell.x, bestCell.y)
end
";

        private static bool luaTypesRegistered;

        public string DebugName => $"ScentFollowLua({scentKey})";
        public string Description = "Tracks a scent using a Lua-known map fed by C# mini-sniff, sniff, and move commands, using max(ground, air) scent strength per cell.";

        private readonly string scentKey;
        private readonly ScentMedium medium;
        private readonly float minThreshold;
        private readonly float maxSeconds;
        private readonly string luaScript;

        private Script? script;
        private TaskContext? currentContext;
        private Task_MoveToCell? moveTask;
        private bool initialized;
        private bool lostScent;
        private bool foundTarget;
        private float startedTime;
        private int trackedAgentId = -1;

        public Task_ScentFollowLua(
            string scentKey,
            ScentMedium medium,
            float minThreshold = 0.0002f,
            float maxSeconds = 120f,
            string? luaScript = null)
        {
            this.scentKey = string.IsNullOrWhiteSpace(scentKey) ? "" : scentKey.Trim();
            this.medium = medium;
            this.minThreshold = Mathf.Clamp01(minThreshold);
            this.maxSeconds = Mathf.Max(0.5f, maxSeconds);
            this.luaScript = string.IsNullOrWhiteSpace(luaScript) ? DefaultLuaScript : luaScript;
        }

        public void Start(TaskContext context)
        {
            currentContext = context;
            startedTime = Time.time;
            moveTask = null;
            initialized = false;
            lostScent = false;
            foundTarget = false;
            trackedAgentId = TryParseTrackedAgentId(scentKey);

            EnsureLuaTypesRegistered();

            script = new Script();
            script.Options.DebugPrint = message =>
            {
                Debug.Log("[Lua] " + message);
                BottomBanner.LogRichMessage(BannerSense.None, BannerLevel.None, "<i>[Lua]</i> " + message, includeGameTime: true);
            };

            script.Globals["getMiniSniff"] = (Func<int, int, Table>)GetMiniSniff;
            script.Globals["getSniff"] = (Func<int, int, Table>)GetSniff;
            script.Globals["moveToXYwithMiniSniff"] = (Action<int, int>)MoveToXYwithMiniSniff;
            script.Globals["Response_LostScent"] = (Action)ResponseLostScent;
            script.Globals["Response_FoundScentTarget"] = (Action)ResponseFoundScentTarget;
            script.Globals["MinThreshold"] = minThreshold;

            try
            {
                script.DoString(luaScript);
                initialized = true;
            }
            catch (InterpreterException exception)
            {
                Debug.LogError("[Task_ScentFollowLua] Lua load error: " + exception.DecoratedMessage);
                lostScent = true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[Task_ScentFollowLua] General load error: " + exception);
                lostScent = true;
            }
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            currentContext = context;

            if (!initialized || script == null)
                return lostScent ? TaskTickResult.Failed("lua_init_failed") : TaskTickResult.Running();

            if (Time.time - startedTime > maxSeconds)
                return TaskTickResult.Failed("scent_follow_timeout");

            if (moveTask != null)
            {
                TaskTickResult moveResult = moveTask.Tick(context, deltaTimeSeconds);
                if (moveResult.Status == TaskStatus.Succeeded)
                {
                    moveTask.Stop(context);
                    moveTask = null;
                }
                else if (moveResult.Status == TaskStatus.Failed)
                {
                    moveTask.Stop(context);
                    moveTask = null;
                    return TaskTickResult.Failed(moveResult.FailureReason ?? "move_failed");
                }
            }

            if (foundTarget)
                return TaskTickResult.Succeeded();

            if (lostScent)
                return TaskTickResult.Failed("lost_scent");

            Vector2Int currentCell = context.CurrentCellPos;
            script.Globals["CurrentX"] = currentCell.x;
            script.Globals["CurrentY"] = currentCell.y;
            script.Globals["MoveInProgress"] = moveTask != null;
            script.Globals["IsAdjacentToScentSource"] = IsAdjacentToScentSource();

            try
            {
                DynValue tickFunction = script.Globals.Get("tick");
                if (tickFunction.IsNil())
                    return TaskTickResult.Failed("lua_tick_missing");

                script.Call(tickFunction);
            }
            catch (InterpreterException exception)
            {
                Debug.LogError("[Task_ScentFollowLua] Lua runtime error: " + exception.DecoratedMessage);
                return TaskTickResult.Failed("lua_runtime_error");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Task_ScentFollowLua] General runtime error: " + exception);
                return TaskTickResult.Failed("lua_runtime_error");
            }

            if (foundTarget)
                return TaskTickResult.Succeeded();

            if (lostScent)
                return TaskTickResult.Failed("lost_scent");

            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
            if (moveTask != null)
            {
                moveTask.Stop(context);
                moveTask = null;
            }
        }

        private static void EnsureLuaTypesRegistered()
        {
            if (luaTypesRegistered)
                return;

            UserData.RegisterType<KnownMapCell>();
            luaTypesRegistered = true;
        }

        private Table GetMiniSniff(int x, int y)
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

        private Table GetSniff(int x, int y)
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

        private Table BuildSniffTable(Vector2Int[] offsets, int x, int y, bool includeBlocked)
        {
            Table result = new Table(script);
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

                result.Set(outIndex++, DynValue.FromObject(script, knownMapCell));
            }

            return result;
        }

        private void MoveToXYwithMiniSniff(int x, int y)
        {
            if (currentContext == null)
                return;

            Vector2Int current = currentContext.CurrentCellPos;
            if (current.x == x && current.y == y)
                return;

            if (moveTask != null)
            {
                moveTask.Stop(currentContext);
                moveTask = null;
            }

            moveTask = new Task_MoveToCell(x, y, 0.25f);
            moveTask.Start(currentContext);
        }

        private void ResponseLostScent()
        {
            if (currentContext != null)
                currentContext.Blackboard.SetString("scent.follow.response", "lost_scent");

            lostScent = true;
        }

        private void ResponseFoundScentTarget()
        {
            if (currentContext != null)
                currentContext.Blackboard.SetString("scent.follow.response", "found_scent_target");

            foundTarget = true;
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

            scentModule.TryGetScentStrengthAtCell(scentKey, cell.pos, height, ScentMedium.Ground, out float groundStrength);
            scentModule.TryGetScentStrengthAtCell(scentKey, cell.pos, height, ScentMedium.Air, out float airStrength);

            return Mathf.Max(groundStrength, airStrength);
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
}
