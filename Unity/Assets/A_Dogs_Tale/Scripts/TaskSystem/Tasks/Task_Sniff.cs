#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DogGame.LLM;
using DogGame.Modules;
using UnityEngine;

namespace DogGame.Tasks
{
    /// <summary>
    /// One-shot command that samples the scent selected in ScentRegistry at the
    /// sniffing agent's cell and in each traversable neighboring direction.
    /// </summary>
    public sealed class Task_Sniff : IAgentTask, ITaskWithReport
    {
        private const float GroundWeight = 0.7f;
        private const float AirWeight = 0.3f;
        private const float ComparisonEpsilon = 0.000001f;

        public string DebugName => "Sniff";
        public string Description = "Samples the selected scent and reports its strength and uphill directions.";

        // Retained for callers that consume the task's detected-scent results.
        public readonly List<DetectedScent> AirScents = new();
        public readonly List<DetectedScent> GroundScents = new();

        private readonly HashSet<string> ignoreKeys;
        private readonly ScentSource? requestedScent;
        private bool executed;
        private bool hasReport;
        private string reportJson = string.Empty;

        public Task_Sniff(HashSet<string>? ignoreKeys = null)
        {
            this.ignoreKeys = ignoreKeys ?? new HashSet<string>();
        }

        /// <summary>Use this overload when an AI is sniffing for a specific scent.</summary>
        public Task_Sniff(ScentSource selectedScent, HashSet<string>? ignoreKeys)
        {
            requestedScent = selectedScent;
            this.ignoreKeys = ignoreKeys ?? new HashSet<string>();
        }

        public static void RunTask_Sniff(TaskContext context)
        {
            var task = new Task_Sniff();
            task.Start(context);
            task.Tick(context, 0f);
        }

        public void Start(TaskContext context)
        {
            executed = false;
            hasReport = false;
            reportJson = string.Empty;
            AirScents.Clear();
            GroundScents.Clear();
        }

        public void Stop(TaskContext context)
        {
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            if (executed)
                return TaskTickResult.Succeeded();

            executed = true;
            WorldObject? agent = context.Agent;
            if (agent == null)
                return TaskTickResult.Failed("missing_agent");

            ScentPerceptionModule scentModule = agent.scentPerceptionModule;
            if (scentModule == null)
                return TaskTickResult.Failed("missing_scent_perception_module");

            if (agent.locationModule == null || agent.locationModule.cell == null)
                return TaskTickResult.Failed("missing_location");

            ScentRegistry? registry = scentModule.dir != null ? scentModule.dir.scentRegistry : null;
            ScentSource? selectedScent = ResolveTargetScent(agent, scentModule, registry);
            if (selectedScent == null)
            {
                Debug.Log($"[Task_Sniff] agent={agent.DisplayName}: no scent detected", agent);
                return TaskTickResult.Succeeded();
            }

            string scentKey = registry!.BuildScentKey(selectedScent);
            if (ignoreKeys.Contains(scentKey))
            {
                Debug.Log($"[Task_Sniff] agent={agent.DisplayName}: selected scent {scentKey} is ignored", agent);
                return TaskTickResult.Succeeded();
            }

            SniffResult result = SampleSelectedScent(agent, scentModule, selectedScent, scentKey);
            PopulateDetectedScents(result);
            reportJson = BuildReportJson(context, result);
            hasReport = true;
            LogResult(agent, result);
            return TaskTickResult.Succeeded();
        }

        private ScentSource? ResolveTargetScent(
            WorldObject agent,
            ScentPerceptionModule scentModule,
            ScentRegistry? registry)
        {
            if (requestedScent != null)
                return requestedScent;
            if (registry?.SelectedTargetScent != null)
                return registry.SelectedTargetScent;
            if (registry == null)
                return null;

            List<ScentDetection> detections = registry.CollectScentsAtCell(
                agent.locationModule.cell,
                scentModule.scentSystem);
            for (int i = 0; i < detections.Count; i++)
            {
                ScentSource source = detections[i].scentSource;
                if (source != null && !ignoreKeys.Contains(registry.BuildScentKey(source)))
                    return source;
            }

            return null;
        }

        public bool TryGetReportJson(out string reportJsonOut)
        {
            reportJsonOut = reportJson;
            return hasReport && !string.IsNullOrWhiteSpace(reportJsonOut);
        }

        private static SniffResult SampleSelectedScent(
            WorldObject agent,
            ScentPerceptionModule scentModule,
            ScentSource selectedScent,
            string scentKey)
        {
            Vector2Int center = agent.locationModule.pos2;
            int height = agent.locationModule.height;
            SampleStrength(scentModule, scentKey, center, height, out float centerAir, out float centerGround);
            float centerCombined = Combine(centerAir, centerGround);

            var result = new SniffResult
            {
                scentKey = scentKey,
                scentName = string.IsNullOrWhiteSpace(selectedScent.scentName) ? scentKey : selectedScent.scentName,
                category = selectedScent.category,
                sourceAgentId = selectedScent.agentId,
                cell = center,
                worldLocation = agent.locationModule.pos3d,
                air = centerAir,
                ground = centerGround,
                combined = centerCombined,
                isLocalMaximum = true,
                strongestDirection = DirFlags.None,
                strongestNeighborStrength = 0f
            };

            Pathfinding? pathfinding = scentModule.dir != null ? scentModule.dir.pathfinding : null;
            foreach (DirFlags direction in DirFlagsEx.All8)
            {
                if (pathfinding == null || !pathfinding.IsStepTraversable(center, direction))
                    continue;

                Vector2Int neighbor = center + direction.ToVector2Int();
                SampleStrength(scentModule, scentKey, neighbor, height, out float air, out float ground);
                float combined = Combine(air, ground);
                if (result.strongestDirection == DirFlags.None || combined > result.strongestNeighborStrength)
                {
                    result.strongestDirection = direction;
                    result.strongestNeighborStrength = combined;
                }

                if (combined > centerCombined + ComparisonEpsilon)
                {
                    result.increasingDirections.Add(direction);
                    result.isLocalMaximum = false;
                }
            }

            return result;
        }

