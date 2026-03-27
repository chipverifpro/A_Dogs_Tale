using UnityEngine;
using System;
using System.Collections.Generic;
/*
AgentMovementModule (high-level locomotion)

This should be your locomotion controller, not a physics thing.

Responsibilities:
	•	Owns the current desired velocity / direction for the agent:
	•	World-space move vector
	•	Desired speed (walk, trot, sprint)
	•	Handles:
	•	Blending between input / pathfinding / steering
	•	Stopping, slowing, acceleration/deceleration
	•	Passing the final desired motion down to MotionModule
	•	Does not know about CharacterController, rigidbodies, etc.
*/

namespace DogGame.Modules
{

    /// <summary>
    /// High-level locomotion module that converts "movement intent" into an actual
    /// velocity and delegates to MotionModule to move the agent.
    ///
    /// Responsibilities:
    ///   - Store a desired world-space velocity (from decisions / input).
    ///   - Apply acceleration and deceleration toward that desired velocity.
    ///   - Call MotionModule.Move() each frame with the current velocity.
    ///
    /// This module does NOT read input directly and does NOT move transforms itself.
    /// Decision modules (Player, Wanderer, Follower, etc.) should call SetDesiredMove()
    /// or SetDesiredVelocity() based on their logic.
    /// </summary>
    public class AgentMovementModule : WorldModule
    {
        [Header("For following and routing")]
        // next crumb in trail we are following
        public Crumb next_actualCrumb;
        public Crumb next_formationCrumb;

        [Header("Current Destination")]
        // Object or Location we are going towards.
        public WorldObject targetObject;        // for continuous tracking of a (possibly moving) target object
                                                // every tick, update targetLocation to it's current world location
        public bool keepFollowingTargetObject;  // if (true) then upon arrival, we wait for the target to move and keep following it indefinitely.
                                                // if (false) then upon arrival, this task is complete.
        public Vector3? targetLocation;         // for travelling to a destination location or current location of target object

        public float stopDistanceFromObject;    // when heading to an object, don't run inside it.
                                                //   (should be radius of agent + radius of target)
                                                // also used as follow distance when continuing to follow agents.
                                                //   (should be packModule.followDistanceMeters)

        [Header("Pathfinding")]
        [SerializeField] private bool useGridPathfinding = true;
        [SerializeField] private float pathWaypointArrivalRadius = 0.20f;
        [SerializeField] private float pathSmoothingIntervalSeconds = 0.20f;
        [SerializeField] private float movingTargetRepathIntervalSeconds = 0.20f;
        [SerializeField] private bool enablePathDebugLogging = false;
        private readonly List<Vector2Int> activePathCells = new();
        private int activePathIndex = -1;
        private Vector2Int activePathGoalCell;
        private bool hasActivePath = false;
        private float smoothingCooldownSeconds = 0f;
        private float movingTargetRepathCooldownSeconds = 0f;
        private Vector2Int lastKnownTargetObjectCell;
        private bool hasLastKnownTargetObjectCell = false;
        private Pathfinding pathfinding;

        [Header("Stall Recovery")]
        [SerializeField] private bool enableStallRecovery = true;
        [SerializeField] private int stallTicksBeforeRecover = 3;
        [SerializeField] private float stallPositionEpsilon = 0.01f;
        [SerializeField] private float centerRecoveryArrivalRadius = 0.08f;
        [SerializeField] private bool enableStallDebugLogging = false;
        private Vector3 lastStallCheckPosition = Vector3.zero;
        private int consecutiveStallTicks = 0;
        private bool recoveringToCellCenter = false;
        private Vector3 recoveryTargetWorld = Vector3.zero;
        
        [Header("Acceleration")]
        [Tooltip("Acceleration toward desired velocity in meters per second squared.")]
        [SerializeField] private float accelerationMetersPerSecondSquared = 12.0f;

        [Tooltip("Deceleration when stopping or changing direction in meters per second squared.")]
        [SerializeField] private float decelerationMetersPerSecondSquared = 16.0f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = false;

