#nullable enable
using System.Collections.Generic;
using UnityEngine;
using DogGame.AI.Perception;
using DogGame.LLM; // wherever your TaskControler / tasks live
using DogGame.Tasks;
using DogGame.Modules;

public sealed class ReactionEngine : MonoBehaviour
{
    public WorldObject worldObject = null!;
    public TaskControler taskController = null!;

    private void Awake()
    {
        if (worldObject == null)
            worldObject = GetComponentInParent<WorldObject>();

        if (taskController == null)
            taskController = GetComponentInParent<TaskControler>();
    }

    public void HandlePerceptionEvents(List<PerceptionEvent> events)
    {
        if (events == null || events.Count == 0 || taskController == null)
            return;

        // v1: pick the most interesting smell event
        PerceptionEvent e = events[0];

        switch (e.Sense)
        {
            case PerceptionSense.Scent:
                if (e.Scent.HasValue)
                {
                    var s = e.Scent.Value;
                    Debug.Log(
                        $"[ReactionEngine] {worldObject.DisplayName} noticed {e.Type} SCENT " +
                        $"{s.Category} '{s.ScentName}' " +
                        $"strength={e.Strength01:0.00} novelty={e.Novelty01:0.00} interest={e.Interest01:0.00}"
                    );
                }
                break;

            case PerceptionSense.Vision:
                if (e.Vision.HasValue)
                {
                    var v = e.Vision.Value;
                    Debug.Log(
                        $"[ReactionEngine] {worldObject.DisplayName} saw {e.Type} " +
                        $"{v.Kind} {v.Relation} " +
                        $"dist={v.DistanceMeters:0.0}m speed={v.SpeedMps:0.0}m/s " +
                        $"interest={e.Interest01:0.00}"
                    );
                }
                break;

            default:
                Debug.Log(
                    $"[ReactionEngine] {worldObject.DisplayName} perceived {e.Type} " +
                    $"interest={e.Interest01:0.00}"
                );
                break;
        }
        // v1 response examples:
        // Food -> move (investigate)
        // Dog/Human -> maybe look/bark later; for now, just move a little "investigate"
        if (e.Scent.HasValue && e.Scent.Value.Category == ScentCategory.Food && e.Interest01 > 0.25f)
        {
            // For now: move to current cell center or a nearby probe point.
            // Better v2: follow gradient to neighbor cell with higher strength.
            //taskController.taskQueue.Enqueue(new Task_Wait(0.15f)); // "sniff" beat
            //taskController.taskQueue.Enqueue(new Task_MoveToCell((int)e.WorldPos.x, (int)e.WorldPos.y, 0.35f)); // if you have cell pos to go to.

            // Reaction: bark + sniff, but if sniff fails, just end and resume previous
            var seq = new Task_Sequence(new IAgentTask[]
            {
                new Task_Bark(10),
                new Task_Try(
                    tryTask: new Task_Sniff(1.0f),      // your sniff task
                    onFail: new Task_Wait(0.1f))
            });

            taskController.Submit(new TaskRequest(
                task: seq,
                priority: 80,
                source: TaskSource.Reaction,
                canInterrupt: true,
                resumePrevious: true,
                tag: "reaction_bark_sniff"
            ));
        }
    }
}