#nullable enable
using System.Collections.Generic;
using UnityEngine;
using DogGame.AI.Perception;
using System;
using static DungeonGenerator;
using InspectorTools;

namespace DogGame.Modules
{
    // TODO: add a type DetectedScent that can be used for:
    // 1) scent memory
    // 2) scents to ignore
    // 3) scents observed
    // 4) follow scent
    //.  Field ideas: agentId, pointer to scentinfo struct, airOrGround, lastStrength, lastTime, lastLocationCell, novelty/interest, currently tracking, nearby strengths (8 ways)
    //.  Function ideas: Create, update, sniff nearby, determine novelty/interest (functions exist below)

    [InspectorNote("Sensory_Modules/Scent Perception Module", "Generate events for scents detected, sniff command.")]
    [DisallowMultipleComponent]
    public sealed class ScentPerceptionModule : WorldModule
    {
        [Header("Inputs")]
        public ScentAirGround scentSystem = null!;       // assign/reference your global scent system
        public int maxEventsPerTick = 2;

        [Header("Thresholds")]
        [Tooltip("If scent not seen for this long, treat as new-ish again.")]
        public float reNoticeSeconds = 10f;

        [Tooltip("If strength increases by this amount, emit change event.")]
        public float strengthJumpThreshold01 = 0.25f;

        [Tooltip("Minimum strength required to consider the smell at all.")]
        public float minStrength01 = 0.02f;

        [Header("Interest weights")]
        public float strengthWeight = 0.65f;
        public float noveltyWeight = 0.35f;

        [Header("Strong scent logging")]
        [SerializeField] private bool logStrongScentsToBottomBanner = true;
        [SerializeField] private float strongScentLogThreshold01 = 0.35f;
        [SerializeField] private float strongScentRelogSeconds = 20f;
        [SerializeField] private float strongScentLogStrengthJumpThreshold01 = 0.20f;

        private readonly ScentMemory scentMemory = new();
        private readonly Dictionary<string, StrongScentLogState> strongScentLogMemory = new();
            
        protected override void Awake()
        {
            
        }

        public List<PerceptionEvent> TickScent(float deltaTime)
        {
            var events = new List<PerceptionEvent>(maxEventsPerTick);

            if (worldObject == null || scentSystem == null)
                return events;

            Cell cell = worldObject.locationModule.cell;
            List<ScentDetection> detections = dir!.scentRegistry.CollectScentsAtCell(cell, scentSystem);

            if (detections == null || detections.Count == 0)
                return events;

            float timeNow = Time.time;

            for (int i = 0; i < detections.Count; i++)
            {
                var det = detections[i];
                if (det.scentSource == null)
                    continue;

                float strength01 = Mathf.Clamp01(det.combinedStrength);
                if (strength01 < minStrength01)
                    continue;

                string scentKey = BuildScentKey(det.scentSource);

                //bool seenBefore = scentMemory.TryGet(scentKey, out float lastStrength01, out float lastTime);
                bool seenBefore = scentMemory.TryGetInfo(
                    scentKey,
                    out float lastStrength01,
                    out float lastTime,
                    out float bestStrength01,
                    out ScentFamiliarity familiarity,
                    out string? learnedName,
                    out int learnedAgentId);
                
                float timeSince = seenBefore ? (timeNow - lastTime) : 999f;

                float novelty01 = ComputeNovelty(seenBefore, timeSince);
                float interest01 = ComputeInterest(det.scentSource, strength01, novelty01);

                bool reNotice = !seenBefore || timeSince >= reNoticeSeconds;
                bool strongJump = seenBefore && (strength01 - lastStrength01) >= strengthJumpThreshold01;

                if (!reNotice && !strongJump)
                    continue;

                var type = !seenBefore ? PerceptionEventType.NewSmell : PerceptionEventType.SmellStrengthChanged;
                string scentDisplayName = ResolveScentDisplayName(det.scentSource, seenBefore, familiarity, learnedName);
                int currentRoomId = cell != null ? cell.room_number : -1;
                worldObject.worldMemoryModule?.RecordScentDetection(
                    det.scentSource.agentId,
                    scentDisplayName,
                    det.scentSource.category,
                    currentRoomId,
                    strength01);

                events.Add(PerceptionEvent.MakeScent(
                    observer: worldObject,
                    type: type,
                    worldPos: worldObject.transform.position,
                    scentKey: scentKey,
                    category: det.scentSource.category,
                    scentName: scentDisplayName,
                    strength01: strength01,
                    novelty01: novelty01,
                    interest01: interest01));

                LogStrongScentIfNeeded(det, scentKey, scentDisplayName, type, strength01, interest01, timeNow);

                scentMemory.Update(scentKey, strength01, timeNow);
            }

            if (events.Count == 0)
                return events;

            // Sort by interest descending and take top N
            events.Sort((a, b) => b.Interest01.CompareTo(a.Interest01));
            if (events.Count > maxEventsPerTick)
                events.RemoveRange(maxEventsPerTick, events.Count - maxEventsPerTick);

            // Hand off to your reaction engine / task controller
            //var reactionEngine = worldObject.GetComponentInParent<ReactionEngine>();
            //worldObject.reactionModule.HandlePerceptionEvents(events);
            return events;
        }

