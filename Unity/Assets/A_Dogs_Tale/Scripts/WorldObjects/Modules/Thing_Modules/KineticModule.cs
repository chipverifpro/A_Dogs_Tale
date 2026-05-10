using InspectorTools;
using UnityEngine;

namespace DogGame.Modules
{
    [InspectorNote("Thing_Modules/Kinetic Module", "Simple impulse-driven movement for thrown, rolled, or kicked items.")]
    [DisallowMultipleComponent]
    public class KineticModule : WorldModule
    {
        [Header("Body")]
        [Tooltip("Transform moved by this module. If null, this transform is used.")]
        [SerializeField] private Transform bodyRoot;
        [Tooltip("If true, apply the serialized Height World value to the body on Awake.")]
        [SerializeField] private bool applyHeightWorldOnAwake;
        [Tooltip("World-space center height. Synced from the body during simulation.")]
        [SerializeField] private float heightWorld;
        [Tooltip("Use WorldObject.sizeRadius as the collision radius. Off by default because item footprints are often larger than the visual ball radius.")]
        [SerializeField] private bool useWorldObjectRadius = false;
        [SerializeField, Min(0.01f)] private float radius = 0.15f;

        [Header("Forces")]
        [Tooltip("World-space gravity acceleration.")]
        [SerializeField] private Vector3 gravity = new(0f, -9.81f, 0f);
        [Tooltip("Velocity loss per second while airborne.")]
        [SerializeField, Min(0f)] private float airResistance = 0.08f;
        [Tooltip("Horizontal deceleration per second while touching ground.")]
        [SerializeField, Min(0f)] private float groundFriction = 3f;
        [Tooltip("If true, ApplyImpulse divides incoming impulse by WorldObject.Weight.")]
        [SerializeField] private bool scaleImpulseByWeight = true;

        [Header("Bounces")]
        [SerializeField, Range(0f, 1f)] private float bounceFactor = 0.45f;
        [SerializeField, Range(0f, 1f)] private float wallBounceFactor = 0.55f;
        [SerializeField, Range(0f, 1f)] private float groundHorizontalBounceDamping = 0.85f;
        [SerializeField, Min(0f)] private float minBounceSpeed = 0.35f;

        [Header("Collision")]
        [SerializeField] private bool useUnityPhysics = true;
        [SerializeField] private bool useMotionModuleWallConstraints = false;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField, Min(0f)] private float collisionSkin = 0.02f;
        [SerializeField, Min(0.05f)] private float groundProbeDistance = 2f;
        [Tooltip("Fallback to dungeon height data when no physics ground is found.")]
        [SerializeField] private bool useDungeonHeightFallback = true;
        [SerializeField, Min(0)] private int heightfieldSearchSteps = 50;

        [Header("Sleep")]
        [SerializeField, Min(0f)] private float sleepHorizontalSpeed = 0.04f;
        [SerializeField, Min(0f)] private float sleepVerticalSpeed = 0.08f;
        [SerializeField, Min(0f)] private float maxSimulationStep = 0.033f;

        [Header("Visual")]
        [SerializeField] private bool rotateWhileMoving = true;
        [SerializeField, Min(0f)] private float rollingRotationScale = 1f;

        [Header("Editor Test")]
        [Tooltip("Impulse used by the component context menu action in Play Mode.")]
        [SerializeField] private Vector3 testImpulse = new(0f, 2f, 5f);
        [SerializeField] private bool stopBeforeTestImpulse = true;

        [Header("State")]
        [SerializeField] private Vector3 velocityWorld;
        [SerializeField] private bool inMotion;
        [SerializeField] private bool grounded;

        private MotionModule motionModule;

        public bool IsMoving => inMotion;
        public bool IsGrounded => grounded;
        public Vector3 VelocityWorld => velocityWorld;

        public float HeightWorld
        {
            get => heightWorld;
            set
            {
                heightWorld = value;
                Transform body = Body;
                Vector3 position = body.position;
                position.y = value;
                body.position = position;
            }
        }

        private Transform Body => bodyRoot != null ? bodyRoot : transform;

        protected override void Awake()
        {
            base.Awake();

            if (bodyRoot == null)
                bodyRoot = transform;

            motionModule = GetComponent<MotionModule>();
            if (applyHeightWorldOnAwake)
                HeightWorld = heightWorld;
            else
                heightWorld = Body.position.y;
        }

        public override void Tick(float deltaTime)
        {
            if (!inMotion || deltaTime <= 0f)
                return;

            float remaining = deltaTime;
            float step = Mathf.Max(0.001f, maxSimulationStep);
            while (remaining > 0f && inMotion)
            {
                float dt = Mathf.Min(remaining, step);
                Simulate(dt);
                remaining -= dt;
            }
        }

        public void ApplyImpulse(Vector3 impulseWorld)
        {
            float divisor = scaleImpulseByWeight ? Mathf.Max(0.01f, worldObject != null ? worldObject.Weight : 1f) : 1f;
            velocityWorld += impulseWorld / divisor;
            Wake();
        }

        public void SetVelocity(Vector3 initialVelocityWorld)
        {
            velocityWorld = initialVelocityWorld;
            Wake();
        }

