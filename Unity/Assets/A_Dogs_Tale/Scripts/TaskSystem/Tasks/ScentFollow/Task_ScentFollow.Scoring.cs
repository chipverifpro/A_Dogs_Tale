#nullable enable
using System;
using DogGame.Modules;
using UnityEngine;
using static DungeonGenerator;

namespace DogGame.Tasks
{
    public sealed partial class Task_ScentFollow
    {
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

            float currentStrength = GetKnownStrength(centerPos);

            foreach (DirFlags dir in DirFlagsEx.All8)
            {
                Vector2Int pos = centerPos + dir.ToVector2Int();
                bool isImmediateBacktrack = pos == prevCellPos;

                float strength01 = GetLastStrength(pos);

                if (strength01 <= 0f && scentModule.TryGetScentStrengthAtCell(scentKey, pos, height, medium, out float liveStrength))
                    strength01 = liveStrength;

                if (strength01 <= 0f)
                    continue;

                if (!exploring)
                {
                    const float downhillTolerance = 0.02f;
                    if (strength01 + downhillTolerance < currentStrength && isImmediateBacktrack)
                        continue;
                }

                float delta01 = GetRiseDelta(pos);
                float score = ScoreNeighbor(pos, strength01, isImmediateBacktrack, exploring, delta01);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = dir;
                    bestPos = pos;
                    bestStrength = strength01;
                }
            }

            return bestDir != DirFlags.None;
        }

        private float ScoreNeighbor(Vector2Int pos, float strength01, bool isImmediateBacktrack, bool exploring, float delta01)
        {
            float score = wStrength * strength01;

            const float maxRiseBonus = 0.25f;
            const float localRiseScale = 0.15f;
            if (delta01 > 0f)
                score += Mathf.Clamp(delta01 / localRiseScale, 0f, 1f) * maxRiseBonus;

            int visits = GetVisitCount(pos);
            float visitPenalty = Mathf.Log(1f + visits);
            score -= wVisitPenalty * visitPenalty;

            float lastVisit = GetLastVisitTime(pos);
            float dtVisit = Time.time - lastVisit;
            if (dtVisit >= 0f && dtVisit < recentVisitWindowSeconds)
            {
                float t = 1f - dtVisit / recentVisitWindowSeconds;
                score -= wRecentVisitPenalty * t;
            }

            if (isImmediateBacktrack)
                score -= immediateBacktrackPenalty;

            if (exploring)
            {
                float noveltyBonus = visits == 0 ? 0.35f : 0.15f / (1f + visits);
                if (dtVisit > recentVisitWindowSeconds)
                    noveltyBonus += 0.10f;

                score += noveltyBonus;
            }

            float seenAge = GetSeenAgeSeconds(pos);
            if (seenAge > staleMemorySeconds)
            {
                float extra = (seenAge - staleMemorySeconds) / staleMemorySeconds;
                score -= wStalePenalty * Mathf.Clamp01(extra);
            }

            if (seenAge < 2.0f)
                score += RiseBonus01(delta01);

            return score;
        }

        public Vector2Int JumpToHighestScore(Vector2Int currentPos)
        {
            float hiScore = float.NegativeInfinity;
            Vector2Int bestPos = currentPos;

            foreach (var kvp in memory)
            {
                Vector2Int pos = kvp.Key;
                ScentMemory info = kvp.Value;

                float strength01 = info.peakCombinedScent;
                if (strength01 <= 0f)
                    continue;

                float delta01 = GetStrengthDelta(pos, strength01);
                float score = ScoreNeighbor(pos, strength01, isImmediateBacktrack: false, exploring: true, delta01);

                float seenAge = Time.time - info.timeChecked;
                if (seenAge > staleMemorySeconds)
                {
                    float extra = (seenAge - staleMemorySeconds) / staleMemorySeconds;
                    score -= wStalePenalty * Mathf.Clamp01(extra);
                }

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
