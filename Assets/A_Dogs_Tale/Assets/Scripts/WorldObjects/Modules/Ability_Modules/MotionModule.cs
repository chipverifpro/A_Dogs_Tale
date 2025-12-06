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

namespace DogGame.AI
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
            if (bodyRoot == null)
            {
                bodyRoot = transform;
            }
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
        /// Convenience: instantly teleport the body to a new position without any velocity.
        /// Useful for respawns, teleports, etc.
        /// </summary>
        public void Teleport(Vector3 worldPosition)
        {
            if (bodyRoot == null)
                return;

            bodyRoot.position = worldPosition;
            verticalVelocity = Vector3.zero;
        }
    }
}