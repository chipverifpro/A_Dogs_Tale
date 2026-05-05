#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using DogGame.Lua;
using DogGame.LLM;
using DogGame.Tasks;
using InspectorTools;

namespace DogGame.Modules
{
    #region LuaScript
    [InspectorNote("AgentDecision_Modules/Explore Decision Module", "Explores the entire map by going through every door.")]
    [DisallowMultipleComponent]
    public class ExploreDecisionModule : AgentDecisionModuleBase
    {
private const string DefaultLuaExploreScript = @"state = {
    roomPath = {},
    enteredByDoor = {},
    usedDoors = {},
    centeredRooms = {},
    pendingAction = nil,
    pendingDoorId = nil,
    lastLog = nil
}

local function topRoomId()
    return state.roomPath[#state.roomPath]
end

local function log(message)
    if state.lastLog ~= message then
        print('[ExploreLua] ' .. message)
        state.lastLog = message
    end
end

local function clearPendingAction()
    state.pendingAction = nil
    state.pendingDoorId = nil
end

local function doorExistsInCurrentRoom(doorId)
    if doorId == nil or Room == nil or not Room.IsValid then
        return false
    end

    for i = 1, Room.DoorCount do
        if Room.GetDoorId(i) == doorId then
            return true
        end
    end

    return false
end

local function resetPathToCurrentRoom(message)
    state.roomPath = { Room.Id }
    state.enteredByDoor = {}
    log(message)
    clearPendingAction()
end

local function validateCurrentEntryDoor()
    local entryDoorId = state.enteredByDoor[#state.enteredByDoor]
    if entryDoorId ~= nil and not doorExistsInCurrentRoom(entryDoorId) then
        resetPathToCurrentRoom('Remembered entry door ' .. tostring(entryDoorId) .. ' is not in current room ' .. tostring(Room.Id) .. '; resetting explore path but keeping used-door memory')
    end
end

local function syncCurrentRoom()
    if Room == nil or not Room.IsValid then
        log('Room invalid; waiting for room state')
        return false
    end

    local currentRoomId = Room.Id
    local topRoom = topRoomId()

    if topRoom == nil then
        state.roomPath[1] = currentRoomId
        state.enteredByDoor[1] = nil
        log('Starting explore in room ' .. tostring(currentRoomId) .. ' with ' .. tostring(Room.DoorCount) .. ' doors')
        clearPendingAction()
        return true
    end

    if topRoom == currentRoomId then
        validateCurrentEntryDoor()
        clearPendingAction()
        return true
    end

    if state.pendingAction == 'forward' then
        state.roomPath[#state.roomPath + 1] = currentRoomId
        state.enteredByDoor[#state.enteredByDoor + 1] = state.pendingDoorId
        if state.pendingDoorId ~= nil then
            state.usedDoors[state.pendingDoorId] = true
        end
        log('Entered room ' .. tostring(currentRoomId) .. ' through door ' .. tostring(state.pendingDoorId))
    elseif state.pendingAction == 'backtrack' then
        if #state.roomPath > 1 then
            state.roomPath[#state.roomPath] = nil
            state.enteredByDoor[#state.enteredByDoor] = nil
        end

        if topRoomId() ~= currentRoomId then
            state.roomPath[#state.roomPath + 1] = currentRoomId
            state.enteredByDoor[#state.enteredByDoor + 1] = state.pendingDoorId
        end

        if state.pendingDoorId ~= nil then
            state.usedDoors[state.pendingDoorId] = true
        end
        log('Backtracked into room ' .. tostring(currentRoomId) .. ' through door ' .. tostring(state.pendingDoorId))
    else
        resetPathToCurrentRoom('Room changed without pending action; resetting path in room ' .. tostring(currentRoomId))
        return true
    end

    validateCurrentEntryDoor()
    clearPendingAction()
    return true
end

local function chooseNearestUnusedDoor()
    if Room == nil or not Room.IsValid then
        return nil
    end

    for i = 1, Room.DoorCount do
        local doorId = Room.GetDoorId(i)
        if doorId ~= nil and doorId >= 0 and not state.usedDoors[doorId] then
            return doorId
        end
    end

    return nil
end

function tick()
    if not syncCurrentRoom() then
        return
    end

    log('tick room=' .. tostring(Room.Id) .. ' doorCount=' .. tostring(Room.DoorCount) .. ' pending=' .. tostring(state.pendingAction))

    local nextDoorId = chooseNearestUnusedDoor()
    if nextDoorId ~= nil then
        state.pendingAction = 'forward'
        state.pendingDoorId = nextDoorId
        log('Issuing GoThroughDoor(' .. tostring(nextDoorId) .. ')')
        GoThroughDoor(nextDoorId)
        return
    end

    if VisitRoomCenterBeforeBacktracking and not state.centeredRooms[Room.Id] then
        state.centeredRooms[Room.Id] = true
        state.pendingAction = 'center'
        log('No unused doors in room ' .. tostring(Room.Id) .. '; issuing GoToRoomCenter()')
        GoToRoomCenter()
        return
    end

    local entryDoorId = state.enteredByDoor[#state.enteredByDoor]
    if entryDoorId ~= nil then
        if not doorExistsInCurrentRoom(entryDoorId) then
            resetPathToCurrentRoom('Cannot backtrack through stale entry door ' .. tostring(entryDoorId) .. ' from room ' .. tostring(Room.Id) .. '; idle here')
            return
        end

        state.pendingAction = 'backtrack'
        state.pendingDoorId = entryDoorId
        log('Backtracking through door ' .. tostring(entryDoorId))
        GoThroughDoor(entryDoorId)
        return
    end

    log('No unused doors and no entry door to backtrack through; idle in room ' .. tostring(Room.Id))
end
";

        #endregion

        public override AgentDecisionType DecisionType => AgentDecisionType.Explorer;

        private enum ExplorePhase
        {
            None,
            MoveToDoor,
            MoveThroughDoor,
            MoveToRoomCenter
        }

        private struct DoorGoal
        {
            public int roomIndex;
            public int neighborRoomIndex;
            public Vector2Int doorCell;
            public Vector2Int throughCell;
            public DirFlags direction;
            public Vector3 doorMap;
            public Vector3 throughMap;
            public string key;
            public string reverseKey;
        }

        [Tooltip("When enabled, a dead-end room is explored by moving to its center before the dog backtracks.")]
        [SerializeField] private bool visitRoomCenterBeforeBacktracking = true;
        [Header("Lua Explore")]
        [Tooltip("Runs the explore behavior through the Lua runtime instead of the built-in C# door queue.")]
        [SerializeField] private bool useLuaExploreScript = true;
        [SerializeField] private bool debugLuaExplore = true;
        [SerializeField] private string luaExploreFileName = "ExploreMode.lua";
        [TextArea(8, 30)]
        [SerializeField] private string luaExploreScript = "";
        //[SerializeField] private float arriveDistance = 0.35f;
        [SerializeField] private int maxDoorsPerRefresh = 32;

        private readonly List<DoorGoal> toExplore = new();
        private readonly HashSet<string> queuedDoorKeys = new();
        private readonly HashSet<string> exploredDoorKeys = new();

        private DoorGoal activeDoor;
        private Vector3 activeRoomCenterMap;
        private ExplorePhase phase;
        private bool needsDoorRefresh = true;
        private int queuedRoomIndex = -1;

        private LuaRuntime? luaRuntime;
        private TaskController? luaTaskController;
        private bool luaScriptLoaded;
        private string loadedLuaExploreScript = "";
        private readonly AgentState luaAgentState = new();
        private bool lastLuaTaskControllerDriving;
        private string lastLuaTaskName = "";
        private int lastLuaRoomId = int.MinValue;
        private int lastLuaDoorCount = -1;
        private bool lastLuaRoomValid;
        private string loadedLuaExploreFriendlyName = "";

        public override void Initialize(AgentModule agentController)
        {
            base.Initialize(agentController);
        }

        #region Tick

        private int debugDoubleTick = -1;
        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            if (dir.gen.buildComplete == false) return;

            if (ShouldReturnToManualControl())
            {
                CancelExploreTasks();
                worldObject.agentModule.SwitchDecisionModule(AgentDecisionType.Player);
                return;
            }

            if (useLuaExploreScript)
            {
                TickLuaExplore(deltaTime);
                return;
            }
            
            if (worldObject.agentMovementModule == null || worldObject.locationModule == null)
            {
                Debug.LogWarning(
                    $"[ExploreDecisionModule {worldObject.DisplayName}] Missing movement or location module; cannot explore.",
                    this);
                return;
            }

            Cell currentCell = worldObject.locationModule.cell;
            if (currentCell == null)
            {
                Debug.LogWarning($"[ExploreDecisionModule {worldObject.DisplayName}] No current cell; cannot explore.");
                return;
            }

            if (needsDoorRefresh)
            {
                RefreshDoorsForRoom(currentCell.room_number);
                queuedRoomIndex = currentCell.room_number;
                needsDoorRefresh = false;
            }

            if (phase == ExplorePhase.None)
            {
                if (!TryActivateNextDoor(currentCell))
                {
                    worldObject.agentMovementModule.ClearDesiredTarget();
                    worldObject.agentMovementModule.ClearDesiredMove();
                }
                return;
            }

            if (phase == ExplorePhase.MoveToDoor && !worldObject.agentMovementModule.MoveToDestinationInProgress)
            {
                phase = ExplorePhase.MoveThroughDoor;
                worldObject.agentMovementModule.SetDesiredTargetLocationMap(activeDoor.throughMap, WalkMode.None, requestPathfinding: true);
                //Debug.Log(
                //    $"{worldObject.DisplayName} [ExploreDecisionModule] reached door {activeDoor.key}; moving through to [{activeDoor.throughCell.x},{activeDoor.throughCell.y}]");
                return;
            }

            if (phase == ExplorePhase.MoveThroughDoor && !worldObject.agentMovementModule.MoveToDestinationInProgress)
            {
                //Debug.Log(
                //    $"{worldObject.DisplayName} [ExploreDecisionModule] completed door traversal {activeDoor.key} -> room {activeDoor.neighborRoomIndex}");
                MarkDoorExplored(activeDoor);
                phase = ExplorePhase.None;
                RefreshDoorsForRoom(activeDoor.neighborRoomIndex);
                queuedRoomIndex = activeDoor.neighborRoomIndex;
                needsDoorRefresh = false;

                if (TryStartDeadEndRoomCenterVisit(activeDoor.neighborRoomIndex))
                    return;
            }

            if (phase == ExplorePhase.MoveToRoomCenter && !worldObject.agentMovementModule.MoveToDestinationInProgress)
            {
                //Debug.Log(
                //    $"{worldObject.DisplayName} [ExploreDecisionModule] reached room center at {activeRoomCenterMap}; resuming door search");
                phase = ExplorePhase.None;
            }
        }

        private void TickLuaExplore(float deltaTime)
        {
            luaTaskController ??= GetComponent<TaskController>();
            if (luaTaskController == null)
            {
                Debug.LogWarning($"[ExploreDecisionModule {worldObject.DisplayName}] Lua explore requires a TaskController.");
                return;
            }

            if (luaTaskController.IsDriving)
            {
                LogLuaTaskState();
                luaTaskController.Tick(deltaTime);
                return;
            }

            if (!EnsureLuaExploreReady())
                return;

            UpdateLuaExploreState();
            luaRuntime!.SetState(luaAgentState);
            luaRuntime.SetGlobal("VisitRoomCenterBeforeBacktracking", visitRoomCenterBeforeBacktracking);
            LogLuaRoomState();

            if (!luaRuntime.CallFunction("tick"))
                LogLuaExplore($"Lua explore tick failed for '{loadedLuaExploreFriendlyName}'.");
        }

        # endregion

        private bool ShouldReturnToManualControl()
        {
            GameInputRouter router = dir != null && dir.gameInputRouter != null
                ? dir.gameInputRouter
                : GameInputRouter.Instance;

            if (router == null || router.InputState == null || !router.IsControlled(worldObject))
                return false;

            PlayerInputState state = router.InputState;
            return state.moveAxis.sqrMagnitude > 0.0001f || Mathf.Abs(state.strafeAxis) > 0.0001f;
        }
        
        private bool EnsureLuaExploreReady()
        {
            luaTaskController ??= GetComponent<TaskController>();
            if (luaTaskController == null)
            {
                Debug.LogWarning($"[ExploreDecisionModule {worldObject.DisplayName}] Lua explore requires a TaskController.");
                return false;
            }

            if (luaRuntime == null)
            {
                luaRuntime = new LuaRuntime();
                LogLuaExplore("Created LuaRuntime for explore mode.");

                var bootstrapEvent = new PerceptionEvent(
                    observer: worldObject,
                    sense: PerceptionSense.Scent,
                    type: PerceptionEventType.SomethingInteresting,
                    worldPos: worldObject.transform.position,
                    target: null,
                    strength01: 0f,
                    novelty01: 0f,
                    interest01: 0f);

                luaRuntime.RegisterBindings(new DogLuaBindings(luaTaskController, worldObject, bootstrapEvent));
                luaAgentState.InitState(worldObject, luaAgentState);
                LogLuaExplore("Registered DogLuaBindings and initialized AgentState.");
            }

            string scriptSource = ResolveLuaExploreScriptSource(out string friendlyName);

            if (luaScriptLoaded &&
                string.Equals(loadedLuaExploreScript, scriptSource, StringComparison.Ordinal))
            {
                return true;
            }

            UpdateLuaExploreState();
            luaRuntime.SetState(
                luaAgentState.Dog,
                luaAgentState.Vision,
                luaAgentState.Hearing,
                luaAgentState.Scent,
                luaAgentState.Pack,
                luaAgentState.Env,
                luaAgentState.Room,
                luaAgentState.Task,
                luaAgentState.Memory,
                luaAgentState.Time);
            luaRuntime.SetGlobal("VisitRoomCenterBeforeBacktracking", visitRoomCenterBeforeBacktracking);

            LogLuaExplore($"Loading Lua explore script '{friendlyName}'. chars={scriptSource.Length}");
            luaScriptLoaded = luaRuntime.LoadScript(scriptSource, friendlyName);
            if (luaScriptLoaded)
            {
                loadedLuaExploreScript = scriptSource;
                loadedLuaExploreFriendlyName = friendlyName;
                LogLuaExplore("Lua explore script loaded successfully.");
            }
            else
            {
                LogLuaExplore("Lua explore script failed to load.");
            }

            return luaScriptLoaded;
        }

        private string ResolveLuaExploreScriptSource(out string friendlyName)
        {
            if (!string.IsNullOrWhiteSpace(luaExploreScript))
            {
                friendlyName = "ExploreDecisionModule.luaExploreScript";
                return luaExploreScript;
            }

            string scriptFile = string.IsNullOrWhiteSpace(luaExploreFileName)
                ? "ExploreMode.lua"
                : luaExploreFileName.Trim();

            if (LuaScriptLoader.TryLoad(scriptFile, out string source, out friendlyName, out string? error))
                return source;

            friendlyName = $"default:{scriptFile}";
            LogLuaExplore($"Could not load '{scriptFile}' ({error}); using built-in default explore script.");
            return DefaultLuaExploreScript;
        }

        private void UpdateLuaExploreState()
        {
            luaAgentState.Room.UpdateState(Detail.High);
            luaAgentState.Task.UpdateState(Detail.Low);
            luaAgentState.Time.UpdateState(Detail.Low);
        }

        private void ResetLuaExploreRuntime()
        {
            luaRuntime = null;
            luaTaskController = null;
            luaScriptLoaded = false;
            loadedLuaExploreScript = "";
            loadedLuaExploreFriendlyName = "";
            lastLuaTaskControllerDriving = false;
            lastLuaTaskName = "";
            lastLuaRoomId = int.MinValue;
            lastLuaDoorCount = -1;
            lastLuaRoomValid = false;
        }

        private void CancelExploreTasks()
        {
            luaTaskController ??= GetComponent<TaskController>();
            luaTaskController?.CancelAllTasks();
        }

        private void LogLuaExplore(string message)
        {
            if (!debugLuaExplore)
                return;

            //Debug.Log($"[ExploreDecisionModule {worldObject.DisplayName}][Lua] {message}", this);
        }

        private void LogLuaRoomState()
        {
            if (!debugLuaExplore)
                return;

            bool roomValid = luaAgentState.Room.IsValid;
            int roomId = luaAgentState.Room.Id;
            int doorCount = luaAgentState.Room.DoorCount;

            if (roomValid == lastLuaRoomValid &&
                roomId == lastLuaRoomId &&
                doorCount == lastLuaDoorCount)
            {
                return;
            }

            lastLuaRoomValid = roomValid;
            lastLuaRoomId = roomId;
            lastLuaDoorCount = doorCount;
            LogLuaExplore($"RoomState valid={roomValid} roomId={roomId} doorCount={doorCount}");
        }

        private void LogLuaTaskState()
        {
            if (!debugLuaExplore || luaTaskController == null)
                return;

            bool isDriving = luaTaskController.IsDriving;
            string taskName = luaTaskController.taskExecutor.CurrentTaskName ?? "";

            if (isDriving == lastLuaTaskControllerDriving && taskName == lastLuaTaskName)
                return;

            lastLuaTaskControllerDriving = isDriving;
            lastLuaTaskName = taskName;
            LogLuaExplore($"TaskController driving={isDriving} currentTask='{(string.IsNullOrEmpty(taskName) ? "<none>" : taskName)}'");
        }

        private void RefreshDoorsForRoom(int roomIndex)
        {
            if (worldObject.llmWorldStateModule == null || dir == null || dir.gen == null || dir.gen.rooms == null)
                return;

            if (roomIndex < 0 || roomIndex >= dir.gen.rooms.Count)
                return;

            Room room = dir.gen.rooms[roomIndex];
            if (room == null)
                return;

            RectInt roomBounds = room.GetBounds();
            worldObject.llmWorldStateModule.BuildDoorsList(worldObject.pos3d_map, room, roomBounds, maxDoorsPerRefresh);

            List<DogGame.LLM.Agent.LLMWorldStateModule.FoundDoor> foundDoors =
                worldObject.llmWorldStateModule.GetDoorsInRoom(worldObject.pos3d_map, room, roomBounds, maxDoorsPerRefresh);

            for (int i = 0; i < foundDoors.Count; i++)
            {
                if (!TryCreateDoorGoal(roomIndex, foundDoors[i], out DoorGoal goal))
                    continue;

                if (exploredDoorKeys.Contains(goal.key) || queuedDoorKeys.Contains(goal.key))
                    continue;

                toExplore.Add(goal);
                queuedDoorKeys.Add(goal.key);
                //Debug.Log(
                //    $"{worldObject.DisplayName} [ExploreDecisionModule] Queued door {goal.key} -> room {goal.neighborRoomIndex} for {worldObject.DisplayName}");
            }
        }

        private bool TryActivateNextDoor(Cell currentCell)
        {
            while (toExplore.Count > 0)
            {
                int index = FindBestQueuedDoorIndex(currentCell);
                if (index < 0)
                    return false;

                DoorGoal goal = toExplore[index];
                toExplore.RemoveAt(index);
                queuedDoorKeys.Remove(goal.key);

                if (exploredDoorKeys.Contains(goal.key))
                    continue;

                activeDoor = goal;
                phase = ExplorePhase.MoveToDoor;
                worldObject.agentMovementModule.SetDesiredTargetLocationMap(goal.doorMap, WalkMode.None, requestPathfinding: true);
                //Debug.Log(
                //    $"{worldObject.DisplayName} [ExploreDecisionModule] heading to door {goal.key} from room {goal.roomIndex} toward room {goal.neighborRoomIndex}");
                return true;
            }

            return false;
        }

        private void MarkDoorExplored(DoorGoal goal)
        {
            exploredDoorKeys.Add(goal.key);
            exploredDoorKeys.Add(goal.reverseKey);
        }

        private int FindBestQueuedDoorIndex(Cell currentCell)
        {
            if (currentCell == null || toExplore.Count == 0)
                return -1;

            int bestCurrentRoomIndex = -1;
            float bestCurrentRoomDist = float.PositiveInfinity;

            int bestFallbackIndex = -1;
            float bestFallbackDist = float.PositiveInfinity;

            Vector3 currentPos = worldObject.pos3d_map;

            for (int i = 0; i < toExplore.Count; i++)
            {
                DoorGoal goal = toExplore[i];
                float dist = (goal.doorMap - currentPos).sqrMagnitude;

                if (goal.roomIndex == currentCell.room_number)
                {
                    if (dist < bestCurrentRoomDist)
                    {
                        bestCurrentRoomDist = dist;
                        bestCurrentRoomIndex = i;
                    }
                    continue;
                }

                if (dist < bestFallbackDist)
                {
                    bestFallbackDist = dist;
                    bestFallbackIndex = i;
                }
            }

            return bestCurrentRoomIndex >= 0 ? bestCurrentRoomIndex : bestFallbackIndex;
        }

        private bool TryStartDeadEndRoomCenterVisit(int roomIndex)
        {
            if (!visitRoomCenterBeforeBacktracking)
                return false;

            if (HasQueuedDoorForRoom(roomIndex))
                return false;

            if (!TryGetRoomCenterMap(roomIndex, out Vector3 roomCenterMap))
                return false;

            float stopDistance = worldObject.agentMovementModule.StopDistance;
            Vector3 delta = roomCenterMap - worldObject.pos3d_map;
            if (delta.sqrMagnitude <= stopDistance * stopDistance)
                return false;

            activeRoomCenterMap = roomCenterMap;
            phase = ExplorePhase.MoveToRoomCenter;
            worldObject.agentMovementModule.SetDesiredTargetLocationMap(activeRoomCenterMap, WalkMode.None, requestPathfinding: true);
            //Debug.Log(
            //    $"{worldObject.DisplayName} [ExploreDecisionModule] room {roomIndex} is a dead end; visiting center at {activeRoomCenterMap} before backtracking");
            return true;
        }

        private bool HasQueuedDoorForRoom(int roomIndex)
        {
            for (int i = 0; i < toExplore.Count; i++)
            {
                if (toExplore[i].roomIndex == roomIndex)
                    return true;
            }

            return false;
        }

        private bool TryGetRoomCenterMap(int roomIndex, out Vector3 roomCenterMap)
        {
            roomCenterMap = default;

            if (dir == null || dir.gen == null || dir.gen.rooms == null)
                return false;

            if (roomIndex < 0 || roomIndex >= dir.gen.rooms.Count)
                return false;

            Room room = dir.gen.rooms[roomIndex];
            if (room == null || room.cells == null || room.cells.Count == 0)
                return false;

            Vector3 averageCenter = Vector3.zero;
            for (int i = 0; i < room.cells.Count; i++)
                averageCenter += room.cells[i].center3d_f;

            averageCenter /= room.cells.Count;

            Cell bestCell = room.cells[0];
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < room.cells.Count; i++)
            {
                Cell candidate = room.cells[i];
                float distance = (candidate.center3d_f - averageCenter).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCell = candidate;
                }
            }

            roomCenterMap = bestCell.center3d_f;
            return true;
        }

        private bool TryCreateDoorGoal(int roomIndex, DogGame.LLM.Agent.LLMWorldStateModule.FoundDoor foundDoor, out DoorGoal goal)
        {
            goal = default;

            if (dir == null || dir.gen == null || dir.gen.cellGrid == null)
                return false;

            Vector2Int step = foundDoor.direction.ToVector2Int();
            Vector2Int throughCell = foundDoor.pos + step;

            if (throughCell.x < 0 || throughCell.x >= dir.cfg.mapWidth || throughCell.y < 0 || throughCell.y >= dir.cfg.mapHeight)
                return false;

            Cell doorCell = dir.gen.cellGrid[foundDoor.pos.x, foundDoor.pos.y];
            Cell nextCell = dir.gen.cellGrid[throughCell.x, throughCell.y];
            if (doorCell == null || nextCell == null)
                return false;

            int neighborRoomIndex = nextCell.room_number;
            if (neighborRoomIndex < 0)
                return false;

            goal = new DoorGoal
            {
                roomIndex = roomIndex,
                neighborRoomIndex = neighborRoomIndex,
                doorCell = foundDoor.pos,
                throughCell = throughCell,
                direction = foundDoor.direction,
                doorMap = CellCenterMap(doorCell),
                throughMap = CellCenterMap(nextCell),
                key = BuildDoorKey(roomIndex, foundDoor.pos, foundDoor.direction),
                reverseKey = BuildDoorKey(neighborRoomIndex, throughCell, foundDoor.direction.Opposite())
            };
            Vector3 doorDelta = (goal.throughMap - goal.doorMap).normalized;
            goal.throughMap += doorDelta * 0.3f;
            //Debug.Log($"{worldObject.DisplayName} new DoorGoal: doorMap = {goal.doorMap}, throughMap = {goal.throughMap}");
            return true;
        }

        private static Vector3 CellCenterMap(Cell cell)
        {
            return cell.center3d_f;
        }

        private static string BuildDoorKey(int roomIndex, Vector2Int cell, DirFlags direction)
        {
            return $"{roomIndex}:{cell.x},{cell.y}:{(int)direction}";
        }

        public override void BeginDecisionModule(bool resume=false)
        {
            UseAutonomousFaceMovement();
            toExplore.Clear();
            queuedDoorKeys.Clear();
            phase = ExplorePhase.None;
            needsDoorRefresh = true;
            queuedRoomIndex = -1;
        }
        public override void EndDecisionModule()
        {
            CancelExploreTasks();
            toExplore.Clear();
            queuedDoorKeys.Clear();
            StopMovementIntent();
            phase = ExplorePhase.None;
            needsDoorRefresh = true;
            queuedRoomIndex = -1;
        }
    }
}
