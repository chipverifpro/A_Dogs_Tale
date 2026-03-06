using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DogGame.LLM
{
    public sealed class PlanV2DebugLauncher : MonoBehaviour
    {
        [SerializeField]
        private SidecarPlanClientV2 sidecarPlanClient;

        [SerializeField]
        private Key triggerKey = Key.P;

        private Keyboard keyboard;

        public String DebugPlan;

        private void Awake()
        {
            keyboard = Keyboard.current;
        }

        private void Update()
        {
            keyboard ??= Keyboard.current;

            if (keyboard == null)
                return;

            if (!IsTriggerPressedThisFrame(keyboard))
                return;

            if (sidecarPlanClient == null)
            {
                Debug.LogError("[PlanV2DebugLauncher] SidecarPlanClientV2 reference missing.");
                return;
            }

            Debug.Log("[PlanV2DebugLauncher] Sending debug plan request...");
            sidecarPlanClient.RequestDebugPlan();
        }

        private bool IsTriggerPressedThisFrame(Keyboard currentKeyboard)
        {
            return triggerKey switch
            {
                Key.P => currentKeyboard.pKey.wasPressedThisFrame,
                Key.Space => currentKeyboard.spaceKey.wasPressedThisFrame,
                Key.Enter => currentKeyboard.enterKey.wasPressedThisFrame,
                _ => false
            };
        }
    }
}