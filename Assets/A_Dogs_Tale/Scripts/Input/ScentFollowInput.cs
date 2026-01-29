#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using DogGame.LLM;
using DogGame.Tasks;
using Unity.AppUI.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DogGame.Modules
{
    public sealed class ScentFollowInput : MonoBehaviour
    {
        [Tooltip("If null, we'll try GetComponentInParent<WorldObject>().")]
        [SerializeField] private WorldObject? observer;

        [Tooltip("Key binding for Scent Follow.")]
        [SerializeField] private string binding = "<Keyboard>/h";

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
            WorldObject playerAgent = Directory.Instance.playerPack.packLeader;
            //HashSet<string> scentFollowContext = new(playerAgent.ObjectId);
            playerAgent.taskController.EnqueueTask(
                // TODO: Replace hardcoded agent:3 with something else.
                task: new Task_ScentFollow(scentKey: "agent:3", medium: ScentMedium.Ground),
                priority: 80,
                source: TaskSource.Player,  // or AI/LLM/etc
                applyMode: LLMApplyMode.Interrupt,
                tag: "scentFollow_key",
                front: true);
        }
    }
}