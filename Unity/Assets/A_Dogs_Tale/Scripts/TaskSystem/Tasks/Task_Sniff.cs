#nullable enable
using System.Collections.Generic;
using UnityEngine;
using DogGame.Modules;
using DogGame.LLM;
using static DungeonGenerator;
using System.Globalization;

namespace DogGame.Tasks
{
    /// <summary>
    /// One-shot sniff task.
    /// Gathers scents at the agent's current cell, filters ignored scents,
    /// and reports air/ground scents sorted by strength.
    /// </summary>
    public sealed class Task_Sniff : IAgentTask, ITaskWithReport
    {
        public string DebugName => "Sniff";

        private readonly HashSet<string> ignoreKeys;
        private bool executed;

        // Results (can be consumed by UI / follow-up tasks later)
        public readonly List<DetectedScent> AirScents = new();
        public readonly List<DetectedScent> GroundScents = new();

        public Task_Sniff(HashSet<string>? ignoreKeys = null)
        {
            this.ignoreKeys = ignoreKeys ?? new HashSet<string>();
        }

        public static void RunTask_Sniff(TaskContext context)
        {
            Task_Sniff task = new();
            task.executed = false;
            Debug.Log($"TaskSniff.RunTask_Sniff [{context.Agent.DisplayName}]");
            task.Tick(context, 0.0f);
        }

        public void Stop(TaskContext context)
        {
            // No cleanup needed (one-shot task)
        }

        // -------------------------------------------------------
        // Internals
        // -------------------------------------------------------

        private void SniffAtCurrentCell(
            TaskContext context,
            ScentPerceptionModule scentModule)
        {
            Cell cell = context.Agent.locationModule.cell;
            var detections = scentModule.dir!.scentRegistry
                .CollectScentsAtCell(cell, scentModule.scentSystem);

            if (detections == null || detections.Count == 0)
                return;

            float timeNow = Time.time;
            Vector2Int cellPos = cell.pos;

            foreach (var det in detections)
            {
                if (det.scentSource == null)
                    continue;

                float strength01 = Mathf.Clamp01(det.combinedStrength);
                if (strength01 < scentModule.minStrength01)
                    continue;

                string scentKey = BuildScentKey(det.scentSource);
                if (ignoreKeys.Contains(scentKey))
                    continue;

                // Split air / ground if available
                float air01 = TryGetAirStrength01(det);
                float ground01 = TryGetGroundStrength01(det);

                // Fallback if system only provides combined
                if (air01 <= 0f && ground01 <= 0f)
                    air01 = strength01;

                if (air01 > 0f)
                {
                    AirScents.Add(MakeDetected(
                        det, scentKey, ScentMedium.Air, air01, cellPos, timeNow));
                }

                if (ground01 > 0f)
                {
                    GroundScents.Add(MakeDetected(
                        det, scentKey, ScentMedium.Ground, ground01, cellPos, timeNow));
                }
            }

            AirScents.Sort((a, b) => b.strength01.CompareTo(a.strength01));
            GroundScents.Sort((a, b) => b.strength01.CompareTo(a.strength01));

            string airScentKey;
            string groundScentKey;
            DirFlags direction_strongest;
            if (AirScents.Count == 0 && GroundScents.Count == 0)
            {
                Debug.Log("No unmasked scents detected.");
                return;
            }
            if (AirScents.Count > 0)
            {
                airScentKey = AirScents[0].scentKey;
                direction_strongest = SniffNearbyCurrentCell(airScentKey, cell.pos, cell.height, scentModule, sniffGround:true);
            }
            if (GroundScents.Count > 0)
            {
                groundScentKey = GroundScents[0].scentKey;
                direction_strongest = SniffNearbyCurrentCell(groundScentKey, cell.pos, cell.height, scentModule, sniffGround:false);
            }
        }

        public struct RelPos
        {
            public Vector2Int pos;
            public float airScent;
            public float groundScent;
        }

