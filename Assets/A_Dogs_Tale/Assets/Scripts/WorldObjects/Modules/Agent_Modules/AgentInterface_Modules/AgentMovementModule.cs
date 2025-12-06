using UnityEngine;

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

namespace DogGame.AI
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
        [Header("Dependencies")]
        [Tooltip("Low-level motion executor. Auto assigned.")]
        private MotionModule motion;

        [Header("Speed Settings")]
        [Tooltip("Maximum walking speed in meters per second.")]
        [SerializeField] private float walkSpeedMetersPerSecond = 3.0f;

        [Tooltip("Maximum running speed in meters per second.")]
        [SerializeField] private float runSpeedMetersPerSecond = 6.0f;

        [Header("Acceleration")]
        [Tooltip("Acceleration toward desired velocity in meters per second squared.")]
        [SerializeField] private float accelerationMetersPerSecondSquared = 12.0f;

        [Tooltip("Deceleration when stopping or changing direction in meters per second squared.")]
        [SerializeField] private float decelerationMetersPerSecondSquared = 16.0f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = false;

        // Current velocity we are actually moving with (world-space, horizontal+vertical from MotionModule)
        private Vector3 currentVelocity = Vector3.zero;

        // Desired velocity requested by decision modules (world-space, horizontal only here)
        private Vector3 desiredVelocity = Vector3.zero;

        // Used to choose between walk/run speeds when using SetDesiredMove()
        private bool desireRun = false;
        private float speedFactor01 = 1.0f; // 0..1 scaling of walk/run speed

        /// <summary>
        /// Exposes the current velocity for other systems (e.g., animation).
        /// </summary>
        public Vector3 CurrentVelocity => currentVelocity;

        /// <summary>
        /// Exposes the desired velocity for debugging or higher-level logic.
        /// </summary>
        public Vector3 DesiredVelocity => desiredVelocity;

        protected override void Awake()
        {
            base.Awake();

            if (motion == null)
            {
                motion = GetComponent<MotionModule>();
                if (motion == null)
                {
                    Debug.LogError($"[AgentMovementModule] No MotionModule found. Movement will be disabled.", this);
                    enabled = false;
                    return;
                }
            }
        }

        /// <summary>
        /// Called by decision modules to set a desired movement direction and speed.
        ///
        /// worldDirection01: world-space direction, will be normalized and Y set to 0.
        /// speedFactor: 0..1 scale applied to walk/run speed.
        /// run: if true, uses runSpeed, otherwise walkSpeed.
        /// </summary>
        public void SetDesiredMove(Vector3 worldDirection01, float speedFactor = 1.0f, bool run = false)
        {
            worldDirection01.y = 0f;

            if (worldDirection01.sqrMagnitude > 1f)
                worldDirection01.Normalize();

            desireRun = run;
            speedFactor01 = Mathf.Clamp01(speedFactor);

            float baseSpeed = run ? runSpeedMetersPerSecond : walkSpeedMetersPerSecond;
            float targetSpeed = baseSpeed * speedFactor01;

            desiredVelocity = worldDirection01 * targetSpeed;
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

        /// <summary>
        /// Called once per frame by the AgentModule/AgentDecision system.
        /// This is where we blend current velocity toward desiredVelocity and
        /// then ask MotionModule to actually move the character.
        /// </summary>
        public override void Tick(float deltaTime)
        {
            if (motion == null)
                return;

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

            // Delegate to MotionModule for actual movement + rotation
            motion.Move(currentVelocity, deltaTime);
        }
    }
}