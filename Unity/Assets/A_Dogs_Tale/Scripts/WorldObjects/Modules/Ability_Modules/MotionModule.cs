using UnityEngine;
using System;
using System.Collections.Generic;
using InspectorTools;

/*
MotionModule responsibilities:

Low-level physical movement & rotation
“Make the dog’s body go here with this velocity.”

Typical responsibilities:
	•	Apply movement (transform.position += vel * dt)
	•	Handle rotation towards move direction
	•	Apply gravity
	•	Handle stepping/slope clamp (if you implement it)
	•	Drive animator speed parameters (optional)
	•	Maybe handle root motion blending

MotionModule is the motor.
*/

namespace DogGame.Modules
{
    /// <summary>
    /// Lowest-level movement: applies velocity and rotation to the agent's body.
    /// This component should be the ONLY place that writes to position/rotation for the agent.
    /// 
    /// Higher-level code (AgentMovementModule, decision modules) should:
    ///   - Compute a desired map-space velocity
    ///   - Call ApplyMotionMap(desiredVelocity, deltaTime) every frame
    /// </summary>
    
    public enum MotionControlMode
    {
        DirectInput,   // WASD / stick, immediate control
        GoalDirected,  // Anything that produces a destination / path (click, pathfinding)
        Autopilot,     // takeover by motivations
    }

    public enum FacingMode
    {
        FaceMovementDirection,
        FaceTarget,
        Strafe,     // don't rotate, just move sideways.
        Manual   // e.g. animation or some other system controls rotation (or do not turn)
    }
        public enum WalkMode
    {
        None = 0,
        Walk,
        Run,
        Sneak,
        Cautious,
        Crawl,
        Backpedal,
        Strafe
        // Trot, Sprint, ...
    }

    [Serializable]
    public struct WalkModeSpeed
    {
        public WalkMode mode;
        public float maxSpeed;
    }

    [InspectorNote("Ability_Modules/Motion Module", "Convert desired velocity vector to motion, dealing with blockages, acceleration, speed.")]
    [DisallowMultipleComponent]
    public class MotionModule : WorldModule
    {
        [Header("Body Setup")]
        [Tooltip("Transform that represents the root of the dog body. If null, this.transform is used.")]
        [SerializeField] public Transform bodyRoot;

        //[Header("Rotation")]
        //[Tooltip("Rotate to face the horizontal movement direction.")]
        //[SerializeField] private bool faceMovementDirection = true;   // now handled by facingMode

        [Tooltip("Degrees per second to turn toward the movement direction.")]
        [SerializeField] private float rotationSpeedDegreesPerSecond = 360f;

        [Header("Gravity (optional)")]
        [Tooltip("If true, apply gravity to vertical motion.")]
        [SerializeField] private bool useGravity = false;

        [Tooltip("Gravity acceleration, in meters per second squared (negative is downward).")]
        [SerializeField] private float gravityMetersPerSecondSquared = -9.81f;

        [Tooltip("Clamp maximum downward speed (terminal velocity). Set to 0 to disable.")]
        [SerializeField] private float maxFallSpeedMetersPerSecond = 50f;

        [Header("Wall Clearance")]
        [SerializeField] private bool constrainToCellWalls = true;
        [SerializeField] private int wallConstraintIterations = 4;
        [SerializeField] private bool recoverFromWallIntersections = true;
        [SerializeField] private float wallRecoveryClearance = 0.30f;
        [SerializeField] private int invalidCellRecoverySearchRadius = 12;

        public bool ConstrainToCellWalls
        {
            get => constrainToCellWalls;
            set => constrainToCellWalls = value;
        }

        // Internal vertical velocity (for gravity, jumps, etc.)
        private Vector3 verticalVelocity = Vector3.zero;

        [Header("Control player facing")]
        [Tooltip("Assigned based on type of travel (is there a destination or are we just moving?)")]
        public MotionControlMode motionControlMode;
        [Tooltip("Assigned by DecisionModule based on desire to strafe or walk backwards")]
        public bool isBackpedaling;

        [Tooltip("Assigned based on current driver")]
        public FacingMode facingMode;
        [Tooltip("Used if facingMode = FaceTarget")]
        public Transform facingTarget;

        //public bool isStrafing;     // MOVED TO PLAYERAGENT MODE temporarily disables rotation for FaceMovementDirection.

        public float maxHorizontalAcceleration = 40f;

        public WalkMode currentWalkMode = WalkMode.Walk;

        private Vector3 horizontalVelocity = Vector3.zero;
        public Vector3 HorizontalVelocity => horizontalVelocity;
        public float HorizontalSpeed => new Vector2(horizontalVelocity.x, horizontalVelocity.z).magnitude;
        
        [SerializeField] private List<WalkModeSpeed> maxSpeedsByMode = new()
        {
            new WalkModeSpeed { mode = WalkMode.None,      maxSpeed = 0f },
            new WalkModeSpeed { mode = WalkMode.Walk,      maxSpeed = 2.0f },
            new WalkModeSpeed { mode = WalkMode.Run,       maxSpeed = 4.5f },
            new WalkModeSpeed { mode = WalkMode.Sneak,     maxSpeed = 1.3f },
            new WalkModeSpeed { mode = WalkMode.Cautious,  maxSpeed = 1.0f },
            new WalkModeSpeed { mode = WalkMode.Crawl,     maxSpeed = 0.8f },
            new WalkModeSpeed { mode = WalkMode.Backpedal, maxSpeed = 1.6f },
            new WalkModeSpeed { mode = WalkMode.Strafe,    maxSpeed = 1.8f }
        };

        public float GetMaxSpeedByCurrentWalkMode()
        {
            return GetMaxSpeedByWalkMode(currentWalkMode);
        }

        public float GetMaxSpeedByWalkMode(WalkMode walkMode)
        {
            for (int i = 0; i < maxSpeedsByMode.Count; i++)
            {
                if (maxSpeedsByMode[i].mode == walkMode)
                    return maxSpeedsByMode[i].maxSpeed;
            }

            return maxSpeedsByMode[(int)WalkMode.Walk].maxSpeed;
        }