        private DirFlags SniffNearbyCurrentCell(string agentId, Vector2Int centerPos, int height, ScentPerceptionModule scentModule, bool sniffGround)
        {
            //Cell cell = context.Agent.locationModule.cell;
            //Vector2Int centerPos = cell.pos;
            List<RelPos> relPosList = new();
            Cell relCell;
            NeighborMatch match;
                float maxAir = 0f;
                float maxGround = 0f;
                Vector2Int maxAirPos = new(-1,-1);
                Vector2Int maxGroundPos = new(-1,-1);
                DirFlags maxAirDir = DirFlags.None;
                DirFlags maxGroundDir = DirFlags.None; 
            foreach (DirFlags direction in DirFlagsEx.All8)
            {
                Vector2Int relPosLoc = centerPos + direction.ToVector2Int();
                Directory.Instance.gen.hf.TryQueryAt(relPosLoc.x, relPosLoc.y, height, 50, out match);
                if (match.roomId<0 || match.cellId<0) continue; // no cell found
                relCell = Directory.Instance.gen.rooms[match.roomId].cells[match.cellId];

                foreach (ScentInCell scentInCell in relCell.scents)
                {
                    string scentKey = $"agent:{scentInCell.agentId}";
                    Debug.Log($"scentInCell={scentKey} ?? agentId={agentId}");
                    if (scentKey == agentId)
                    {
                        //RelPos relPos = new()
                        //{
                        //    pos = relPosLoc,
                        //    airScent = scentInCell.airIntensity,
                        //    groundScent = scentInCell.groundIntensity
                        //};
                        //relPosList.Add(relPos);
                        if (maxAir < scentInCell.airIntensity)
                        {
                            maxAir = scentInCell.airIntensity;
                            maxAirPos = relPosLoc;
                            maxAirDir = direction;
                        }
                        if (maxGround < scentInCell.groundIntensity)
                        {
                            maxGround = scentInCell.groundIntensity;
                            maxGroundPos = relPosLoc;
                            maxGroundDir = direction;
                        }
                    }
                }
            }
            Debug.Log($"Sniff: strongest air scent for {agentId} is {DirFlagsEx.ToLongName(maxAirDir)} at {maxAirPos} intensity={maxAir}");
            Debug.Log($"Sniff: strongest ground scent for {agentId} is {DirFlagsEx.ToLongName(maxGroundDir)} at {maxGroundPos} intensity={maxGround}");
        
            if (sniffGround) return maxGroundDir;
            return maxAirDir;
        }

        private static DetectedScent MakeDetected(
            ScentDetection det,
            string key,
            ScentMedium medium,
            float strength01,
            Vector2Int cell,
            float time)
        {
            return new DetectedScent
            {
                scentKey = key,
                category = det.scentSource!.category,
                scentName = det.scentSource.scentName ?? det.scentSource.category.ToString(),
                medium = medium,
                strength01 = strength01,
                cell = cell,
                time = time,
                agentId = det.scentSource.agentId,
                ignored = false
            };
        }

        private static void BuildDefaultIgnoreKeys(
            WorldObject agent,
            HashSet<string> ignore)
        {
            // Ignore self
            if (agent.ObjectId >= 0)
                ignore.Add($"agent:{agent.ObjectId}");

            // Ignore pack members (if any)
            var pack = agent.packMemberModule.currentPack;
            if (pack?.packAgentList == null)
                return;

            foreach (var member in pack.packAgentList)
            {
                int id = member.ObjectId;
                if (id >= 0)
                    ignore.Add($"agent:{id}");
            }
        }

        private static string BuildScentKey(ScentSource source)
        {
            if (source.agentId >= 0)
                return $"agent:{source.agentId}";

            string name = string.IsNullOrWhiteSpace(source.scentName)
                ? "Unnamed"
                : source.scentName.Trim();

            return $"{source.category}:{name}";
        }

        private static float TryGetAirStrength01(ScentDetection det)
        {
            // TODO: replace with real field when confirmed
            return det.airStrength;
            //return 0.5f;    // dummy return value nonzero
        }

        private static float TryGetGroundStrength01(ScentDetection det)
        {
            // TODO: replace with real field when confirmed
            return det.groundStrength;
            //return 0.6f;    // dummy return value nonzero
        }

        private void DebugLogResults(WorldObject agent)
        {
            if (AirScents.Count == 0 && GroundScents.Count == 0)
            {
                Debug.Log($"[Task_Sniff] {agent.name}: no scents detected", agent);
                return;
            }

            WorldObject agent_i;
            Debug.Log(
                $"[Task_Sniff] {agent.name}: air={AirScents.Count} ground={GroundScents.Count}",
                agent);
            for (int i=0; i<AirScents.Count; i++)
            {
                WorldObjectRegistry.Instance.TryGet(AirScents[i].agentId, out agent_i);
                Debug.Log($"Air[{i}] = {AirScents[i].agentId}:{agent_i.DisplayName}, strength={AirScents[i].strength01}");
            }
            for (int i=0; i<GroundScents.Count; i++)
            {
                WorldObjectRegistry.Instance.TryGet(GroundScents[i].agentId, out agent_i);
                Debug.Log($"Ground[{i}] = {GroundScents[i].agentId}:{agent_i.DisplayName}, strength={GroundScents[i].strength01}");
            }
        }

