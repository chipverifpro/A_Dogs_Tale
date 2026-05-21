using System.Collections.Generic;
using UnityEngine;
using DogGame.Noise;
using InspectorTools;

namespace DogGame.Modules
{
    /// <summary>
    /// Per-agent listener module. Queries NoiseManager for recent NoiseEvents and produces HeardNoise results.
    /// First pass: distance + room penalties + basic voice targeting. LOS/occlusion comes next.
    /// </summary>
    [InspectorNote("Sensory_Modules/Hearing Module", "Generate events for sounds heard.")]
    [DisallowMultipleComponent]
    public class HearingModule : WorldModule
    {
        [Header("Update rate")]
        [SerializeField] private bool useTick = true;
        [SerializeField] private float tickIntervalSeconds = 0.25f;

        [Header("Hearing thresholds")]
        [SerializeField] private float hearingWindowSeconds = 8f;
        [SerializeField] private float hearThresholdNormalized = 0.08f;       // threshold in normalized 0..1 units
        [SerializeField] private float normalizationScale = 3.0f;             // converts raw loudness to 0..1

        [Header("Self-noise filtering")]
        [SerializeField] private bool suppressSelfNoise = true;

        [SerializeField] private bool allowSelfDistress = true;

        [Tooltip("Allow self-generated noises with priority >= this (e.g., door slam, glass shatter).")]
        [SerializeField] private int allowSelfPriorityAtOrAbove = 7;

        [Tooltip("Always suppress self movement noises (prevents spam).")]
        [SerializeField] private bool alwaysSuppressSelfFootsteps = true;
        
        [Header("Pack noise filtering")]
        [SerializeField] private bool suppressPackFootsteps = true;

        [Tooltip("Only applies to movement noises. Other pack noises still come through.")]
        [SerializeField] private bool suppressOnlyMovementFromPack = true;

        [Header("Room penalties")]
        [SerializeField] private float sameRoomFactor = 1.0f;
        [SerializeField] private float adjacentRoomFactor = 0.75f;
        [SerializeField] private float differentRoomFactor = 0.40f;

        [Header("Ranking / caps")]
        [SerializeField] private int maxRawHeard = 24;
        [SerializeField] private int maxLLMItems = 8;
        [SerializeField] private int maxPerceptionEvents = 8;

        [Header("Listener state")]
        [Range(0.1f, 3f)]
        [SerializeField] private float sensitivity = 1.0f; // global multiplier

        [Header("Movement trend")]
        [SerializeField] private float movementTrendMemorySeconds = 0.5f;
        [SerializeField] private float movementTrendDistanceThresholdMeters = 0.05f;

        // Persistent query cursor
        private ulong lastSeenNoiseId = 0;

        // Timer for non-every-frame updates
        private float tickAccumulatorSeconds = 0f;

        // Scratch lists to avoid allocations
        private readonly List<NoiseEvent> scratchEvents = new(64);
        private readonly List<HeardNoise> currentHeard = new(32);
        private readonly List<HeardNoise> summarizedForLLM = new(16);
        private readonly Dictionary<string, MovementTrendSample> movementTrendBySource = new(64);

        // Public outputs
        public IReadOnlyList<HeardNoise> CurrentHeardNoises => currentHeard;
        public NoiseSummaryForLLM CurrentLLMSummary { get; private set; }
        public readonly List<PerceptionEvent> perceptionEvents = new();

        public List<PerceptionEvent> GetPerceptionEvents() => perceptionEvents;
        public void ClearPerceptionEvents() => perceptionEvents.Clear();
        public bool DisplayHearingSummary = true;
        
        private int debugDoubleTick = -1;
        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            if (!useTick) return;

            tickAccumulatorSeconds += deltaTime;
            if (tickAccumulatorSeconds < tickIntervalSeconds)
                return;

            tickAccumulatorSeconds = 0f;
            UpdateHearing(); // no deltaTime param (rule compliance)
        }

