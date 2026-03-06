using UnityEngine;

namespace DogGame.LLM
{
    public sealed class BarkAction : ExecutableAction
    {
        private readonly int barkCount;
        private readonly float barkDurationSeconds;

        private bool hasBegun;
        private float elapsedTime;

        public BarkAction(int barkCount, float barkDurationSeconds = 0.35f)
        {
            this.barkCount = Mathf.Max(1, barkCount);
            this.barkDurationSeconds = Mathf.Max(0.05f, barkDurationSeconds);
        }

        public override string ActionType => "Bark";

        public override void Begin(GameObject agentObject)
        {
            if (hasBegun)
                return;

            hasBegun = true;
            elapsedTime = 0f;

            string agentName = agentObject != null ? agentObject.name : "UnknownAgent";
            Debug.Log($"[BarkAction] {agentName} barked. Count={barkCount}, Duration={barkDurationSeconds:0.00}s");

            // Later:
            // - trigger bark animation
            // - play bark sound
            // - emit bark/noise event
        }

        public override void Tick(GameObject agentObject, float deltaTime)
        {
            if (!hasBegun)
                return;

            elapsedTime += deltaTime;
        }

        public override bool IsComplete(GameObject agentObject)
        {
            return hasBegun && elapsedTime >= barkDurationSeconds;
        }

        public override void Cancel(GameObject agentObject)
        {
            Debug.Log($"[BarkAction] Cancelled at {elapsedTime:0.00}s.");
        }

        public override bool CanBeInterruptedNow(GameObject agentObject)
        {
            return false;
        }
    }
}