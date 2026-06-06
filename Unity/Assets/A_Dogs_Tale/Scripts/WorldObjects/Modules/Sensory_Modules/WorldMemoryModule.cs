#nullable enable
using System;
using System.Collections.Generic;
using DogGame.Noise;
using UnityEngine;
using InspectorTools;

namespace DogGame.Modules
{
    public enum LastKnownLocationKind
    {
        Unknown = 0,
        InRoom,
        HeldByAgent,
        HeldByContainer,
        BuriedBySelf
    }

    public enum WorldMemorySignalKind
    {
        Unknown = 0,
        Seen,
        Scent,
        Sound,
        BuriedBySelf
    }

    [Serializable]
    public sealed class LastKnownLocationFact
    {
        public int objectId = -1;
        public string displayName = "unknown";
        public LastKnownLocationKind kind = LastKnownLocationKind.Unknown;
        public int roomId = -1;
        public int holderObjectId = -1;
        public float observedTimeSeconds = -1f;
        public int observedFrame = -1;
        public float confidence01 = 0f;
        public WorldMemorySignalKind source = WorldMemorySignalKind.Unknown;
    }

    [Serializable]
    public sealed class ScentRoomMemoryFact
    {
        public int objectId = -1;
        public string displayName = "unknown";
        public ScentCategory category = ScentCategory.Unknown;
        public int roomId = -1;
        public float maxStrength01 = 0f;
        public float lastStrength01 = 0f;
        public float firstDetectedTimeSeconds = -1f;
        public float lastDetectedTimeSeconds = -1f;
        public int detectionCount = 0;
    }

    [Serializable]
    public sealed class SoundRoomMemoryFact
    {
        public int objectId = -1;
        public string displayName = "unknown";
        public int heardFromRoomId = -1;
        public int sourceRoomId = -1;
        public NoiseCategory category = NoiseCategory.Other;
        public NoiseSubtype subtype = NoiseSubtype.Unknown;
        public float maxLoudness01 = 0f;
        public float lastLoudness01 = 0f;
        public float firstHeardTimeSeconds = -1f;
        public float lastHeardTimeSeconds = -1f;
        public float confidence01 = 0f;
        public int hearingCount = 0;
    }

    public readonly struct WorldMemorySearchLead
    {
        public readonly int objectId;
        public readonly string displayName;
        public readonly WorldMemorySignalKind source;
        public readonly LastKnownLocationKind locationKind;
        public readonly int roomId;
        public readonly int holderObjectId;
        public readonly float confidence01;
        public readonly float observedTimeSeconds;
        public readonly float signalStrength01;

        public WorldMemorySearchLead(
            int objectId,
            string displayName,
            WorldMemorySignalKind source,
            LastKnownLocationKind locationKind,
            int roomId,
            int holderObjectId,
            float confidence01,
            float observedTimeSeconds,
            float signalStrength01)
        {
            this.objectId = objectId;
            this.displayName = string.IsNullOrWhiteSpace(displayName) ? "unknown" : displayName;
            this.source = source;
            this.locationKind = locationKind;
            this.roomId = roomId;
            this.holderObjectId = holderObjectId;
            this.confidence01 = Mathf.Clamp01(confidence01);
            this.observedTimeSeconds = observedTimeSeconds;
            this.signalStrength01 = Mathf.Clamp01(signalStrength01);
        }
    }

    [InspectorNote("Sensory_Modules/World Memory Module", "Tracks per-agent remembered object locations, scent leads, sound leads, and self-buried items.")]
    [DisallowMultipleComponent]
    public sealed class WorldMemoryModule : WorldModule
    {
        [Header("Confidence Decay")]
        [Tooltip("Seconds for remembered scent leads to lose half their confidence.")]
        [SerializeField] private float scentConfidenceHalfLifeSeconds = 180f;

        [Tooltip("Seconds for remembered sound leads to lose half their confidence.")]
        [SerializeField] private float soundConfidenceHalfLifeSeconds = 60f;

        private readonly Dictionary<int, LastKnownLocationFact> lastKnownLocationsByObjectId = new();
        private readonly Dictionary<int, Dictionary<int, ScentRoomMemoryFact>> scentByObjectThenRoom = new();
        private readonly Dictionary<int, Dictionary<int, SoundRoomMemoryFact>> soundByObjectThenRoom = new();

