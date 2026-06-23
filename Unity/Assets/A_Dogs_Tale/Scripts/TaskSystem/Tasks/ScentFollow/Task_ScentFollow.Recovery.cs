#nullable enable
using DogGame.LLM;
using DogGame.Modules;
using UnityEngine;

namespace DogGame.Tasks
{
    public sealed partial class Task_ScentFollow
    {
        private const float FrontierUsefulScentThreshold = 0.08f;
        private const float FrontierStaleNeighborSeconds = 8f;

        private TaskTickResult EnterBacktrackOrCastSearch(
            ScentPerceptionModule scentModule,
            Vector2Int centerPos,
            int height,
            string reason)
        {
            PenalizeTrailUncertainty(0.18f);

            if (TryFindBestFrontier(scentModule, centerPos, height, out Vector2Int frontier, out float frontierScore))
            {
                activeBacktrackTarget = frontier;
                Debug.Log($"[ScentFollow] Frontier selected {frontier} score={frontierScore:0.000} ({reason})");
                EnterState(ScentFollowState.Backtrack, reason);
                return TaskTickResult.Running();
            }

            activeBacktrackTarget = null;
            castSearchAnchor = lastStrongScentLocation ?? centerPos;
            currentCastRadius = 1;
            EnterState(ScentFollowState.CastSearch, reason);
            return TaskTickResult.Running();
        }

        private bool TryFindBestFrontier(
            ScentPerceptionModule scentModule,
            Vector2Int currentPos,
            int height,
            out Vector2Int frontierPos,
            out float frontierScore)
        {
            frontierPos = default;
            frontierScore = float.NegativeInfinity;

            foreach (var kvp in memory)
            {
                Vector2Int pos = kvp.Key;
                ScentMemory info = kvp.Value;

                if (pos == currentPos)
                    continue;

                if (!IsFrontierCandidate(scentModule, pos, height, info))
                    continue;

                float score = ScoreFrontier(currentPos, pos, info);
                if (score > frontierScore)
                {
                    frontierScore = score;
                    frontierPos = pos;
                }
            }

            return frontierScore > float.NegativeInfinity;
        }

        private bool IsFrontierCandidate(
            ScentPerceptionModule scentModule,
            Vector2Int pos,
            int height,
            ScentMemory info)
        {
            if (!info.sniffed && !info.visited)
                return false;

            if (info.blocked || info.deadEnd || info.targetDetected)
                return false;

            if (info.peakCombinedScent < FrontierUsefulScentThreshold)
                return false;

            return HasUsefulFrontierNeighbor(scentModule, pos, height);
        }

        private bool HasUsefulFrontierNeighbor(ScentPerceptionModule scentModule, Vector2Int pos, int height)
        {
            foreach (DirFlags dirFlag in DirFlagsEx.All8)
            {
                Vector2Int neighborPos = pos + dirFlag.ToVector2Int();
                if (!TryIsValidCellAt(scentModule, neighborPos, height))
                    continue;

                if (!memory.TryGetValue(neighborPos, out ScentMemory neighbor))
                    return true;

                if (neighbor.blocked)
                    continue;

                if (!neighbor.explored || WasRecentlyIncreased(neighborPos))
                    return true;

                float seenAge = Time.time - neighbor.timeChecked;
                if (seenAge > FrontierStaleNeighborSeconds)
                    return true;
            }

            return false;
        }

        private float ScoreFrontier(Vector2Int currentPos, Vector2Int pos, ScentMemory info)
        {
            float seenAge = Time.time - info.timeChecked;
            float trustedStrength = info.peakCombinedScent * FreshnessFactor01(seenAge);
            int manhattan = Mathf.Abs(pos.x - currentPos.x) + Mathf.Abs(pos.y - currentPos.y);

            float score = trustedStrength;
            score -= manhattan * 0.03f;
            score -= info.visitCount * 0.08f;

            if (WasRecentlyIncreased(pos))
                score += 0.25f;

            if (info.visited && !info.explored)
                score += 0.15f;

            if (lastStrongScentLocation.HasValue)
            {
                int anchorDistance = Mathf.Abs(pos.x - lastStrongScentLocation.Value.x) +
                                     Mathf.Abs(pos.y - lastStrongScentLocation.Value.y);
                score -= anchorDistance * 0.01f;
            }

            return score;
        }

        private void RewardTrailProgress(float strength01, bool improved)
        {
            float gain = strength01 >= trailFollowThreshold01 ? 0.08f : 0.03f;

            if (improved)
                gain += 0.04f;

            trailConfidence = Mathf.Clamp01(trailConfidence + gain);
        }

        private void PenalizeTrailUncertainty(float amount)
        {
            trailConfidence = Mathf.Clamp01(trailConfidence - amount);
        }
    }
}
