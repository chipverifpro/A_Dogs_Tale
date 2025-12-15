using UnityEngine;

namespace DogGame.AI
{
    public sealed class PackLoyaltyMotivation
    {
        private readonly PackLoyaltyTuning tuning;
        public readonly IPackProvider packProvider;

        public PackLoyaltyMotivation(PackLoyaltyTuning tuning, IPackProvider packProvider)
        {
            this.tuning = tuning;
            this.packProvider = packProvider;
        }

        public PackLoyaltyResult Evaluate(
            IAgentHandle self,
            in TrainingProfile trainingProfile) // from earlier structure
        {
            if (self == null || self.Transform == null || tuning == null || packProvider == null)
                return default;

            if (!packProvider.IsInPack(self))
                return default;

            Vector3 selfPosition = self.Transform.position;
            Vector3 packCentroid = packProvider.GetPackCentroid(self);
            float packDistress01 = Mathf.Clamp01(packProvider.GetPackDistress01(self));

            float separationMeters = Vector3.Distance(selfPosition, packCentroid);

            // Map separation to 0..1 stimulus using comfort/max radius.
            float separationStimulus01 = RemapClamped(
                separationMeters,
                tuning.comfortRadiusMeters,
                tuning.maxRadiusMeters);

            // Distress increases urgency.
            float distressBoost = 1f + packDistress01 * tuning.distressMultiplier;

            // Training suppression (use focus + obedience as first pass).
            float trainingSuppression01 = Mathf.Clamp01(
                (trainingProfile.focus + trainingProfile.obedience) * 0.5f * tuning.maxTrainingSuppression);

            float rawUrge = separationStimulus01 * tuning.baseWeight * distressBoost;
            float urge01 = Mathf.Clamp01(rawUrge * (1f - trainingSuppression01));

            bool isActive = urge01 >= tuning.activationThreshold;

            // Directive selection (first pass):
            IAgentHandle leader = packProvider.GetLeader(self);
            PackLoyaltyDirective directive = PackLoyaltyDirective.ReturnToPackCentroid;
            Vector3 targetPosition = packCentroid;
            string reason = $"Sep={separationMeters:F1}m sepStim={separationStimulus01:F2} distress={packDistress01:F2}";

            if (leader != null && leader.Transform != null)
            {
                directive = PackLoyaltyDirective.FollowLeader;
                targetPosition = leader.Transform.position;
                reason += " -> FollowLeader";
            }

            // Later: pick distressed packmate if distress high + known target.
            // directive = PackLoyaltyDirective.AssistDistressedPackmate;

            return new PackLoyaltyResult
            {
                urge01 = urge01,
                isActive = isActive,
                directive = isActive ? directive : PackLoyaltyDirective.None,
                targetPosition = targetPosition,
                targetAgent = leader,
                debugReason = reason
            };
        }

        private static float RemapClamped(float value, float inMin, float inMax)
        {
            if (inMax <= inMin) return value >= inMax ? 1f : 0f;
            return Mathf.Clamp01((value - inMin) / (inMax - inMin));
        }
    }
}