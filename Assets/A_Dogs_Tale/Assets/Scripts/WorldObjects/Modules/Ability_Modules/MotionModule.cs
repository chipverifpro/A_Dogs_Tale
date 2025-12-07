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
    public class MotionModule : WorldModule
    {
        [Header("Body Setup")]
        [Tooltip("Transform that represents the root of the dog body. If null, this.transform is used.")]
        [SerializeField] private Transform bodyRoot;

        [Header("Rotation")]
        [Tooltip("Rotate to face the horizontal movement direction.")]
        [SerializeField] private bool faceMovementDirection = true;

        [Tooltip("Degrees per second to turn toward the movement direction.")]
        [SerializeField] private float rotationSpeedDegreesPerSecond = 720f;

        [Header("Gravity (optional)")]
        [Tooltip("If true, apply gravity to vertical motion.")]
        [SerializeField] private bool useGravity = false;

        [Tooltip("Gravity acceleration, in meters per second squared (negative is downward).")]
        [SerializeField] private float gravityMetersPerSecondSquared = -9.81f;

        [Tooltip("Clamp maximum downward speed (terminal velocity). Set to 0 to disable.")]
        [SerializeField] private float maxFallSpeedMetersPerSecond = 50f;

        // Internal vertical velocity (for gravity, jumps, etc.)
        private Vector3 verticalVelocity = Vector3.zero;

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
        public void Move(Vector3 desiredHorizontalVelocity, float deltaTime)
        {
            if (bodyRoot == null)
                return;

            // Ensure horizontal only for the input velocity
            desiredHorizontalVelocity.y = 0f;

            // 1. Update vertical velocity with gravity, if enabled
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
                // If we are not using gravity, don't accumulate vertical velocity
                verticalVelocity = Vector3.zero;
            }

            // 2. Combine horizontal and vertical components
            Vector3 frameVelocity = desiredHorizontalVelocity + verticalVelocity;

            // 3. Apply rotation to face movement direction (horizontal plane only)
            if (faceMovementDirection)
            {
                Vector3 flatDirection = new Vector3(desiredHorizontalVelocity.x, 0f, desiredHorizontalVelocity.z);
                if (flatDirection.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
                    bodyRoot.rotation = Quaternion.RotateTowards(
                        bodyRoot.rotation,
                        targetRotation,
                        rotationSpeedDegreesPerSecond * deltaTime);
                }
            }

            // 4. Apply position change
            Vector3 displacement = frameVelocity * deltaTime;
            bodyRoot.position += displacement;
        }

        /// <summary>
        /// Directly sets vertical velocity (e.g., for jumps).
        /// Positive values go upward, negative downward.
        /// </summary>
        public void SetVerticalVelocity(float newVerticalSpeed)
        {
            verticalVelocity.y = newVerticalSpeed;
        }

        /// <summary>
        /// Clears any stored vertical velocity (for example after grounding).
        /// </summary>
        public void ResetVerticalVelocity()
        {
            verticalVelocity = Vector3.zero;
        }

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

    }
}