// ===============================================
// 2) UPDATE: VisionPerceptionModule to emit unified PerceptionEvent
// ===============================================
// This is the minimal diff-style change:
// - Add: using DogGame.AI.Perception;
// - Add a public list: public readonly List<PerceptionEvent> perceptionEvents = new();
// - In Tick(): clear it and push events via PerceptionEvent.MakeVision(...)
// - Keep your existing detections list for debugging/scoring; events are the unified output.

#nullable enable
using System.Collections.Generic;
using UnityEngine;
using DogGame.AI.Perception;

namespace DogGame.Modules
{
    public sealed class VisionPerceptionModule : WorldModule
    {
        [Header("Vision")]
        [SerializeField] private float viewRadius = 12f;
        [SerializeField, Range(30f, 270f)] private float peripheralFovDeg = 160f;
        [SerializeField] private float eyeHeight = 0.60f;
        [SerializeField] private float targetAimHeight = 0.50f;

        [Header("LOS")]
        [SerializeField] private LayerMask occluderMask = ~0;

        [Header("Motion thresholds (m/s)")]
        [SerializeField] private float movingSpeedThreshold = 0.20f;
        [SerializeField] private float fastSpeedThreshold = 3.50f;

        [Header("Output limits")]
        [SerializeField] private int maxDetections = 12;
        [SerializeField] private int maxEvents = 16;

        [Header("Leader visibility event")]
        [SerializeField] private float leaderNotVisibleCooldownSeconds = 2.0f;

        public struct VisionDetection
        {
            public WorldObject target;
            public VisionTargetKind kind;
            public SocialRelation relation;

            public float distance;
            public float angleDeg;
            public float speed;
            public float sizeScore;
            public float score;
            public bool isNewlySeen;
        }

        public readonly List<VisionDetection> detections = new();

        // Unified output for ReactionEngine:
        public readonly List<PerceptionEvent> perceptionEvents = new();

        private readonly Dictionary<int, LastSeenInfo> lastSeen = new();
        private readonly Dictionary<int, WorldObject> lastSeenTargetRef = new();
        private readonly HashSet<int> visibleThisTick = new();
        private readonly HashSet<int> visibleLastTick = new();

        private float leaderNotVisibleCooldown;

        private struct LastSeenInfo
        {
            public Vector3 lastPosWorld;
            public int lastSeenFrame;
        }

        public List<PerceptionEvent> GetPerceptionEvents()
        {
            return perceptionEvents;
        }

