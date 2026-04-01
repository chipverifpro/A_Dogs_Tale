using UnityEngine;
using System.Collections.Generic;

namespace DogGame.Modules
{
    public class WandererDecisionModule : AgentDecisionModuleBase
    {
        public override AgentDecisionType DecisionType => AgentDecisionType.Wanderer;

        [SerializeField] private float dwellSecondsAtDestination = 2f;
        [SerializeField] private WalkMode wanderWalkMode = WalkMode.Walk;

        private Vector3 currentWanderTargetMap;
        private float dwellRemainingSeconds;

        private enum WanderState
        {
            PickTarget,
            MoveToTarget,
            WaitAtTarget
        }

        private WanderState state = WanderState.PickTarget;

        public override void Initialize(AgentModule agentController)
        {
            base.Initialize(agentController);
            //PickNewTarget();  // Don't do here, not everything needed has been initialized yet.
        }

        private int debugDoubleTick = -1;
        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            if (worldObject.agentMovementModule == null)
            {
                Debug.LogWarning(
                    $"[WanderDecisionModule {worldObject.DisplayName}] " +
                    $"No AgentMovementModule found; cannot wander.",
                    this);
                return;
            }

            switch (state)
            {
                case WanderState.PickTarget:
                    if (!PickNewTargetInCurrentRoom())
                    {
                        worldObject.agentMovementModule.ClearDesiredMove();
                        return;
                    }
                    break;

                case WanderState.MoveToTarget:
                    if (!worldObject.agentMovementModule.MoveToDestinationInProgress)
                    {
                        dwellRemainingSeconds = Mathf.Max(0f, dwellSecondsAtDestination);
                        state = WanderState.WaitAtTarget;
                        worldObject.agentMovementModule.ClearDesiredMove();
                    }
                    break;

                case WanderState.WaitAtTarget:
                    dwellRemainingSeconds -= deltaTime;
                    worldObject.agentMovementModule.ClearDesiredMove();
                    if (dwellRemainingSeconds <= 0f)
                        state = WanderState.PickTarget;
                    break;
            }
        }

    //   [SerializeField] private LocationModule locationModule;
    //   [SerializeField] private float wanderRadius = 5f;
    //   [SerializeField] private float minTimeBetweenTargets = 1.5f;
    //   [SerializeField] private float maxTimeBetweenTargets = 4.0f;

        private bool PickNewTargetInCurrentRoom()
        {
            Room room = GetCurrentRoom();
            if (room == null || room.cells == null || room.cells.Count == 0)
                return false;

            List<Cell> candidates = room.cells;
            int currentCellX = Mathf.FloorToInt(worldObject.pos3d_map.x);
            int currentCellY = Mathf.FloorToInt(worldObject.pos3d_map.z);

            Cell chosen = null;
            int startIndex = Random.Range(0, candidates.Count);
            for (int attempt = 0; attempt < candidates.Count; attempt++)
            {
                Cell candidate = candidates[(startIndex + attempt) % candidates.Count];
                if (candidate == null)
                    continue;

                if (candidate.x == currentCellX && candidate.y == currentCellY && candidates.Count > 1)
                    continue;

                chosen = candidate;
                break;
            }

            if (chosen == null)
                return false;

            currentWanderTargetMap = chosen.center3d_f;
            state = WanderState.MoveToTarget;
            worldObject.agentMovementModule.SetDesiredTargetLocationMap(
                currentWanderTargetMap,
                wanderWalkMode,
                requestPathfinding: true,
                allowDoors: false);
            return true;
        }

        private Room GetCurrentRoom()
        {
            Cell currentCell = worldObject?.locationModule?.cell;
            if (currentCell == null || dir?.gen?.rooms == null)
                return null;

            int roomIndex = currentCell.room_number;
            if (roomIndex < 0 || roomIndex >= dir.gen.rooms.Count)
                return null;

            return dir.gen.rooms[roomIndex];
        }

        public override void BeginDecisionModule(bool resume=false)
        {
            state = WanderState.PickTarget;
            dwellRemainingSeconds = 0f;
        }
        public override void EndDecisionModule()
        {
            dwellRemainingSeconds = 0f;
            worldObject?.agentMovementModule?.ClearDesiredMove();
        }
    }
}