        public IReadOnlyDictionary<int, LastKnownLocationFact> LastKnownLocationsByObjectId => lastKnownLocationsByObjectId;

        public void RecordSeenObject(WorldObject target, float confidence01 = 1f)
        {
            if (target == null || target.ObjectId <= 0)
                return;

            WorldObject holder = ResolveHolder(target);
            LastKnownLocationKind kind = LastKnownLocationKind.InRoom;
            int holderId = -1;
            int roomId;

            if (holder != null)
            {
                holderId = holder.ObjectId;
                roomId = ResolveRoomId(holder);
                kind = IsAgent(holder) ? LastKnownLocationKind.HeldByAgent : LastKnownLocationKind.HeldByContainer;
            }
            else
            {
                roomId = ResolveRoomId(target);
            }

            UpsertLastKnownLocation(
                target.ObjectId,
                target.DisplayName,
                kind,
                roomId,
                holderId,
                confidence01,
                WorldMemorySignalKind.Seen);
        }

        public void RecordContainerContents(WorldObject container, float confidence01 = 1f)
        {
            if (container == null || container.containerModule == null)
                return;

            IReadOnlyList<WorldObject> heldItems = container.containerModule.HeldItems;
            for (int i = 0; i < heldItems.Count; i++)
            {
                WorldObject item = heldItems[i];
                if (item == null || item.ObjectId <= 0)
                    continue;

                UpsertLastKnownLocation(
                    item.ObjectId,
                    item.DisplayName,
                    IsAgent(container) ? LastKnownLocationKind.HeldByAgent : LastKnownLocationKind.HeldByContainer,
                    ResolveRoomId(container),
                    container.ObjectId,
                    confidence01,
                    WorldMemorySignalKind.Seen);
            }
        }

        public void RecordScentDetection(
            int objectId,
            string displayName,
            ScentCategory category,
            int roomId,
            float strength01)
        {
            if (objectId <= 0 || roomId < 0)
                return;

            Dictionary<int, ScentRoomMemoryFact> byRoom = GetOrCreateNested(scentByObjectThenRoom, objectId);
            if (!byRoom.TryGetValue(roomId, out ScentRoomMemoryFact fact))
            {
                fact = new ScentRoomMemoryFact
                {
                    objectId = objectId,
                    roomId = roomId,
                    firstDetectedTimeSeconds = Time.time
                };
                byRoom[roomId] = fact;
            }

            fact.displayName = NormalizeName(displayName);
            fact.category = category;
            fact.lastStrength01 = Mathf.Clamp01(strength01);
            fact.maxStrength01 = Mathf.Max(fact.maxStrength01, fact.lastStrength01);
            fact.lastDetectedTimeSeconds = Time.time;
            fact.detectionCount++;
        }

        public void RecordHeardNoise(in HeardNoise heard)
        {
            int objectId = heard.attributedEmitterId;
            if (objectId <= 0)
                return;

            string displayName = heard.attributedEmitterRef != null
                ? heard.attributedEmitterRef.DisplayName
                : "unknown";

            RecordSoundLead(
                objectId,
                displayName,
                GetObserverRoomId(),
                heard.sourceRoomId,
                heard.perceivedLoudness01,
                heard.confidence01 * Mathf.Max(0.1f, heard.attributionConfidence01),
                heard.category,
                heard.subtype);
        }

        public void RecordSoundLead(
            int objectId,
            string displayName,
            int heardFromRoomId,
            int sourceRoomId,
            float loudness01,
            float confidence01,
            NoiseCategory category,
            NoiseSubtype subtype)
        {
            if (objectId <= 0)
                return;

            int roomKey = sourceRoomId >= 0 ? sourceRoomId : heardFromRoomId;
            if (roomKey < 0)
                return;

            Dictionary<int, SoundRoomMemoryFact> byRoom = GetOrCreateNested(soundByObjectThenRoom, objectId);
            if (!byRoom.TryGetValue(roomKey, out SoundRoomMemoryFact fact))
            {
                fact = new SoundRoomMemoryFact
                {
                    objectId = objectId,
                    heardFromRoomId = heardFromRoomId,
                    sourceRoomId = sourceRoomId,
                    firstHeardTimeSeconds = Time.time
                };
                byRoom[roomKey] = fact;
            }

            fact.displayName = NormalizeName(displayName);
            fact.heardFromRoomId = heardFromRoomId;
            fact.sourceRoomId = sourceRoomId;
            fact.category = category;
            fact.subtype = subtype;
            fact.lastLoudness01 = Mathf.Clamp01(loudness01);
            fact.maxLoudness01 = Mathf.Max(fact.maxLoudness01, fact.lastLoudness01);
            fact.confidence01 = Mathf.Max(fact.confidence01, Mathf.Clamp01(confidence01));
            fact.lastHeardTimeSeconds = Time.time;
            fact.hearingCount++;
        }

