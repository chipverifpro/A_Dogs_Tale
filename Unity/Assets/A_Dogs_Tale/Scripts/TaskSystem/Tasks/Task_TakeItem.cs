#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    /// <summary>
    /// "Pick up" an item WorldObject by attaching it to the agent.
    /// v1: parents to agent transform and disables rigidbody physics if present.
    /// Writes blackboard item.carriedId.
    /// </summary>
    public sealed class Task_TakeItem : IAgentTask
    {
        public string DebugName => "TakeItem";
        public string Description = "Takes a target item by parenting it to the agent, disabling its collider and physics, and recording the carried item on the blackboard.";
        //private readonly WorldObject item;

        //public Task_TakeItem(WorldObject item)
        //{
        //    this.item = item;
        //}

        public void Start(TaskContext context) { }

        public TaskTickResult Tick(TaskContext context, float dt)
        {
            if (!TryResolveItem(context, out var item, out var error))
                return TaskTickResult.Failed(error);

            if (item == null)
                return TaskTickResult.Failed("Item is null");

            // If already carrying something, fail (or drop first; your choice)
            if (context.Blackboard.TryGetInt("item.carriedId", out int carriedId) && carriedId > 0)
                return TaskTickResult.Failed("Already carrying an item");

            // Parent item to agent
            var t = item.transform;
            t.SetParent(context.AgentTransform, worldPositionStays: true);

            // Put it roughly in front / slightly up (tweak later)
            t.localPosition = new Vector3(0.25f, 0.6f, 0.4f);

            // Disable physics if present
            if (item.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Optionally disable collider to prevent bumping
            if (item.TryGetComponent<Collider>(out var col))
                col.enabled = false;

            // Record carried item
            int id = item.ObjectId;
            if (id <= 0)
                return TaskTickResult.Failed("Item has no ObjectId (not registered?)");

            context.Blackboard.SetInt("item.carriedId", id);
            context.Blackboard.SetInt("item.lastTakenId", id);

            return TaskTickResult.Succeeded();
        }

        public void Stop(TaskContext context) { }

        private bool TryResolveItem(
            TaskContext context,
            out WorldObject item,
            out string error)
        {
            error = null!;
            item = null!;

            // 1) Prefer explicit blackboard target
            if (context.Blackboard.TryGetInt("item.targetId", out int id) && id > 0)
            {
                if (WorldObjectRegistry.Instance &&
                    WorldObjectRegistry.Instance.TryGet(id, out var wo))
                {
                    item = wo;
                    return true;
                }

                error = $"item.targetId={id} not found";
                return false;
            }

            // 2) Fallback: last seen vision target
            if (context.Blackboard.TryGetInt("vision.lastTargetId", out int vid) &&
                WorldObjectRegistry.Instance &&
                WorldObjectRegistry.Instance.TryGet(vid, out var vwo))
            {
                item = vwo;
                return true;
            }

            error = "No item to take (no targetId)";
            return false;
        }
    }
}