        [Header("Velocity")]
        // Current velocity we are actually moving with (world-space, horizontal+vertical from MotionModule)
        private Vector3 currentVelocity = Vector3.zero;

        // Desired velocity requested by decision modules (world-space, horizontal only here)
        private Vector3 desiredVelocity = Vector3.zero;

        [Header("Max Speed")]
        // Used to choose between walk/run speeds when using SetDesiredMove()
        //private bool desireRun = false;
        public WalkMode walkMode;
        public float maxWalkModeSpeed;      // result of lookup WalkMode to Max Speed
        private float speedFactor01 = 1.0f; // 0..1 scaling of walk/run speed

        /// <summary>
        /// Usually travel distance = desiredVelocity * deltaTime.  Limit that.
        /// Never travel farther than maxDistance on a tick.
        /// A) it could be how far the target is away so we don't overshoot.
        /// B) it could be how far to a barrier so we don't go through it.
        /// C) with tile-based bump detector, we only looked 1 tile ahead for collisions.
        ///    we will want to recalculate bumping into objects the next tile,
        ///    so don't move beyond that until we have checked again.
        /// </summary>
        public float maxDistance = 1f;      // TODO: Move to MotionModule


        protected override void Awake()
        {
            base.Awake();
            EnsureRuntimeReferences();
        }

        private void OnEnable()
        {
            EnsureRuntimeReferences();
        }

        // this just forwards the change to motionModule where it is kept.
        public void SetWalkMode(WalkMode walkMode)
        {
            worldObject.motionModule.SetWalkMode(walkMode);
        }

        // Set target in world location space, and we will travel to it until arrived.
        public void SetDesiredTargetLocation(UnityEngine.Vector3 targetLocation_world)
        {
            SetDesiredTargetLocation(targetLocation_world, WalkMode.None, requestPathfinding: true);
        }

        // Set target once, and we will keep following it until we arrive.
        public void SetDesiredTargetWorldObject(WorldObject target, bool keepFollowing=false)
        {
            this.targetObject = target;
            this.keepFollowingTargetObject = keepFollowing;
            this.targetLocation = target != null ? target.pos3d_world : null;
            CacheTargetObjectCell();
            RebuildPathToCurrentTarget(forceRebuild: true);
        }

        public void ClearDesiredTarget()
        {
            ClearDesiredTargetWorldObject();
            ClearDesiredTargetLocation();
        }

        public void ClearDesiredTargetWorldObject()
        {
            targetObject = null;
            keepFollowingTargetObject = false;
            hasLastKnownTargetObjectCell = false;
            movingTargetRepathCooldownSeconds = 0f;
        }

        public void ClearDesiredTargetLocation()
        {
            targetLocation = null;
            ClearActivePath();
            hasLastKnownTargetObjectCell = false;
            recoveringToCellCenter = false;
            consecutiveStallTicks = 0;
        }

        public void UpdateDesiredVelocityFromTargetIfAny()
        {
            if (targetObject!=null) 
            {
                
            }
        }

        private bool EnsureRuntimeReferences()
        {
            if (pathfinding == null)
            {
                pathfinding = dir != null
                    ? (dir.pathfinding != null ? dir.pathfinding : FindFirstObjectByType<Pathfinding>())
                    : FindFirstObjectByType<Pathfinding>();
            }

            if (dir != null && dir.pathfinding == null && pathfinding != null)
                dir.pathfinding = pathfinding;

            return worldObject != null && worldObject.motionModule != null;
        }

        private void ClearActivePath()
        {
            activePathCells.Clear();
            activePathIndex = -1;
            hasActivePath = false;
            smoothingCooldownSeconds = 0f;
        }

        private void CacheTargetObjectCell()
        {
            if (targetLocation.HasValue && TryGetGridCell(targetLocation.Value, GetTargetCoordinateSpaceOwner(), out Vector2Int cell))
            {
                lastKnownTargetObjectCell = cell;
                hasLastKnownTargetObjectCell = true;
            }
            else
            {
                hasLastKnownTargetObjectCell = false;
            }
        }

