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

        // Object or Location we are going towards.
        public WorldObject targetObject;        // for continuous tracking of a (possibly moving) target object
                                                // every tick, update targetLocation to it's current world location
        public bool keepFollowingTargetObject;  // if (true) then upon arrival, we wait for the target to move and keep following it indefinitely.
                                                // if (false) then upon arrival, this task is complete.
        public Vector3? targetLocation;         // for travelling to a destination location or current location of target object

        public bool targetMoved;                // not yet used: is this tick's target objet position different than last?  Use case?

        public float stopDistanceFromObject;    // when heading to an object, don't run inside it.
                                                //   (should be radius of agent + radius of target)
                                                // also used as follow distance when continuing to follow agents.
                                                //   (should be packModule.followDistanceMeters)
        
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

        // this just forwards the change to motionModule where it is kept.
        public void SetWalkMode(WalkMode walkMode)
        {
            worldObject.motionModule.SetWalkMode(walkMode);
        }

        // Set target in world location space, and we will travel to it until arrived.
        public void SetDesiredTargetLocation(Vector3 targetLocation_world)
        {
            this.targetLocation = targetLocation_world;
            // clear the target object or targetLocation will get overwritten every tick.
            this.targetObject = null;
            this.keepFollowingTargetObject = false;
        }

        // Set target once, and we will keep following it until we arrive.
        public void SetDesiredTargetWorldObject(WorldObject target, bool keepFollowing=false)
        {
            this.targetObject = target;
            this.keepFollowingTargetObject = keepFollowing;
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
        }

        public void ClearDesiredTargetLocation()
        {
            targetLocation = null;
        }

        // Called every tick when a target object is not null.  Finds target and heads to it.
        // (DecisionModule probably should check if we can still see it or still guess it's location)
        public void PointTowardTargetObjectLocation()
        {
            Vector3 targetLocation_world=Vector3.zero;
            maxDistance = 1f;
            if (targetObject==null) 
            {
                return;
            }
            // update target location
            if (targetObject.locationModule!=null)
                targetLocation_world = targetObject.locationModule.pos3d_world;

            // check target poisition versus where it was last tick
            if (targetLocation != targetLocation_world)
            {
                targetMoved = true;
                targetLocation = targetLocation_world;
            }

            // we have a new target, so now go to it:
            PointTowardTargetLocation();
        }

        // Called every tick when a target location is not null.
        // Or, called every tick after target object is found and target location is updated.
        public void PointTowardTargetLocation()
        {
            if (targetLocation == null) 
            {
                return;
            }

            // find our location
            Vector3 ourLocation_world = worldObject.locationModule.pos3d_world;

            // direction and distance to target for move command.
            Vector3 desired_move = (Vector3)targetLocation - ourLocation_world;
            
            float distanceToTarget = desired_move.magnitude;

            // determine if we should limit the distance travelled (because we are close)
            float stopDistanceFromTarget;
            if (targetObject) // if object, don't bump into it; or use pack's formationSpacing to determine follow distance.
            {
                if (keepFollowingTargetObject && worldObject.packMemberModule!=null && worldObject.packMemberModule.currentPack!=null)
                    stopDistanceFromTarget = worldObject.packMemberModule.currentPack.formationSpacing;
                else
                    stopDistanceFromTarget = stopDistanceFromObject;
            }
            else // not an object, point all the way to destination.
                stopDistanceFromTarget = 999f;

            // clamp maxDistance if we are very close to avoid overshoot and stop at correct distance.
            maxDistance = Mathf.Min(distanceToTarget, distanceToTarget-stopDistanceFromTarget, 1f); // never more than 1.0 per tick or we may end up running THROUGH walls.

            // We now have desired_move = a vector to the destination we want to go.
            //             
            // note: no need to normalize, it will be done in the function.
            SetDesiredMove(desired_move);
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

            //speedFactor01 = Mathf.Clamp01(speedFactor);  // removed clamp to allow downhill speeds faster than 1.0x

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
                PointTowardTargetObjectLocation();   // sets targetLocation to point to the object, then GoTowardLocation.
            }
            else if (targetLocation != null)
            {
                PointTowardTargetLocation();        // moves twoards target
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
            worldObject.motionModule.Move(currentVelocity, deltaTime, maxDistance);

            // After moving, Are we there yet?
            if (targetLocation!=null)
            {
                float distanceRemaining = Vector3.Magnitude(worldObject.locationModule.pos3d_world - (Vector3)targetLocation);
                if (distanceRemaining < 0.01f)
                {
                    ClearDesiredTargetLocation();
                    if (targetObject!=null)
                        ClearDesiredTargetWorldObject();
                }
            }
        }
    }
}