        public void SetWalkMode(WalkMode newWalkMode)
        {
            currentWalkMode = newWalkMode;
        }

        protected override void Awake()
        {
            if (worldObject == null)
            {
                //worldObject = GetComponent<WorldObject>();
                if (worldObject == null)
                {
                    Debug.LogError($"MotionModule Awake: worldObject not found");
                }
            }
            if (bodyRoot == null)
            {
                bodyRoot = transform;
            }
        }

        #region Tick

        public override void Tick(float deltaTime)
        {
            Debug.LogWarning($"MotionModule {worldObject.DisplayName}: Tick {deltaTime} DOES NOTHING");
        
            // Tick Does NOTHING: everything is action calls.
        }

        #endregion

        /// <summary>
        /// Main entry point: apply movement for this frame.
        /// 
        /// Call this once per frame from a higher level module (e.g., AgentMovementModule),
        /// passing in the desired horizontal world-space velocity.
        /// </summary>
        /// <param name="desiredHorizontalVelocity">
        /// World-space velocity that the agent should move with on this frame.
        /// Y component is ignored here; vertical movement is handled by gravity / verticalVelocity.
        /// </param>
        /// <param name="deltaTime">Time step (usually Time.deltaTime).</param>


        /// <summary>
        /// Clear any motion-related cached state (e.g., gravity / vertical speed).
        /// Call this after teleporting or hard-resetting the agent.
        /// 
        /// Since we only manage vertical velocity in this module,
        /// ResetMotionState() is the same as ResetVerticalVelocity()
        /// </summary>
        public void ResetMotionState()
        {
            // Stop any vertical movement (no more falling from previous position)
            verticalVelocity = Vector3.zero;
            horizontalVelocity = Vector3.zero;

            // We do NOT change position or rotation here; Teleport already did that.
        }

        public void StopHorizontalMotion()
        {
            horizontalVelocity = Vector3.zero;
        }

        #region Teleport

        // ===== Teleport family of commands =====

        /// <summary>
        /// Convenience: instantly teleport the body to a new position without any velocity.
        /// Useful for respawns, teleports, etc.
        /// </summary>
        public void Teleport(Vector3? worldPosition, bool resetMotion = true)
        {
            if (bodyRoot == null || worldPosition==null)
                return;
            Debug.Log($"{worldObject.DisplayName} Teleport from {bodyRoot.position} to {worldPosition}");
            bodyRoot.position = (Vector3)worldPosition;
            if (resetMotion)
            {
                ResetMotionState();
                worldObject.agentMovementModule?.ClearDesiredMove();
            }
        }

        // teleport with full control of rotation and angle.
        public void TeleportWithRotate(Vector3 worldPosition, Quaternion worldRotation, bool resetMotion = true)
        {
            transform.SetPositionAndRotation(worldPosition, worldRotation);

            if (resetMotion)
            {
                ResetMotionState();
                worldObject.agentMovementModule?.ClearDesiredMove();
            }
        }

        // only uses rotation around vertical axis.
        public void TeleportUpright(Vector3 position, Quaternion rotation, bool resetMotion = true)
        {
            rotation = Quaternion.FromToRotation(rotation * Vector3.up, Vector3.up) * rotation;
            TeleportWithRotate(position, rotation, resetMotion);
        }

        // if ground is tilted, we might want to do this...
        public void TeleportAlignToGround(Vector3 position, Vector3 groundNormal, float extraYaw = 0f, bool resetMotion = true)
        {
            // Create rotation aligned to surface normal
            Quaternion align = Quaternion.FromToRotation(Vector3.up, groundNormal);

            // Add optional yaw (turning left/right relative to ground plane)
            Quaternion finalRotation = Quaternion.Euler(0, extraYaw, 0) * align;

            TeleportWithRotate(position, finalRotation, resetMotion);
        }

        #endregion
        #region ApplyMotion

        private int debugDoubleTick = -1;
        public void ApplyMotionMap(Vector3 desiredMapVelocity, float deltaTime, float maxDistanceMap, float maxSpeedMultiplier = 1.0f)
        {
            if (worldObject == null || deltaTime <= 0f)
            {
                ApplyMotion(desiredMapVelocity, deltaTime, maxDistanceMap, maxSpeedMultiplier);
                return;
            }

            Vector3 currentMapPosition = worldObject.WorldToMapPosition(bodyRoot != null ? bodyRoot.position : transform.position);
            Vector3 targetMapPosition = currentMapPosition + (desiredMapVelocity * deltaTime);
            Vector3 currentWorldPosition = bodyRoot != null ? bodyRoot.position : transform.position;
            Vector3 targetWorldPosition = worldObject.MapToWorldPosition(targetMapPosition);
            Vector3 desiredWorldVelocity = (targetWorldPosition - currentWorldPosition) / deltaTime;

            // The current map<->world transform is translational, so the scalar stop radius carries over directly.
            ApplyMotion(desiredWorldVelocity, deltaTime, maxDistanceMap, maxSpeedMultiplier);
        }