        public override void Tick(float deltaTime)
        {
            //Debug.Log("VisionPerceptionModule.Tick");
            detections.Clear();
            perceptionEvents.Clear();

            if (worldObject == null)
            {
                Debug.Log("worldObject is null");
                return;
            }

            //if (!WorldObjectRegistry.HasInstance)
            //{
            //    Debug.Log("WorldObjectRegister.HasInstance = false");
            //    return;
            //}

            var registry = WorldObjectRegistry.Instance;
            if (registry == null)
            {
                Debug.Log("registry is null");
                return;
            }

            // swap visibility sets
            visibleLastTick.Clear();
            foreach (var id in visibleThisTick) visibleLastTick.Add(id);
            visibleThisTick.Clear();

            if (leaderNotVisibleCooldown > 0f)
                leaderNotVisibleCooldown = Mathf.Max(0f, leaderNotVisibleCooldown - deltaTime);

            Vector3 agentPos = worldObject.pos3d_world;
            Vector3 eyePos = agentPos + Vector3.up * eyeHeight;

            Vector3 forward = worldObject.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            float viewRadiusSqr = viewRadius * viewRadius;
            float halfFov = peripheralFovDeg * 0.5f;

            foreach (var target in registry.GetAllObjects())
            {
                if (target == null || target == worldObject)
                {
                    //Debug.Log($"bad target {worldObject.DisplayName}.");
                    continue;
                }                
                //Debug.Log($"[VisionPerceptionModule:{worldObject.DisplayName}] checking {target.DisplayName}");
                
                Vector3 targetPos = target.pos3d_world;

                Vector3 toTarget = targetPos - agentPos;
                toTarget.y = 0f;
                float sqrDist = toTarget.sqrMagnitude;
                if (sqrDist > viewRadiusSqr || sqrDist < 0.0001f)
                {
                    //Debug.Log("Distance too great.");
                    continue;
                }

                float dist = Mathf.Sqrt(sqrDist);

                Vector3 dir = toTarget / dist;
                float angleDeg = Vector3.Angle(forward, dir);
                if (angleDeg > halfFov)
                {
                    //Debug.Log($"VisionPerceptionModule {worldObject.DisplayName}: Out of peripheral vision {target.DisplayName}.");
                    continue;
                }
                // LOS
                Vector3 aimPoint = targetPos + Vector3.up * targetAimHeight;
                Vector3 ray = aimPoint - eyePos;
                float rayLen = ray.magnitude;
                if (rayLen < 0.0001f)
                {
                    Debug.Log("rayLen too short.");
                    continue;
                }

                Vector3 rayDir = ray / rayLen;
                if (Physics.Raycast(eyePos, rayDir, rayLen, occluderMask, QueryTriggerInteraction.Ignore))
                {
                    //Debug.Log("Raycast unsuccessful.");
                    continue;
                }

                int key = target.GetInstanceID();
                visibleThisTick.Add(key);

                bool wasVisible = visibleLastTick.Contains(key);
                bool isNew = !wasVisible;

                float speed = EstimateSpeedFromHistory(target, key, deltaTime);
                VisionTargetKind kind = ClassifyKind(target);
                SocialRelation relation = GetRelation(worldObject, target);

                float sizeScore = EstimateSizeScore(target);
                float score = ComputeScore(dist, angleDeg, speed, sizeScore, kind, relation);

                //Debug.Log($"[{worldObject.DisplayName} Vision] Detected {target.DisplayName}");

                detections.Add(new VisionDetection
                {
                    target = target,
                    kind = kind,
                    relation = relation,
                    distance = dist,
                    angleDeg = angleDeg,
                    speed = speed,
                    sizeScore = sizeScore,
                    score = score,
                    isNewlySeen = isNew
                });

                // Update last seen
                lastSeen[key] = new LastSeenInfo
                {
                    lastPosWorld = targetPos,
                    lastSeenFrame = Time.frameCount
                };

                lastSeenTargetRef[key] = target;
            }

            // Emit TargetLostSight for anything that was visible last tick but not this tick
            // (keep it cheap + capped)
            int lostBudget = Mathf.Max(0, maxEvents - perceptionEvents.Count);
            if (lostBudget > 0)
            {
                foreach (var id in visibleLastTick)
                {
                    if (perceptionEvents.Count >= maxEvents) break;
                    if (visibleThisTick.Contains(id)) continue;

                    if (!lastSeen.TryGetValue(id, out var ls))
                        continue;

                    // Try to find the target ref; it may have been destroyed.
                    lastSeenTargetRef.TryGetValue(id, out var lastTarget);

                    // If destroyed, prune it (optional).
                    if (lastTarget == null)
                    {
                        lastSeenTargetRef.Remove(id);
                    }

                    // This is a "context" event; keep it moderate unless you want it to trigger reactions.
                    perceptionEvents.Add(PerceptionEvent.MakeVision(
                        observer: worldObject,
                        type: PerceptionEventType.TargetLostSight,
                        worldPos: ls.lastPosWorld,
                        target: lastTarget,
                        strength01: 0.35f,
                        novelty01: 0.8f,
                        interest01: 0.35f,
                        distanceMeters: 0f,
                        speedMps: 0f,
                        angleDeg: 0f,
                        kind: VisionTargetKind.Unknown,
                        relation: SocialRelation.NonPack));
                }
            }

            detections.Sort((a, b) => b.score.CompareTo(a.score));
            if (detections.Count > maxDetections)
                detections.RemoveRange(maxDetections, detections.Count - maxDetections);

            // Emit unified events
            for (int i = 0; i < detections.Count && perceptionEvents.Count < maxEvents; i++)
            {
                var d = detections[i];

                // Normalize some values to 0..1 for the PerceptionEvent fields
                float strength01 = Mathf.Clamp01(d.score); // already roughly 0..~2; clamp is fine for now
                float novelty01 = d.isNewlySeen ? 1f : 0f;
                float interest01 = Mathf.Clamp01(d.score); // same as strength for v1; you can separate later

                // Primary event type
                var type = d.isNewlySeen ? PerceptionEventType.TargetNewlySeen : PerceptionEventType.TargetSeen;

                perceptionEvents.Add(PerceptionEvent.MakeVision(
                    observer: worldObject,
                    type: type,
                    worldPos: d.target.pos3d_world,
                    target: d.target,
                    strength01: strength01,
                    novelty01: novelty01,
                    interest01: interest01,
                    distanceMeters: d.distance,
                    speedMps: d.speed,
                    angleDeg: d.angleDeg,
                    kind: d.kind,
                    relation: d.relation));

                // Motion tags (optional extra events)
                if (perceptionEvents.Count >= maxEvents) break;

                if (d.speed >= fastSpeedThreshold)
                {
                    perceptionEvents.Add(PerceptionEvent.MakeVision(
                        observer: worldObject,
                        type: PerceptionEventType.TargetMovingFast,
                        worldPos: d.target.pos3d_world,
                        target: d.target,
                        strength01: Mathf.Clamp01(d.speed / fastSpeedThreshold),
                        novelty01: 0f,
                        interest01: Mathf.Clamp01(d.score),
                        distanceMeters: d.distance,
                        speedMps: d.speed,
                        angleDeg: d.angleDeg,
                        kind: d.kind,
                        relation: d.relation));
                }
                else if (d.speed >= movingSpeedThreshold)
                {
                    perceptionEvents.Add(PerceptionEvent.MakeVision(
                        observer: worldObject,
                        type: PerceptionEventType.TargetMoving,
                        worldPos: d.target.pos3d_world,
                        target: d.target,
                        strength01: Mathf.Clamp01(d.speed / fastSpeedThreshold),
                        novelty01: 0f,
                        interest01: Mathf.Clamp01(d.score),
                        distanceMeters: d.distance,
                        speedMps: d.speed,
                        angleDeg: d.angleDeg,
                        kind: d.kind,
                        relation: d.relation));
                }
            }

            // Leader-not-visible event (cooldowned)
            if (perceptionEvents.Count < maxEvents)
                EmitLeaderNotVisible();
            
//            if (perceptionEvents.Count>0)
//                Debug.Log($"VisionPerceptionModule {worldObject.DisplayName}: #perceptionEvents = {perceptionEvents.Count}");
        }

