using UnityEngine;
using DogGame.Noise;
using InspectorTools;

namespace DogGame.Modules
{
    [InspectorNote("Output_Modules/Noise Maker Module", "Generate events for footsteps and barks.")]

    [DisallowMultipleComponent]
    public class NoiseMakerModule : WorldModule
    {
        private int debugDoubleTick = -1;

        [Header("Movement Noise (auto emit)")]
        [SerializeField] private bool emitFootsteps = true;

        [Tooltip("Ignore tiny jitter. Movement below this speed emits no movement noise.")]
        [SerializeField] private float minSpeedMetersPerSecond = 0.10f;

        [Header("Movement Noise Classification")]
        [Tooltip("Category emitted by automatic movement noise. Dogs usually use Movement; robots may use Mechanism.")]
        [SerializeField] private NoiseCategory movementNoiseCategory = NoiseCategory.Movement;
        [SerializeField] private NoiseSemanticTags movementSemanticTags = NoiseSemanticTags.None;
        [SerializeField] private string movementProfileIdPrefix = "Auto.Movement";

        [Header("Movement Noise Subtype by WalkMode")]
        [SerializeField] private NoiseSubtype movementSubtypeWalk = NoiseSubtype.FootstepWalk;
        [SerializeField] private NoiseSubtype movementSubtypeRun = NoiseSubtype.FootstepRun;
        [SerializeField] private NoiseSubtype movementSubtypeSneak = NoiseSubtype.SneakStep;
        [SerializeField] private NoiseSubtype movementSubtypeCautious = NoiseSubtype.SneakStep;
        [SerializeField] private NoiseSubtype movementSubtypeCrawl = NoiseSubtype.SneakStep;
        [SerializeField] private NoiseSubtype movementSubtypeStrafe = NoiseSubtype.FootstepWalk;
        [SerializeField] private NoiseSubtype movementSubtypeBackpedal = NoiseSubtype.FootstepWalk;

        [Header("Step distance by WalkMode (meters per step)")]
        [SerializeField] private float stepDistanceWalk = 0.85f;
        [SerializeField] private float stepDistanceRun = 0.55f;
        [SerializeField] private float stepDistanceSneak = 1.10f;
        [SerializeField] private float stepDistanceCautious = 1.00f;
        [SerializeField] private float stepDistanceCrawl = 1.20f;
        [SerializeField] private float stepDistanceStrafe = 0.90f;
        [SerializeField] private float stepDistanceBackpedal = 0.95f;

        [Header("Loudness by WalkMode (at 1m)")]
        [SerializeField] private float loudnessWalk = 0.60f;
        [SerializeField] private float loudnessRun = 1.10f;
        [SerializeField] private float loudnessSneak = 0.35f;
        [SerializeField] private float loudnessCautious = 0.45f;
        [SerializeField] private float loudnessCrawl = 0.25f;
        [SerializeField] private float loudnessStrafe = 0.65f;
        [SerializeField] private float loudnessBackpedal = 0.65f;

        [Header("Range hint by WalkMode (meters)")]
        [SerializeField] private float rangeWalk = 12f;
        [SerializeField] private float rangeRun = 18f;
        [SerializeField] private float rangeSneak = 6f;
        [SerializeField] private float rangeCautious = 9f;
        [SerializeField] private float rangeCrawl = 5f;
        [SerializeField] private float rangeStrafe = 12f;
        [SerializeField] private float rangeBackpedal = 12f;

        [Header("Bark Noise")]
        [SerializeField] private string barkClipName = "Bark";
        [SerializeField] private float barkLoudnessAtFullVolume = 1.6f;
        [SerializeField] private float barkRangeAtFullVolume = 24f;

        private MotionModule motionModule;

        private Vector3 lastPositionWorld;
        private bool hasLastPosition = false;
        private float accumulatedStepDistance = 0f;

        protected override void Awake()
        {
            base.Awake();
            motionModule = GetComponent<MotionModule>();
        }

        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            if (!emitFootsteps) return;
            if (worldObject == null) return;
            if (worldObject.locationModule == null) return;
            if (!Dir.Instance.gen.buildComplete) return;

            EmitFootsteps(deltaTime);
        }

        private void EmitFootsteps(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            Vector3 currentPos = worldObject.locationModule.pos3d_world;

            if (!hasLastPosition)
            {
                lastPositionWorld = currentPos;
                hasLastPosition = true;
                accumulatedStepDistance = 0f;
                return;
            }

            // Horizontal delta distance
            Vector3 delta = currentPos - lastPositionWorld;
            delta.y = 0f;
            float distanceMoved = delta.magnitude;
            lastPositionWorld = currentPos;

            if (distanceMoved <= 0f) return;

            float speed = distanceMoved / deltaTime;

            // Prefer MotionModule velocity if available (more accurate than position delta)
            if (motionModule != null)
            {
                // If you add HorizontalVelocity accessor, use it here:
                Vector3 hv = motionModule.HorizontalVelocity;
                speed = new Vector2(hv.x, hv.z).magnitude;
            }

            if (speed < minSpeedMetersPerSecond)
                return;

            WalkMode mode = WalkMode.Walk;
            if (motionModule != null)
                mode = motionModule.currentWalkMode;

            if (mode == WalkMode.None)
                return;

            GetFootstepParamsForMode(mode, out NoiseSubtype subtype, out float stepDistance, out float loudness, out float rangeHint);

            accumulatedStepDistance += distanceMoved;

            while (accumulatedStepDistance >= stepDistance)
            {
                accumulatedStepDistance -= stepDistance;

                NoiseProfile profile = new NoiseProfile
                {
                    profileId = BuildMovementProfileId(mode, subtype),
                    category = movementNoiseCategory,
                    subtype = subtype,
                    semanticTags = movementSemanticTags,

                    sourceLoudnessAtOneMeter = loudness,
                    effectiveRangeHintMeters = rangeHint,
                    priority = 0,
                    impulseIntervalSeconds = 0f
                };

                Emit(profile);
            }
        }

