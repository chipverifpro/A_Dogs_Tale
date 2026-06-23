#nullable enable
using System;
using System.Collections.Generic;
using DogGame.LLM;
using DogGame.Modules;
using UnityEngine;
using static DungeonGenerator;

namespace DogGame.Tasks
{
    public sealed partial class Task_ScentFollow
    {
        private const float DefaultGroundWeight = 0.75f;
        private const float DefaultAirWeight = 0.25f;
        private const float AirTrackingGroundWeight = 0.45f;
        private const float AirTrackingAirWeight = 0.55f;
        private const float AcquireGroundWeight = 0.55f;
        private const float AcquireAirWeight = 0.45f;
        private const float FollowGroundWeight = 0.75f;
        private const float FollowAirWeight = 0.25f;
        private const float CastGroundWeight = 0.45f;
        private const float CastAirWeight = 0.55f;
        private const float AbsoluteIncreaseThreshold = 0.05f;
        private const float RelativeIncreaseMultiplier = 1.15f;
        private const float IncreasedMemoryDurationSeconds = 5f;

        private struct ScentMemory
        {
            public Vector2Int location;
            public string scentKey;

            public float airScent;
            public float groundScent;
            public float combinedScent;
            public float previousCombinedScent;
            public float peakCombinedScent;

            public float timeChecked;
            public float timeLastIncreased;
            public float lastVisitTime;

            public bool sniffed;
            public bool visited;
            public bool explored;
            public bool blocked;
            public bool deadEnd;
            public bool targetDetected;

            public Vector2Int cameFrom;
            public bool hasCameFrom;

            public int visitCount;
        }

        private readonly Dictionary<Vector2Int, ScentMemory> memory = new();

        private void ClearScentMemory()
        {
            memory.Clear();
        }

        private void TryNoteScentAt(TaskContext context, Vector2Int pos)
        {
            var agent = context.Agent;
            if (agent == null)
                return;

            var scentModule = agent.scentPerceptionModule;
            if (scentModule == null)
                return;

            int height = agent.locationModule.cell.height;

            SampleTrackedScentAtCell(scentModule, pos, height, out float air, out float ground);
            NoteScentObserved(pos, air, ground);
        }

        private void UpdateMemoryFromLocalSniff(
            TaskContext context,
            ScentPerceptionModule scentModule,
            Vector2Int centerPos,
            int height)
        {
            const int radius = 2;

            if (context.Agent == null)
                return;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radius * radius)
                        continue;

                    Vector2Int pos = new(centerPos.x + dx, centerPos.y + dy);

                    if (!TryIsValidCellAt(scentModule, pos, height))
                        continue;