        public void ApplyMotion(Vector3 desiredHorizontalVelocity, float deltaTime, float maxDistance, float maxSpeedMultiplier = 1.0f)
        {
            //Debug.Log($"{worldObject.DisplayName}:MotionModule.ApplyMotion({desiredHorizontalVelocity}, {deltaTime}, {maxDistance})");
            
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Move run more than once per frame");
            debugDoubleTick = Time.frameCount;

            if (bodyRoot == null)
                return;

            // --- 0. Enforce horizontal-only for input ---
            desiredHorizontalVelocity.y = 0f;

            // Clamp desired speed if you like
            float maxHorizontalSpeed = GetMaxSpeedByCurrentWalkMode() * Mathf.Max(0f, maxSpeedMultiplier);
            if (maxHorizontalSpeed > 0f)
            {
                float desiredSpeed = desiredHorizontalVelocity.magnitude;
                if (desiredSpeed > maxHorizontalSpeed)
                {
                    desiredHorizontalVelocity = desiredHorizontalVelocity.normalized * maxHorizontalSpeed;
                }
            }

            // --- 1. Update horizontal velocity via acceleration ---
            horizontalVelocity = ComputeHorizontalVelocity(horizontalVelocity,
                                                        desiredHorizontalVelocity,
                                                        maxHorizontalAcceleration,
                                                        deltaTime);

            // --- 2. Apply rotation based on facing mode ---
            IntegrateHorizontalRotation(horizontalVelocity, deltaTime);

            // --- 3. Update vertical velocity (gravity) ---
            IntegrateVerticalVelocity(deltaTime);

            // --- 4. Determine combined velocity ---
            Vector3 frameVelocity = horizontalVelocity + verticalVelocity;

            // --- 5. Propose frame delta (already maxDistance-clamped)
            Vector3 proposedDelta = Vector3.ClampMagnitude(frameVelocity * deltaTime, maxDistance);
            Vector3 proposedPosition = bodyRoot.position + proposedDelta;

            // --- 6. Apply world-space constraints before committing the move
            Vector3 startPosition = bodyRoot.position;
            Vector3 constrainedPosition = ResolveConstrainedWorldPosition(startPosition, proposedPosition, applyLeashConstraints: true);
            constrainedPosition = PreventConstraintRebound(startPosition, proposedPosition, constrainedPosition);
            constrainedPosition = AdjustAgentHeightToFloor(constrainedPosition);
            constrainedPosition = RecoverFromInvalidWallSpace(constrainedPosition);

            // --- 7. Commit
            Vector3 actualDelta = constrainedPosition - startPosition;
            bodyRoot.position = constrainedPosition;

            // --- 8. Optional: make horizontalVelocity reflect the actual move (reduces later jitter)
            if (deltaTime > 0f)
            {
                Vector3 actualFrameVelocity = actualDelta / deltaTime;

                // Keep your gravity model intact; only adjust horizontal.
                horizontalVelocity = new Vector3(actualFrameVelocity.x, 0f, actualFrameVelocity.z);
            }

            //Debug.Log($"{worldObject.DisplayName}:MotionModule.ApplyMotion complete");
        }

        public Vector3 ResolveConstrainedWorldPosition(Vector3 fromWorld, Vector3 desiredWorldPosition, bool applyLeashConstraints = true)
        {
            Vector3 constrainedPosition = desiredWorldPosition;

            if (constrainToCellWalls)
            {
                constrainedPosition = ConstrainPositionToWalls(fromWorld, constrainedPosition);
            }

            if (applyLeashConstraints && dir.leashSystem != null)
            {
                constrainedPosition = dir.leashSystem.ConstrainDesiredPosition(worldObject, constrainedPosition);
            }

            return constrainedPosition;
        }

