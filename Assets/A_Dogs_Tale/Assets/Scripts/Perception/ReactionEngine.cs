#nullable enable
using System.Collections.Generic;
using UnityEngine;
using DogGame.AI.Perception;
using DogGame.LLM; // wherever your AgentTaskController / tasks live
using DogGame.Tasks;

public sealed class ReactionEngine : MonoBehaviour
{
    public WorldObject worldObject = null!;
    public AgentTaskController taskController = null!;

    private void Awake()
    {
        if (worldObject == null)
            worldObject = GetComponentInParent<WorldObject>();

        if (taskController == null)
            taskController = GetComponentInParent<AgentTaskController>();
    }

    public void HandlePerceptionEvents(List<PerceptionEvent> events)
    {
        if (events == null || events.Count == 0 || taskController == null)
            return;

        // v1: pick the most interesting smell event
        PerceptionEvent e = events[0];

        Debug.Log($"[ReactionEngine] {worldObject.DisplayName} noticed {e.Type} {e.Category} '{e.ScentName}' " +
                  $"strength={e.Strength01:0.00} novelty={e.Novelty01:0.00} interest={e.Interest01:0.00}");

        // v1 response examples:
        // Food -> move (investigate)
        // Dog/Human -> maybe look/bark later; for now, just move a little "investigate"
        if (e.Category == ScentCategory.Food && e.Interest01 > 0.25f)
        {
            // For now: move to current cell center or a nearby probe point.
            // Better v2: follow gradient to neighbor cell with higher strength.
            taskController.TaskQueue.Enqueue(new Task_Wait(0.15f)); // "sniff" beat
            taskController.TaskQueue.Enqueue(new Task_MoveToCell((int)e.WorldPos.x, (int)e.WorldPos.y, 0.35f)); // if you have cell pos to go to.
        }
    }
}