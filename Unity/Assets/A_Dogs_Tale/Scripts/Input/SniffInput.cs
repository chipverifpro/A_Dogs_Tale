#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using DogGame.LLM;
using DogGame.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DogGame.Modules
{
    public sealed class SniffInput : MonoBehaviour
    {
        [Tooltip("If null, we'll try GetComponentInParent<WorldObject>().")]
        [SerializeField] private WorldObject? observer;

        [Tooltip("Key binding for sniff.")]
        [SerializeField] private string binding = "<Keyboard>/g";

        private InputAction? sniffAction;

        private void Awake()
        {
            //TODO: get this elsewhere...
            //if (observer == null)
            //    observer = worldObject;

            sniffAction = new InputAction(
                name: "Sniff",
                type: InputActionType.Button,
                binding: binding
            );
        }

        private void OnEnable()
        {
            if (sniffAction == null) return;
            sniffAction.performed += OnSniffPerformed;
            sniffAction.Enable();
        }

        private void OnDisable()
        {
            if (sniffAction == null) return;
            sniffAction.performed -= OnSniffPerformed;
            sniffAction.Disable();
        }

        private void OnSniffPerformed(InputAction.CallbackContext ctx)
        {
            //if (observer == null)
            //{
            //    Debug.LogError($"SniffInput.OnSniffPerformed(ctx): observer is NULL.");
            //    return;
            //}
            // One-shot manual sniff by player
            //TaskContext sniffContext = new(Dir.Instance.playerPack.packLeader);
            //Debug.Log($"SniffInput.OnSniffPerformed {sniffContext.Agent.DisplayName}");
            //Task_Sniff.RunTask_Sniff(sniffContext);

            
            WorldObject playerAgent = Dir.Instance.playerPack.packLeader;
            HashSet<string> sniffContext = new(playerAgent.ObjectId);
            playerAgent.taskController.EnqueueTask(
                task: new Task_Sniff(sniffContext),
                priority: 60,
                source: TaskSource.Player,  // or AI/LLM/etc
                applyMode: LLMApplyMode.Interrupt,
                tag: "sniff_key",
                front: true);
        }
    }
}
