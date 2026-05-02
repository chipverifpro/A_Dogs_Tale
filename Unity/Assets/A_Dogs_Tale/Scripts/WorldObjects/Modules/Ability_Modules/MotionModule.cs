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
            for (int i = 0; i < maxSpeedsByMode.Count; i++)
            {
                if (maxSpeedsByMode[i].mode == currentWalkMode)
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
        public void ApplyMotionMap(Vector3 desiredMapVelocity, float deltaTime, float maxDistanceMap)
        {
            if (worldObject == null || deltaTime <= 0f)
            {
                ApplyMotion(desiredMapVelocity, deltaTime, maxDistanceMap);
                return;
            }

            Vector3 currentMapPosition = worldObject.WorldToMapPosition(bodyRoot != null ? bodyRoot.position : transform.position);
            Vector3 targetMapPosition = currentMapPosition + (desiredMapVelocity * deltaTime);
            Vector3 currentWorldPosition = bodyRoot != null ? bodyRoot.position : transform.position;
            Vector3 targetWorldPosition = worldObject.MapToWorldPosition(targetMapPosition);
            Vector3 desiredWorldVelocity = (targetWorldPosition - currentWorldPosition) / deltaTime;

            // The current map<->world transform is translational, so the scalar stop radius carries over directly.
            ApplyMotion(desiredWorldVelocity, deltaTime, maxDistanceMap);
        }

        public void ApplyMotion(Vector3 desiredHorizontalVelocity, float deltaTime, float maxDistance)
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
            float maxHorizontalSpeed = GetMaxSpeedByCurrentWalkMode();
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
            Vector3 constrainedPosition = ResolveConstrainedWorldPosition(bodyRoot.position, proposedPosition, applyLeashConstraints: true);

            // --- 7. Commit
            Vector3 actualDelta = constrainedPosition - bodyRoot.position;
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

        public Vector3 ApplyExternalWorldDisplacement(Vector3 desiredWorldDelta, float deltaTime, bool applyLeashConstraints = true)
        {
            if (bodyRoot == null || desiredWorldDelta.sqrMagnitude <= 0f)
                return Vector3.zero;

            Vector3 startPosition = bodyRoot.position;
            Vector3 constrainedPosition = ResolveConstrainedWorldPosition(
                startPosition,
                startPosition + desiredWorldDelta,
                applyLeashConstraints);

            Vector3 actualDelta = constrainedPosition - startPosition;
            bodyRoot.position = constrainedPosition;

            if (deltaTime > 0f)
            {
                Vector3 actualFrameVelocity = actualDelta / deltaTime;
                horizontalVelocity = new Vector3(actualFrameVelocity.x, 0f, actualFrameVelocity.z);
            }

            return actualDelta;
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

            return (northEndWall ? DirFlags.N : DirFlags.None)
                 | (southEndWall ? DirFlags.S : DirFlags.None)
                 | (westEndWall ? DirFlags.W : DirFlags.None)
                 | (eastEndWall ? DirFlags.E : DirFlags.None);
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
            float rounded = Mathf.Round(value * 100f) / 100f;

            if (stayInSameCell)
            {
                float cell = Mathf.Floor(value);
                rounded = Mathf.Clamp(rounded, cell, cell + 0.99f);
            }

            value = rounded;
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