        private bool TryGetGridCell(Vector3 worldPosition, WorldObject coordinateSpaceOwner, out Vector2Int cell)
        {
            cell = default;

            if (dir == null || dir.gen == null || !dir.gen.buildComplete || dir.gen.cellGrid == null)
                return false;

            Vector3 mapPosition = coordinateSpaceOwner != null
                ? coordinateSpaceOwner.WorldToMapPosition(worldPosition)
                : worldPosition;

            int x = Mathf.FloorToInt(mapPosition.x);
            int y = Mathf.FloorToInt(mapPosition.z);
            if (!dir.gen.In(x, y))
                return false;

            if (dir.gen.cellGrid[x, y] == null)
                return false;

            cell = new Vector2Int(x, y);
            return true;
        }

        private bool TryGetGridCellData(Vector2Int pos, out Cell cell)
        {
            cell = null;

            if (dir == null || dir.gen == null || dir.gen.cellGrid == null || !dir.gen.In(pos.x, pos.y))
                return false;

            cell = dir.gen.cellGrid[pos.x, pos.y];
            return cell != null;
        }

        private Vector3 CellCenterWorld(Vector2Int cell, float fallbackHeight)
        {
            float height = fallbackHeight;
            if (dir != null && dir.gen != null && dir.gen.cellGrid != null && dir.gen.In(cell.x, cell.y))
            {
                Cell gridCell = dir.gen.cellGrid[cell.x, cell.y];
                if (gridCell != null)
                    height = gridCell.height;
            }

            Vector3 mapPosition = new Vector3(Mathf.Floor(cell.x) + 0.5f, height, Mathf.Floor(cell.y) + 0.5f);
            return mapPosition;
            //return worldObject != null ? worldObject.MapToWorldPosition(mapPosition) : mapPosition;
        }

        private WorldObject GetTargetCoordinateSpaceOwner()
        {
            return targetObject != null ? targetObject : worldObject;
        }

        private bool RebuildPathToCurrentTarget(bool forceRebuild = false)
        {
            if (!useGridPathfinding || targetLocation == null)
            {
                ClearActivePath();
                return false;
            }

            if (!EnsureRuntimeReferences() || pathfinding == null)
            {
                ClearActivePath();
                return false;
            }

            if (!TryGetGridCell(worldObject.pos3d_world, worldObject, out Vector2Int startCell) ||
                !TryGetGridCell(targetLocation.Value, GetTargetCoordinateSpaceOwner(), out Vector2Int goalCell))
            {
                ClearActivePath();
                return false;
            }

            if (!forceRebuild && hasActivePath && goalCell == activePathGoalCell)
                return true;

            List<Vector2Int> path = pathfinding.FindPath(startCell, goalCell);
            ClearActivePath();
            activePathGoalCell = goalCell;

            if (path.Count <= 1)
                return false;

            for (int i = 1; i < path.Count; i++)
                activePathCells.Add(path[i]);

            hasActivePath = activePathCells.Count > 0;
            activePathIndex = hasActivePath ? 0 : -1;
            smoothingCooldownSeconds = 0f;

            TrySmoothActivePath(force: true);

            if (enablePathDebugLogging)
                Debug.Log($"[AgentMovementModule] {worldObject.DisplayName} path cells={activePathCells.Count} start={startCell} goal={goalCell}", this);

            return hasActivePath;
        }

        private bool FollowActivePath()
        {
            if (!hasActivePath || activePathIndex < 0 || activePathIndex >= activePathCells.Count)
                return false;

            while (activePathIndex < activePathCells.Count)
            {
                Vector3 waypoint = CellCenterWorld(activePathCells[activePathIndex], worldObject.locationModule.height);
                if (!PointTowardWorldLocation(waypoint, pathWaypointArrivalRadius))
                    return true;

                activePathIndex++;
                smoothingCooldownSeconds = 0f;
                TrySmoothActivePath(force: true);
            }

            ClearActivePath();
            return false;
        }