        private static string BuildScentKey(ScentSource source)
        {
            // Prefer stable id when possible
            if (source.agentId >= 0)
                return $"agent:{source.agentId}";

            // Fallback for non-agent sources
            string name = string.IsNullOrWhiteSpace(source.scentName) ? "Unnamed" : source.scentName.Trim();
            return $"{source.category}:{name}";
        }

        private static float ComputeNovelty(bool seenBefore, float timeSinceLast)
        {
            if (!seenBefore) return 1f;
            // after ~20s, smells feel "new-ish" again
            return Mathf.Clamp01(timeSinceLast / 20f);
        }

        private float ComputeInterest(ScentSource source, float strength01, float novelty01)
        {
            float baseInterest = strengthWeight * strength01 + noveltyWeight * novelty01;

            // Category bias (v1): tune later per-agent personality
            float categoryBias = source.category switch
            {
                ScentCategory.Food => 1.30f,
                ScentCategory.Human => 1.10f,
                ScentCategory.Dog => 1.05f,
                _ => 1.0f
            };

            // Familiarity bias (v1): new smells get a little boost
            float familiarityBias = source.familiarity switch
            {
                ScentFamiliarity.New => 1.15f,
                _ => 1.0f
            };

            return Mathf.Clamp01(baseInterest * categoryBias * familiarityBias);
        }

        private void LogStrongScentIfNeeded(
            ScentDetection detection,
            string scentKey,
            string scentDisplayName,
            PerceptionEventType eventType,
            float strength01,
            float interest01,
            float timeNow)
        {
            if (!logStrongScentsToBottomBanner || worldObject == null || detection.scentSource == null)
                return;

            if (strength01 < strongScentLogThreshold01)
                return;

            if (strongScentLogMemory.TryGetValue(scentKey, out StrongScentLogState lastLog))
            {
                bool cooldownExpired = timeNow - lastLog.timeSeconds >= Mathf.Max(0f, strongScentRelogSeconds);
                bool strengthJumped = strength01 - lastLog.strength01 >= Mathf.Max(0f, strongScentLogStrengthJumpThreshold01);
                if (!cooldownExpired && !strengthJumped)
                    return;
            }

            string changeLabel = eventType == PerceptionEventType.NewSmell ? "new" : "stronger";
            string category = detection.scentSource.category.ToString();
            string room = GetCurrentRoomLabel();
            string message =
                $"[{worldObject.DisplayName}] smelled strong {changeLabel} {category} scent " +
                $"{QuoteIfNeeded(scentDisplayName)} " +
                $"strength={strength01:0.00} air={detection.airStrength:0.00} ground={detection.groundStrength:0.00} " +
                $"interest={interest01:0.00} room={room}";

            Debug.Log(message, worldObject);
            BottomBanner.LogAgentMessage(worldObject, BannerSense.Smell, BannerLevel.High, message, true);

            strongScentLogMemory[scentKey] = new StrongScentLogState
            {
                strength01 = strength01,
                timeSeconds = timeNow
            };
        }

        private string GetCurrentRoomLabel()
        {
            if (worldObject == null || worldObject.locationModule == null)
                return "unknown";

            Cell currentCell = worldObject.locationModule.cell;
            if (currentCell == null)
                return "unknown";

            return ResolveRoomName(currentCell.room_number);
        }

        private string ResolveRoomName(int roomId)
        {
            if (roomId < 0 || dir == null || dir.gen == null || dir.gen.rooms == null || roomId >= dir.gen.rooms.Count)
                return $"Room {roomId}";

            Room room = dir.gen.rooms[roomId];
            if (room == null)
                return $"Room {roomId}";

            if (!string.IsNullOrWhiteSpace(room.name) && !IsRawGeneratedRoomName(room.name, roomId))
                return room.name.Trim();

            return ResolveSemanticRoomName(dir.gen.rooms, roomId);
        }

