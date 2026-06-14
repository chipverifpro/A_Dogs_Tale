#nullable enable
using DogGame.LLM;
using DogGame.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DogGame.Modules
{
    public sealed class SniffInput : MonoBehaviour
    {
        private const string DefaultSniffBinding = "<Keyboard>/n";

        [Tooltip("If null, we'll try GetComponentInParent<WorldObject>().")]
        [SerializeField] private WorldObject? observer;

        [Tooltip("Key binding for sniff.")]
        [SerializeField] private string binding = DefaultSniffBinding;

        private InputAction? sniffAction;

        private void Awake()
        {
            //TODO: get this elsewhere...
            //if (observer == null)
            //    observer = worldObject;

            sniffAction = new InputAction(
                name: "Sniff",
                type: InputActionType.Button,
                binding: string.IsNullOrWhiteSpace(binding) ? DefaultSniffBinding : binding
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
            TryRunPlayerSniff("sniff_key");
        }

        public static bool TryRunPlayerSniff(string tag = "sniff_player")
        {
            Dir dir = Dir.Instance;
            WorldObject? playerAgent = dir != null && dir.playerPack != null
                ? dir.playerPack.packLeader
                : null;
            if (playerAgent == null || playerAgent.taskController == null)
            {
                Debug.LogWarning("[SniffInput] Missing player pack leader or task controller.");
                BottomBanner.Show(BannerSense.Smell, BannerLevel.Low, "No dog is available to sniff.");
                return false;
            }

            playerAgent.taskController.EnqueueTask(
                task: new Task_Sniff(),
                priority: 60,
                source: TaskSource.Player,
                applyMode: LLMApplyMode.Interrupt,
                tag: tag,
                front: true);

            return true;
        }
    }
}