        protected override void Update()
        {
            base.Update();

            if (useTick) return;

            // If not using Tick, run at the same cadence in Update.
            tickAccumulatorSeconds += Time.deltaTime;
            if (tickAccumulatorSeconds < tickIntervalSeconds)
                return;

            tickAccumulatorSeconds = 0f;
            UpdateHearing();
        }

        /// <summary>
        /// Linear hearing update pipeline: query -> filter -> rank -> export.
        /// </summary>
        private void UpdateHearing()
        {
            currentHeard.Clear();
            perceptionEvents.Clear();

            if (NoiseManager.Instance == null)
            {
                CurrentLLMSummary = default;
                return;
            }

            NoiseManager.Instance.GetEventsAfter(lastSeenNoiseId, scratchEvents);
            if (scratchEvents.Count == 0)
            {
                CurrentLLMSummary = default;
                return;
            }

            // Advance lastSeenNoiseId to newest returned (they are chronological)
            lastSeenNoiseId = scratchEvents[scratchEvents.Count - 1].noiseId;

            float now = Time.time;
            int listenerRoomId = worldObject?.locationModule?.cell?.room_number ?? -1;
            Vector3 listenerPos = worldObject != null ? worldObject.transform.position : transform.position;

            int listenerId = NoiseIdUtil.GetWorldObjectIdOrUnknown(worldObject);

            // 1) Build raw heard list (cheap filter)
            for (int i = 0; i < scratchEvents.Count; i++)
            {
                NoiseEvent evt = scratchEvents[i];
                if (ShouldIgnoreNoiseEvent(evt, listenerId))
                    continue;

                // Age gate
                float age = now - evt.timeSeconds;
                if (age < 0f) age = 0f;
                if (age > hearingWindowSeconds) continue;

                // Distance
                float distanceMeters = Vector3.Distance(listenerPos, evt.position);

                // Fast range gate (hint-based)
                float rangeHint = evt.effectiveRangeHintMeters;
                if (rangeHint > 0f && distanceMeters > rangeHint * 1.25f)
                    continue;

                // Attenuation (simple: 1 / max(d,1)^2)
                float d = Mathf.Max(1f, distanceMeters);
                float distanceAtten = 1f / (d * d);

                float roomFactor = ComputeRoomFactor(listenerRoomId, evt.roomId);
                float rawPerceived = evt.sourceLoudnessAtOneMeter * distanceAtten * roomFactor * sensitivity;

                float perceived01 = Mathf.Clamp01(rawPerceived / Mathf.Max(0.0001f, normalizationScale));
                if (perceived01 < hearThresholdNormalized && evt.priority < 5)
                    continue;

                // Build heard record
                HeardNoise heard = new HeardNoise
                {
                    noiseId = evt.noiseId,
                    timeHeardSeconds = now,
                    timeAgoSeconds = age,

                    category = evt.category,
                    subtype = evt.subtype,
                    semanticTags = evt.semanticTags,

                    perceivedLoudness01 = perceived01,
                    audibilityScore = ComputeAudibilityScore(perceived01, evt.category, evt.priority),

                    distanceMeters = distanceMeters,
                    distanceBand = ComputeDistanceBand(distanceMeters),
                    directionToSource = ComputeDirection(listenerPos, evt.position),

                    sourceRoomId = evt.roomId,
                    roomRelation = ComputeRoomRelation(listenerRoomId, evt.roomId),
                    occlusion01 = 0f, // LOS pass later
                    confidence01 = ComputeConfidence(perceived01, listenerRoomId, evt.roomId),

                    // Attribution baseline
                    attributionType = SourceAttributionType.Unknown,
                    attributedEmitterId = evt.emitterId,
                    attributedEmitterRef = evt.emitterRef,
                    attributionConfidence01 = evt.emitterRef != null ? 0.6f : 0.2f,

                    // Voice targeting
                    isIntendedForMe = false,
                    intendedConfidence01 = 0f,
                    speechAct = evt.category == NoiseCategory.Voice ? evt.voiceIntent.speechAct : NoiseSpeechAct.Neutral,
                    heardContentShort = evt.category == NoiseCategory.Voice ? evt.voiceIntent.contentShort : string.Empty,
                    notesShort = string.Empty
                };

                if (evt.category == NoiseCategory.Voice)
                {
                    ComputeVoiceTargeting(evt, listenerPos, ref heard);
                }

                currentHeard.Add(heard);

                if (currentHeard.Count >= maxRawHeard)
                    break;
            }

            // 2) Summarize + cap for LLM
            NoiseSummarizer.SummarizeForLLM(currentHeard, maxLLMItems, summarizedForLLM);
            ApplyMovementTrendMemory(summarizedForLLM);
            CurrentLLMSummary = BuildLLMSummary(summarizedForLLM, listenerRoomId, hearingWindowSeconds);
            BuildPerceptionEvents(summarizedForLLM, listenerPos);
            
            foreach (var h in summarizedForLLM)
            {
                worldObject.worldMemoryModule?.RecordHeardNoise(h);

                string direction = DirectionTokenFromVector(h.directionToSource);
                string movement = GetMovementDescription(h.subtype);
                string sourceRoomName = ResolveRoomName(h.sourceRoomId);
                string hearingEventString = 
                    $"[{worldObject.DisplayName}] heard " +
                    $"{h.category}/{h.subtype} " +
                    $"from {GetSourceNameForDebug(h)} " +
                    $"dir={direction} " +
                    $"{(string.IsNullOrEmpty(movement) ? "" : $"movement={movement} ")}" +
                    $"loud={h.perceivedLoudness01:0.00} " +
                    $"dist={h.distanceMeters:0.0}m " +
                    $"room={sourceRoomName} " +
                    $"{(string.IsNullOrEmpty(h.notesShort) ? "" : $"notes={h.notesShort}")}";
                
                if (DisplayHearingSummary)
                    Debug.Log(hearingEventString);
                //BottomBanner.LogMessage(BannerSense.Hearing, BannerLevel.High, hearingEventString, true);
            }            
            //if (summarizedForLLM.Count > 0)
            //    Debug.Log($"[{worldObject.DisplayName}] Heard: {summarizedForLLM[0].category}/{summarizedForLLM[0].subtype} loud={summarizedForLLM[0].perceivedLoudness01:0.00} notes={summarizedForLLM[0].notesShort}");
        }

