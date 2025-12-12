using System;
using NUnit.Framework.Internal.Filters;
using UnityEditor.EditorTools;
using UnityEngine;

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
    ///   - Compute a desired world-space velocity
    ///   - Call Move(desiredVelocity, deltaTime) every frame
    /// </summary>
    
    public enum MotionControlMode
    {
        DirectInput,   // WASD / stick, immediate control
        GoalDirected,  // Anything that produces a destination / path (click, pathfinding)
    }

    public enum FacingMode
    {
        FaceMovementDirection,
        FaceTarget,
        Strafe,     // don't rotate, just move sideways.
        Manual   // e.g. animation or some other system controls rotation (or do not turn)
    }
    
    public class MotionModule : WorldModule
    {
        [Header("Body Setup")]
        [Tooltip("Transform that represents the root of the dog body. If null, this.transform is used.")]
        [SerializeField] public Transform bodyRoot;

        [Header("Rotation")]
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

        //[NonSerialized] public Transform bodyRoot;

        //public bool useGravity = true;
        //public float gravityMetersPerSecondSquared = -25f;
        //public float maxFallSpeedMetersPerSecond = 40f;

        public float maxHorizontalAcceleration = 40f;
        public float maxHorizontalSpeed = 8f;

        //public float rotationSpeedDegreesPerSecond = 720f;

        //public FacingMode facingMode = FacingMode.FaceMovementDirection;
        //public Transform facingTarget;

        private Vector3 horizontalVelocity = Vector3.zero;
        //private Vector3 verticalVelocity = Vector3.zero;


        protected override void Awake()
        {
            if (worldObject == null)
            {
                worldObject = GetComponent<WorldObject>();
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

        public override void Tick(float deltaTime)
        {
            //Debug.Log($"MotionModule {worldObject.DisplayName}: Tick {deltaTime}");
        
        }

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
        /// ResetMotion() is the same as ResetVerticalVelocity()
        /// </summary>
        public void ResetMotion()
        {
            // Stop any vertical movement (no more falling from previous position)
            verticalVelocity = Vector3.zero;

            // If you later add cached horizontal velocity here, clear it too.
            // e.g. currentHorizontalVelocity = Vector3.zero;

            // We do NOT change position or rotation here; Teleport already did that.
        }


        // ===== Teleport family of commands =====

        /// <summary>
        /// Convenience: instantly teleport the body to a new position without any velocity.
        /// Useful for respawns, teleports, etc.
        /// </summary>
        public void Teleport(Vector3 worldPosition, bool resetMotion = true)
        {
            if (bodyRoot == null)
                return;

            bodyRoot.position = worldPosition;
            if (resetMotion)
            {
                ResetMotion();
                worldObject.agentMovementModule?.ClearDesiredMove();
            }
        }

        // teleport with full control of rotation and angle.
        public void Teleport(Vector3 worldPosition, Quaternion worldRotation, bool resetMotion = true)
        {
            transform.SetPositionAndRotation(worldPosition, worldRotation);

            if (resetMotion)
            {
                ResetMotion();
                worldObject.agentMovementModule?.ClearDesiredMove();
            }
        }

        // only uses rotation around vertical axis.
        public void TeleportUpright(Vector3 position, Quaternion rotation, bool resetMotion = true)
        {
            rotation = Quaternion.FromToRotation(rotation * Vector3.up, Vector3.up) * rotation;
            Teleport(position, rotation, resetMotion);
        }

        // if ground is tilted, we might want to do this...
        public void TeleportAlignToGround(Vector3 position, Vector3 groundNormal, float extraYaw = 0f, bool resetMotion = true)
        {
            // Create rotation aligned to surface normal
            Quaternion align = Quaternion.FromToRotation(Vector3.up, groundNormal);

            // Add optional yaw (turning left/right relative to ground plane)
            Quaternion finalRotation = Quaternion.Euler(0, extraYaw, 0) * align;

            Teleport(position, finalRotation, resetMotion);
        }

        public void Move(Vector3 desiredHorizontalVelocity, float deltaTime)
        {
            if (bodyRoot == null)
                return;

            // --- 0. Enforce horizontal-only for input ---
            desiredHorizontalVelocity.y = 0f;

            // Clamp desired speed if you like
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
            ApplyHorizontalRotation(horizontalVelocity, deltaTime);

            // --- 3. Update vertical velocity (gravity) ---
            UpdateVerticalVelocity(deltaTime);

            // --- 4. Apply total displacement ---
            Vector3 frameVelocity = horizontalVelocity + verticalVelocity;
            bodyRoot.position += frameVelocity * deltaTime;
        }

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

        private void ApplyHorizontalRotation(Vector3 effectiveHorizontalVelocity, float deltaTime)
        {
            // No rotation if there's effectively no movement
            Vector3 flatVel = new Vector3(effectiveHorizontalVelocity.x, 0f, effectiveHorizontalVelocity.z);
            if (flatVel.sqrMagnitude < 0.0001f)
                return;

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
                    Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
                    bodyRoot.rotation = Quaternion.RotateTowards(
                        bodyRoot.rotation,
                        targetRotation,
                        rotationSpeedDegreesPerSecond * deltaTime);
                }
                // else: we are backpedaling or strafing → don't auto-rotate
            }
            else if (facingMode == FacingMode.FaceTarget && facingTarget != null)
            {
                Vector3 toTarget = facingTarget.position - bodyRoot.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                    bodyRoot.rotation = Quaternion.RotateTowards(
                        bodyRoot.rotation,
                        targetRotation,
                        rotationSpeedDegreesPerSecond * deltaTime);
                }
            }
            // FacingMode.Strafe → no rotation here
            // FacingMode.Manual → no rotation here
        }

        private void UpdateVerticalVelocity(float deltaTime)
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

    }
}