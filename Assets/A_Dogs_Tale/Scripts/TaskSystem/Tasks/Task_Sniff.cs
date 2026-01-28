#nullable enable
using System.Collections.Generic;
using UnityEngine;
using DogGame.Modules;
using DogGame.AI.Perception;
using DogGame.LLM;
using System.Threading.Tasks;

namespace DogGame.Tasks
{
    /// <summary>
    /// One-shot sniff task.
    /// Gathers scents at the agent's current cell, filters ignored scents,
    /// and reports air/ground scents sorted by strength.
    /// </summary>
    public sealed class Task_Sniff : IAgentTask
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

        public void Start(TaskContext context)
        {
            executed = false;
            AirScents.Clear();
            GroundScents.Clear();

            // Build default ignore list if caller didn’t provide one
            if (ignoreKeys.Count == 0 && context.Agent != null)
            {
                BuildDefaultIgnoreKeys(context.Agent, ignoreKeys);
            }
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            if (executed)
                return TaskTickResult.Succeeded();

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

            // Optional: debug log
            DebugLogResults(context.Agent);

            return TaskTickResult.Succeeded();
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
            // return det.airStrength01;
            return 0.5f;    // dummy return value nonzero
        }

        private static float TryGetGroundStrength01(ScentDetection det)
        {
            // TODO: replace with real field when confirmed
            // return det.groundStrength01;
            return 0.6f;    // dummy return value nonzero
        }

        private void DebugLogResults(WorldObject agent)
        {
            if (AirScents.Count == 0 && GroundScents.Count == 0)
            {
                Debug.Log($"[Task_Sniff] {agent.name}: no scents detected", agent);
                return;
            }

            Debug.Log(
                $"[Task_Sniff] {agent.name}: air={AirScents.Count} ground={GroundScents.Count}",
                agent);
            for (int i=0; i<AirScents.Count; i++)
            {
                Debug.Log($"Air[{i}] = {AirScents[i].agentId}");
            }
            for (int i=0; i<GroundScents.Count; i++)
            {
                Debug.Log($"Air[{i}] = {GroundScents[i].agentId}");
            }
        }
    }
}