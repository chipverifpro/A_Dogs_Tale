#nullable enable
using UnityEngine;
using DogGame.Modules;
using System.Collections.Generic;
using DogGame.AI.Perception;
using DogGame.LLM; // wherever your TaskControler / tasks live
using DogGame.Tasks;

using static DogGame.Modules.VisionPerceptionModule;

public class ReactionModule : WorldModule
{
    public TaskControler taskController = null!;

    protected override void Awake()
    {
        if (taskController == null)
            taskController = GetComponentInParent<TaskControler>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public override void Tick(float dt)
    {
        List<PerceptionEvent> events = new();
        
        // Scent
        List<PerceptionEvent> events_scent = worldObject.scentPerceptionModule.TickScent(0f);
        if (events_scent.Count>0) events.AddRange(events_scent);
        
        // Vision
        List<PerceptionEvent> events_vision = worldObject.visionPerceptionModule.GetPerceptionEvents();
        if (events_vision.Count>0) events.AddRange(events_vision);
        
        HandlePerceptionEvents(events);
    }

    public void HandlePerceptionEvents(List<PerceptionEvent> events)
    {
        if (events == null || events.Count == 0 || taskController == null)
            return;
        Debug.Log($"{worldObject.DisplayName} ReactionModule.HandlePerceptionEvents {events.Count}");

        foreach (PerceptionEvent e in events)
        {
            switch (e.Sense)
            {
                case PerceptionSense.Scent:
                    if (e.Scent.HasValue)
                    {
                        var s = e.Scent.Value;
                        Debug.Log(
                            $"[ReactionModule] {worldObject.DisplayName} noticed {e.Type} SCENT " +
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
                            $"[ReactionModule] {worldObject.DisplayName} saw {e.Type} " +
                            $"{v.Kind} {v.Relation} " +
                            $"dist={v.DistanceMeters:0.0}m speed={v.SpeedMps:0.0}m/s " +
                            $"interest={e.Interest01:0.00}"
                        );
                    }
                    break;

                default:
                    Debug.Log(
                        $"[ReactionModule] {worldObject.DisplayName} perceived {e.Type} " +
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
}

