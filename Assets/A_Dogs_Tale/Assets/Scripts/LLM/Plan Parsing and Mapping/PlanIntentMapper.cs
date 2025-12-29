#nullable enable
using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DogGame.Tasks;

namespace DogGame.LLM
{
    public static class PlanIntentMapper
    {
        public static bool TryEnqueueTasksFromPlan(PlanResponseV1 plan, AgentTaskQueue queue, out string? error)
        {
            error = null;

            if (plan.Intentions == null || plan.Intentions.Count == 0)
            {
                error = "Plan contains no intentions.";
                return false;
            }

            int enqueuedCount = 0;

            foreach (var intention in plan.Intentions)
            {
                if (intention == null)
                    continue;

                switch (intention.Type)
                {
                    case PlanIntentionType.add_task:
                        if (TryBuildTaskFromAddTask(intention, out var task, out var taskError))
                        {
                            queue.Enqueue(task!);
                            enqueuedCount++;
                        }
                        else
                        {
                            Debug.LogWarning($"add_task mapping rejected: {taskError}");
                        }
                        break;

                    case PlanIntentionType.noop:
                        break;

                    // You can decide later whether set_goal becomes an internal goal system,
                    // or you just treat it as a "hint" for now.
                    case PlanIntentionType.set_goal:
                        // v1: ignore or log. (Or enqueue a small Wait to simulate.)
                        break;

                    default:
                        // For v1, ignore other types until you implement them.
                        break;
                }
            }

            if (enqueuedCount == 0)
            {
                error = "No tasks were enqueued from the plan (all were ignored or rejected).";
                return false;
            }

            return true;
        }

        private static bool TryBuildTaskFromAddTask(PlanIntentionV1 intention, out IAgentTask? task, out string? error)
        {
            task = null;
            error = null;

            JObject? parameters = intention.Parameters;
            if (parameters == null)
            {
                error = "add_task missing parameters object.";
                return false;
            }

            string? taskName = parameters.Value<string>("task");
            if (string.IsNullOrWhiteSpace(taskName))
            {
                error = "add_task.parameters.task is required.";
                return false;
            }

            // Normalize a bit
            taskName = taskName.Trim();

            // Supported tasks (v1)
            switch (taskName)
            {
                case "wait":
                {
                    float seconds = (float)(parameters.Value<double?>("seconds") ?? 1.0);
                    seconds = Mathf.Clamp(seconds, 0f, 30f);
                    task = new Task_Wait(seconds);
                    return true;
                }

                case "move_to_cell":
                {
                    // parameters.locationCell: [x,y]
                    var locationCellToken = parameters["locationCell"];
                    if (locationCellToken == null || locationCellToken.Type != JTokenType.Array)
                    {
                        error = "move_to_cell requires parameters.locationCell as [x,y].";
                        return false;
                    }

                    var arr = (JArray)locationCellToken;
                    if (arr.Count != 2 || arr[0].Type != JTokenType.Integer || arr[1].Type != JTokenType.Integer)
                    {
                        error = "move_to_cell parameters.locationCell must be [int,int].";
                        return false;
                    }

                    int cellX = arr[0]!.Value<int>();
                    int cellY = arr[1]!.Value<int>();

                    float stopRadius = (float)(parameters.Value<double?>("stopRadius") ?? 0.35);
                    stopRadius = Mathf.Clamp(stopRadius, 0.05f, 2.0f);

                    task = new Task_MoveToCell(cellX, cellY, stopRadius);
                    return true;
                }

                default:
                    error = $"Unsupported add_task.parameters.task \"{taskName}\" (v1 supports wait, move_to_cell).";
                    return false;
            }
        }
    }
}