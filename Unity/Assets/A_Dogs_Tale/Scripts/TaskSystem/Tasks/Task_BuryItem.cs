#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    /// <summary>
    /// "Buries" the currently carried item by hiding it underground and disabling interaction.
    /// v1 implementation = hide + disable physics/renderer.
    /// Clears blackboard key: item.carriedId
    /// Sets: item.buried = true
    /// </summary>
    public sealed class Task_BuryItem : IAgentTask
    {
        public string DebugName => $"BuryItem(depth={depthMeters:0.00})";
        public string Description = "Buries the currently carried item by detaching it, moving it below the agent, disabling its collider, physics, and renderers, and updating blackboard item state.";

        private readonly float depthMeters;

        public Task_BuryItem(float depthMeters = 0.15f)
        {
            this.depthMeters = Mathf.Max(0.01f, depthMeters);
        }

        public void Start(TaskContext context) { }

        public TaskTickResult Tick(TaskContext context, float dt)
        {
            if (!TryResolveCarriedItem(context, out var item, out var error))
                return TaskTickResult.Failed(error);

            Transform t = item.transform;

            // Detach from agent
            t.SetParent(null, worldPositionStays: true);

            // Move slightly below ground at agent location
            Vector3 p = context.AgentTransform.position;
            t.position = new Vector3(p.x, p.y - depthMeters, p.z);

            // Disable collider
            if (item.TryGetComponent<Collider>(out var col))
                col.enabled = false;

            // Disable physics
            if (item.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Hide visuals
            var renderers = item.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = false;

            // Blackboard bookkeeping
            context.Blackboard.SetInt("item.carriedId", 0);
            context.Blackboard.SetBool("item.buried", true);
            context.Blackboard.SetInt("item.lastBuriedId", item.ObjectId);

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
                error = "No carried item to bury.";
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
