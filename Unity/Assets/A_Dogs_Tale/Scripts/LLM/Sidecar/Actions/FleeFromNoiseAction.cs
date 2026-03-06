using UnityEngine;

namespace DogGame.LLM
{
    public sealed class FleeFromNoiseAction : ExecutableAction
    {
        private readonly float fleeDurationSeconds;
        private readonly float moveSpeed;
        private readonly float minimumInterruptTimeSeconds;

        private float elapsedTime;
        private Vector3 fleeDirection = Vector3.zero;
        private bool hasBegun;

        public FleeFromNoiseAction(
            float fleeDurationSeconds,
            float moveSpeed = 3f,
            float minimumInterruptTimeSeconds = 0.35f)
        {
            this.fleeDurationSeconds = Mathf.Max(0.1f, fleeDurationSeconds);
            this.moveSpeed = Mathf.Max(0.1f, moveSpeed);
            this.minimumInterruptTimeSeconds = Mathf.Max(0f, minimumInterruptTimeSeconds);
        }

        public override string ActionType => "FleeFromNoise";

        public override void Begin(GameObject agentObject)
        {
            if (hasBegun)
                return;

            hasBegun = true;
            elapsedTime = 0f;

            if (agentObject != null)
            {
                fleeDirection = (-agentObject.transform.forward).normalized;

                if (fleeDirection.sqrMagnitude < 0.0001f)
                    fleeDirection = Vector3.back;
            }
            else
            {
                fleeDirection = Vector3.back;
            }

            Debug.Log($"[FleeFromNoiseAction] Begin. Duration={fleeDurationSeconds:0.00}s Speed={moveSpeed:0.00}");
        }

        public override void Tick(GameObject agentObject, float deltaTime)
        {
            if (!hasBegun || agentObject == null)
                return;

            elapsedTime += deltaTime;

            Vector3 movement = fleeDirection * moveSpeed * deltaTime;
            agentObject.transform.position += movement;
        }

        public override bool IsComplete(GameObject agentObject)
        {
            return hasBegun && elapsedTime >= fleeDurationSeconds;
        }

        public override void Cancel(GameObject agentObject)
        {
            Debug.Log($"[FleeFromNoiseAction] Cancelled at {elapsedTime:0.00}s.");
        }

        public override bool CanBeInterruptedNow(GameObject agentObject)
        {
            return hasBegun && elapsedTime >= minimumInterruptTimeSeconds;
        }
    }
}