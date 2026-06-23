#nullable enable
using System;
using DogGame.Modules;
using UnityEngine;
using static DungeonGenerator;

namespace DogGame.Tasks
{
    public sealed partial class Task_ScentFollow
    {
        private readonly struct ScentMoveCandidate
        {
            public ScentMoveCandidate(
                DirFlags directionFlag,
                Vector2Int pos,
                Vector2Int direction,
                float strength01,
                float trustedStrength01,
                float improvement01,
                float seenAgeSeconds)
            {
                DirectionFlag = directionFlag;
                Pos = pos;
                Direction = direction;
                Strength01 = strength01;
                TrustedStrength01 = trustedStrength01;
                Improvement01 = improvement01;
                SeenAgeSeconds = seenAgeSeconds;
            }

            public readonly DirFlags DirectionFlag;
            public readonly Vector2Int Pos;
            public readonly Vector2Int Direction;
            public readonly float Strength01;
            public readonly float TrustedStrength01;
            public readonly float Improvement01;
            public readonly float SeenAgeSeconds;
        }

        private bool TryPickNextStep(
            ScentPerceptionModule scentModule,
            Vector2Int centerPos,
            int height,
            bool exploring,
            out DirFlags bestDir,
            out Vector2Int bestPos,
            out float bestStrength,
            out float bestScore)
        {
            bestDir = DirFlags.None;
            bestPos = default;
            bestStrength = 0f;
            bestScore = float.NegativeInfinity;

            float currentStrength = GetLastStrength(centerPos);
            bool foundCandidate = false;
            BeginCandidateDebug(centerPos);

            foreach (DirFlags dir in DirFlagsEx.All8)
            {
                if (!TryBuildCandidate(scentModule, centerPos, height, dir, currentStrength, out ScentMoveCandidate candidate))
                    continue;

                bool isImmediateBacktrack = candidate.Pos == prevCellPos;
                if (!exploring)
                {
                    const float downhillTolerance = 0.02f;
                    if (candidate.TrustedStrength01 + downhillTolerance < currentStrength && isImmediateBacktrack)
                        continue;
                }

                float score = ScoreCandidate(candidate, currentStrength, isImmediateBacktrack, exploring);
                RecordCandidateDebug(candidate.Pos, candidate.Strength01, candidate.TrustedStrength01, candidate.Improvement01, score);
                foundCandidate = true;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = candidate.DirectionFlag;
                    bestPos = candidate.Pos;
                    bestStrength = candidate.Strength01;
                }
            }

            MarkExplored(centerPos);
            if (!foundCandidate)
                MarkDeadEnd(centerPos);

            return bestDir != DirFlags.None;
        }

        private bool TryBuildCandidate(
            ScentPerceptionModule scentModule,
            Vector2Int centerPos,
            int height,
            DirFlags directionFlag,
            float currentStrength01,
            out ScentMoveCandidate candidate)
        {
            Vector2Int direction = directionFlag.ToVector2Int();
            Vector2Int pos = centerPos + direction;
            candidate = default;

            if (IsBlocked(pos))
                return false;

            if (!TryIsValidMoveCandidate(scentModule, centerPos, pos, height, directionFlag))
            {
                MarkBlocked(pos);
                return false;
            }

            float strength01 = GetLastStrength(pos);
            if (strength01 <= 0f && scentModule.TryGetScentStrengthAtCell(scentKey, pos, height, medium, out float liveStrength))
                strength01 = liveStrength;

            if (strength01 < minDetectableScent01)
                return false;

            float seenAgeSeconds = GetSeenAgeSeconds(pos);
            float trustedStrength01 = strength01 * FreshnessFactor01(seenAgeSeconds);
            float improvement01 = trustedStrength01 - currentStrength01;

            candidate = new ScentMoveCandidate(
                directionFlag,
                pos,
                direction,
                strength01,
                trustedStrength01,
                improvement01,
                seenAgeSeconds);
            return true;
        }

        private bool TryIsValidMoveCandidate(
            ScentPerceptionModule scentModule,
            Vector2Int centerPos,
            Vector2Int candidatePos,
            int height,
            DirFlags directionFlag)
        {
            if (!TryIsValidCellAt(scentModule, candidatePos, height))
                return false;

            if (!directionFlag.IsDiagonal())
                return true;

            Vector2Int step = directionFlag.ToVector2Int();
            Vector2Int horizontalPos = new(centerPos.x + step.x, centerPos.y);
            Vector2Int verticalPos = new(centerPos.x, centerPos.y + step.y);

            bool horizontalOpen = TryIsValidCellAt(scentModule, horizontalPos, height);
            bool verticalOpen = TryIsValidCellAt(scentModule, verticalPos, height);

            return horizontalOpen || verticalOpen;
        }