        private string ResolveSemanticRoomName(List<Room> rooms, int roomId)
        {
            if (rooms == null || roomId < 0 || roomId >= rooms.Count || rooms[roomId] == null)
                return $"Room {roomId}";

            DungeonSettings settings = null!;
            if (dir != null)
                settings = dir.cfg != null ? dir.cfg : dir.gen != null ? dir.gen.cfg : null!;

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

        private static string QuoteIfNeeded(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "\"Unknown\"" : $"\"{value.Trim()}\"";
        }

        /// <summary>
        /// Manual sniff: returns a report of scents in the observer's current cell.
        /// Ignores any scent whose key is in ignoreKeys. Results are sorted by strength descending.
        /// Air & ground separated.
        /// </summary>
        public SniffReport SniffHere(HashSet<string>? ignoreKeys = null)
        {
            var report = new SniffReport
            {
                time = Time.time,
                cell = worldObject != null ? worldObject.locationModule.cell.pos : default
            };

            if (worldObject == null || scentSystem == null || dir == null || dir.scentRegistry == null)
                return report;

            Cell cell = worldObject.locationModule.cell;

            // You already use this in TickScent()
            List<ScentDetection> detections = dir.scentRegistry.CollectScentsAtCell(cell, scentSystem);
            if (detections == null || detections.Count == 0)
                return report;

            Vector2Int cellPos = cell.pos;
            float timeNow = Time.time;

            for (int i = 0; i < detections.Count; i++)
            {
                var det = detections[i];
                if (det.scentSource == null)
                    continue;

                float combined01 = Mathf.Clamp01(det.combinedStrength);
                if (combined01 < minStrength01)
                    continue;

                string key = BuildScentKey(det.scentSource);

                bool ignored = ignoreKeys != null && ignoreKeys.Contains(key);
                if (ignored)
                    continue;

                int currentRoomId = cell != null ? cell.room_number : -1;
                worldObject.worldMemoryModule?.RecordScentDetection(
                    det.scentSource.agentId,
                    det.scentSource.scentName ?? "unknown",
                    det.scentSource.category,
                    currentRoomId,
                    combined01);

                // If your ScentDetection has separated channels, use them.
                // If it does NOT, this still works: we treat "combined" as both unknown.
                float air01 = TryGetAirStrength01(det);
                float ground01 = TryGetGroundStrength01(det);

                // If you only have combined, put it in air (or ground) consistently:
                if (air01 <= 0f && ground01 <= 0f)
                    air01 = combined01;

                if (air01 >= minStrength01)
                {
                    report.air.Add(new DetectedScent
                    {
                        scentKey = key,
                        category = det.scentSource.category,
                        scentName = det.scentSource.scentName ?? det.scentSource.category.ToString(),
                        medium = ScentMedium.Air,
                        strength01 = air01,
                        cell = cellPos,
                        time = timeNow,
                        agentId = det.scentSource.agentId,
                        ignored = false
                    });
                }

                if (ground01 >= minStrength01)
                {
                    report.ground.Add(new DetectedScent
                    {
                        scentKey = key,
                        category = det.scentSource.category,
                        scentName = det.scentSource.scentName ?? det.scentSource.category.ToString(),
                        medium = ScentMedium.Ground,
                        strength01 = ground01,
                        cell = cellPos,
                        time = timeNow,
                        agentId = det.scentSource.agentId,
                        ignored = false
                    });
                }
            }

            report.air.Sort((a, b) => b.strength01.CompareTo(a.strength01));
            report.ground.Sort((a, b) => b.strength01.CompareTo(a.strength01));

            return report;
        }

        // ---- Helpers to adapt to whatever fields your ScentDetection actually has ----

        private static float TryGetAirStrength01(ScentDetection det)
        {
            // If your ScentDetection already has airStrength/air01, use it here.
            // Otherwise return 0 and we'll fall back to combined.
            // Example guesses (change to match your struct):
            // return Mathf.Clamp01(det.airStrength);
            // return Mathf.Clamp01(det.air01);

            return 0f;
        }

        private static float TryGetGroundStrength01(ScentDetection det)
        {
            // If your ScentDetection already has groundStrength/ground01, use it here.
            // Example guesses:
            // return Mathf.Clamp01(det.groundStrength);
            // return Mathf.Clamp01(det.ground01);

            return 0f;
        }


        // =============== Background Sniff ==================

        [SerializeField] private bool enableBackgroundSniff = true;

        // You can throttle if you want:
        [SerializeField] private float tickIntervalSeconds = 0.25f;
        private float nextTickTime;


        private void ScentBackgroundTick()
        {
            if (!enableBackgroundSniff) return;

            if (Time.time < nextTickTime) return;
            nextTickTime = Time.time + Mathf.Max(0.05f, tickIntervalSeconds);

            List<PerceptionEvent> events = TickScent(Time.deltaTime);
            if (events == null || events.Count == 0) return;

            // TODO: Route these to your reaction engine, task controller, or LLMThinkModule triggers.
            // For now, just log:
            for (int i = 0; i < events.Count; i++)
                Debug.Log($"[ScentEvent] {worldObject.name} {events[i].Type} {events[i].Target!.ObjectId} strength={events[i].Strength01:0.00} novelty={events[i].Novelty01:0.00} interest={events[i].Interest01:0.00}", worldObject);
        }

        public bool TryFindStrongestNeighborForScent(
            string scentKey,
            Vector2Int centerPos,
            int height,
            ScentMedium medium,
            out DirFlags bestDir,
            out Vector2Int bestPos,
            out float bestStrength)
        {
            bestDir = DirFlags.None;
            bestPos = centerPos;
            bestStrength = 0f;

            // Safety
            if (dir == null || dir.gen == null || dir.gen.hf == null)
                return false;

            NeighborMatch match;

            foreach (DirFlags direction in DirFlagsEx.All8)
            {
                Vector2Int relPosLoc = centerPos + direction.ToVector2Int();

                dir.gen.hf.TryQueryAt(relPosLoc.x, relPosLoc.y, height, 50, out match);
                if (match.roomId < 0 || match.cellId < 0) 
                    continue;

                Cell relCell = dir.gen.rooms[match.roomId].cells[match.cellId];
                if (relCell.scents == null)
                    continue;

                foreach (ScentInCell scentInCell in relCell.scents)
                {
                    // Your scentKey convention for agents is "agent:<id>"
                    string cellKey = $"agent:{scentInCell.agentId}";
                    if (cellKey != scentKey)
                        continue;

                    float strength =
                        (medium == ScentMedium.Ground)
                            ? scentInCell.groundIntensity
                            : scentInCell.airIntensity;

                    if (strength > bestStrength)
                    {
                        bestStrength = strength;
                        bestPos = relPosLoc;
                        bestDir = direction;
                    }
                }
            }

            return bestDir != DirFlags.None && bestStrength > 0f;
        }

        public float TryGetStrengthAt(Vector2Int pos, int height, string scentKey, ScentMedium medium)
        {
            float strength01;
            TryGetScentStrengthAtCell(scentKey, pos, height, medium, out strength01);
            return strength01;
        }

        public bool TryGetScentStrengthAtCell(
            string scentKey,
            Vector2Int pos,
            int height,
            ScentMedium medium,
            out float strength01)
        {
            strength01 = 0f;

            if (dir == null || dir.gen == null || dir.gen.hf == null)
                return false;

            if (string.IsNullOrWhiteSpace(scentKey) || !scentKey.StartsWith("agent:", StringComparison.Ordinal))
                return false;

            if (!int.TryParse(scentKey.AsSpan(6), out int targetAgentId))
                return false;

            NeighborMatch match;
            dir.gen.hf.TryQueryAt(pos.x, pos.y, height, 50, out match);
            if (match.roomId < 0 || match.cellId < 0)
                return false;

            Cell cell = dir.gen.rooms[match.roomId].cells[match.cellId];
            if (cell.scents == null)
                return false;

            float best = 0f;

            foreach (ScentInCell scentInCell in cell.scents)
            {
                if (scentInCell.agentId != targetAgentId)
                    continue;

                float v =
                    (medium == ScentMedium.Ground)
                        ? scentInCell.groundIntensity
                        : scentInCell.airIntensity;

                if (v > best)
                    best = v;
            }

            if (best <= 0f)
                return false;

            strength01 = best;
            return true;
        }

        private static string ResolveScentDisplayName(
            ScentSource source,
            bool seenBefore,
            ScentFamiliarity familiarity,
            string? learnedName)
        {
            // If we've actually identified it, prefer the learned name (e.g., "Hot Dog Thief")
            if (seenBefore && familiarity >= ScentFamiliarity.Identified && !string.IsNullOrWhiteSpace(learnedName))
                return learnedName!;

            // Otherwise fall back to the source-provided name if any
            if (!string.IsNullOrWhiteSpace(source.scentName))
                return source.scentName!;

            // Otherwise category label
            return source.category.ToString();
        }

        public void PromoteScentFamiliarity(string scentKey, ScentFamiliarity atLeast)
        {
            if (string.IsNullOrWhiteSpace(scentKey))
                return;

            scentMemory.PromoteFamiliarity(scentKey, atLeast);
        }

        public void IdentifyScent(string scentKey, string displayName, int agentId = -1)
        {
            if (string.IsNullOrWhiteSpace(scentKey))
                return;

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = "Unknown";

            scentMemory.Identify(scentKey, displayName, agentId);
        }

        private struct StrongScentLogState
        {
            public float strength01;
            public float timeSeconds;
        }

    }
}