        public void AddVelocity(Vector3 deltaVelocityWorld)
        {
            velocityWorld += deltaVelocityWorld;
            Wake();
        }

        public void Stop()
        {
            velocityWorld = Vector3.zero;
            inMotion = false;
            grounded = true;
        }

        [ContextMenu("Apply Test Impulse")]
        private void ApplyTestImpulse()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("KineticModule test impulse only runs in Play Mode.", this);
                return;
            }

            if (stopBeforeTestImpulse)
                Stop();

            ApplyImpulse(testImpulse);
        }

        [ContextMenu("Stop Kinetic Motion")]
        private void StopKineticMotion()
        {
            Stop();
        }

        private void Wake()
        {
            inMotion = velocityWorld.sqrMagnitude > 0.000001f;
            grounded = false;
        }

        private void Simulate(float deltaTime)
        {
            IntegrateForces(deltaTime);

            Transform body = Body;
            Vector3 start = body.position;
            Vector3 desiredDelta = velocityWorld * deltaTime;
            Vector3 actualDelta = ResolveCollisionDisplacement(start, desiredDelta, deltaTime);
            Vector3 nextPosition = start + actualDelta;

            ResolveGround(ref nextPosition, deltaTime);

            body.position = nextPosition;
            heightWorld = nextPosition.y;
            RotateBody(actualDelta);
            SleepIfStopped();
        }

        private void IntegrateForces(float deltaTime)
        {
            velocityWorld += gravity * deltaTime;

            if (airResistance > 0f)
                velocityWorld *= Mathf.Exp(-airResistance * deltaTime);
        }

        private Vector3 ResolveCollisionDisplacement(Vector3 start, Vector3 desiredDelta, float deltaTime)
        {
            if (desiredDelta.sqrMagnitude <= 0.0000001f)
                return Vector3.zero;

            Vector3 physicsResolvedDelta = ResolveUnityPhysicsCollision(start, desiredDelta);
            Vector3 constrainedDelta = ResolveMotionModuleConstraint(start, physicsResolvedDelta, deltaTime);
            return constrainedDelta;
        }

        private Vector3 ResolveUnityPhysicsCollision(Vector3 start, Vector3 desiredDelta)
        {
            if (!useUnityPhysics)
                return desiredDelta;

            float distance = desiredDelta.magnitude;
            if (distance <= 0.00001f)
                return desiredDelta;

            Vector3 direction = desiredDelta / distance;
            float castRadius = GetRadius();
            RaycastHit[] hits = Physics.SphereCastAll(
                start,
                castRadius,
                direction,
                distance + collisionSkin,
                collisionMask,
                QueryTriggerInteraction.Ignore);

            if (!TryGetNearestExternalHit(hits, out RaycastHit hit, includeGround: false))
                return desiredDelta;

            float moveDistance = Mathf.Max(0f, hit.distance - collisionSkin);
            Vector3 resolvedDelta = direction * Mathf.Min(moveDistance, distance);
            velocityWorld = Vector3.Reflect(velocityWorld, hit.normal) * wallBounceFactor;

            return resolvedDelta;
        }

        private Vector3 ResolveMotionModuleConstraint(Vector3 start, Vector3 desiredDelta, float deltaTime)
        {
            if (!useMotionModuleWallConstraints || motionModule == null || desiredDelta.sqrMagnitude <= 0.0000001f)
                return desiredDelta;

            Vector3 actualDelta = motionModule.ApplyExternalWorldDisplacement(
                desiredDelta,
                deltaTime,
                applyLeashConstraints: false);

            Body.position = start;

            if ((actualDelta - desiredDelta).sqrMagnitude > 0.0001f)
                BounceFromConstrainedDelta(desiredDelta, actualDelta);

            return actualDelta;
        }

        private void BounceFromConstrainedDelta(Vector3 desiredDelta, Vector3 actualDelta)
        {
            Vector3 desiredHorizontal = new(desiredDelta.x, 0f, desiredDelta.z);
            Vector3 actualHorizontal = new(actualDelta.x, 0f, actualDelta.z);

            if (desiredHorizontal.sqrMagnitude <= 0.000001f)
                return;

            Vector3 blocked = desiredHorizontal - actualHorizontal;
            if (blocked.sqrMagnitude <= 0.000001f)
                return;

            Vector3 normal = -blocked.normalized;
            Vector3 horizontalVelocity = new(velocityWorld.x, 0f, velocityWorld.z);
            horizontalVelocity = Vector3.Reflect(horizontalVelocity, normal) * wallBounceFactor;
            velocityWorld.x = horizontalVelocity.x;
            velocityWorld.z = horizontalVelocity.z;
        }

        private void ResolveGround(ref Vector3 position, float deltaTime)
        {
            grounded = false;

            if (!TryGetGroundHeight(position, deltaTime, out float groundHeight, out Vector3 groundNormal))
                return;

            float minCenterY = groundHeight + GetRadius();
            if (position.y > minCenterY)
                return;

            position.y = minCenterY;
            grounded = true;

            if (velocityWorld.y < -minBounceSpeed)
            {
                BounceOffGround(groundNormal);
            }
            else
            {
                velocityWorld.y = 0f;
                ApplyGroundFriction(deltaTime);
            }
        }

        private void BounceOffGround(Vector3 groundNormal)
        {
            velocityWorld = Vector3.Reflect(velocityWorld, groundNormal.normalized) * bounceFactor;
            velocityWorld.x *= groundHorizontalBounceDamping;
            velocityWorld.z *= groundHorizontalBounceDamping;
        }

        private void ApplyGroundFriction(float deltaTime)
        {
            Vector3 horizontal = new(velocityWorld.x, 0f, velocityWorld.z);
            horizontal = Vector3.MoveTowards(horizontal, Vector3.zero, groundFriction * deltaTime);
            velocityWorld.x = horizontal.x;
            velocityWorld.z = horizontal.z;
        }

        private bool TryGetGroundHeight(Vector3 position, float deltaTime, out float groundHeight, out Vector3 groundNormal)
        {
            if (TryGetPhysicsGround(position, deltaTime, out groundHeight, out groundNormal))
                return true;

            if (useDungeonHeightFallback && TryGetDungeonGround(position, out groundHeight))
            {
                groundNormal = Vector3.up;
                return true;
            }

            groundHeight = 0f;
            groundNormal = Vector3.up;
            return false;
        }

        private bool TryGetPhysicsGround(Vector3 position, float deltaTime, out float groundHeight, out Vector3 groundNormal)
        {
            groundHeight = 0f;
            groundNormal = Vector3.up;

            if (!useUnityPhysics)
                return false;

            float castDistance = Mathf.Max(groundProbeDistance, GetRadius() + Mathf.Abs(velocityWorld.y) * deltaTime + 0.1f);
            Vector3 origin = position + Vector3.up * 0.05f;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                castDistance + 0.05f,
                collisionMask,
                QueryTriggerInteraction.Ignore);

            if (!TryGetNearestExternalHit(hits, out RaycastHit hit, includeGround: true))
                return false;

            groundHeight = hit.point.y;
            groundNormal = hit.normal;
            return groundNormal.y > 0.35f;
        }

        private bool TryGetDungeonGround(Vector3 position, out float groundHeight)
        {
            groundHeight = 0f;

            if (dir == null || dir.gen == null || !dir.gen.buildComplete || dir.gen.hf == null)
                return false;

            Vector3 mapPosition = worldObject != null ? worldObject.WorldToMapPosition(position) : position;
            int x = Mathf.FloorToInt(mapPosition.x);
            int y = Mathf.FloorToInt(mapPosition.z);
            float unitHeight = dir.cfg != null ? Mathf.Max(0.0001f, dir.cfg.unitHeight) : 1f;
            int heightSteps = Mathf.RoundToInt(mapPosition.y / unitHeight);

            if (!dir.gen.hf.TryQueryAt(x, y, heightSteps, heightfieldSearchSteps, out var match))
                return false;

            Vector3 groundMap = new(mapPosition.x, match.z * unitHeight, mapPosition.z);
            Vector3 groundWorld = worldObject != null ? worldObject.MapToWorldPosition(groundMap) : groundMap;
            groundHeight = groundWorld.y;
            return true;
        }

        private bool TryGetNearestExternalHit(RaycastHit[] hits, out RaycastHit nearest, bool includeGround)
        {
            nearest = default;
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || IsOwnCollider(hit.collider))
                    continue;

                if (!includeGround && hit.normal.y > 0.45f)
                    continue;

                if (hit.distance < nearestDistance)
                {
                    nearest = hit;
                    nearestDistance = hit.distance;
                }
            }

            return nearest.collider != null;
        }

        private bool IsOwnCollider(Collider collider)
        {
            Transform candidate = collider.transform;
            Transform ownBody = Body;
            return candidate == transform ||
                   candidate == ownBody ||
                   candidate.IsChildOf(transform) ||
                   transform.IsChildOf(candidate);
        }

        private void RotateBody(Vector3 actualDelta)
        {
            if (!rotateWhileMoving || grounded == false)
                return;

            Vector3 horizontalDelta = new(actualDelta.x, 0f, actualDelta.z);
            float distance = horizontalDelta.magnitude;
            if (distance <= 0.0001f)
                return;

            Vector3 axis = Vector3.Cross(Vector3.up, horizontalDelta.normalized);
            float circumference = 2f * Mathf.PI * Mathf.Max(0.001f, GetRadius());
            float degrees = (distance / circumference) * 360f * rollingRotationScale;
            Body.Rotate(axis, degrees, Space.World);
        }

        private void SleepIfStopped()
        {
            Vector2 horizontal = new(velocityWorld.x, velocityWorld.z);
            if (!grounded || horizontal.magnitude > sleepHorizontalSpeed || Mathf.Abs(velocityWorld.y) > sleepVerticalSpeed)
                return;

            Stop();
        }

        private float GetRadius()
        {
            if (useWorldObjectRadius && worldObject != null)
                return Mathf.Max(0.01f, worldObject.sizeRadius);

            return Mathf.Max(0.01f, radius);
        }
    }
}
