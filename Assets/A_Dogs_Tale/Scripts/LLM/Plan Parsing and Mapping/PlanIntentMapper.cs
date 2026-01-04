#nullable enable
using Newtonsoft.Json.Linq;
using UnityEngine;
using DogGame.Tasks;

namespace DogGame.LLM
{
    public static class PlanIntentMapper
    {
        // Reasonable default for LLM-generated "chosen behaviors"
        private const int DefaultLlmPriority = 60;

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
                    {
                        if (TryBuildTaskFromAddTask(intention, out var task, out var taskError))
                        {
                            // Build metadata (priority/interrupt/resume) for this task
                            var request = BuildRequestForAddTask(intention, task!);
                            queue.Enqueue(new TaskRequest(task!, priority: 60, source: TaskSource.LLM, canInterrupt: false));
                            enqueuedCount++;
                        }
                        else
                        {
                            Debug.LogWarning($"add_task mapping rejected: {taskError}");
                        }
                        break;
                    }

                    case PlanIntentionType.noop:
                        break;

                    case PlanIntentionType.set_goal:
                        // v1: ignore or log
                        break;

                    default:
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

        private static TaskRequest BuildRequestForAddTask(PlanIntentionV1 intention, IAgentTask task)
        {
            // Defaults for LLM tasks
            int priority = DefaultPriorityForTask(task);
            bool canInterrupt = false;          // LLM plans usually should NOT preempt reactions/player
            bool resumePrevious = false;        // LLM plan steps are typically the plan itself
            bool clearStackOnStart = false;     // never clear; reserve for panic/player takeover
            string? tag = "llm_plan";

            // Optional overrides from JSON
            JObject? parameters = intention.Parameters;

            if (parameters != null)
            {
                priority = Mathf.Clamp(parameters.Value<int?>("priority") ?? priority, 0, 100);
                canInterrupt = parameters.Value<bool?>("canInterrupt") ?? canInterrupt;
                resumePrevious = parameters.Value<bool?>("resumePrevious") ?? resumePrevious;
                clearStackOnStart = parameters.Value<bool?>("clearStackOnStart") ?? clearStackOnStart;
                tag = parameters.Value<string?>("tag") ?? tag;
            }

            return new TaskRequest(
                task: task,
                priority: priority,
                source: TaskSource.LLM,
                canInterrupt: canInterrupt,
                resumePrevious: resumePrevious,
                clearStackOnStart: clearStackOnStart,
                tag: tag
            );
        }

        private static int DefaultPriorityForTask(IAgentTask task)
        {
            // Keep these in the 50–69 band so reflex reactions can interrupt.
            // Adjust as you add more tasks.
            if (task is Task_MoveToCell) return 60;
            if (task is Task_Wait)       return 40;

            return DefaultLlmPriority;
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

            taskName = taskName.Trim();

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