        private string BuildMovementProfileId(WalkMode mode, NoiseSubtype subtype)
        {
            string prefix = string.IsNullOrWhiteSpace(movementProfileIdPrefix)
                ? "Auto.Movement"
                : movementProfileIdPrefix.Trim();

            return $"{prefix}.{mode}.{subtype}";
        }

        private void GetFootstepParamsForMode(
            WalkMode mode,
            out NoiseSubtype subtype,
            out float stepDistance,
            out float loudness,
            out float rangeHint)
        {
            // Default values
            subtype = movementSubtypeWalk;
            stepDistance = stepDistanceWalk;
            loudness = loudnessWalk;
            rangeHint = rangeWalk;

            switch (mode)
            {
                case WalkMode.Run:
                    subtype = movementSubtypeRun;
                    stepDistance = stepDistanceRun;
                    loudness = loudnessRun;
                    rangeHint = rangeRun;
                    break;

                case WalkMode.Sneak:
                    subtype = movementSubtypeSneak;
                    stepDistance = stepDistanceSneak;
                    loudness = loudnessSneak;
                    rangeHint = rangeSneak;
                    break;

                case WalkMode.Cautious:
                    subtype = movementSubtypeCautious;
                    stepDistance = stepDistanceCautious;
                    loudness = loudnessCautious;
                    rangeHint = rangeCautious;
                    break;

                case WalkMode.Crawl:
                    subtype = movementSubtypeCrawl;
                    stepDistance = stepDistanceCrawl;
                    loudness = loudnessCrawl;
                    rangeHint = rangeCrawl;
                    break;

                case WalkMode.Strafe:
                    subtype = movementSubtypeStrafe;
                    stepDistance = stepDistanceStrafe;
                    loudness = loudnessStrafe;
                    rangeHint = rangeStrafe;
                    break;

                case WalkMode.Backpedal:
                    subtype = movementSubtypeBackpedal;
                    stepDistance = stepDistanceBackpedal;
                    loudness = loudnessBackpedal;
                    rangeHint = rangeBackpedal;
                    break;

                case WalkMode.Walk:
                default:
                    // already set
                    break;
            }
        }

        // --- Existing Emit API ---
        public ulong Emit(
            in NoiseProfile profile,
            Vector3? positionOverride = null,
            VoiceIntentData? voiceIntentOverride = null)
        {
            if (!profile.IsValid)
            {
                Debug.LogWarning("[NoiseMakerModule] Emit called with invalid NoiseProfile.");
                return 0;
            }

            if (NoiseManager.Instance == null)
            {
                Debug.LogWarning("[NoiseMakerModule] No NoiseManager in scene; cannot emit noise.");
                return 0;
            }

            Vector3 emissionPosition = positionOverride ?? worldObject.transform.position;

            int roomId = worldObject.locationModule.cell?.room_number ?? -1;
            int emitterId = NoiseIdUtil.GetWorldObjectIdOrUnknown(worldObject);

            var noiseEvent = new NoiseEvent
            {
                timeSeconds = Time.time,

                emitterId = emitterId,
                emitterRef = worldObject,

                category = profile.category,
                subtype = profile.subtype,
                semanticTags = profile.semanticTags,

                position = emissionPosition,
                roomId = roomId,

                sourceLoudnessAtOneMeter = profile.sourceLoudnessAtOneMeter,
                effectiveRangeHintMeters = profile.effectiveRangeHintMeters,
                priority = profile.priority,

                impulseDurationSeconds = 0f,
                profileId = profile.profileId
            };

            if (profile.category == NoiseCategory.Voice)
            {
                noiseEvent.voiceIntent = voiceIntentOverride ?? new VoiceIntentData
                {
                    contentShort = string.Empty,
                    speechAct = NoiseSpeechAct.Neutral,
                    targetingMode = VoiceTargetingMode.Unknown,
                    targetRef = null,
                    targetId = NoiseIdUtil.UnknownId,
                    targetHintFlags = VoiceTargetHint.None,
                    clarity = 1f
                };
            }

            return NoiseManager.Instance.Add(ref noiseEvent);
        }

        public ulong Bark(float volume01 = 1f, VoiceIntentData? voiceIntentOverride = null)
        {
            float clampedVolume = Mathf.Clamp01(volume01);

            dir.audioPlayer?.PlayClip(barkClipName, Mathf.Max(0.1f, clampedVolume));

            NoiseProfile profile = new NoiseProfile
            {
                profileId = "Dog.Bark",
                category = NoiseCategory.Voice,
                subtype = NoiseSubtype.Bark,
                semanticTags = NoiseSemanticTags.None,
                sourceLoudnessAtOneMeter = Mathf.Max(0.1f, barkLoudnessAtFullVolume * Mathf.Max(0.2f, clampedVolume)),
                effectiveRangeHintMeters = Mathf.Max(1f, barkRangeAtFullVolume * Mathf.Lerp(0.35f, 1f, clampedVolume)),
                priority = 1,
                impulseIntervalSeconds = 0f
            };

            return Emit(profile, voiceIntentOverride: voiceIntentOverride);
        }
    }
}