        public void RecordBuriedBySelf(WorldObject item, float confidence01 = 1f)
        {
            if (item == null || item.ObjectId <= 0)
                return;

            UpsertLastKnownLocation(
                item.ObjectId,
                item.DisplayName,
                LastKnownLocationKind.BuriedBySelf,
                GetObserverRoomId(),
                -1,
                confidence01,
                WorldMemorySignalKind.BuriedBySelf);
        }

        public bool TryGetLastKnownLocation(int objectId, out LastKnownLocationFact fact)
        {
            return lastKnownLocationsByObjectId.TryGetValue(objectId, out fact);
        }

        public bool TryGetBestScentLead(int objectId, out WorldMemorySearchLead lead)
        {
            lead = default;
            if (!scentByObjectThenRoom.TryGetValue(objectId, out Dictionary<int, ScentRoomMemoryFact> byRoom))
                return false;

            bool found = false;
            ScentRoomMemoryFact best = null!;
            float bestConfidence = 0f;
            foreach (ScentRoomMemoryFact fact in byRoom.Values)
            {
                float confidence = ComputeDecayedConfidence(
                    fact.maxStrength01,
                    fact.lastDetectedTimeSeconds,
                    scentConfidenceHalfLifeSeconds);

                if (!found || confidence > bestConfidence)
                {
                    found = true;
                    best = fact;
                    bestConfidence = confidence;
                }
            }

            if (!found)
                return false;

            lead = new WorldMemorySearchLead(
                best.objectId,
                best.displayName,
                WorldMemorySignalKind.Scent,
                LastKnownLocationKind.InRoom,
                best.roomId,
                -1,
                bestConfidence,
                best.lastDetectedTimeSeconds,
                best.maxStrength01);
            return true;
        }

        public bool TryGetBestSoundLead(int objectId, out WorldMemorySearchLead lead)
        {
            lead = default;
            if (!soundByObjectThenRoom.TryGetValue(objectId, out Dictionary<int, SoundRoomMemoryFact> byRoom))
                return false;

            bool found = false;
            SoundRoomMemoryFact best = null!;
            float bestConfidence = 0f;
            foreach (SoundRoomMemoryFact fact in byRoom.Values)
            {
                float confidence = ComputeDecayedConfidence(
                    fact.maxLoudness01 * fact.confidence01,
                    fact.lastHeardTimeSeconds,
                    soundConfidenceHalfLifeSeconds);

                if (!found || confidence > bestConfidence)
                {
                    found = true;
                    best = fact;
                    bestConfidence = confidence;
                }
            }

            if (!found)
                return false;

            int roomId = best.sourceRoomId >= 0 ? best.sourceRoomId : best.heardFromRoomId;
            lead = new WorldMemorySearchLead(
                best.objectId,
                best.displayName,
                WorldMemorySignalKind.Sound,
                LastKnownLocationKind.InRoom,
                roomId,
                -1,
                bestConfidence,
                best.lastHeardTimeSeconds,
                best.maxLoudness01);
            return true;
        }

