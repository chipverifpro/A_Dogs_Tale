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

            List<ExecutableAction> translatedActions = TranslatePlan(response);

            if (translatedActions.Count == 0)
            {
                Debug.LogWarning("[AgentPlanExecutor] Incoming plan translated to zero actions.");
                return;
            }

            bool interruptedCurrentAction = TryInterruptCurrentAction();

            ClearPendingActions();
            EnqueueActions(translatedActions);

            if (interruptedCurrentAction)
            {
                Debug.Log($"[AgentPlanExecutor] Current action interrupted. Replaced with {pendingActions.Count} queued action(s).");
            }
            else
            {
                Debug.Log($"[AgentPlanExecutor] Pending queue replaced with {pendingActions.Count} action(s). Current action continues.");
            }
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

        private bool TryInterruptCurrentAction()
        {
            if (currentAction == null)
                return true;

            if (!currentAction.CanBeInterruptedNow(controlledAgentObject))
                return false;

            Debug.Log($"[AgentPlanExecutor] Interrupting current action '{currentAction.ActionType}'.");

            currentAction.Cancel(controlledAgentObject);
            currentAction = null;

            return true;
        }

        private void ClearPendingActions()
        {
            pendingActions.Clear();
        }

        private void EnqueueActions(List<ExecutableAction> actions)
        {
            foreach (ExecutableAction action in actions)
            {
                if (action != null)
                    pendingActions.Enqueue(action);
            }
        }

        private List<ExecutableAction> TranslatePlan(PlanResponseV2 response)
        {
            List<ExecutableAction> actions = new();

            foreach (PlanIntentionV2 intention in response.intentions)
            {
                ExecutableAction action = TranslateIntention(intention);

                if (action != null)
                    actions.Add(action);
            }

            return actions;
        }

        private ExecutableAction TranslateIntention(PlanIntentionV2 intention)
        {
            if (intention == null)
                return null;

            switch (intention.type)
            {
                case "Bark":
                    return new BarkAction(intention.count);

                case "FleeFromNoise":
                    return new FleeFromNoiseAction(intention.seconds);

                default:
                    Debug.LogWarning($"[AgentPlanExecutor] Unsupported intention '{intention.type}'.");
                    return null;
            }
        }
    }
}