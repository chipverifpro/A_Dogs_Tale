using UnityEngine;

namespace DogGame.AI
{
    public class PlayerDecisionModule : AgentDecisionModuleBase
    {
        public override void Tick(float deltaTime)
        {
            // TODO: Plug into your input system instead of directly reading Input.
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical   = Input.GetAxisRaw("Vertical");

            Vector3 inputDirection = new Vector3(horizontal, 0f, vertical);

            if (inputDirection.sqrMagnitude > 0.01f)
            {
                Vector3 worldDirection = inputDirection.normalized;
                Vector3 targetPosition = agent.transform.position + worldDirection;

                movement.MoveTowards(targetPosition, deltaTime);
            }
            else
            {
                movement.Stop();
            }

            // TODO: handle bark button, pack commands, interactions, etc.
        }
    }
}