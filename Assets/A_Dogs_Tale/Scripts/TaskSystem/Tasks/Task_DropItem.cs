#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    /// <summary>
    /// Drops the currently carried item at the agent's feet.
    /// Clears blackboard key: item.carriedId
    /// </summary>
    public sealed class Task_DropItem : IAgentTask
    {
        public string DebugName => "DropItem";

        public void Start(TaskContext context) { }

        public TaskTickResult Tick(TaskContext context, float dt)
        {
            if (!TryResolveCarriedItem(context, out var item, out var error))
                return TaskTickResult.Failed(error);

            Transform t = item.transform;

            // Unparent from agent
            t.SetParent(null, worldPositionStays: true);

            // Place slightly offset at feet
            Vector3 basePos = context.AgentTransform.position;
            t.position = new Vector3(
                basePos.x + 0.25f,
                basePos.y,
                basePos.z + 0.25f);

            // Re-enable collider
            if (item.TryGetComponent<Collider>(out var col))
                col.enabled = true;

            // Re-enable physics
            if (item.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Clear carried state
            context.Blackboard.SetInt("item.carriedId", 0);

            return TaskTickResult.Succeeded();
        }

        public void Stop(TaskContext context) { }

        // ---------- helpers ----------

        private static bool TryResolveCarriedItem(
            TaskContext context,
            out WorldObject item,
            out string error)
        {
            error = string.Empty;
            item = null!;

            if (!context.Blackboard.TryGetInt("item.carriedId", out int id) || id <= 0)
            {
                error = "No carried item to drop.";
                return false;
            }

            if (!WorldObjectRegistry.Instance ||
                !WorldObjectRegistry.Instance.TryGet(id, out var wo) ||
                wo == null)
            {
                error = $"Carried item id={id} not found in registry.";
                return false;
            }

            item = wo;
            return true;
        }
    }
}