        private void BuildPerceptionEvents(List<HeardNoise> ranked, Vector3 listenerPos)
        {
            if (ranked == null || ranked.Count == 0)
                return;

            int maxEvents = Mathf.Max(1, maxPerceptionEvents);
            int count = Mathf.Min(maxEvents, ranked.Count);

            for (int i = 0; i < count; i++)
            {
                var h = ranked[i];

                Vector3 eventPos = listenerPos;
                if (h.directionToSource != Vector3.zero)
                    eventPos = listenerPos + (h.directionToSource * Mathf.Max(0f, h.distanceMeters));

                PerceptionEventType type =
                    (h.category == NoiseCategory.Voice && h.subtype == NoiseSubtype.Bark)
                        ? PerceptionEventType.BarkHeard
                        : PerceptionEventType.LoudNoise;

                float novelty01 = 1f - Mathf.Clamp01(h.timeAgoSeconds / Mathf.Max(0.001f, hearingWindowSeconds));

                perceptionEvents.Add(PerceptionEvent.MakeSound(
                    observer: worldObject,
                    type: type,
                    worldPos: eventPos,
                    target: h.attributedEmitterRef,
                    strength01: h.perceivedLoudness01,
                    novelty01: novelty01,
                    interest01: Mathf.Clamp01(h.audibilityScore),
                    loudness01: h.perceivedLoudness01,
                    distanceMeters: h.distanceMeters,
                    category: h.category,
                    subtype: h.subtype,
                    addressedToMe: h.isIntendedForMe,
                    addressedConfidence01: h.intendedConfidence01));
            }
        }

        private float ComputeRoomFactor(int listenerRoomId, int sourceRoomId)
        {
            if (listenerRoomId < 0 || sourceRoomId < 0) return 1f;

            if (listenerRoomId == sourceRoomId) return sameRoomFactor;

            // Placeholder "adjacent": room numbers +/- 1. Replace with real portal adjacency later.
            if (Mathf.Abs(listenerRoomId - sourceRoomId) == 1) return adjacentRoomFactor;

            return differentRoomFactor;
        }