        private float ScoreCandidate(ScentMoveCandidate candidate, float currentStrength01, bool isImmediateBacktrack, bool exploring)
        {
            float score = wStrength * candidate.TrustedStrength01;

            if (candidate.Improvement01 > smallImprovementThreshold01)
                score += candidate.Improvement01 * wImprovement;

            if (WasRecentlyIncreased(candidate.Pos))
                score += wIncreaseBonus;

            score += DirectionBonus(candidate.Direction);

            if (IsExplored(candidate.Pos))
                score -= wExploredPenalty;

            int visits = GetVisitCount(candidate.Pos);
            score -= visits * wVisitPenalty;

            float lastVisit = GetLastVisitTime(candidate.Pos);
            float dtVisit = Time.time - lastVisit;
            if (dtVisit >= 0f && dtVisit < recentVisitWindowSeconds)
            {
                float t = 1f - dtVisit / recentVisitWindowSeconds;
                score -= wRecentVisitPenalty * t;
            }

            if (IsRecentCell(candidate.Pos))
                score -= wRecentVisitPenalty;

            if (isImmediateBacktrack)
                score -= exploring ? immediateBacktrackPenalty * 0.25f : immediateBacktrackPenalty;

            score -= TurnPenalty(candidate.Direction, candidate.Strength01, currentStrength01);

            if (IsDeadEnd(candidate.Pos) && !WasRecentlyIncreased(candidate.Pos))
                score -= wDeadEndPenalty;

            if (exploring)
            {
                float noveltyBonus = visits == 0 ? 0.35f : 0.15f / (1f + visits);
                if (dtVisit > recentVisitWindowSeconds)
                    noveltyBonus += 0.10f;

                score += noveltyBonus;
            }

            if (candidate.SeenAgeSeconds > staleMemorySeconds)
            {
                float extra = (candidate.SeenAgeSeconds - staleMemorySeconds) / staleMemorySeconds;
                score -= wStalePenalty * Mathf.Clamp01(extra);
            }

            if (candidate.SeenAgeSeconds < 2.0f)
                score += RiseBonus01(GetRiseDelta(candidate.Pos));

            return score;
        }

        public Vector2Int JumpToHighestScore(Vector2Int currentPos)
        {
            float hiScore = float.NegativeInfinity;
            Vector2Int bestPos = currentPos;
            float currentStrength = GetLastStrength(currentPos);

            foreach (var kvp in memory)
            {
                Vector2Int pos = kvp.Key;
                ScentMemory info = kvp.Value;

                if (info.blocked || info.deadEnd)
                    continue;

                float strength01 = info.peakCombinedScent;
                if (strength01 < minDetectableScent01)
                    continue;

                float seenAge = Time.time - info.timeChecked;
                Vector2Int direction = ClampStep(pos - currentPos);
                ScentMoveCandidate candidate = new(
                    DirFlagsEx.FromVector2Int(direction),
                    pos,
                    direction,
                    strength01,
                    strength01 * FreshnessFactor01(seenAge),
                    strength01 - currentStrength,
                    seenAge);

                float score = ScoreCandidate(candidate, currentStrength, isImmediateBacktrack: false, exploring: true);
                int manhattan = Mathf.Abs(pos.x - currentPos.x) + Mathf.Abs(pos.y - currentPos.y);
                score -= 0.01f * manhattan;

                if (score > hiScore)
                {
                    hiScore = score;
                    bestPos = pos;
                }
            }

            return bestPos;
        }

        private float FreshnessFactor01(float seenAgeSeconds)
        {
            if (seenAgeSeconds <= 0f)
                return 1f;

            float staleTime = Mathf.Max(1f, staleMemorySeconds);
            float t = Mathf.Clamp01(seenAgeSeconds / staleTime);
            return Mathf.Lerp(1f, 0.4f, t);
        }

        private float DirectionBonus(Vector2Int candidateDirection)
        {
            if (!previousMoveDirection.HasValue)
                return 0f;

            Vector2 previous = previousMoveDirection.Value;
            Vector2 candidate = candidateDirection;
            if (previous.sqrMagnitude <= 0.0001f || candidate.sqrMagnitude <= 0.0001f)
                return 0f;

            previous.Normalize();
            candidate.Normalize();
            return Vector2.Dot(previous, candidate) * wDirection;
        }

