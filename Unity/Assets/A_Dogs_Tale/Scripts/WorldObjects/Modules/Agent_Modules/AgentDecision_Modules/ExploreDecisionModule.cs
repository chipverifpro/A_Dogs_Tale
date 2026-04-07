using System.Collections.Generic;
using UnityEngine;

namespace DogGame.Modules
{
    public class ExploreDecisionModule : AgentDecisionModuleBase
    {
        public override AgentDecisionType DecisionType => AgentDecisionType.Explorer;

        private enum ExplorePhase
        {
            None,
            MoveToDoor,
            MoveThroughDoor
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

        [SerializeField] private WalkMode exploreWalkMode = WalkMode.Walk;
        //[SerializeField] private float arriveDistance = 0.35f;
        [SerializeField] private int maxDoorsPerRefresh = 32;

        private readonly List<DoorGoal> toExplore = new();
        private readonly HashSet<string> queuedDoorKeys = new();
        private readonly HashSet<string> exploredDoorKeys = new();

        private DoorGoal activeDoor;
        private ExplorePhase phase;
        private bool needsDoorRefresh = true;
        private int queuedRoomIndex = -1;

        public override void Initialize(AgentModule agentController)
        {
            base.Initialize(agentController);
        }

        private int debugDoubleTick = -1;
        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            if (dir.gen.buildComplete == false) return;
            
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
                worldObject.agentMovementModule.SetDesiredTargetLocationMap(activeDoor.throughMap, exploreWalkMode, requestPathfinding: true);
                Debug.Log(
                    $"{worldObject.DisplayName} [ExploreDecisionModule] reached door {activeDoor.key}; moving through to [{activeDoor.throughCell.x},{activeDoor.throughCell.y}]");
                return;
            }

            if (phase == ExplorePhase.MoveThroughDoor && !worldObject.agentMovementModule.MoveToDestinationInProgress)
            {
                Debug.Log(
                    $"{worldObject.DisplayName} [ExploreDecisionModule] completed door traversal {activeDoor.key} -> room {activeDoor.neighborRoomIndex}");
                phase = ExplorePhase.None;
                needsDoorRefresh = true;
                queuedRoomIndex = -1;
            }
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
                Debug.Log(
                    $"{worldObject.DisplayName} [ExploreDecisionModule] Queued door {goal.key} -> room {goal.neighborRoomIndex} for {worldObject.DisplayName}");
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

                exploredDoorKeys.Add(goal.key);
                exploredDoorKeys.Add(goal.reverseKey);

                activeDoor = goal;
                phase = ExplorePhase.MoveToDoor;
                worldObject.agentMovementModule.SetDesiredTargetLocationMap(goal.doorMap, exploreWalkMode, requestPathfinding: true);
                Debug.Log(
                    $"{worldObject.DisplayName} [ExploreDecisionModule] heading to door {goal.key} from room {goal.roomIndex} toward room {goal.neighborRoomIndex}");
                return true;
            }

            return false;
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
            Debug.Log($"{worldObject.DisplayName} new DoorGoal: doorMap = {goal.doorMap}, throughMap = {goal.throughMap}");
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
            toExplore.Clear();
            queuedDoorKeys.Clear();
            exploredDoorKeys.Clear();
            phase = ExplorePhase.None;
            needsDoorRefresh = true;
            queuedRoomIndex = -1;
        }
        public override void EndDecisionModule()
        {
            toExplore.Clear();
            queuedDoorKeys.Clear();
            worldObject.agentMovementModule?.ClearDesiredTarget();
            phase = ExplorePhase.None;
            needsDoorRefresh = true;
            queuedRoomIndex = -1;
        }
    }
}
