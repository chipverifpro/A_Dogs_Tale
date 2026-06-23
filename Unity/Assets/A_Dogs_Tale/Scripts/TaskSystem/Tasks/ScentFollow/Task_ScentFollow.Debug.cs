#nullable enable
using System.Text;
using UnityEngine;

namespace DogGame.Tasks
{
    public sealed partial class Task_ScentFollow
    {
        private readonly struct CandidateDebugInfo
        {
            public CandidateDebugInfo(Vector2Int pos, float strength, float trustedStrength, float improvement, float score)
            {
                Pos = pos;
                Strength = strength;
                TrustedStrength = trustedStrength;
                Improvement = improvement;
                Score = score;
            }

            public readonly Vector2Int Pos;
            public readonly float Strength;
            public readonly float TrustedStrength;
            public readonly float Improvement;
            public readonly float Score;
        }

        private readonly System.Collections.Generic.List<CandidateDebugInfo> lastCandidateDebug = new();
        private Vector2Int lastCandidateDebugCenter;

        public bool TryGetReportJson(out string reportJson)
        {
            if (reportStatus == "running")
            {
                reportJson = "";
                return false;
            }

            reportJson =
                "{" +
                "\"task\":\"scent_follow\"," +
                $"\"status\":\"{EscapeJson(reportStatus)}\"," +
                $"\"reason\":\"{EscapeJson(reportReason)}\"," +
                $"\"scentKey\":\"{EscapeJson(scentKey)}\"," +
                $"\"medium\":\"{medium}\"," +
                $"\"state\":\"{currentState}\"," +
                $"\"cell\":\"{reportCell.x},{reportCell.y}\"," +
                $"\"strength\":{reportStrength:0.000}," +
                $"\"trailConfidence\":{trailConfidence:0.000}," +
                $"\"steps\":{stepsTaken}" +
                "}";
            return true;
        }

        private void BeginCandidateDebug(Vector2Int centerPos)
        {
            lastCandidateDebugCenter = centerPos;
            lastCandidateDebug.Clear();
        }

        private void RecordCandidateDebug(Vector2Int pos, float strength, float trustedStrength, float improvement, float score)
        {
            lastCandidateDebug.Add(new CandidateDebugInfo(pos, strength, trustedStrength, improvement, score));
        }

        private void DebugPrintScentFollowSnapshot(Vector2Int centerPos)
        {
            StringBuilder sb = new();
            sb.AppendLine(
                $"[ScentFollow] state={currentState} scent={scentKey} medium={medium} " +
                $"cell={centerPos} confidence={trailConfidence:0.00} steps={stepsTaken} " +
                $"lastStrong={(lastStrongScentLocation.HasValue ? lastStrongScentLocation.Value.ToString() : "none")} " +
                $"lastBest={lastBestPos} bestStrength={lastBestStrength:0.000} " +
                $"castRadius={currentCastRadius}");

            if (activeBacktrackTarget.HasValue)
                sb.AppendLine($"  backtrackTarget={activeBacktrackTarget.Value}");

            if (lastCandidateDebug.Count > 0)
            {
                sb.AppendLine($"  candidates from {lastCandidateDebugCenter}:");
                for (int i = 0; i < lastCandidateDebug.Count; i++)
                {
                    CandidateDebugInfo candidate = lastCandidateDebug[i];
                    sb.AppendLine(
                        $"    {candidate.Pos} strength={candidate.Strength:0.000} " +
                        $"trusted={candidate.TrustedStrength:0.000} improve={candidate.Improvement:0.000} " +
                        $"score={candidate.Score:0.000}");
                }
            }

            Debug.Log(sb.ToString());
        }

        private void DebugPrintScentMemory()
        {
            if (memory.Count == 0)
            {
                Debug.Log("[ScentFollow] Memory is empty");
                return;
            }

            StringBuilder sb = new();
            sb.AppendLine($"[ScentFollow] Memory ({memory.Count} cells):");

            foreach (var kvp in memory)
            {
                Vector2Int pos = kvp.Key;
                ScentMemory cell = kvp.Value;

                sb.AppendLine(
                    $"  {pos.x,3},{pos.y,3} | " +
                    $"air={cell.airScent:0.000} " +
                    $"ground={cell.groundScent:0.000} " +
                    $"combined={cell.combinedScent:0.000} " +
                    $"peak={cell.peakCombinedScent:0.000} " +
                    $"sniffed={cell.sniffed} " +
                    $"visited={cell.visited} " +
                    $"visits={cell.visitCount} " +
                    $"explored={cell.explored} " +
                    $"deadEnd={cell.deadEnd} " +
                    $"blocked={cell.blocked} " +
                    $"lastSeen={Time.time - cell.timeChecked:0.0}s ago"
                );
            }

            Debug.Log(sb.ToString());
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}
