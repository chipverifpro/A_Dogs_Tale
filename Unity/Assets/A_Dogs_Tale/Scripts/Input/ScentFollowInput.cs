#nullable enable
using DogGame.LLM;
using DogGame.Tasks;
using UnityEngine;

namespace DogGame.Modules
{
    public sealed class ScentFollowInput : MonoBehaviour
    {
        [Tooltip("If null, we'll try GetComponentInParent<WorldObject>().")]
        [SerializeField] private WorldObject? observer;

        public static bool TryRunPlayerScentFollow(string tag = "scentFollow_player")
        {
            Dir dir = Dir.Instance;
            if (dir == null || dir.scentRegistry == null)
            {
                Debug.LogError("[ScentFollowInput] Missing Dir or ScentRegistry.");
                return false;
            }

            ScentSource selectedScent = dir.scentRegistry.SelectedTargetScent;
            if (selectedScent == null || selectedScent.agentId < 0)
            {
                BottomBanner.Show(BannerSense.Smell, BannerLevel.Low, "Choose a scent from the nose menu first.");
                return false;
            }

            WorldObject ?playerAgent = dir.playerPack != null ? dir.playerPack.packLeader : null;
            if (playerAgent == null || playerAgent.taskController == null)
            {
                Debug.LogError("[ScentFollowInput] Missing player agent or task controller.");
                return false;
            }

            playerAgent.taskController.EnqueueTask(
                task: new Task_ScentFollow(
                    scentKey: dir.scentRegistry.SelectedTargetScentKey,
                    medium: ScentMedium.Ground),
                priority: 80,
                source: TaskSource.Player,  // or AI/LLM/etc
                applyMode: LLMApplyMode.Interrupt,
                tag: tag,
                front: true);

            return true;
        }
    }
}