        private void TrySmoothActivePath(bool force = false)
        {
            if (!hasActivePath || activePathIndex < 0 || activePathIndex >= activePathCells.Count)
                return;

            if (!force)
            {
                if (smoothingCooldownSeconds > 0f)
                    return;

                smoothingCooldownSeconds = pathSmoothingIntervalSeconds;
            }

            if (!TryGetGridCell(worldObject.pos3d_world, worldObject, out Vector2Int currentCellPos))
                return;

            if (!TryGetGridCellData(currentCellPos, out Cell currentCell))
                return;

            int roomIndex = currentCell.room_number;
            if (dir == null || dir.gen == null || dir.gen.rooms == null || roomIndex < 0 || roomIndex >= dir.gen.rooms.Count)
                return;

            Room room = dir.gen.rooms[roomIndex];
            if (room == null)
                return;

            int furthestVisibleIndex = activePathIndex;
            for (int i = activePathIndex; i < activePathCells.Count; i++)
            {
                Vector2Int candidatePos = activePathCells[i];
                if (!TryGetGridCellData(candidatePos, out Cell candidateCell))
                    break;

                if (candidateCell.room_number != roomIndex)
                    break;

                if (!RoomLOS.HasLineOfSight(room, currentCellPos, candidatePos))
                    break;

                furthestVisibleIndex = i;
            }

            if (furthestVisibleIndex > activePathIndex)
            {
                if (enablePathDebugLogging)
                    Debug.Log($"[AgentMovementModule] {worldObject.DisplayName} smoothed path index {activePathIndex} -> {furthestVisibleIndex}", this);

                activePathIndex = furthestVisibleIndex;
            }
        }

        private void UpdateTargetObjectAndMaybeRepath()
        {
            if (targetObject == null)
                return;

            targetLocation = targetObject.pos3d_world;

            if (!useGridPathfinding)
                return;

            if (!targetLocation.HasValue || !TryGetGridCell(targetLocation.Value, GetTargetCoordinateSpaceOwner(), out Vector2Int targetCell))
            {
                hasLastKnownTargetObjectCell = false;
                return;
            }

            if (!hasLastKnownTargetObjectCell)
            {
                lastKnownTargetObjectCell = targetCell;
                hasLastKnownTargetObjectCell = true;
                return;
            }

            if (targetCell == lastKnownTargetObjectCell)
                return;

            if (movingTargetRepathCooldownSeconds > 0f)
            {
                lastKnownTargetObjectCell = targetCell;
                return;
            }

            lastKnownTargetObjectCell = targetCell;
            movingTargetRepathCooldownSeconds = movingTargetRepathIntervalSeconds;

            if (enablePathDebugLogging)
                Debug.Log($"[AgentMovementModule] {worldObject.DisplayName} repathing for moving target cell={targetCell}", this);

            RebuildPathToCurrentTarget(forceRebuild: true);
        }

        private bool PointTowardWorldLocation(Vector3 targetLocation_world, float stopDistance = 0f)
        {
            maxDistance = 1f;

            Vector3 desired_move = targetLocation_world - worldObject.pos3d_world;
            desired_move.y = 0f;

            float distanceToTarget = desired_move.magnitude;
            if (distanceToTarget <= stopDistance)
            {
                maxDistance = 0f;
                desiredVelocity = Vector3.zero;
                return true;
            }

            float remainingDistance = Mathf.Max(0f, distanceToTarget - stopDistance);
            maxDistance = Mathf.Clamp(Mathf.Min(remainingDistance, 1f), 0f, 1f);
            SetDesiredMove(desired_move, maxDistance: maxDistance);
            return false;
        }

        // Called every tick when a target object is not null.  Finds target and heads to it.
        // (DecisionModule probably should check if we can still see it or still guess it's location)
        public void PointTowardTargetObjectLocation()
        {
            if (targetObject!=null) 
            {
                targetLocation = targetObject.pos3d_world;
            }

            if (targetLocation == null) 
                return;

            // determine if we should limit the distance travelled (because we are close)
            float stopDistanceFromTarget;
            if (targetObject) // if object, don't bump into it; or use pack's formationSpacing to determine follow distance.
            {
                if (keepFollowingTargetObject && worldObject.packMemberModule!=null && worldObject.packMemberModule.currentPack!=null)
                    stopDistanceFromTarget = worldObject.packMemberModule.currentPack.formationSpacing;
                else
                    stopDistanceFromTarget = stopDistanceFromObject;
            }
            else
            {
                stopDistanceFromTarget = 0f;
            }

            PointTowardWorldLocation(targetLocation.Value, stopDistanceFromTarget);
        }

