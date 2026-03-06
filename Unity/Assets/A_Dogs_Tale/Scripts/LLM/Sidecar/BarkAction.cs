using UnityEngine;

namespace DogGame.LLM
{
    public sealed class BarkAction : ExecutableAction
    {
        private readonly int barkCount;
        private bool hasBegun;

        public BarkAction(int barkCount)
        {
            this.barkCount = Mathf.Max(1, barkCount);
        }

        public override string ActionType => "Bark";

        public override void Begin(GameObject agentObject)
        {
            if (hasBegun)
                return;

            hasBegun = true;

            string agentName = agentObject != null ? agentObject.name : "UnknownAgent";
            Debug.Log($"[BarkAction] {agentName} barked. Count={barkCount}");

            // Later:
            // - trigger bark animation
            // - play bark audio
            // - emit noise event into world
        }

        public override bool IsComplete(GameObject agentObject)
        {
            return hasBegun;
        }
    }
}