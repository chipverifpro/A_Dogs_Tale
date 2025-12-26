//using System.Numerics;
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

        public WorldObject targetObject;        // for continuous tracking of a (possibly moving) target object
        public Vector3 targetObjectPosition;    // updates every tick so we can head directly to it
        public bool keepTrackingTarget;         // if not set, we only go for one tick and stop.
        public bool targetMoved;                // not yet used: is this tick's target objet position different than last?

        public Vector3? targetPosition;         // for travelling to a destination location instead of a target object above

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
        /// Usually travel distance = desiredVelocity * deltaTime.  Limit that.
        /// Never travel farther than maxDistance on a tick.
        /// A) it could be how far the target is away so we don't overshoot.
        /// B) it could be how far to a barrier so we don't go through it.
        /// C) with tile-based bump detector, we only looked 1 tile ahead for collisions.
        ///    we will want to recalculate bumping into objects the next tile,
        ///    so don't move beyond that until we have checked again.
        /// </summary>
        public float maxDistance = 1f;

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
        }

        // Set target once, and we will keep following it.
        public void SetDesiredTargetWorldObject(WorldObject target, bool keepTrackingTarget=true)
        {
            targetObject = target;
            this.keepTrackingTarget = keepTrackingTarget;
        }

        public void ClearDesiredTargetWorldObject()
        {
            targetObject = null;
            keepTrackingTarget = false;
        }

        // Called every tick when a target object is not null.  Finds target and heads to it.
        // (DecisionModule probably should check if we can still see it or still guess it's location)
        public void GoTowardTargetObjectPosition()
        {
            Vector3 targetLocation_world=Vector3.zero;
            if (targetObject==null) 
            {
                maxDistance = 1f;
                return;
            }
            // update target location
            if (targetObject.locationModule!=null)
                targetLocation_world = targetObject.locationModule.pos3d_world;

            // check target poisition versus where it was last tick
            if (targetObjectPosition != targetLocation_world)
            {
                targetMoved = true;
                targetObjectPosition = targetLocation_world;
            }
            // find our location
            Vector3 ourLocation_world = worldObject.locationModule.pos3d_world;

            // direction and distance to target for move command.
            Vector3 desired_move = targetLocation_world - ourLocation_world;
            
            // clamp maxDistance if we are very close to avoid overshoot.
            maxDistance = Mathf.Min(desired_move.magnitude, 1f);

            // Decision module told us to keep move again, by updating the desired movement.
            // note: no need to normalize, it will be done in the function.
            SetDesiredMove(desired_move, keepTrackingTarget: keepTrackingTarget);   
        }

        public void GoTowardTargetPosition()
        {
            if (targetPosition == null) 
            {
                return;
            }

            // find our location
            Vector3 ourLocation_world = worldObject.locationModule.pos3d_world;

            // direction and distance to target for move command.
            Vector3 desired_move = (Vector3)targetPosition - ourLocation_world;
            
            // clamp maxDistance if we are very close to avoid overshoot.
            maxDistance = Mathf.Min(desired_move.magnitude, 1f);

            // Decision module told us to keep move again, by updating the desired movement.
            // note: no need to normalize, it will be done in the function.
            SetDesiredMove(desired_move, keepTrackingTarget: false);
        }

        /// <summary>
        /// Called by decision modules to set a desired movement direction and speed.
        ///
        /// worldDirection01: world-space direction, will be normalized and Y set to 0.
        /// speedFactor: 0..1 scale applied to walk/run speed.
        /// run: if true, uses runSpeed, otherwise walkSpeed.
        /// </summary>
        public void SetDesiredMove(Vector3 worldDirection01, float speedFactor = 1.0f, bool run = false, bool keepTrackingTarget=false)
        {
            worldDirection01.y = 0f;

            if (worldDirection01.sqrMagnitude > 1f)
                worldDirection01.Normalize();

            desireRun = run;
            speedFactor01 = Mathf.Clamp01(speedFactor);

            float baseSpeed = run ? runSpeedMetersPerSecond : walkSpeedMetersPerSecond;
            float targetSpeed = baseSpeed * speedFactor01;

            desiredVelocity = worldDirection01 * targetSpeed;

            // if requested, then we stop heading for the target object.
            // this is probably because the user gave a manual move command.
            if (!keepTrackingTarget) ClearDesiredTargetWorldObject();
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
            //Debug.Log($"AgentMovementModule {worldObject.DisplayName}: Tick {deltaTime}");

            if (worldObject.motionModule == null)
                return;

            if (targetObject != null)
            {
                GoTowardTargetObjectPosition();   // calls SetDesiredMove to point to the object.
            }
            
            if (targetPosition != null)
            {
                GoTowardTargetPosition();
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
            worldObject.motionModule.Move(currentVelocity, deltaTime, 999f);
        }
    }
}