        /// <summary>
        /// Called by decision modules to set a desired movement direction and speed.
        ///
        /// worldDirection01: world-space direction, will be normalized and Y set to 0.
        /// speedFactor: scale applied to walk/run speed. (USE CASE: for up/down slopes?)
        /// changeWalkMode: if not None, changes walkMode before moving.  Allows simple commands Run(direction) / Walk(direction instead of two separate actions.
        /// </summary>
        public void SetDesiredMove(Vector3 worldDirection01, float maxDistance = 1.0f, float speedFactor = 1.0f, WalkMode changeWalkMode = WalkMode.None)
        {
            worldDirection01.y = 0f;

            if (worldDirection01.sqrMagnitude > 1f)
                worldDirection01.Normalize();               // unit vector

            this.maxDistance = Mathf.Max(0f, maxDistance);
            speedFactor01 = Mathf.Max(0f, speedFactor);

            // If requested, change the walk mode
            if (changeWalkMode != WalkMode.None)
                worldObject.motionModule.SetWalkMode(changeWalkMode);

            // get the agent's current maximum movement speed based on WalkMode.
            // TODO: determine if we are backpedaling or strafing???
            float baseSpeed = worldObject.motionModule.GetMaxSpeedByCurrentWalkMode();

            float targetSpeed = baseSpeed * speedFactor01;  // scale by factor in this call's parameters

            desiredVelocity = worldDirection01 * targetSpeed;   // multiply direction unit vector by speed.
        }

        /// <summary>
        /// Directly sets a desired world-space velocity (horizontal only).
        /// Use this when AI/pathfinding already computed an exact velocity vector.
        /// </summary>
        public void SetDesiredVelocity(Vector3 worldVelocity)
        {
            worldVelocity.y = 0f;
            desiredVelocity = worldVelocity;
        }

        /// <summary>
        /// Clears desired velocity, causing the agent to decelerate to a stop.
        /// </summary>
        public void ClearDesiredMove()
        {
            desiredVelocity = Vector3.zero;
        }

        private int debugDoubleTick = -1;
        /// <summary>
        /// Called once per frame by the AgentModule/AgentDecision system.
        /// This is where we blend current velocity toward desiredVelocity and
        /// then ask MotionModule to actually move the character.
        /// </summary>
        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            if (enableDebugLogging && Time.frameCount % 30 == 0)
            {
                Debug.Log($"[{worldObject.DisplayName}] MoveTick: targetObj={(targetObject? targetObject.DisplayName : "null")} " +
                        $"targetPos={(targetLocation.HasValue ? targetLocation.Value.ToString() : "null")} " +
                        $"desiredVel={desiredVelocity} currentVel={currentVelocity} walkMode={walkMode}", this);
            }

            if (worldObject.motionModule == null)
                return;

            if (smoothingCooldownSeconds > 0f)
                smoothingCooldownSeconds = Mathf.Max(0f, smoothingCooldownSeconds - deltaTime);
            if (movingTargetRepathCooldownSeconds > 0f)
                movingTargetRepathCooldownSeconds = Mathf.Max(0f, movingTargetRepathCooldownSeconds - deltaTime);

            //Debug.Log($"{worldObject.DisplayName}:targetObject={targetObject},targetLocation={targetLocation}");
            if (targetObject != null || targetLocation != null)
            {
                if (recoveringToCellCenter)
                {
                    if (PointTowardWorldLocation(recoveryTargetWorld, centerRecoveryArrivalRadius))
                    {
                        recoveringToCellCenter = false;
                        consecutiveStallTicks = 0;
                        if (enableStallDebugLogging)
                            Debug.Log($"[AgentMovementModule] {worldObject.DisplayName} finished center-cell recovery.", this);
                    }
                }
                else
                {
                    UpdateTargetObjectAndMaybeRepath();

                    RebuildPathToCurrentTarget();
                    TrySmoothActivePath();

                    if (!FollowActivePath())
                        PointTowardTargetObjectLocation();
                }
            }
            else
            {
                recoveringToCellCenter = false;
                consecutiveStallTicks = 0;
            }

