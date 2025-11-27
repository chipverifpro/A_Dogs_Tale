using UnityEngine;

namespace DogGame.AI
{
    [RequireComponent(typeof(CharacterController))]
    public class AgentMovementModule : WorldModule
    {
        public float maxMoveSpeed = 3.5f;
        public float rotationSpeedDegreesPerSecond = 720f;

        private CharacterController characterController;

        private Vector3 currentVelocity;

        protected override void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        public void MoveTowards(Vector3 worldTargetPosition, float deltaTime)
        {
            Vector3 toTarget = worldTargetPosition - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.01f)
            {
                currentVelocity = Vector3.zero;
                return;
            }

            Vector3 direction = toTarget.normalized;
            currentVelocity = direction * maxMoveSpeed;

            // Rotate toward movement direction
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotationSpeedDegreesPerSecond * deltaTime
                );
            }

            // Actual movement
            characterController.Move(currentVelocity * deltaTime);
        }

        public void Stop()
        {
            currentVelocity = Vector3.zero;
        }
    }
}