        private float TurnPenalty(Vector2Int candidateDirection, float candidateStrength01, float currentStrength01)
        {
            if (!previousMoveDirection.HasValue)
                return 0f;

            Vector2 previous = previousMoveDirection.Value;
            Vector2 candidate = candidateDirection;
            if (previous.sqrMagnitude <= 0.0001f || candidate.sqrMagnitude <= 0.0001f)
                return 0f;

            previous.Normalize();
            candidate.Normalize();
            float alignment = Vector2.Dot(previous, candidate);
            if (alignment >= 0.5f)
                return 0f;

            float scentDifference = Mathf.Abs(candidateStrength01 - currentStrength01);
            float similarScentFactor = 1f - Mathf.Clamp01(scentDifference / smallImprovementThreshold01);
            float turnSharpness = alignment < 0f ? 1f : 0.5f;
            return wTurnPenalty * turnSharpness * similarScentFactor;
        }

        private float RiseBonus01(float delta01)
        {
            if (delta01 <= 0f)
                return 0f;

            float t = Mathf.Clamp01(delta01 / riseScale);
            return wRiseBonus * t;
        }

        public bool TryEstimateScentGradient(
            string scentKey,
            Vector2Int centerPos,
            int height,
            ScentMedium medium,
            int radius,
            out DirFlags bestDir,
            out float confidence01)
        {
            bestDir = DirFlags.None;
            confidence01 = 0f;

            if (string.IsNullOrWhiteSpace(scentKey))
                return false;

            if (dir == null || dir.gen == null || dir.gen.hf == null)
                return false;

            radius = Mathf.Clamp(radius, 1, 3);
            Vector2 gradient = Vector2.zero;
            float totalWeight = 0f;
            float centerStrength = GetScentStrengthAtCellUnsafe(scentKey, centerPos, height, medium);

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int chebyshev = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    float distWeight = chebyshev switch
                    {
                        1 => 1.0f,
                        2 => 0.5f,
                        3 => 0.33f,
                        _ => 0.0f
                    };
                    if (distWeight <= 0f)
                        continue;

                    Vector2Int pos = new(centerPos.x + dx, centerPos.y + dy);

                    dir.gen.hf.TryQueryAt(pos.x, pos.y, height, 50, out NeighborMatch match);
                    if (match.roomId < 0 || match.cellId < 0)
                        continue;

                    Cell cell = dir.gen.rooms[match.roomId].cells[match.cellId];
                    if (cell.scents == null)
                        continue;

                    if (!TryGetScentStrengthInCell(cell, scentKey, medium, out float strength))
                        continue;

                    float delta = Mathf.Max(0f, strength - centerStrength);
                    float diagonalPenalty = dx != 0 && dy != 0 ? 0.85f : 1.0f;
                    float weight = distWeight * diagonalPenalty;

                    Vector2 dirVec = new(dx, dy);
                    float mag = dirVec.magnitude;
                    if (mag > 0.0001f)
                        dirVec /= mag;

                    gradient += dirVec * (delta * weight);
                    totalWeight += delta * weight;
                }
            }

            if (totalWeight <= 0f || gradient.sqrMagnitude <= 0.000001f)
                return false;

            bestDir = DirFlagsEx.FromVector2(gradient);
            confidence01 = Mathf.Clamp01(totalWeight * 2.0f);
            return bestDir != DirFlags.None;
        }

        private static bool TryGetScentStrengthInCell(Cell cell, string scentKey, ScentMedium medium, out float strength)
        {
            strength = 0f;

            if (cell.scents == null || cell.scents.Count == 0)
                return false;

            for (int i = 0; i < cell.scents.Count; i++)
            {
                ScentInCell scent = cell.scents[i];
                string cellKey = $"agent:{scent.agentId}";
                if (!string.Equals(cellKey, scentKey, StringComparison.Ordinal))
                    continue;

                strength = medium == ScentMedium.Ground ? scent.groundIntensity : scent.airIntensity;
                return strength > 0f;
            }

            return false;
        }

        private float GetScentStrengthAtCellUnsafe(string scentKey, Vector2Int pos, int height, ScentMedium medium)
        {
            if (dir == null || dir.gen == null || dir.gen.hf == null)
                return 0f;

            dir.gen.hf.TryQueryAt(pos.x, pos.y, height, 50, out NeighborMatch match);
            if (match.roomId < 0 || match.cellId < 0)
                return 0f;

            Cell cell = dir.gen.rooms[match.roomId].cells[match.cellId];
            if (cell.scents == null)
                return 0f;

            return TryGetScentStrengthInCell(cell, scentKey, medium, out float strength) ? strength : 0f;
        }
    }
}