        private RoomRelation ComputeRoomRelation(int listenerRoomId, int sourceRoomId)
        {
            if (listenerRoomId < 0 || sourceRoomId < 0) return RoomRelation.Unknown;
            if (listenerRoomId == sourceRoomId) return RoomRelation.SameRoom;
            if (Mathf.Abs(listenerRoomId - sourceRoomId) == 1) return RoomRelation.Adjacent;
            return RoomRelation.Different;
        }

        private static DistanceBand ComputeDistanceBand(float distanceMeters)
        {
            if (distanceMeters < 6f) return DistanceBand.Near;
            if (distanceMeters < 18f) return DistanceBand.Mid;
            return DistanceBand.Far;
        }

        private static Vector3 ComputeDirection(Vector3 listenerPos, Vector3 sourcePos)
        {
            Vector3 delta = sourcePos - listenerPos;
            float mag = delta.magnitude;
            if (mag < 0.0001f) return Vector3.zero;
            return delta / mag;
        }

        private float ComputeAudibilityScore(float perceived01, NoiseCategory category, int priority)
        {
            float categoryWeight = category switch
            {
                NoiseCategory.Voice => 1.3f,
                NoiseCategory.Impact => 1.15f,
                NoiseCategory.Mechanism => 1.1f,
                NoiseCategory.Movement => 1.0f,
                NoiseCategory.Ambient => 0.3f,
                _ => 1.0f
            };

            float priorityWeight = 1f + Mathf.Clamp(priority, 0, 10) * 0.08f;
            return perceived01 * categoryWeight * priorityWeight;
        }

        private float ComputeConfidence(float perceived01, int listenerRoomId, int sourceRoomId)
        {
            // First pass confidence: mainly loudness + same-room boost
            float baseConf = Mathf.Clamp01(perceived01 * 1.25f);
            if (listenerRoomId > 0 && sourceRoomId > 0 && listenerRoomId != sourceRoomId)
                baseConf *= 0.85f;
            return baseConf;
        }

        private void ComputeVoiceTargeting(in NoiseEvent evt, Vector3 listenerPos, ref HeardNoise heard)
        {
            // 1) Explicit target
            if (evt.voiceIntent.targetRef != null && evt.voiceIntent.targetRef == worldObject)
            {
                heard.isIntendedForMe = true;
                heard.intendedConfidence01 = 1.0f;
                heard.notesShort = AppendNote(heard.notesShort, "addressed to me (explicit)");
                return;
            }

            // 2) If emitter unknown, can't infer much
            if (evt.emitterRef == null || evt.voiceIntent.targetingMode == VoiceTargetingMode.Broadcast)
            {
                heard.isIntendedForMe = false;
                heard.intendedConfidence01 = 0f;
                return;
            }

            // 3) Heuristic inference (simple)
            float score = 0f;

            // Speech act bias
            if (evt.voiceIntent.speechAct == NoiseSpeechAct.Call ||
                evt.voiceIntent.speechAct == NoiseSpeechAct.Scold ||
                evt.voiceIntent.speechAct == NoiseSpeechAct.Request ||
                evt.voiceIntent.speechAct == NoiseSpeechAct.Threaten)
            {
                score += 0.1f;
            }

            // Facing cone: if speaker faces listener within ~60 degrees
            Vector3 speakerPos = evt.emitterRef.transform.position;
            Vector3 toListener = (listenerPos - speakerPos);
            float dist = toListener.magnitude;
            if (dist > 0.001f)
            {
                Vector3 toListenerNorm = toListener / dist;
                float facingDot = Vector3.Dot(evt.emitterRef.transform.forward, toListenerNorm);
                if (facingDot > 0.5f) score += 0.35f; // ~60deg cone
            }

            // Proximity: close voice more likely directed
            if (heard.distanceMeters < 5f) score += 0.25f;
            else if (heard.distanceMeters < 10f) score += 0.15f;

            // Clarity gating: both emitter clarity and our perceived loudness
            float clarity = Mathf.Clamp01(evt.voiceIntent.clarity <= 0f ? 1f : evt.voiceIntent.clarity);
            score *= Mathf.Lerp(0.4f, 1.0f, clarity);
            score *= Mathf.Lerp(0.3f, 1.0f, heard.perceivedLoudness01);

            heard.intendedConfidence01 = Mathf.Clamp01(score);
            heard.isIntendedForMe = heard.intendedConfidence01 >= 0.65f;

            if (heard.isIntendedForMe)
                heard.notesShort = AppendNote(heard.notesShort, "addressed to me (inferred)");
        }