        private static void SampleStrength(
            ScentPerceptionModule scentModule,
            string scentKey,
            Vector2Int position,
            int height,
            out float air,
            out float ground)
        {
            scentModule.TryGetScentStrengthAtCell(scentKey, position, height, ScentMedium.Air, out air);
            scentModule.TryGetScentStrengthAtCell(scentKey, position, height, ScentMedium.Ground, out ground);
        }

        private static float Combine(float air, float ground)
        {
            return ground * GroundWeight + air * AirWeight;
        }

        private void PopulateDetectedScents(SniffResult result)
        {
            float now = Time.time;
            if (result.air > 0f)
                AirScents.Add(MakeDetected(result, ScentMedium.Air, result.air, now));
            if (result.ground > 0f)
                GroundScents.Add(MakeDetected(result, ScentMedium.Ground, result.ground, now));
        }

        private static DetectedScent MakeDetected(SniffResult result, ScentMedium medium, float strength, float time)
        {
            return new DetectedScent
            {
                scentKey = result.scentKey,
                scentName = result.scentName,
                category = result.category,
                medium = medium,
                strength01 = strength,
                cell = result.cell,
                time = time,
                agentId = result.sourceAgentId,
                ignored = false
            };
        }

        private static void LogResult(WorldObject agent, SniffResult result)
        {
            string increasing = JoinDirections(result.increasingDirections);
            string strongest = result.strongestDirection == DirFlags.None
                ? "None"
                : DirFlagsEx.ToLongName(result.strongestDirection);

            Debug.Log(
                $"[Task_Sniff] scent={result.scentName} ({result.scentKey}), " +
                $"sniffingAgent={agent.DisplayName} ({agent.ObjectId}), " +
                $"worldLocation=Vector3Int({result.worldLocation.x},{result.worldLocation.y},{result.worldLocation.z}), " +
                $"cell=Vector2Int({result.cell.x},{result.cell.y}), " +
                $"strengthAbsolute(combined={result.combined:0.######},air={result.air:0.######},ground={result.ground:0.######}), " +
                $"increasingDirections=[{increasing}], localMaximum={result.isLocalMaximum}, " +
                $"strongestDirection={strongest}, strongestNeighborStrength={result.strongestNeighborStrength:0.######}",
                agent);
        }

        private static string BuildReportJson(TaskContext context, SniffResult result)
        {
            string strongest = result.strongestDirection == DirFlags.None
                ? "None"
                : DirFlagsEx.ToLongName(result.strongestDirection);

            return "{"
                + "\"schema\":\"SniffReportV2\","
                + "\"originRequestId\":\"" + EscapeJson(context.OriginRequestId ?? string.Empty) + "\","
                + "\"agentObjectId\":" + context.Agent!.ObjectId.ToString(CultureInfo.InvariantCulture) + ","
                + "\"scentKey\":\"" + EscapeJson(result.scentKey) + "\","
                + "\"scentName\":\"" + EscapeJson(result.scentName) + "\","
                + "\"worldLocation\":[" + result.worldLocation.x + "," + result.worldLocation.y + "," + result.worldLocation.z + "],"
                + "\"cell\":[" + result.cell.x + "," + result.cell.y + "],"
                + "\"strengthUnit\":\"absolute\","
                + "\"strength\":{\"combined\":" + Format(result.combined) + ",\"air\":" + Format(result.air) + ",\"ground\":" + Format(result.ground) + "},"
                + "\"increasingDirections\":" + MakeDirectionsJson(result.increasingDirections) + ","
                + "\"localMaximum\":" + (result.isLocalMaximum ? "true" : "false") + ","
                + "\"strongestDirection\":\"" + strongest + "\","
                + "\"strongestNeighborStrength\":" + Format(result.strongestNeighborStrength)
                + "}";
        }

        private static string JoinDirections(List<DirFlags> directions)
        {
            var text = new StringBuilder();
            for (int i = 0; i < directions.Count; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(DirFlagsEx.ToLongName(directions[i]));
            }
            return text.ToString();
        }

        private static string MakeDirectionsJson(List<DirFlags> directions)
        {
            var text = new StringBuilder("[");
            for (int i = 0; i < directions.Count; i++)
            {
                if (i > 0) text.Append(',');
                text.Append('\"').Append(DirFlagsEx.ToLongName(directions[i])).Append('\"');
            }
            return text.Append(']').ToString();
        }

        private static string Format(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private sealed class SniffResult
        {
            public string scentKey = string.Empty;
            public string scentName = string.Empty;
            public ScentCategory category;
            public int sourceAgentId;
            public Vector2Int cell;
            public Vector3Int worldLocation;
            public float air;
            public float ground;
            public float combined;
            public bool isLocalMaximum;
            public DirFlags strongestDirection;
            public float strongestNeighborStrength;
            public readonly List<DirFlags> increasingDirections = new();
        }
    }
}