        /// <summary>
        /// Tests a map-space segment against the same radius-aware wall and
        /// diagonal-wall constraints used by actual agent motion.
        /// </summary>
        public bool IsMapSegmentClear(
            Vector3 fromMap,
            Vector3 toMap,
            bool allowDoors = true,
            float sampleDistance = 0.20f)
        {
            if (!constrainToCellWalls || worldObject == null || dir == null || dir.gen == null || !dir.gen.buildComplete)
                return true;

            Vector3 delta = toMap - fromMap;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
                return true;

            Vector2 segmentStart = new(fromMap.x, fromMap.z);
            Vector2 segmentEnd = new(toMap.x, toMap.z);
            if (!HasSweptRadiusClearance(segmentStart, segmentEnd, GetWallClearanceRadius(), allowDoors))
                return false;

            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.05f, sampleDistance)));
            Vector3 previousMap = fromMap;
            const float toleranceSqr = 0.0025f * 0.0025f;

            for (int step = 1; step <= steps; step++)
            {
                Vector3 desiredMap = Vector3.Lerp(fromMap, toMap, step / (float)steps);
                Vector3 fromWorld = worldObject.MapToWorldPosition(previousMap);
                Vector3 desiredWorld = worldObject.MapToWorldPosition(desiredMap);
                Vector3 constrainedWorld = ResolveConstrainedWorldPosition(
                    fromWorld,
                    desiredWorld,
                    applyLeashConstraints: false);
                Vector3 constrainedMap = worldObject.WorldToMapPosition(constrainedWorld);

                Vector2 error = new(constrainedMap.x - desiredMap.x, constrainedMap.z - desiredMap.z);
                if (error.sqrMagnitude > toleranceSqr)
                    return false;

                previousMap = desiredMap;
            }

            return true;
        }

        private bool HasSweptRadiusClearance(Vector2 start, Vector2 end, float radius, bool allowDoors)
        {
            float clearance = Mathf.Max(0.001f, radius);
            int minX = Mathf.FloorToInt(Mathf.Min(start.x, end.x) - clearance) - 1;
            int maxX = Mathf.FloorToInt(Mathf.Max(start.x, end.x) + clearance) + 1;
            int minY = Mathf.FloorToInt(Mathf.Min(start.y, end.y) - clearance) - 1;
            int maxY = Mathf.FloorToInt(Mathf.Max(start.y, end.y) + clearance) + 1;
            float clearanceSqr = clearance * clearance;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!TryGetCell(x, y, out Cell cell))
                        continue;

                    if (IsBlockingEdge(cell, x, y, DirFlags.W, allowDoors) &&
                        SegmentDistanceSqr(start, end, new Vector2(x, y), new Vector2(x, y + 1f)) < clearanceSqr)
                        return false;
                    if (IsBlockingEdge(cell, x, y, DirFlags.S, allowDoors) &&
                        SegmentDistanceSqr(start, end, new Vector2(x, y), new Vector2(x + 1f, y)) < clearanceSqr)
                        return false;

                    // East and north are needed only at the outer edge/void; shared
                    // interior edges are covered once by the neighbor's west/south.
                    if (!TryGetCell(x + 1, y, out _) &&
                        SegmentDistanceSqr(start, end, new Vector2(x + 1f, y), new Vector2(x + 1f, y + 1f)) < clearanceSqr)
                        return false;
                    if (!TryGetCell(x, y + 1, out _) &&
                        SegmentDistanceSqr(start, end, new Vector2(x, y + 1f), new Vector2(x + 1f, y + 1f)) < clearanceSqr)
                        return false;

                    DiagonalOpenDirection diagonal = GetDiagonalOpenDirection(cell.walls, cell.doors);
                    if (diagonal != DiagonalOpenDirection.None)
                    {
                        Vector2 diagonalA;
                        Vector2 diagonalB;
                        if (diagonal == DiagonalOpenDirection.NE || diagonal == DiagonalOpenDirection.SW)
                        {
                            diagonalA = new Vector2(x, y + 1f);
                            diagonalB = new Vector2(x + 1f, y);
                        }
                        else
                        {
                            diagonalA = new Vector2(x, y);
                            diagonalB = new Vector2(x + 1f, y + 1f);
                        }

                        if (SegmentDistanceSqr(start, end, diagonalA, diagonalB) < clearanceSqr)
                            return false;
                    }
                }
            }

            return true;
        }

        private bool IsBlockingEdge(Cell cell, int x, int y, DirFlags edge, bool allowDoors)
        {
            Vector2Int offset = edge.ToVector2Int();
            TryGetCell(x + offset.x, y + offset.y, out Cell neighbor);
            if (neighbor == null)
                return true;

            DirFlags opposite = edge.Opposite();
            bool hasDoor = (cell.doors & edge) != 0 || (neighbor.doors & opposite) != 0;
            if (allowDoors && hasDoor)
                return false;

            return (cell.walls & edge) != 0 ||
                (neighbor.walls & opposite) != 0 ||
                (!allowDoors && hasDoor);
        }

        private static float SegmentDistanceSqr(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
        {
            if (SegmentsIntersect(a0, a1, b0, b1))
                return 0f;

            return Mathf.Min(
                Mathf.Min(PointSegmentDistanceSqr(a0, b0, b1), PointSegmentDistanceSqr(a1, b0, b1)),
                Mathf.Min(PointSegmentDistanceSqr(b0, a0, a1), PointSegmentDistanceSqr(b1, a0, a1)));
        }

        private static float PointSegmentDistanceSqr(Vector2 point, Vector2 segmentA, Vector2 segmentB)
        {
            Vector2 segment = segmentB - segmentA;
            float lengthSqr = segment.sqrMagnitude;
            if (lengthSqr <= 1e-8f)
                return (point - segmentA).sqrMagnitude;

            float t = Mathf.Clamp01(Vector2.Dot(point - segmentA, segment) / lengthSqr);
            return (point - (segmentA + segment * t)).sqrMagnitude;
        }

        private static bool SegmentsIntersect(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
        {
            float d1 = Cross(a1 - a0, b0 - a0);
            float d2 = Cross(a1 - a0, b1 - a0);
            float d3 = Cross(b1 - b0, a0 - b0);
            float d4 = Cross(b1 - b0, a1 - b0);
            return ((d1 <= 0f && d2 >= 0f) || (d1 >= 0f && d2 <= 0f)) &&
                   ((d3 <= 0f && d4 >= 0f) || (d3 >= 0f && d4 <= 0f));
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        public Vector3 ApplyExternalWorldDisplacement(Vector3 desiredWorldDelta, float deltaTime, bool applyLeashConstraints = true)
        {
            if (bodyRoot == null || desiredWorldDelta.sqrMagnitude <= 0f)
                return Vector3.zero;

            Vector3 startPosition = bodyRoot.position;
            Vector3 constrainedPosition = ResolveConstrainedWorldPosition(
                startPosition,
                startPosition + desiredWorldDelta,
                applyLeashConstraints);
            constrainedPosition = PreventConstraintRebound(startPosition, startPosition + desiredWorldDelta, constrainedPosition);
            constrainedPosition = AdjustAgentHeightToFloor(constrainedPosition);
            constrainedPosition = RecoverFromInvalidWallSpace(constrainedPosition);

            Vector3 actualDelta = constrainedPosition - startPosition;
            bodyRoot.position = constrainedPosition;

            if (deltaTime > 0f)
            {
                Vector3 actualFrameVelocity = actualDelta / deltaTime;
                horizontalVelocity = new Vector3(actualFrameVelocity.x, 0f, actualFrameVelocity.z);
            }

            return actualDelta;
        }

        private Vector3 AdjustAgentHeightToFloor(Vector3 worldPosition)
        {
            if (!IsAgentMotionOwner() || worldObject == null || dir == null || dir.gen == null || !dir.gen.buildComplete)
                return worldPosition;

            Vector3 mapPosition = worldObject.WorldToMapPosition(worldPosition);
            if (!dir.gen.TrySampleFloorAtMapPosition(mapPosition, threshold: 50, out float floorMapY, out _, out _))
                return worldPosition;

            mapPosition.y = floorMapY;
            return worldObject.MapToWorldPosition(mapPosition);
        }

        private bool IsAgentMotionOwner()
        {
            return worldObject != null &&
                   (worldObject.Kind == WorldObjectKind.Agent ||
                    worldObject.agentModule != null ||
                    worldObject.GetComponent<AgentModule>() != null);
        }

        private static Vector3 PreventConstraintRebound(Vector3 fromWorld, Vector3 proposedWorld, Vector3 constrainedWorld)
        {
            Vector3 desiredDelta = proposedWorld - fromWorld;
            desiredDelta.y = 0f;

            Vector3 actualDelta = constrainedWorld - fromWorld;
            float constrainedY = constrainedWorld.y;
            actualDelta.y = 0f;

            if (desiredDelta.sqrMagnitude < 1e-10f || actualDelta.sqrMagnitude < 1e-10f)
                return constrainedWorld;

            float desiredDistance = desiredDelta.magnitude;
            float actualDistance = actualDelta.magnitude;

            if (Vector3.Dot(actualDelta, desiredDelta) <= 0f)
            {
                constrainedWorld.x = fromWorld.x;
                constrainedWorld.z = fromWorld.z;
                constrainedWorld.y = constrainedY;
                return constrainedWorld;
            }

            if (actualDistance > desiredDistance)
            {
                Vector3 cappedDelta = actualDelta.normalized * desiredDistance;
                constrainedWorld.x = fromWorld.x + cappedDelta.x;
                constrainedWorld.z = fromWorld.z + cappedDelta.z;
                constrainedWorld.y = constrainedY;
            }

            return constrainedWorld;
        }

        #endregion
        #region WallCollisions

        private Vector3 ConstrainPositionToWalls(Vector3 fromWorld, Vector3 toWorld)
        {
            if (dir == null || dir.gen == null || dir.gen.cfg == null || !dir.gen.buildComplete || dir.gen.cellGrid == null)
                return toWorld;

            float clearance = GetWallClearanceRadius();
            if (clearance <= 0f)
                return toWorld;

            Vector3 fromMap3 = worldObject != null ? worldObject.WorldToMapPosition(fromWorld) : fromWorld;
            Vector3 toMap3 = worldObject != null ? worldObject.WorldToMapPosition(toWorld) : toWorld;
            Vector2 from = new Vector2(fromMap3.x, fromMap3.z);
            Vector2 to = new Vector2(toMap3.x, toMap3.z);
            Vector2 resolved = ResolveGridConstraints(from, to, clearance, Mathf.Max(1, wallConstraintIterations));

            Vector3 resolvedMap = new Vector3(resolved.x, toMap3.y, resolved.y);
            return worldObject != null ? worldObject.MapToWorldPosition(resolvedMap) : resolvedMap;
        }

        private float GetWallClearanceRadius()
        {
            if (worldObject != null)
                return Mathf.Max(0f, worldObject.sizeRadius);

            return 0.30f;
        }

        private Vector3 RecoverFromInvalidWallSpace(Vector3 worldPosition)
        {
            if (!recoverFromWallIntersections ||
                !constrainToCellWalls ||
                !IsAgentMotionOwner() ||
                worldObject == null ||
                dir == null ||
                dir.gen == null ||
                dir.gen.cfg == null ||
                !dir.gen.buildComplete ||
                dir.gen.cellGrid == null)
            {
                return worldPosition;
            }

            float clearance = Mathf.Max(GetWallClearanceRadius(), wallRecoveryClearance);
            if (clearance <= 0f)
                return worldPosition;

            Vector3 mapPosition = worldObject.WorldToMapPosition(worldPosition);
            Vector2 map2 = new(mapPosition.x, mapPosition.z);

            int cellX = Mathf.FloorToInt(map2.x);
            int cellY = Mathf.FloorToInt(map2.y);

            if (TryFindNearestLegalInRoomPoint(map2, cellX, cellY, clearance, out Vector2 recoveredPoint))
            {
                map2 = recoveredPoint;
            }
            else if (!TryGetValidAgentCell(cellX, cellY, out Cell cell))
            {
                if (!TryFindNearestValidCell(cellX, cellY, invalidCellRecoverySearchRadius, out cell))
                    return worldPosition;

                map2 = GetCellInteriorCenter(cell);
            }

            Vector3 recoveredMap = new(map2.x, mapPosition.y, map2.y);
            return AdjustAgentHeightToFloor(worldObject.MapToWorldPosition(recoveredMap));
        }

        private bool TryGetValidAgentCell(int x, int y, out Cell cell)
        {
            cell = null;

            if (!TryGetCell(x, y, out Cell candidate))
                return false;

            if (candidate.room_number < 0 ||
                dir == null ||
                dir.gen == null ||
                dir.gen.rooms == null ||
                candidate.room_number >= dir.gen.rooms.Count)
            {
                return false;
            }

            cell = candidate;
            return true;
        }

        private bool TryFindNearestValidCell(int originX, int originY, int maxRadius, out Cell cell)
        {
            cell = null;

            maxRadius = Mathf.Max(0, maxRadius);
            for (int radius = 0; radius <= maxRadius; radius++)
            {
                for (int y = originY - radius; y <= originY + radius; y++)
                {
                    for (int x = originX - radius; x <= originX + radius; x++)
                    {
                        if (radius > 0 &&
                            x > originX - radius &&
                            x < originX + radius &&
                            y > originY - radius &&
                            y < originY + radius)
                        {
                            continue;
                        }

                        if (TryGetValidAgentCell(x, y, out cell))
                            return true;
                    }
                }
            }

            return false;
        }

        private bool TryFindNearestLegalInRoomPoint(
            Vector2 mapPosition,
            int originX,
            int originY,
            float clearance,
            out Vector2 legalPoint)
        {
            legalPoint = default;
            float bestDistanceSqr = float.PositiveInfinity;
            bool found = false;

            int radius = Mathf.Max(1, Mathf.CeilToInt(clearance) + 1);
            for (int y = originY - radius; y <= originY + radius; y++)
            {
                for (int x = originX - radius; x <= originX + radius; x++)
                {
                    if (!TryGetValidAgentCell(x, y, out Cell cell))
                        continue;

                    Vector2 candidate = ClosestLegalPointInCell(mapPosition, cell, clearance);
                    float distanceSqr = (candidate - mapPosition).sqrMagnitude;
                    if (distanceSqr >= bestDistanceSqr)
                        continue;

                    bestDistanceSqr = distanceSqr;
                    legalPoint = candidate;
                    found = true;
                }
            }

            return found;
        }

        private Vector2 ClosestLegalPointInCell(Vector2 mapPosition, Cell cell, float clearance)
        {
            float minX = cell.pos.x + 0.001f;
            float maxX = cell.pos.x + 0.999f;
            float minY = cell.pos.y + 0.001f;
            float maxY = cell.pos.y + 0.999f;

            if (EdgeBlocked(GetCellInteriorCenter(cell), cell.pos.x, cell.pos.y, DirFlags.W, clearance))
                minX = cell.pos.x + clearance;

            if (EdgeBlocked(GetCellInteriorCenter(cell), cell.pos.x, cell.pos.y, DirFlags.E, clearance))
                maxX = cell.pos.x + 1f - clearance;

            if (EdgeBlocked(GetCellInteriorCenter(cell), cell.pos.x, cell.pos.y, DirFlags.S, clearance))
                minY = cell.pos.y + clearance;

            if (EdgeBlocked(GetCellInteriorCenter(cell), cell.pos.x, cell.pos.y, DirFlags.N, clearance))
                maxY = cell.pos.y + 1f - clearance;

            Vector2 candidate = new(
                Mathf.Clamp(mapPosition.x, minX, maxX),
                Mathf.Clamp(mapPosition.y, minY, maxY));

            candidate = PushOutOfDiagonalWallSpace(candidate, cell, clearance);
            return candidate;
        }

        private static Vector2 GetCellInteriorCenter(Cell cell)
        {
            return new Vector2(cell.pos.x + 0.5f, cell.pos.y + 0.5f);
        }

        private Vector2 PushOutOfBlockedWallClearance(Vector2 mapPosition, Cell cell, float clearance)
        {
            int x = cell.pos.x;
            int y = cell.pos.y;

            if (EdgeBlocked(mapPosition, x, y, DirFlags.W, clearance))
                mapPosition.x = Mathf.Max(mapPosition.x, x + clearance);

            if (EdgeBlocked(mapPosition, x, y, DirFlags.E, clearance))
                mapPosition.x = Mathf.Min(mapPosition.x, x + 1f - clearance);

            if (EdgeBlocked(mapPosition, x, y, DirFlags.S, clearance))
                mapPosition.y = Mathf.Max(mapPosition.y, y + clearance);

            if (EdgeBlocked(mapPosition, x, y, DirFlags.N, clearance))
                mapPosition.y = Mathf.Min(mapPosition.y, y + 1f - clearance);

            return mapPosition;
        }

        private Vector2 PushOutOfDiagonalWallSpace(Vector2 mapPosition, Cell cell, float clearance)
        {
            DiagonalOpenDirection diag = GetDiagonalOpenDirection(cell.walls, cell.doors);
            if (diag == DiagonalOpenDirection.None)
                return mapPosition;

            Vector2 local = new(mapPosition.x - cell.pos.x, mapPosition.y - cell.pos.y);
            float offset = clearance * 1.41421356f;

            switch (diag)
            {
                case DiagonalOpenDirection.NE:
                    if (local.x + local.y > 1f - offset)
                        ProjectToLine(ref local, 1f, 1f, 1f - offset);
                    break;

                case DiagonalOpenDirection.SW:
                    if (local.x + local.y < offset)
                        ProjectToLine(ref local, 1f, 1f, offset);
                    break;

                case DiagonalOpenDirection.SE:
                    if (local.x - local.y > offset)
                        ProjectToLine(ref local, 1f, -1f, offset);
                    break;

                case DiagonalOpenDirection.NW:
                    if (local.x - local.y < -(1f - offset))
                        ProjectToLine(ref local, 1f, -1f, -(1f - offset));
                    break;
            }

            local.x = Mathf.Clamp(local.x, 0.001f, 0.999f);
            local.y = Mathf.Clamp(local.y, 0.001f, 0.999f);
            return new Vector2(cell.pos.x + local.x, cell.pos.y + local.y);
        }

        private static void ProjectToLine(ref Vector2 point, float a, float b, float c)
        {
            float denom = (a * a) + (b * b);
            if (denom <= 1e-6f)
                return;

            float distance = ((a * point.x) + (b * point.y) - c) / denom;
            point.x -= a * distance;
            point.y -= b * distance;
        }

        private Vector2 ResolveGridConstraints(Vector2 from, Vector2 to, float radius, int maxIters)
        {
            Cleanup(ref from);
            Cleanup(ref to);

            int width = dir.gen.cfg.mapWidth;
            int height = dir.gen.cfg.mapHeight;
            Vector2 final = from;

            float xmin = radius;
            float xmax = width - radius;
            float ymin = radius;
            float ymax = height - radius;
            to.x = Mathf.Clamp(to.x, xmin, xmax);
            to.y = Mathf.Clamp(to.y, ymin, ymax);

            if ((from - to).sqrMagnitude < 1e-10f)
                return to;

            for (int iter = 0; iter < maxIters; iter++)
            {
                int cellX = Mathf.FloorToInt(from.x);
                int cellY = Mathf.FloorToInt(from.y);

                if (!TryGetCell(cellX, cellY, out Cell cell))
                    return final;

                float cxmin = Mathf.Max(cellX - 1f + radius, xmin);
                float cxmax = Mathf.Min(cellX + 2f - radius, xmax);
                float cymin = Mathf.Max(cellY - 1f + radius, ymin);
                float cymax = Mathf.Min(cellY + 2f - radius, ymax);

                if (EdgeBlocked(from, cellX, cellY, DirFlags.E, radius)) cxmax = Mathf.Min(cxmax, cellX + 1f - radius);
                if (EdgeBlocked(from, cellX, cellY, DirFlags.W, radius)) cxmin = Mathf.Max(cxmin, cellX + 0f + radius);
                if (EdgeBlocked(from, cellX, cellY, DirFlags.N, radius)) cymax = Mathf.Min(cymax, cellY + 1f - radius);
                if (EdgeBlocked(from, cellX, cellY, DirFlags.S, radius)) cymin = Mathf.Max(cymin, cellY + 0f + radius);

                Vector2 tempTarget = new Vector2(
                    Mathf.Clamp(to.x, cxmin, cxmax),
                    Mathf.Clamp(to.y, cymin, cymax)
                );

                Vector2 onTheWay = tempTarget;
                Vector2 moveDir = to - from;
                float tempDistance = (tempTarget - from).magnitude;

                if (moveDir.sqrMagnitude > 1e-8f)
                {
                    moveDir.Normalize();
                    Vector2 startLocal = new Vector2(from.x - cellX, from.y - cellY);

                    if (TryDistanceToDiagonalWall(cell, startLocal, moveDir, 1f, radius, out float diagonalDist) &&
                        diagonalDist >= 0f &&
                        diagonalDist <= tempDistance)
                    {
                        Vector2 hit = from + moveDir * diagonalDist;
                        Vector2 remaining = tempTarget - hit;
                        GetDiagonalTangentAndNormal(cell, out Vector2 tangent, out Vector2 normal);

                        float slideLength = Vector2.Dot(remaining, tangent);
                        Vector2 slideTarget = hit + tangent * slideLength;
                        slideTarget = new Vector2(
                            Mathf.Clamp(slideTarget.x, cxmin, cxmax),
                            Mathf.Clamp(slideTarget.y, cymin, cymax)
                        );

                        const float epsilon = 1e-4f;
                        slideTarget += (-normal) * epsilon;
                        onTheWay = slideTarget;
                    }
                }

                final = onTheWay;

                if ((onTheWay - to).sqrMagnitude < 1e-10f)
                    break;

                if ((onTheWay - from).sqrMagnitude < 1e-10f)
                    break;

                from = onTheWay;
            }

            Cleanup(ref final);
            return final;
        }

        private bool TryGetCell(int x, int y, out Cell cell)
        {
            cell = null;

            if (dir == null || dir.gen == null || dir.gen.cellGrid == null || !dir.gen.In(x, y))
                return false;

            cell = dir.gen.cellGrid[x, y];
            return cell != null;
        }

        private bool EdgeBlocked(Vector2 currentPosition, int x, int y, DirFlags dirFlag, float radius)
        {
            if (!TryGetCell(x, y, out Cell cell))
                return true;

            Vector2Int step = dirFlag.ToVector2Int();
            TryGetCell(x + step.x, y + step.y, out Cell neighborCell);
            if (neighborCell == null)
                return true;

            DirFlags opposite = dirFlag.Opposite();

            bool sharedDoor =
                (cell.doors & dirFlag) != 0 ||
                (neighborCell != null && (neighborCell.doors & opposite) != 0);

            if (sharedDoor)
                return false;

            bool hasWall =
                (cell.walls & dirFlag) != 0 ||
                (neighborCell != null && (neighborCell.walls & opposite) != 0);

            bool blockedByEndWall = (EndOfWallBlockers(currentPosition, x, y, radius) & dirFlag) != 0;
            return hasWall || blockedByEndWall;
        }

        private DirFlags EndOfWallBlockers(Vector2 worldXZ, int x, int y, float radius)
        {
            Cleanup(ref worldXZ);

            float localX = worldXZ.x % 1f;
            float localY = worldXZ.y % 1f;
            float oneMinusRadius = 1f - radius;
            CleanupFloat(ref localX);
            CleanupFloat(ref localY);

            bool southEdge = localY < radius;
            bool northEdge = localY > oneMinusRadius;
            bool westEdge = localX < radius;
            bool eastEdge = localX > oneMinusRadius;

            bool southEndWall = false;
            bool northEndWall = false;
            bool westEndWall = false;
            bool eastEndWall = false;

            Cell southCell = GetNeighborOrEmpty(x, y - 1);
            Cell northCell = GetNeighborOrEmpty(x, y + 1);
            Cell westCell = GetNeighborOrEmpty(x - 1, y);
            Cell eastCell = GetNeighborOrEmpty(x + 1, y);

            if (eastEdge)
            {
                southEndWall = (southCell.walls & DirFlags.E) != 0;
                northEndWall = (northCell.walls & DirFlags.E) != 0;
            }
            if (westEdge)
            {
                southEndWall = (southCell.walls & DirFlags.W) != 0;
                northEndWall = (northCell.walls & DirFlags.W) != 0;
            }
            if (northEdge)
            {
                westEndWall = (westCell.walls & DirFlags.N) != 0;
                eastEndWall = (eastCell.walls & DirFlags.N) != 0;
            }
            if (southEdge)
            {
                westEndWall = (westCell.walls & DirFlags.S) != 0;
                eastEndWall = (eastCell.walls & DirFlags.S) != 0;
            }

            return DirFlags.None;   // DEBUG disable this function.  Prevents getting stuck on walls/doors.

            //return (northEndWall ? DirFlags.N : DirFlags.None)
            //     | (southEndWall ? DirFlags.S : DirFlags.None)
            //     | (westEndWall ? DirFlags.W : DirFlags.None)
            //     | (eastEndWall ? DirFlags.E : DirFlags.None);
        }

        private Cell GetNeighborOrEmpty(int x, int y)
        {
            if (TryGetCell(x, y, out Cell cell))
                return cell;

            return new Cell(x, y)
            {
                walls = DirFlags.None,
                doors = DirFlags.None,
            };
        }

        private static void CleanupFloat(ref float value, bool stayInSameCell = true)
        {
            const float boundaryEpsilon = 0.00001f;

            if (stayInSameCell)
            {
                float cell = Mathf.Floor(value);
                float local = value - cell;

                if (local < boundaryEpsilon)
                {
                    value = cell;
                    return;
                }

                if (local > 1f - boundaryEpsilon)
                {
                    value = cell + 1f - boundaryEpsilon;
                    return;
                }
            }
            else
            {
                float nearestInteger = Mathf.Round(value);
                if (Mathf.Abs(value - nearestInteger) < boundaryEpsilon)
                    value = nearestInteger;
            }
        }

        private static void Cleanup(ref Vector2 value, bool stayInSameCell = true)
        {
            CleanupFloat(ref value.x, stayInSameCell);
            CleanupFloat(ref value.y, stayInSameCell);
        }

        private static bool TryDistanceToDiagonalWall(
            Cell cell,
            Vector2 startLocal,
            Vector2 direction,
            float cellSize,
            float agentRadius,
            out float distance)
        {
            distance = 0f;

            DiagonalOpenDirection diag = GetDiagonalOpenDirection(cell.walls, cell.doors);
            if (diag == DiagonalOpenDirection.None)
                return false;

            float a;
            float b;
            float c;
            float clearanceOffset = (agentRadius / Mathf.Max(1e-5f, cellSize)) * 1.41421356f;

            switch (diag)
            {
                case DiagonalOpenDirection.NE:
                    a = 1f; b = 1f; c = 1f + clearanceOffset;
                    break;
                case DiagonalOpenDirection.SW:
                    a = 1f; b = 1f; c = clearanceOffset;
                    break;
                case DiagonalOpenDirection.SE:
                    a = 1f; b = -1f; c = clearanceOffset;
                    break;
                case DiagonalOpenDirection.NW:
                    a = 1f; b = -1f; c = -(1f - clearanceOffset);
                    break;
                default:
                    return false;
            }

            float denom = a * direction.x + b * direction.y;
            if (Mathf.Abs(denom) < 1e-6f)
                return false;

            float num = cellSize * c - (a * startLocal.x + b * startLocal.y);
            float t = num / denom;
            Vector2 hit = startLocal + direction * t;

            if (hit.x < -1e-4f || hit.x > cellSize + 1e-4f || hit.y < -1e-4f || hit.y > cellSize + 1e-4f)
                return false;

            distance = t;
            return true;
        }

        private static DiagonalOpenDirection GetDiagonalOpenDirection(DirFlags walls, DirFlags doors)
        {
            if (walls.Count() != 2 || doors != DirFlags.None)
                return DiagonalOpenDirection.None;

            if ((walls & (DirFlags.N | DirFlags.E)) == (DirFlags.N | DirFlags.E)) return DiagonalOpenDirection.SW;
            if ((walls & (DirFlags.S | DirFlags.E)) == (DirFlags.S | DirFlags.E)) return DiagonalOpenDirection.NW;
            if ((walls & (DirFlags.S | DirFlags.W)) == (DirFlags.S | DirFlags.W)) return DiagonalOpenDirection.NE;
            if ((walls & (DirFlags.N | DirFlags.W)) == (DirFlags.N | DirFlags.W)) return DiagonalOpenDirection.SE;
            return DiagonalOpenDirection.None;
        }

        private static void GetDiagonalTangentAndNormal(Cell cell, out Vector2 tangent, out Vector2 normal)
        {
            switch (GetDiagonalOpenDirection(cell.walls, cell.doors))
            {
                case DiagonalOpenDirection.NE:
                    tangent = new Vector2(1f, -1f).normalized;
                    normal = new Vector2(1f, 1f).normalized;
                    break;
                case DiagonalOpenDirection.SW:
                    tangent = new Vector2(1f, -1f).normalized;
                    normal = new Vector2(-1f, -1f).normalized;
                    break;
                case DiagonalOpenDirection.SE:
                    tangent = new Vector2(1f, 1f).normalized;
                    normal = new Vector2(1f, -1f).normalized;
                    break;
                case DiagonalOpenDirection.NW:
                    tangent = new Vector2(1f, 1f).normalized;
                    normal = new Vector2(-1f, 1f).normalized;
                    break;
                default:
                    tangent = Vector2.zero;
                    normal = Vector2.zero;
                    break;
            }
        }

        #endregion
        #region ComputeVelocity

        private Vector3 ComputeHorizontalVelocity(
            Vector3 currentVelocity,
            Vector3 desiredVelocity,
            float maxAcceleration,
            float deltaTime)
        {
            // Ignore any vertical in both
            currentVelocity.y = 0f;
            desiredVelocity.y = 0f;

            Vector3 delta = desiredVelocity - currentVelocity;
            float maxDelta = maxAcceleration * deltaTime;

            if (delta.sqrMagnitude > maxDelta * maxDelta)
            {
                delta = delta.normalized * maxDelta;
            }

            Vector3 newVelocity = currentVelocity + delta;

            // Keep strictly horizontal
            newVelocity.y = 0f;
            return newVelocity;
        }

        private void IntegrateHorizontalRotation(Vector3 effectiveHorizontalVelocity, float deltaTime)
        {
            // No rotation if there's effectively no movement
            Vector3 flatVel = new Vector3(effectiveHorizontalVelocity.x, 0f, effectiveHorizontalVelocity.z);
            if (flatVel.sqrMagnitude < 0.0001f)
                return;

            if (facingMode == FacingMode.Strafe)
                return; // no turning while strafing

            if (facingMode == FacingMode.FaceMovementDirection)
            {
                Vector3 moveDir = flatVel.normalized;
                Vector3 forward = bodyRoot.forward;
                forward.y = 0f;
                forward.Normalize();

                float dot = Vector3.Dot(moveDir, forward); // 1 = forward, 0 = strafe, -1 = backward

                bool isBackpedaling = dot < -0.25f;   // tweak threshold as needed

                // If you want to allow strafing without rotation:
                bool isStrafing = Mathf.Abs(dot) < 0.25f;

                isBackpedaling=false;
                isStrafing=false;

                if (!isBackpedaling && !isStrafing)   // only rotate when mostly moving forward-ish
                {
                    RotateYawTowardsDirection(moveDir, rotationSpeedDegreesPerSecond, deltaTime);
                }
                // else: we are backpedaling or strafing → don't auto-rotate
            }
            else if (facingMode == FacingMode.FaceTarget && facingTarget != null)
            {
                Vector3 toTarget = facingTarget.position - bodyRoot.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    RotateYawTowardsDirection(toTarget.normalized, rotationSpeedDegreesPerSecond, deltaTime);
                }
            }
            // FacingMode.Strafe → no rotation here
            // FacingMode.Manual → no rotation here
        }

        private void RotateYawTowardsDirection(Vector3 direction, float degreesPerSecond, float deltaTime)
        {
            if (bodyRoot == null || direction.sqrMagnitude < 0.0001f || deltaTime <= 0f)
                return;

            Vector3 currentEuler = bodyRoot.rotation.eulerAngles;
            float targetYaw = Quaternion.LookRotation(direction.normalized, Vector3.up).eulerAngles.y;
            float nextYaw = Mathf.MoveTowardsAngle(
                currentEuler.y,
                targetYaw,
                degreesPerSecond * deltaTime);

            bodyRoot.rotation = Quaternion.Euler(currentEuler.x, nextYaw, currentEuler.z);
        }

        private void IntegrateVerticalVelocity(float deltaTime)
        {
            if (useGravity)
            {
                verticalVelocity.y += gravityMetersPerSecondSquared * deltaTime;

                if (maxFallSpeedMetersPerSecond > 0f &&
                    verticalVelocity.y < -maxFallSpeedMetersPerSecond)
                {
                    verticalVelocity.y = -maxFallSpeedMetersPerSecond;
                }
            }
            else
            {
                verticalVelocity = Vector3.zero;
            }
        }
        #endregion
    }
}