        private static string AppendNote(string existing, string note)
        {
            if (string.IsNullOrWhiteSpace(existing)) return note;
            if (existing.Contains(note)) return existing;
            return $"{existing}; {note}";
        }

        private static void BuildRankedListForLLM(List<HeardNoise> raw, List<HeardNoise> rankedOut, int maxItems)
        {
            rankedOut.Clear();
            if (raw == null || raw.Count == 0) return;

            rankedOut.AddRange(raw);
            rankedOut.Sort((a, b) => b.audibilityScore.CompareTo(a.audibilityScore));

            if (rankedOut.Count > maxItems)
                rankedOut.RemoveRange(maxItems, rankedOut.Count - maxItems);
        }

        private NoiseSummaryForLLM BuildLLMSummary(List<HeardNoise> ranked, int listenerRoomId, float windowSeconds)
        {
            // Build compact list. No dedup yet; that’s the next module (NoiseSummarizer).
            var items = new List<HeardNoiseForLLM>(ranked.Count);

            for (int i = 0; i < ranked.Count; i++)
            {
                HeardNoise h = ranked[i];

                string type = $"{h.category}/{h.subtype}";
                string dirToken = DirectionTokenFromVector(h.directionToSource);
                string distToken = h.distanceBand switch
                {
                    DistanceBand.Near => "near",
                    DistanceBand.Mid => "mid",
                    _ => "far"
                };
                string roomToken = ResolveRoomName(h.sourceRoomId);

                string sourceToken = "unknown";
                if (h.attributedEmitterRef != null)
                    sourceToken = "known:" + NoiseIdUtil.GetWorldObjectNameOrUnknown(h.attributedEmitterRef);
                else if (NoiseIdUtil.IsValidWorldObjectId(h.attributedEmitterId))
                    sourceToken = "guess:id" + h.attributedEmitterId;

                items.Add(new HeardNoiseForLLM
                {
                    timeAgoSeconds = h.timeAgoSeconds,
                    type = type,
                    loudness01 = h.perceivedLoudness01,
                    direction = dirToken,
                    distance = distToken,
                    room = roomToken,
                    source = sourceToken,
                    confidence01 = h.confidence01,

                    addressedToMe = h.isIntendedForMe,
                    addressedConfidence01 = h.intendedConfidence01,
                    heardWordsShort = h.heardContentShort ?? string.Empty,
                    speechAct = h.speechAct.ToString(),

                    notesShort = h.notesShort ?? string.Empty,
                    tags = h.semanticTags.ToString()
                });
            }

            return new NoiseSummaryForLLM
            {
                listenerAgentId = NoiseIdUtil.GetWorldObjectIdOrUnknown(worldObject),
                listenerRoomId = listenerRoomId,
                listenerState = "unknown", // wire to your AI/alertness state later
                timeWindowSeconds = windowSeconds,
                heard = items
            };
        }

        private string DirectionTokenFromVector(Vector3 dir)
        {
            if (dir == Vector3.zero) return "here";

            Transform listenerTransform = worldObject != null ? worldObject.transform : transform;
            Vector3 localDir = listenerTransform != null ? listenerTransform.InverseTransformDirection(dir) : dir;
            localDir.y = 0f;
            if (localDir.sqrMagnitude < 0.0001f) return "here";

            float angle = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg; // yaw degrees, 0=listener forward
            if (angle < 0f) angle += 360f;

            if (angle < 22.5f || angle >= 337.5f) return "front";
            if (angle < 67.5f) return "front-right";
            if (angle < 112.5f) return "right";
            if (angle < 157.5f) return "back-right";
            if (angle < 202.5f) return "back";
            if (angle < 247.5f) return "back-left";
            if (angle < 292.5f) return "left";
            return "front-left";
        }