            // Decide which rate to use: acceleration vs deceleration
            float accel = accelerationMetersPerSecondSquared;
            if (desiredVelocity.sqrMagnitude < 0.0001f)
            {
                // Intending to stop; use deceleration
                accel = decelerationMetersPerSecondSquared;
            }

            // Smoothly move currentVelocity toward desiredVelocity
            currentVelocity = Vector3.MoveTowards(
                currentVelocity,
                desiredVelocity,
                accel * deltaTime);

            if (enableDebugLogging && Time.frameCount % 20 == 0)
            {
                Debug.Log(
                    $"[AgentMovementModule] " +
                    $"DesiredVel={desiredVelocity} CurrentVel={currentVelocity}",
                    this);
            }

            // Delegate to MotionModule for actual movement + rotation, clamp at maxDistance.
            worldObject.motionModule.ApplyMotion(currentVelocity, deltaTime, maxDistance);

            UpdateStallRecoveryState();

        }

        public void ClearDesiredMovement()
        {
            targetObject = null;
            targetLocation = null;
            desiredVelocity = Vector3.zero;
            ClearActivePath();
            recoveringToCellCenter = false;
            consecutiveStallTicks = 0;
        }

        public void SetDesiredTargetLocation(Vector3 targetLocation_world, WalkMode mode = WalkMode.Walk, bool requestPathfinding = true)
        {
            targetObject = null;
            targetLocation = targetLocation_world;
            walkMode = mode;
            recoveringToCellCenter = false;
            consecutiveStallTicks = 0;
            if (requestPathfinding)
                RebuildPathToCurrentTarget(forceRebuild: true);
            else
                ClearActivePath();
        }

        // function name is redundant to above function, but this one includes WalkMode.  Which is preferable to use?
        public void SetDesiredVelocity(Vector3 worldVelocity, WalkMode mode = WalkMode.Walk)
        {
            targetObject = null;
            targetLocation = null;
            walkMode = mode;

            // Optionally clamp here by mode max speed
            desiredVelocity = worldVelocity;
        }

        private void UpdateStallRecoveryState()
        {
            if (!enableStallRecovery || worldObject == null || worldObject.locationModule == null)
                return;

            bool hasMovementIntent =
                (targetObject != null || targetLocation != null) &&
                desiredVelocity.sqrMagnitude > 0.0001f &&
                maxDistance > 0.001f;

            Vector3 currentPos = worldObject.pos3d_world;
            Vector3 delta = currentPos - lastStallCheckPosition;
            delta.y = 0f;
            float epsilonSqr = stallPositionEpsilon * stallPositionEpsilon;

            if (!hasMovementIntent)
            {
                consecutiveStallTicks = 0;
                recoveringToCellCenter = false;
                lastStallCheckPosition = currentPos;
                return;
            }

            if (delta.sqrMagnitude <= epsilonSqr)
            {
                consecutiveStallTicks++;
            }
            else
            {
                if (consecutiveStallTicks>5) Debug.Log($"consecutiveStallTicks={consecutiveStallTicks} reset");
                consecutiveStallTicks = 0;
            }

            if (recoveringToCellCenter)
                return;

            if (consecutiveStallTicks < Mathf.Max(1, stallTicksBeforeRecover))
                return;

            Cell currentCell = worldObject.locationModule.cell;
            if (currentCell == null)
                return;

            recoveryTargetWorld = CellCenterWorld(currentCell.pos, currentCell.height);
            //recoveryTargetWorld += new Vector3(-0.5f, 0f, +0.5f);
            recoveringToCellCenter = true;
            consecutiveStallTicks = 0;

            if (enableStallDebugLogging)
            {
                Debug.Log(
                    $"[AgentMovementModule] {worldObject.DisplayName} stall detected; recovering to cell center {currentCell.pos} = {recoveryTargetWorld}.",
                    this);
            }
        }
    }
}