        public bool TryGetBestSearchLead(int objectId, bool includeLastKnownLocation, out WorldMemorySearchLead lead)
        {
            lead = default;
            bool found = false;
            float bestConfidence = 0f;

            if (includeLastKnownLocation && TryGetLastKnownLocation(objectId, out LastKnownLocationFact location))
            {
                lead = new WorldMemorySearchLead(
                    location.objectId,
                    location.displayName,
                    location.source,
                    location.kind,
                    location.roomId,
                    location.holderObjectId,
                    location.confidence01,
                    location.observedTimeSeconds,
                    location.confidence01);
                bestConfidence = lead.confidence01;
                found = true;
            }

            if (TryGetBestScentLead(objectId, out WorldMemorySearchLead scentLead) && (!found || scentLead.confidence01 > bestConfidence))
            {
                lead = scentLead;
                bestConfidence = scentLead.confidence01;
                found = true;
            }

            if (TryGetBestSoundLead(objectId, out WorldMemorySearchLead soundLead) && (!found || soundLead.confidence01 > bestConfidence))
            {
                lead = soundLead;
                found = true;
            }

            return found;
        }

        public bool TryGetBestIndirectSearchLead(int objectId, out WorldMemorySearchLead lead)
        {
            return TryGetBestSearchLead(objectId, includeLastKnownLocation: false, out lead);
        }

        private void UpsertLastKnownLocation(
            int objectId,
            string displayName,
            LastKnownLocationKind kind,
            int roomId,
            int holderObjectId,
            float confidence01,
            WorldMemorySignalKind source)
        {
            if (objectId <= 0)
                return;

            lastKnownLocationsByObjectId[objectId] = new LastKnownLocationFact
            {
                objectId = objectId,
                displayName = NormalizeName(displayName),
                kind = kind,
                roomId = roomId,
                holderObjectId = holderObjectId,
                observedTimeSeconds = Time.time,
                observedFrame = Time.frameCount,
                confidence01 = Mathf.Clamp01(confidence01),
                source = source
            };
        }

        private int GetObserverRoomId()
        {
            return ResolveRoomId(worldObject);
        }

        private int ResolveRoomId(WorldObject target)
        {
            if (target == null)
                return -1;

            if (target.locationModule != null)
            {
                Cell cell = target.locationModule.cell;
                return cell != null ? cell.room_number : -1;
            }

            if (dir == null || dir.gen == null || !dir.gen.buildComplete || dir.gen.hf == null)
                return -1;

            Vector3 mapPos = target.pos3d_map;
            int mapX = Mathf.FloorToInt(mapPos.x);
            int mapY = Mathf.FloorToInt(mapPos.z);
            int mapZ = MapHeightToHeightSteps(mapPos.y);
            Cell cellAtObject = dir.gen.GetCellFromHf(mapX, mapY, mapZ, 50);
            return cellAtObject != null ? cellAtObject.room_number : -1;
        }

        private int MapHeightToHeightSteps(float mapY)
        {
            float unitHeight = dir != null && dir.cfg != null
                ? Mathf.Max(0.0001f, dir.cfg.unitHeight)
                : 1f;
            return Mathf.RoundToInt(mapY / unitHeight);
        }

        private static WorldObject ResolveHolder(WorldObject target)
        {
            if (target == null || target.transform.parent == null)
                return null!;

            WorldObject holder = target.transform.parent.GetComponentInParent<WorldObject>();
            return holder != null && holder != target ? holder : null!;
        }

        private static bool IsAgent(WorldObject obj)
        {
            return obj != null && (obj.Kind == WorldObjectKind.Agent || obj.agentModule != null);
        }

        private static Dictionary<int, TValue> GetOrCreateNested<TValue>(
            Dictionary<int, Dictionary<int, TValue>> root,
            int objectId)
        {
            if (!root.TryGetValue(objectId, out Dictionary<int, TValue> nested))
            {
                nested = new Dictionary<int, TValue>();
                root[objectId] = nested;
            }

            return nested;
        }

        private static float ComputeDecayedConfidence(float baseConfidence01, float lastTimeSeconds, float halfLifeSeconds)
        {
            if (lastTimeSeconds < 0f)
                return 0f;

            float age = Mathf.Max(0f, Time.time - lastTimeSeconds);
            float halfLife = Mathf.Max(0.001f, halfLifeSeconds);
            float decay = Mathf.Pow(0.5f, age / halfLife);
            return Mathf.Clamp01(baseConfidence01 * decay);
        }

        private static string NormalizeName(string displayName)
        {
            return string.IsNullOrWhiteSpace(displayName) ? "unknown" : displayName.Trim();
        }
    }
}
