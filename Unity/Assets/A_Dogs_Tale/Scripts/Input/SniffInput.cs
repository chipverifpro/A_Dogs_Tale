#nullable enable
using DogGame.LLM;
using DogGame.Tasks;
using UnityEngine;

namespace DogGame.Modules
{
    public sealed class SniffInput : MonoBehaviour
    {
        [Tooltip("If null, we'll try GetComponentInParent<WorldObject>().")]
        [SerializeField] private WorldObject? observer;

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
