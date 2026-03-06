using System.Collections.Generic;
using UnityEngine;

namespace DogGame.LLM
{
    public sealed class AgentPlanExecutor : MonoBehaviour
    {
        [SerializeField] private GameObject controlledAgentObject;

        private readonly Queue<ExecutableAction> pendingActions = new();
        private ExecutableAction currentAction;

        private void Awake()
        {
            if (controlledAgentObject == null)
                controlledAgentObject = gameObject;
        }

        private void Update()
        {
            TickExecutor(Time.deltaTime);
        }

        public void ApplyPlan(PlanResponseV2 response)
        {
            if (response == null || response.intentions == null)
            {
                Debug.LogWarning("[AgentPlanExecutor] ApplyPlan called with null or empty response.");
                return;
            }

            pendingActions.Clear();

            foreach (PlanIntentionV2 intention in response.intentions)
            {
                ExecutableAction action = TranslateIntention(intention);

                if (action != null)
                    pendingActions.Enqueue(action);
            }

            Debug.Log($"[AgentPlanExecutor] Queued {pendingActions.Count} action(s).");
        }

        private void TickExecutor(float deltaTime)
        {
            if (currentAction == null)
            {
                StartNextAction();
            }

            if (currentAction == null)
                return;

            currentAction.Tick(controlledAgentObject, deltaTime);

            if (currentAction.IsComplete(controlledAgentObject))
            {
                Debug.Log($"[AgentPlanExecutor] Completed action '{currentAction.ActionType}'.");
                currentAction = null;
            }
        }

        private void StartNextAction()
        {
            if (pendingActions.Count == 0)
                return;

            currentAction = pendingActions.Dequeue();

            Debug.Log($"[AgentPlanExecutor] Starting action '{currentAction.ActionType}'.");
            currentAction.Begin(controlledAgentObject);
        }

        private ExecutableAction TranslateIntention(PlanIntentionV2 intention)
        {
            if (intention == null)
                return null;

            switch (intention.type)
            {
                case "Bark":
                    return new BarkAction(intention.count);

                default:
                    Debug.LogWarning($"[AgentPlanExecutor] Unsupported intention '{intention.type}'.");
                    return null;
            }
        }
    }
}