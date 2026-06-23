#nullable enable
using DogGame.LLM;
using DogGame.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DogGame.Modules
{
    public sealed class ScentFollowInput : MonoBehaviour
    {
        [Tooltip("If null, we'll try GetComponentInParent<WorldObject>().")]
        [SerializeField] private WorldObject? observer;

        [Tooltip("Key binding for Scent Follow.")]
        [SerializeField] private string binding = "<Keyboard>/m";

        private InputAction? scentFollowAction;

        private void Awake()
        {
            //TODO: get this elsewhere...
            //if (observer == null)
            //    observer = worldObject;

            scentFollowAction = new InputAction(
                name: "ScentFollow",
                type: InputActionType.Button,
                binding: binding
            );
        }

        private void OnEnable()
        {
            if (scentFollowAction == null) return;
            scentFollowAction.performed += OnscentFollowPerformed;
            scentFollowAction.Enable();
        }

        private void OnDisable()
        {
            if (scentFollowAction == null) return;
            scentFollowAction.performed -= OnscentFollowPerformed;
            scentFollowAction.Disable();
        }

        private void OnscentFollowPerformed(InputAction.CallbackContext ctx)
        {
            Dir dir = Dir.Instance;
            if (dir == null || dir.scentRegistry == null)
            {
                Debug.LogError("[ScentFollowInput] Missing Dir or ScentRegistry.");
                return;
            }

            ScentSource selectedScent = dir.scentRegistry.SelectedTargetScent;
            if (selectedScent == null || selectedScent.agentId < 0)
            {
                BottomBanner.Show(BannerSense.Smell, BannerLevel.Low, "Choose a scent from the nose menu first.");
                return;
            }

            WorldObject ?playerAgent = dir.playerPack != null ? dir.playerPack.packLeader : null;
            if (playerAgent == null || playerAgent.taskController == null)
            {
                Debug.LogError("[ScentFollowInput] Missing player agent or task controller.");
                return;
            }

            playerAgent.taskController.EnqueueTask(
                task: new Task_ScentFollow(
                    scentKey: dir.scentRegistry.SelectedTargetScentKey,
                    medium: ScentMedium.Ground),
                priority: 80,
                source: TaskSource.Player,  // or AI/LLM/etc
                applyMode: LLMApplyMode.Interrupt,
                tag: "scentFollow_key",
                front: true);
        }
    }
}
