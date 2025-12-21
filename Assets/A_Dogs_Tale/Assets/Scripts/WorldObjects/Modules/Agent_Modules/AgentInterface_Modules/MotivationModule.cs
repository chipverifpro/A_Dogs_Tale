using UnityEngine;
using DogGame.Modules;

//  What we can fill in later (without changing the framework)
//	•	Real pack centroid calculation (exclude self, smoothing, leader bias)
//	•	Identify a distressed packmate target (not just a scalar distress)
//	•	Pack roles: leader/follower/guard
//	•	Better training integration (separate “stay” vs “recall” vs “formation”)
//	•	Debug HUD: bars for urge, separation, distress, suppression

// Overview of the wiring
// At runtime (each tick):
//	1.	PackModule provides pack context (leader, members, centroid, distress signal).
//	2.	MotivationModule evaluates PackLoyalty → produces an “urge” + “suggested action”.
//	3.	DecisionModule can either:
//	    •	Treat it as advice (blend with player input / other goals), or
//	    •	Allow it to interrupt (e.g., if separation is severe).

namespace DogGame.AI
{
    public class MotivationModule : WorldModule
    {
        [Header("Tuning")]
        [SerializeField] private PackLoyaltyTuning packLoyaltyTuning;

        [Header("Debug")]
        [SerializeField] private bool showDebug;

        private PackLoyaltyMotivation packLoyaltyMotivation;

        // Dependencies (inject from AgentModule during Initialize)
        private IAgentHandle selfHandle;
        public IPackProvider packProvider;

        // External state
        public TrainingProfile trainingProfile;

        // Output cache for DecisionModule
        public PackLoyaltyResult latestPackLoyalty;

        public override void Initialize(WorldObject wo) //, IPackProvider packProvider)
        {
            base.Initialize(wo);

            packLoyaltyMotivation = new PackLoyaltyMotivation(packLoyaltyTuning, packProvider);
        }

        public override void Tick(float deltaTime)
        {
            if (packLoyaltyMotivation == null || selfHandle == null)
                return;

            latestPackLoyalty = packLoyaltyMotivation.Evaluate(selfHandle, trainingProfile);

            if (showDebug && latestPackLoyalty.isActive)
            {
                Debug.Log($"[{selfHandle.AgentName}] PackLoyalty urge={latestPackLoyalty.urge01:F2} " +
                          $"dir={latestPackLoyalty.directive} reason={latestPackLoyalty.debugReason}");
            }
        }
    }
}