        private void EmitLeaderNotVisible()
        {
            if (leaderNotVisibleCooldown > 0f)
                return;

            var list = worldObject.packMemberModule?.currentPack?.packAgentList;
            if (list == null || list.Count == 0)
                return;

            var leader = list[0];
            if (leader == null || leader == worldObject)
                return;

            bool leaderVisible = false;
            for (int i = 0; i < detections.Count; i++)
            {
                if (detections[i].target == leader)
                {
                    leaderVisible = true;
                    break;
                }
            }

            if (!leaderVisible)
            {
                leaderNotVisibleCooldown = leaderNotVisibleCooldownSeconds;

                perceptionEvents.Add(PerceptionEvent.MakeVision(
                    observer: worldObject,
                    type: PerceptionEventType.PackLeaderNotVisible,
                    worldPos: leader.pos3d_world,
                    target: leader,
                    strength01: 1f,
                    novelty01: 1f,
                    interest01: 1f,
                    distanceMeters: 0f,
                    speedMps: 0f,
                    angleDeg: 0f,
                    kind: VisionTargetKind.Dog,            // best guess; you can classify leader too
                    relation: SocialRelation.PackLeader));
            }
        }

        private float EstimateSpeedFromHistory(WorldObject target, int key, float dt)
        {
            if (dt <= 0f) return 0f;
            if (!lastSeen.TryGetValue(key, out var ls)) return 0f;

            Vector3 now = target.pos3d_world;
            Vector3 delta = now - ls.lastPosWorld;
            delta.y = 0f;

            float speed = delta.magnitude / dt;
            if (speed > 50f) speed = 50f;
            return speed;
        }

        private float EstimateSizeScore(WorldObject target)
        {
            var r = target.GetComponentInChildren<Renderer>();
            if (r == null) return 0.10f;
            var ext = r.bounds.extents;
            float approx = ext.x + ext.y + ext.z;
            return Mathf.Clamp01(approx / 2.0f);
        }

        private VisionTargetKind ClassifyKind(WorldObject target)
        {
            if (target.CompareTag("Dog")) return VisionTargetKind.Dog;
            if (target.CompareTag("Human")) return VisionTargetKind.Human;
            if (target.CompareTag("Animal")) return VisionTargetKind.Animal;
            if (target.CompareTag("Threat")) return VisionTargetKind.Threat;
            if (target.CompareTag("Item")) return VisionTargetKind.Item;
            return VisionTargetKind.Unknown;
        }

        private SocialRelation GetRelation(WorldObject self, WorldObject other)
        {
            if (self == other) return SocialRelation.Self;

            var list = self.packMemberModule?.currentPack?.packAgentList;
            if (list == null || list.Count == 0)
                return SocialRelation.NonPack;

            if (list[0] == other)
                return SocialRelation.PackLeader;

            for (int i = 1; i < list.Count; i++)
                if (list[i] == other) return SocialRelation.Packmate;

            return SocialRelation.NonPack;
        }

        private float ComputeScore(float distance, float angleDeg, float speed, float sizeScore, VisionTargetKind kind, SocialRelation relation)
        {
            float movement = Mathf.Clamp01(speed / fastSpeedThreshold);
            float distScore = 1f / (1f + distance);

            float halfFov = peripheralFovDeg * 0.5f;
            float angleScore = halfFov > 0.01f ? Mathf.Clamp01(1f - (angleDeg / halfFov)) : 0f;

            float kindBonus = kind switch
            {
                VisionTargetKind.Threat => 0.50f,
                VisionTargetKind.Dog => 0.30f,
                VisionTargetKind.Human => 0.25f,
                VisionTargetKind.Animal => 0.20f,
                VisionTargetKind.Item => 0.10f,
                _ => 0.05f
            };

            float relationBonus = relation switch
            {
                SocialRelation.PackLeader => 0.50f,
                SocialRelation.Packmate => 0.20f,
                SocialRelation.NonPack => 0.10f,
                _ => 0f
            };

            return (0.45f * movement) + (0.25f * distScore) + (0.10f * angleScore) + (0.10f * sizeScore) + kindBonus + relationBonus;
        }
    }
}