        private string ResolveRoomName(int roomId)
        {
            if (roomId < 0)
                return "unknown";

            List<Room> rooms = null;
            if (worldObject != null && worldObject.dir != null && worldObject.dir.gen != null)
                rooms = worldObject.dir.gen.rooms;
            else if (Dir.Instance != null && Dir.Instance.gen != null)
                rooms = Dir.Instance.gen.rooms;

            if (rooms == null || roomId >= rooms.Count || rooms[roomId] == null)
                return $"Room {roomId}";

            Room room = rooms[roomId];
            if (!string.IsNullOrWhiteSpace(room.name) && !IsRawGeneratedRoomName(room.name, roomId))
                return room.name.Trim();

            return ResolveSemanticRoomName(rooms, roomId);
        }

        private string ResolveSemanticRoomName(List<Room> rooms, int roomId)
        {
            if (rooms == null || roomId < 0 || roomId >= rooms.Count || rooms[roomId] == null)
                return $"Room {roomId}";

            DungeonSettings settings = null;
            if (worldObject != null && worldObject.dir != null)
                settings = worldObject.dir.cfg != null ? worldObject.dir.cfg : worldObject.dir.gen != null ? worldObject.dir.gen.cfg : null;
            else if (Dir.Instance != null)
                settings = Dir.Instance.cfg != null ? Dir.Instance.cfg : Dir.Instance.gen != null ? Dir.Instance.gen.cfg : null;

            string label = RoomUseAssigner.GetRoomLabel(rooms[roomId], settings);
            int count = 0;
            for (int i = 0; i <= roomId && i < rooms.Count; i++)
            {
                Room candidate = rooms[i];
                if (candidate == null)
                    continue;

                if (RoomUseAssigner.GetRoomLabel(candidate, settings) == label)
                    count++;
            }

            return $"{label} {Mathf.Max(1, count)}";
        }

        private static bool IsRawGeneratedRoomName(string roomName, int roomId)
        {
            if (string.IsNullOrWhiteSpace(roomName))
                return true;

            string trimmed = roomName.Trim();
            return trimmed == $"Room {roomId}" || trimmed == $"Room {roomId + 1}";
        }

        private static string GetMovementDescription(NoiseSubtype subtype)
        {
            switch (subtype)
            {
                case NoiseSubtype.FootstepWalk:
                    return "walking";
                case NoiseSubtype.FootstepRun:
                    return "running";
                case NoiseSubtype.SneakStep:
                    return "sneaking";
                case NoiseSubtype.Scurry:
                    return "scurrying";
                case NoiseSubtype.WheelRoll:
                    return "rolling";
                case NoiseSubtype.RotorWhirr:
                    return "whirring";
                case NoiseSubtype.WingFlap:
                    return "flapping";
                case NoiseSubtype.Hover:
                    return "hovering";
                case NoiseSubtype.MechanicalWhirr:
                    return "mechanical whirring";
                default:
                    return string.Empty;
            }
        }

        private bool ShouldIgnoreNoiseEvent(in NoiseEvent evt, int listenerId)
        {
            // --- self filtering ---
            if (suppressSelfNoise)
            {
                bool isSelf =
                    (evt.emitterRef != null && evt.emitterRef == worldObject) ||
                    (listenerId > 0 && evt.emitterId == listenerId);

                if (isSelf)
                {
                    // Always suppress self movement noise.
                    if (alwaysSuppressSelfFootsteps && IsMovementNoise(evt.category, evt.subtype))
                        return true;

                    // Allow self distress sounds
                    if (allowSelfDistress && (evt.semanticTags & NoiseSemanticTags.Distress) != 0)
                        return false;

                    // Allow very high-priority self noises
                    if (evt.priority >= allowSelfPriorityAtOrAbove)
                        return false;

                    return true;
                }
            }

            // --- pack filtering (new) ---
            if (suppressPackFootsteps && evt.emitterRef != null)
            {
                // If it's a pack-mate, suppress movement noises.
                if(worldObject.packMemberModule!=null && evt.emitterRef.packMemberModule!=null)
                {
                    if (worldObject.packMemberModule.currentPack == evt.emitterRef.packMemberModule.currentPack)
                    {
                        if (!suppressOnlyMovementFromPack)
                            return true;

                        if (IsMovementNoise(evt.category, evt.subtype))
                            return true;
                    }
                }
            }

            return false;
        }

