using UnityEngine;
using System.Collections.Generic;
using InspectorTools;

namespace DogGame.Modules
{
    [InspectorNote("AgentDecision_Modules/Wander Decision Module", "Agent wanders around the current room only.")]
    [DisallowMultipleComponent]
    public class WandererDecisionModule : AgentDecisionModuleBase
    {
        public override AgentDecisionType DecisionType => AgentDecisionType.Wanderer;

        [SerializeField] private float dwellSecondsAtDestination = 2f;
        [SerializeField] private float moveTargetTimeoutSeconds = 10f;

        private Vector3 currentWanderTargetMap;
        private float dwellRemainingSeconds;
        private float moveTargetElapsedSeconds;

        private enum WanderState
        {
            PickTarget,
            MoveToTarget,
            WaitAtTarget
        }

        private WanderState state = WanderState.PickTarget;

        [Header("Runtime Debug")]
        [SerializeField] private WanderState debugState;
        [SerializeField] private Vector3 debugDesiredTargetLocationMap;
        [SerializeField] private bool debugHasDesiredTarget;
        [SerializeField] private bool debugLastPickSucceeded;
        [SerializeField] private string debugLastPickFailureReason = "";
        [SerializeField] private int debugCurrentRoomIndex = -1;
        [SerializeField] private Vector2Int debugCurrentCell = new Vector2Int(-1, -1);
        [SerializeField] private Vector2Int debugDesiredTargetCell = new Vector2Int(-1, -1);
        [SerializeField] private int debugCandidateCount;
        [SerializeField] private int debugPickAttempts;
        [SerializeField] private float debugMoveTargetElapsedSeconds;
        [SerializeField] private bool debugLastMoveTimedOut;

        public override void Initialize(AgentModule agentController)
        {
            base.Initialize(agentController);
            //PickNewTarget();  // Don't do here, not everything needed has been initialized yet.
        }

        #region Tick

        private int debugDoubleTick = -1;
        public override void Tick(float deltaTime)
        {
            debugState = state;

            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            if (worldObject.agentMovementModule == null)
            {
                ClearDebugTarget("No AgentMovementModule found.");
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
                        debugState = state;
                        worldObject.agentMovementModule.ClearDesiredMove();
                        return;
                    }
                    debugState = state;
                    break;

                case WanderState.MoveToTarget:
                    moveTargetElapsedSeconds += deltaTime;
                    debugMoveTargetElapsedSeconds = moveTargetElapsedSeconds;
                    if (moveTargetTimeoutSeconds > 0f && moveTargetElapsedSeconds >= moveTargetTimeoutSeconds)
                    {
                        debugLastMoveTimedOut = true;
                        state = WanderState.PickTarget;
                        worldObject.agentMovementModule.ClearDesiredMove();
                        debugState = state;
                        return;
                    }

                    if (!worldObject.agentMovementModule.MoveToDestinationInProgress)
                    {
                        dwellRemainingSeconds = Mathf.Max(0f, dwellSecondsAtDestination);
                        state = WanderState.WaitAtTarget;
                        worldObject.agentMovementModule.ClearDesiredMove();
                    }
                    debugState = state;
                    break;

                case WanderState.WaitAtTarget:
                    dwellRemainingSeconds -= deltaTime;
                    worldObject.agentMovementModule.ClearDesiredMove();
                    if (dwellRemainingSeconds <= 0f)
                        state = WanderState.PickTarget;
                    debugState = state;
                    break;
            }
        }
        
        #endregion

    //   [SerializeField] private LocationModule locationModule;
    //   [SerializeField] private float wanderRadius = 5f;
    //   [SerializeField] private float minTimeBetweenTargets = 1.5f;
    //   [SerializeField] private float maxTimeBetweenTargets = 4.0f;

        private bool PickNewTargetInCurrentRoom()
        {
            debugLastPickSucceeded = false;
            debugLastPickFailureReason = "";
            debugPickAttempts = 0;
            debugCandidateCount = 0;
            debugCurrentRoomIndex = -1;
            debugDesiredTargetCell = new Vector2Int(-1, -1);
            debugLastMoveTimedOut = false;

            Room room = GetCurrentRoom();
            if (room == null || room.cells == null || room.cells.Count == 0)
            {
                ClearDebugTarget("Current room has no wander candidates.");
                return false;
            }

            List<Cell> candidates = room.cells;
            Cell currentCell = worldObject.locationModule != null ? worldObject.locationModule.cell : null;
            int currentCellX = currentCell != null ? currentCell.x : Mathf.FloorToInt(worldObject.pos3d_map.x);
            int currentCellY = currentCell != null ? currentCell.y : Mathf.FloorToInt(worldObject.pos3d_map.z);
            debugCandidateCount = candidates.Count;
            debugCurrentCell = new Vector2Int(currentCellX, currentCellY);
            debugCurrentRoomIndex = currentCell != null
                ? currentCell.room_number
                : -1;

            Cell chosen = null;
            int startIndex = Random.Range(0, candidates.Count);
            for (int attempt = 0; attempt < candidates.Count; attempt++)
            {
                debugPickAttempts = attempt + 1;
                Cell candidate = candidates[(startIndex + attempt) % candidates.Count];
                if (candidate == null)
                    continue;

                if (candidate.x == currentCellX && candidate.y == currentCellY && candidates.Count > 1)
                    continue;

                chosen = candidate;
                break;
            }

            if (chosen == null)
            {
                ClearDebugTarget("No valid cell candidate found.");
                return false;
            }

            currentWanderTargetMap = chosen.center3d_f;
            moveTargetElapsedSeconds = 0f;
            debugMoveTargetElapsedSeconds = 0f;
            debugDesiredTargetLocationMap = currentWanderTargetMap;
            debugDesiredTargetCell = new Vector2Int(chosen.x, chosen.y);
            debugHasDesiredTarget = true;
            debugLastPickSucceeded = true;
            state = WanderState.MoveToTarget;
            worldObject.agentMovementModule.SetDesiredTargetLocationMap(
                currentWanderTargetMap,
                WalkMode.None,
                requestPathfinding: true,
                allowDoors: false);
            return true;
        }

        private void ClearDebugTarget(string failureReason)
        {
            debugDesiredTargetLocationMap = Vector3.zero;
            debugHasDesiredTarget = false;
            debugLastPickSucceeded = false;
            debugLastPickFailureReason = failureReason;
            debugDesiredTargetCell = new Vector2Int(-1, -1);
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
            UseAutonomousFaceMovement();
            state = WanderState.PickTarget;
            debugState = state;
            dwellRemainingSeconds = 0f;
            moveTargetElapsedSeconds = 0f;
            debugMoveTargetElapsedSeconds = 0f;
            debugLastMoveTimedOut = false;
            ClearDebugTarget("");
        }
        public override void EndDecisionModule()
        {
            dwellRemainingSeconds = 0f;
            moveTargetElapsedSeconds = 0f;
            debugMoveTargetElapsedSeconds = 0f;
            debugState = state;
            ClearDebugTarget("");
            StopMovementIntent();
        }
    }
}