        private bool hasReport;
        private string reportJson = "";

        public bool TryGetReportJson(out string reportJsonOut)
        {
            reportJsonOut = reportJson;
            return hasReport && !string.IsNullOrWhiteSpace(reportJsonOut);
        }

        private struct SniffNearbyResult
        {
            public bool ok;
            public string scentKey;
            public ScentMedium medium;
            public DirFlags dir;
            public float intensity;
            public Vector2Int cell;
        }

        private SniffNearbyResult bestAir;
        private SniffNearbyResult bestGround;

        public void Start(TaskContext context)
        {
            executed = false;
            hasReport = false;
            reportJson = "";
            bestAir = default;
            bestGround = default;

            AirScents.Clear();
            GroundScents.Clear();

            if (ignoreKeys.Count == 0 && context.Agent != null)
                BuildDefaultIgnoreKeys(context.Agent, ignoreKeys);
        }

#region Tick
        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            if (executed) return TaskTickResult.Succeeded();
            executed = true;

            if (context.Agent == null)
                return TaskTickResult.Failed("missing_agent");

            var scentModule = context.Agent.scentPerceptionModule;
            if (scentModule == null)
                return TaskTickResult.Failed("missing_scent_perception_module");

            if (context.Agent.locationModule == null)
                return TaskTickResult.Failed("missing_location_module");

            // ---- Perform sniff ----
            SniffAtCurrentCell(context, scentModule);

            // Build a single-line JSON report for the LLM/tooling layer.
            reportJson = BuildReportJson(context);
            hasReport = !string.IsNullOrWhiteSpace(reportJson);

            DebugLogResults(context.Agent);
            return TaskTickResult.Succeeded();
        }
#endregion
#region BuildReportJson
        private string BuildReportJson(TaskContext context)
        {
            // Single-line JSON. Keep it small and deterministic.
            // If you later add a proper JSON writer, swap this.
            string requestId = context.OriginRequestId ?? ""; // see below (TaskContext addition)
            string agentId = context.Agent?.ObjectId.ToString(CultureInfo.InvariantCulture) ?? "-1";

            string bestAirDir = bestAir.ok ? DirFlagsEx.ToLongName(bestAir.dir) : "None";
            string bestGroundDir = bestGround.ok ? DirFlagsEx.ToLongName(bestGround.dir) : "None";

            // Top 3 each (keys + strength). Avoid newlines.
            string airTop = MakeTopListJson(AirScents, 3);
            string groundTop = MakeTopListJson(GroundScents, 3);

            return
                "{"
                + "\"schema\":\"SniffReportV1\","
                + "\"originRequestId\":\"" + EscapeJson(requestId) + "\","
                + "\"agentObjectId\":" + agentId + ","
                + "\"cell\":[" + context.Agent!.locationModule.cell.pos.x + "," + context.Agent.locationModule.cell.pos.y + "],"
                + "\"bestAir\":{\"ok\":" + (bestAir.ok ? "true" : "false") + ",\"scentKey\":\"" + EscapeJson(bestAir.scentKey) + "\",\"dir\":\"" + EscapeJson(bestAirDir) + "\",\"intensity\":" + bestAir.intensity.ToString("0.###", CultureInfo.InvariantCulture) + "},"
                + "\"bestGround\":{\"ok\":" + (bestGround.ok ? "true" : "false") + ",\"scentKey\":\"" + EscapeJson(bestGround.scentKey) + "\",\"dir\":\"" + EscapeJson(bestGroundDir) + "\",\"intensity\":" + bestGround.intensity.ToString("0.###", CultureInfo.InvariantCulture) + "},"
                + "\"airTop\":" + airTop + ","
                + "\"groundTop\":" + groundTop
                + "}";
        }

        private static string MakeTopListJson(List<DetectedScent> list, int max)
        {
            int count = Mathf.Min(max, list.Count);
            if (count <= 0) return "[]";

            // [{"key":"agent:12","s":0.77}, ...]
            var parts = new System.Text.StringBuilder(128);
            parts.Append('[');
            for (int i = 0; i < count; i++)
            {
                if (i > 0) parts.Append(',');
                parts.Append("{\"key\":\"").Append(EscapeJson(list[i].scentKey)).Append("\",\"s\":")
                    .Append(list[i].strength01.ToString("0.###", CultureInfo.InvariantCulture)).Append('}');
            }
            parts.Append(']');
            return parts.ToString();
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
#endregion
    }

}