        private static string GetSourceNameForDebug(in HeardNoise heard)
        {
            if (heard.attributedEmitterRef != null)
                return heard.attributedEmitterRef.DisplayName;

            if (heard.attributedEmitterId > 0)
                return $"id:{heard.attributedEmitterId}";

            return "unknown";
        }

        private void ApplyMovementTrendMemory(List<HeardNoise> heardList)
        {
            if (heardList == null || heardList.Count == 0)
                return;

            float now = Time.time;
            float threshold = Mathf.Max(0.01f, movementTrendDistanceThresholdMeters);

            for (int i = 0; i < heardList.Count; i++)
            {
                HeardNoise heard = heardList[i];
                if (!IsMovementTrackedSubtype(heard.subtype) || heard.attributedEmitterId <= 0)
                    continue;

                string key = GetMovementTrendKey(heard);
                string trendLabel = GetMovementTrendLabel(heard.subtype);
                if (movementTrendBySource.TryGetValue(key, out MovementTrendSample previous) &&
                    now - previous.timeSeconds <= movementTrendMemorySeconds)
                {
                    float distanceDelta = heard.distanceMeters - previous.distanceMeters;
                    if (distanceDelta < -threshold)
                    {
                        heard.notesShort = AppendNote(heard.notesShort, $"{trendLabel} approaching");
                    }
                    else if (distanceDelta > threshold)
                    {
                        heard.notesShort = AppendNote(heard.notesShort, $"{trendLabel} receding");
                    }
                    else
                    {
                        heard.notesShort = AppendNote(heard.notesShort, $"{trendLabel} nearby");
                    }

                    heardList[i] = heard;
                }

                movementTrendBySource[key] = new MovementTrendSample
                {
                    distanceMeters = heard.distanceMeters,
                    timeSeconds = now
                };
            }
        }

        private static bool IsMovementTrackedSubtype(NoiseSubtype subtype)
        {
            return subtype == NoiseSubtype.FootstepWalk
                || subtype == NoiseSubtype.FootstepRun
                || subtype == NoiseSubtype.SneakStep
                || subtype == NoiseSubtype.Scurry
                || subtype == NoiseSubtype.WheelRoll
                || subtype == NoiseSubtype.RotorWhirr
                || subtype == NoiseSubtype.WingFlap
                || subtype == NoiseSubtype.Hover
                || subtype == NoiseSubtype.MechanicalWhirr;
        }

        private static bool IsMovementNoise(NoiseCategory category, NoiseSubtype subtype)
        {
            return category == NoiseCategory.Movement || IsMovementTrackedSubtype(subtype);
        }

        private static string GetMovementTrendLabel(NoiseSubtype subtype)
        {
            switch (subtype)
            {
                case NoiseSubtype.WheelRoll:
                    return "rolling";
                case NoiseSubtype.RotorWhirr:
                    return "rotor whirr";
                case NoiseSubtype.WingFlap:
                    return "wing flaps";
                case NoiseSubtype.Hover:
                    return "hovering";
                case NoiseSubtype.MechanicalWhirr:
                    return "mechanical whirr";
                default:
                    return "footsteps";
            }
        }

        private static string GetMovementTrendKey(in HeardNoise heard)
        {
            return $"{heard.attributedEmitterId}:{heard.subtype}";
        }

        private struct MovementTrendSample
        {
            public float distanceMeters;
            public float timeSeconds;
        }
    }
}