                    SampleTrackedScentAtCell(scentModule, pos, height, out float air, out float ground);
                    NoteScentObserved(pos, air, ground);
                }
            }

            SampleTrackedScentAtCell(scentModule, centerPos, height, out float centerAir, out float centerGround);
            NoteScentObserved(centerPos, centerAir, centerGround);
        }

        private void NoteScentObserved(Vector2Int pos, float airScent, float groundScent)
        {
            float now = Time.time;
            float combinedScent = CombineScent(airScent, groundScent);

            if (!memory.TryGetValue(pos, out ScentMemory info))
            {
                info = new ScentMemory
                {
                    location = pos,
                    scentKey = scentKey,
                    previousCombinedScent = combinedScent,
                    peakCombinedScent = combinedScent,
                    timeLastIncreased = -999f,
                    visitCount = 0,
                    lastVisitTime = -1f,
                    blocked = false,
                    targetDetected = false,
                    cameFrom = pos,
                    hasCameFrom = false
                };
            }

            bool hadPriorSniff = info.sniffed;
            float oldCombinedScent = hadPriorSniff ? info.combinedScent : combinedScent;
            bool increased =
                hadPriorSniff &&
                combinedScent > oldCombinedScent + AbsoluteIncreaseThreshold &&
                combinedScent > oldCombinedScent * RelativeIncreaseMultiplier;

            info.location = pos;
            info.scentKey = scentKey;
            info.airScent = airScent;
            info.groundScent = groundScent;
            info.previousCombinedScent = oldCombinedScent;
            info.combinedScent = combinedScent;
            info.timeChecked = now;
            info.sniffed = true;

            if (combinedScent > info.peakCombinedScent)
                info.peakCombinedScent = combinedScent;

            if (increased)
            {
                info.timeLastIncreased = now;
                info.explored = false;
                info.deadEnd = false;
            }

            memory[pos] = info;
        }

        private void NoteVisit(Vector2Int pos)
        {
            float now = Time.time;

            if (!memory.TryGetValue(pos, out ScentMemory info))
            {
                info = new ScentMemory
                {
                    location = pos,
                    scentKey = scentKey,
                    timeChecked = -1f,
                    timeLastIncreased = -999f,
                    blocked = false,
                    targetDetected = false,
                    cameFrom = pos,
                    hasCameFrom = false
                };
            }

            info.visited = true;
            info.visitCount += 1;
            info.lastVisitTime = now;
            memory[pos] = info;

            RememberRecentCell(pos);
        }

        private void MarkTargetDetected(Vector2Int pos)
        {
            if (!memory.TryGetValue(pos, out ScentMemory info))
            {
                info = new ScentMemory
                {
                    location = pos,
                    scentKey = scentKey,
                    timeChecked = Time.time,
                    timeLastIncreased = -999f,
                    lastVisitTime = -1f,
                    cameFrom = pos,
                    hasCameFrom = false
                };
            }

            info.targetDetected = true;
            memory[pos] = info;
        }

        private static bool TryIsValidCellAt(ScentPerceptionModule scentModule, Vector2Int pos, int height)
        {
            var dir = scentModule.dir;
            if (dir == null || dir.gen == null || dir.gen.hf == null)
                return false;

            dir.gen.hf.TryQueryAt(pos.x, pos.y, height, 50, out NeighborMatch match);
            return match.roomId >= 0 && match.cellId >= 0;
        }

        private float GetTrackedScentStrengthAtCell(ScentPerceptionModule scentModule, Vector2Int pos, int height)
        {
            SampleTrackedScentAtCell(scentModule, pos, height, out float airScent, out float groundScent);
            return CombineScent(airScent, groundScent);
        }

        private void SampleTrackedScentAtCell(
            ScentPerceptionModule scentModule,
            Vector2Int pos,
            int height,
            out float airScent,
            out float groundScent)
        {
            airScent = 0f;
            groundScent = 0f;

            var dir = scentModule.dir;
            if (dir == null || dir.gen == null || dir.gen.hf == null)
                return;

            dir.gen.hf.TryQueryAt(pos.x, pos.y, height, 50, out NeighborMatch match);
            if (match.roomId < 0 || match.cellId < 0)
                return;

            Cell cell = dir.gen.rooms[match.roomId].cells[match.cellId];
            if (cell.scents == null)
                return;

            foreach (ScentInCell scentInCell in cell.scents)
            {
                string cellKey = $"agent:{scentInCell.agentId}";
                if (!string.Equals(cellKey, scentKey, StringComparison.Ordinal))
                    continue;

                airScent = Mathf.Max(airScent, scentInCell.airIntensity);
                groundScent = Mathf.Max(groundScent, scentInCell.groundIntensity);
            }
        }

        private float CombineScent(float airScent, float groundScent)
        {
            switch (currentState)
            {
                case ScentFollowState.AcquireScent:
                    return groundScent * AcquireGroundWeight + airScent * AcquireAirWeight;

                case ScentFollowState.FollowTrail:
                    return groundScent * FollowGroundWeight + airScent * FollowAirWeight;

                case ScentFollowState.CastSearch:
                    return groundScent * CastGroundWeight + airScent * CastAirWeight;
            }

            if (medium == ScentMedium.Air)
                return groundScent * AirTrackingGroundWeight + airScent * AirTrackingAirWeight;

            return groundScent * DefaultGroundWeight + airScent * DefaultAirWeight;
        }

        private float GetKnownStrength(Vector2Int pos)
        {
            if (memory.TryGetValue(pos, out var info))
                return info.peakCombinedScent;

            return 0f;
        }

        private int GetVisitCount(Vector2Int pos)
        {
            if (memory.TryGetValue(pos, out var info))
                return info.visitCount;

            return 0;
        }

        private float GetLastVisitTime(Vector2Int pos)
        {
            if (memory.TryGetValue(pos, out var info))
                return info.lastVisitTime;

            return -999f;
        }

        private float GetLastStrength(Vector2Int pos)
        {
            return memory.TryGetValue(pos, out var info) ? info.combinedScent : 0f;
        }

        private float GetStrengthDelta(Vector2Int pos, float currentStrength)
        {
            if (!memory.TryGetValue(pos, out var info))
                return 0f;

            return currentStrength - info.combinedScent;
        }

        private float GetRiseDelta(Vector2Int pos)
        {
            if (!memory.TryGetValue(pos, out var info))
                return 0f;

            return info.combinedScent - info.previousCombinedScent;
        }

        private float GetSeenAgeSeconds(Vector2Int pos)
        {
            return memory.TryGetValue(pos, out var info) ? Time.time - info.timeChecked : 999f;
        }

        private bool WasRecentlyIncreased(Vector2Int pos)
        {
            return memory.TryGetValue(pos, out var info) &&
                   Time.time - info.timeLastIncreased <= IncreasedMemoryDurationSeconds;
        }

        private bool IsExplored(Vector2Int pos)
        {
            return memory.TryGetValue(pos, out var info) && info.explored;
        }

        private bool IsDeadEnd(Vector2Int pos)
        {
            return memory.TryGetValue(pos, out var info) && info.deadEnd;
        }

        private bool IsBlocked(Vector2Int pos)
        {
            return memory.TryGetValue(pos, out var info) && info.blocked;
        }

        private void MarkExplored(Vector2Int pos)
        {
            if (!memory.TryGetValue(pos, out ScentMemory info))
                return;

            info.explored = true;
            memory[pos] = info;
        }

        private void MarkDeadEnd(Vector2Int pos)
        {
            if (!memory.TryGetValue(pos, out ScentMemory info))
                return;

            info.deadEnd = true;
            memory[pos] = info;
        }

        private void MarkBlocked(Vector2Int pos)
        {
            if (!memory.TryGetValue(pos, out ScentMemory info))
            {
                info = new ScentMemory
                {
                    location = pos,
                    scentKey = scentKey,
                    timeChecked = Time.time,
                    timeLastIncreased = -999f,
                    lastVisitTime = -1f,
                    cameFrom = pos,
                    hasCameFrom = false
                };
            }

            info.blocked = true;
            memory[pos] = info;
        }

        private void SetCameFrom(Vector2Int pos, Vector2Int fromPos)
        {
            if (!memory.TryGetValue(pos, out ScentMemory info))
            {
                info = new ScentMemory
                {
                    location = pos,
                    scentKey = scentKey,
                    timeChecked = Time.time,
                    timeLastIncreased = -999f,
                    lastVisitTime = -1f
                };
            }

            info.cameFrom = fromPos;
            info.hasCameFrom = true;
            memory[pos] = info;
        }
    }
}
