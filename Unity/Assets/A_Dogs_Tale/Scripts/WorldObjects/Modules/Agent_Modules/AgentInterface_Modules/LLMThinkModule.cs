#nullable enable
using System;
using DogGame.LLM.Agent;
using DogGame.LLM.Core;
using DogGame.Modules;
using UnityEngine;
using InspectorTools;

namespace DogGame.LLM
{
    /// <summary>
    /// Shared per-agent module that can request LLM plans.
    /// Decision modules call this; ThinkModule builds/enqueues and routes responses back.
    /// Enforces: one inflight request per agent.
    /// </summary>
    [RequireComponent(typeof(LLMConfigModule))]
    [RequireComponent(typeof(LLMWorldStateModule))]
    [InspectorNote("AgentInterface_Modules/LLM Think Module", "Per-agent module that can request LLM plans.  Builds/enqueues and routes responses back.")]
    public sealed class LLMThinkModule : WorldModule
    {
        [Header("Defaults")]
        [SerializeField] private Sophistication defaultSophistication = Sophistication.Low;

        // Enforce one in-flight request per agent
        private bool isInflight;

        /// <summary>
        /// Subscribers receive raw PlanResponseV1 JSON text (already extracted by your client/router).
        /// Typically PlayerDecisionModule subscribes and applies it.
        /// </summary>
        public event Action<string>? PlanJsonReceived;

        public bool CanRequestNow => !isInflight;

        private int debugDoubleTick = -1;
        public float nextThinkTime = 0;
        public float thinkIntervalSeconds = 10;

        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            // Periodically Think about what to do next...
            if (Time.time >= nextThinkTime)
            {
                //string taskPrompt = $"Update at interval time {thinkIntervalSeconds}";
                string taskPrompt = $"Explore the world.";
                TryRequestPlan(taskPrompt, urgency: LLMPlanUrgency.Normal, applyMode: LLMApplyMode.Append, tag: "player_interval");
                nextThinkTime = Time.time + thinkIntervalSeconds;
            }
        }

        public bool TryRequestPlan(
            string userTaskPrompt,
            LLMPlanUrgency urgency = LLMPlanUrgency.Normal,
            LLMApplyMode applyMode = LLMApplyMode.Append,
            string tag = "think_module",
            Vector2Int? eventCell = null,
            Vector3? eventWorld = null,
            Sophistication? sophisticationOverride = null)
        {
            if (isInflight)
                return false;

            if (worldObject.llmConfigModule == null) throw new InvalidOperationException("Missing LLMConfigModule");
            if (worldObject.llmWorldStateModule == null) throw new InvalidOperationException("Missing LLMWorldStateModule");
            if (worldObject.llmWorldScheduler == null) throw new InvalidOperationException("Missing LLMWorldScheduler.Instance");

            Sophistication sophistication =
                sophisticationOverride ??
                ChooseSophistication(worldObject.llmWorldStateModule, worldObject.llmConfigModule.identity.isBoss, worldObject.llmConfigModule.identity.isSimpleCreature) ??
                defaultSophistication;

            string agentId = worldObject.llmConfigModule.identity.ResolveAgentId(gameObject);

            isInflight = true;

            var req = new LLMPlanRequestOnDemand(
                agentId: agentId,
                prompt: userTaskPrompt,
                eventCell: eventCell,
                eventWorld: eventWorld,
                urgency: urgency,
                applyMode: applyMode,
                tag: tag,
                sophistication: sophistication,
                onResponseJson: OnPlanJsonFromScheduler
            );

            worldObject.llmWorldScheduler.EnqueueRequest(req);
            return true;
        }

        private void OnPlanJsonFromScheduler(string planJson)
        {
            // Clear inflight FIRST so subscribers can immediately request again if they want
            isInflight = false;
            Debug.Log($"[LLMThinkModule] OnPlanJsonFromScheduler agent={gameObject.name} chars={planJson?.Length ?? 0}");
            int subscriberCount = PlanJsonReceived?.GetInvocationList()?.Length ?? 0;
            Debug.Log($"[LLMThinkModule] PlanJsonReceived subscribers={subscriberCount} agent={gameObject.name}");

            try
            {
                if (planJson != null)
                {
                    PlanJsonReceived?.Invoke(planJson);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LLMThinkModule] PlanJsonReceived handler threw: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static Sophistication? ChooseSophistication(
            LLMWorldStateModule ws,
            bool isBoss,
            bool isSimpleCreature)
        {
            if (isSimpleCreature)
                return Sophistication.Low;

            if (isBoss || ws.isQuestCritical || ws.isInCombat)
                return Sophistication.High;

            if (ws.distanceToPlayerMeters <= 10f || ws.isPlayerFocusingThisNpc)
                return Sophistication.Medium;

            return Sophistication.Low;
        }

        /// <summary>
        /// Optional: allows caller to cancel gating if the scheduler never returns (timeouts already exist in clients).
        /// </summary>
        public void ForceClearInflight()
        {
            isInflight = false;
        }
    }
}
