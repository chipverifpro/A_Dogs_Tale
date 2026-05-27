#nullable enable
using Newtonsoft.Json.Linq;
using UnityEngine;
using DogGame.Tasks;
using DogGame.Modules;

namespace DogGame.LLM
{
    public static class PlanIntentMapper
    {
        // Reasonable default for LLM-generated "chosen behaviors"
        private const int DefaultLlmPriority = 60;

        public static bool TryEnqueueTasksFromPlan(
            PlanResponseV1 plan,
            TaskQueue queue,
            out string? error,
            System.Collections.Generic.List<string>? mappingNotes = null)
        {
            error = null;

            if (plan.Intentions == null || plan.Intentions.Count == 0)
            {
                error = "Plan contains no intentions.";
                mappingNotes?.Add("rejected: plan contains no intentions.");
                return false;
            }

            int enqueuedCount = 0;

            for (int i = 0; i < plan.Intentions.Count; i++)
            {
                var intention = plan.Intentions[i];
                if (intention == null)
                {
                    mappingNotes?.Add($"intentions[{i}] rejected: intention is null.");
                    continue;
                }

                switch (intention.Type)
                {
                    case PlanIntentionType.add_task:
                    {
                        if (TryBuildTaskFromAddTask(intention, out var task, out var taskError))
                        {
                            // Build metadata (priority/interrupt/resume) for this task
                            var request = BuildRequestForAddTask(intention, task!);
                            queue.Enqueue(new TaskRequest(
                                task: request.Task,
                                priority: request.Priority,
                                source: request.Source,
                                canInterrupt: request.CanInterrupt,
                                resumePrevious: request.ResumePrevious,
                                clearStackOnStart: request.ClearStackOnStart,
                                tag: request.Tag,
                                originRequestId: plan.RequestId));
                            enqueuedCount++;
                            mappingNotes?.Add($"intentions[{i}] accepted: add_task id={intention.Id} task={task!.GetType().Name}.");
                        }
                        else
                        {
                            Debug.LogWarning($"add_task mapping rejected: {taskError}");
                            mappingNotes?.Add($"intentions[{i}] rejected: add_task id={intention.Id} reason={taskError}");
                        }
                        break;
                    }

                    case PlanIntentionType.noop:
                        mappingNotes?.Add($"intentions[{i}] ignored: noop id={intention.Id}.");
                        break;

                    case PlanIntentionType.set_goal:
                        // v1: ignore or log
                        mappingNotes?.Add($"intentions[{i}] ignored: set_goal id={intention.Id} is not mapped to a task.");
                        break;

                    default:
                        mappingNotes?.Add($"intentions[{i}] ignored: {intention.Type} id={intention.Id} is not mapped to a task.");
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

        public static bool TryEnqueueTasksFromPlan(
            PlanResponseV3 plan,
            TaskQueue queue,
            out string? error,
            System.Collections.Generic.List<string>? mappingNotes = null)
        {
            error = null;

            if (plan.Intentions == null || plan.Intentions.Count == 0)
            {
                error = "Plan contains no intentions.";
                mappingNotes?.Add("rejected: plan contains no intentions.");
                return false;
            }

            int enqueuedCount = 0;

            for (int i = 0; i < plan.Intentions.Count; i++)
            {
                var intention = plan.Intentions[i];
                if (intention == null)
                {
                    mappingNotes?.Add($"intentions[{i}] rejected: intention is null.");
                    continue;
                }

                string action = intention.Value<string>("action") ?? "<missing>";
                Debug.Log($"[PlanIntentMapper] Mapping action={action} requestId={plan.RequestId} params={SummarizeIntention(intention)}");

                if (TryBuildTaskFromAction(intention, out var task, out var taskError))
                {
                    var request = BuildRequestForAction(task!, plan.RequestId);
                    queue.Enqueue(request);
                    Debug.Log($"[PlanIntentMapper] Enqueued action={action} task={task!.GetType().Name} requestId={plan.RequestId}");
                    enqueuedCount++;
                    mappingNotes?.Add($"intentions[{i}] accepted: action={action} task={task!.GetType().Name}.");
                }
                else
                {
                    Debug.LogWarning($"PlanResponseV3 action rejected: {taskError}");
                    mappingNotes?.Add($"intentions[{i}] rejected: action={action} reason={taskError}");
                }
            }

            if (enqueuedCount == 0)
            {
                error = "No tasks were enqueued from the V3 plan (all were ignored or rejected).";
                return false;
            }

            return true;
        }

        private static string SummarizeIntention(JObject intention)
        {
            var parts = new System.Collections.Generic.List<string>();

            foreach (var property in intention.Properties())
            {
                if (string.Equals(property.Name, "action", System.StringComparison.Ordinal))
                    continue;

                string value = property.Value.Type == JTokenType.String
                    ? property.Value.Value<string>() ?? ""
                    : property.Value.ToString(Newtonsoft.Json.Formatting.None);

                if (!string.IsNullOrWhiteSpace(value))
                    parts.Add($"{property.Name}={value}");
            }

            return parts.Count == 0 ? "<none>" : string.Join(", ", parts);
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

        private static TaskRequest BuildRequestForAction(IAgentTask task, string originRequestId)
        {
            int priority = DefaultPriorityForTask(task);

            return new TaskRequest(
                task: task,
                priority: priority,
                source: TaskSource.LLM,
                canInterrupt: false,
                resumePrevious: false,
                clearStackOnStart: false,
                tag: "llm_plan_v3",
                originRequestId: originRequestId
            );
        }

        private static int DefaultPriorityForTask(IAgentTask task)
        {
            // Keep these in the 50–69 band so reflex reactions can interrupt.
            // Adjust as you add more tasks.
            if (task is Task_MoveToCell) return 60;
            if (task is Task_Wait)       return 40;
            if (task is Task_SetWalkMode) return 65;
            if (task is Task_MoveToObject) return 60;
            if (task is Task_FaceTarget) return 55;
            if (task is Task_Sniff) return 50;
            if (task is Task_Bark) return 60;
            if (task is Task_Emote) return 45;
            if (task is Task_TakeItem) return 65;
            if (task is Task_DropItem) return 50;
            if (task is Task_BuryItem) return 55;
            if (task is Task_ScentFollow) return 60;
            if (task is Task_Sequence) return 60;
            if (task is Task_GoThroughDoor) return 60;
            if (task is Task_PlaceholderAction) return 45;

            return DefaultLlmPriority;
        }

        private static bool TryBuildTaskFromAction(JObject intention, out IAgentTask? task, out string? error)
        {
            task = null;
            error = null;

            string? action = intention.Value<string>("action");
            if (string.IsNullOrWhiteSpace(action))
            {
                error = "action is required.";
                return false;
            }

            switch (action.Trim())
            {
                case "bark":
                {
                    task = new Task_Bark(MapBarkIntentToVolume(intention.Value<string>("bark_intent")));
                    return true;
                }

                case "emote":
                {
                    string emoteIntent = intention.Value<string>("emote_intent") ?? "curious";
                    task = new Task_Emote(emoteIntent.Trim());
                    return true;
                }

                case "set_walk_mode":
                {
                    string walkMode = intention.Value<string>("walk_mode") ?? "Walk";
                    task = new Task_SetWalkMode(ParseWalkMode(walkMode));
                    return true;
                }

                case "face_object":
                {
                    if (!TryResolveTarget(intention, out var target, out error))
                        return false;

                    task = new Task_FaceTarget(target!, toleranceDeg: 6f, maxSeconds: 1.0f);
                    return true;
                }

                case "move_to_object":
                {
                    if (!TryResolveTarget(intention, out var target, out error))
                        return false;

                    task = new Task_MoveToObject(target!);
                    return true;
                }

                case "examine_object":
                {
                    if (!TryResolveTarget(intention, out var target, out error))
                        return false;

                    task = new Task_Sequence(new IAgentTask[]
                    {
                        new Task_MoveToObject(target!),
                        new Task_FaceTarget(target!, toleranceDeg: 8f, maxSeconds: 1.0f),
                        new Task_Sniff(null)
                    });
                    return true;
                }

                case "sniff":
                {
                    task = new Task_Sniff(null);
                    return true;
                }

                case "take_object":
                {
                    if (!TryResolveTarget(intention, out var target, out error))
                        return false;

                    int targetId = target!.ObjectId;
                    task = new Task_Sequence(new IAgentTask[]
                    {
                        new Task_MoveToObject(target!),
                        new Task_SetInt("item.targetId", targetId),
                        new Task_SetInt("vision.lastTargetId", targetId),
                        new Task_TakeItem()
                    });
                    return true;
                }

                case "drop_object":
                {
                    task = new Task_DropItem();
                    return true;
                }

                case "bury_object":
                {
                    task = new Task_BuryItem();
                    return true;
                }

                case "interact_with_held_object":
                {
                    string interaction = intention.Value<string>("interaction") ?? "";
                    if (string.Equals(interaction, "drop", System.StringComparison.Ordinal))
                    {
                        task = new Task_DropItem();
                        return true;
                    }

                    if (string.Equals(interaction, "bury", System.StringComparison.Ordinal))
                    {
                        task = new Task_BuryItem();
                        return true;
                    }

                    task = BuildPlaceholderAction(action, intention, detail: $"interaction={interaction}");
                    return true;
                }

                case "start_follow_scent":
                {
                    string? targetIdRaw = intention.Value<string>("target_id");
                    if (!TryParseTargetId(targetIdRaw, out int targetId))
                    {
                        error = $"start_follow_scent target_id \"{targetIdRaw}\" is not a valid entity id.";
                        return false;
                    }

                    task = new Task_ScentFollow($"agent:{targetId}", ScentMedium.Ground);
                    return true;
                }

                case "dig":
                {
                    task = new Task_Sequence(new IAgentTask[]
                    {
                        new Task_Emote("dig"),
                        new Task_Wait(1.0f),
                        BuildPlaceholderAction(action, intention)
                    });
                    return true;
                }

                case "go_through_door":
                case "open_door":
                case "close_door":
                {
                    if (TryBuildDoorTask(action.Trim(), intention, out task, out error))
                        return true;

                    if (!TryResolveTarget(intention, out var target, out var targetError))
                    {
                        error = error != null
                            ? $"{error}; object fallback failed: {targetError}"
                            : targetError;
                        return false;
                    }

                    task = BuildTargetedPlaceholderSequence(action, intention, target!);
                    return true;
                }

                case "interact_with_object":
                case "join_pack":
                case "start_follow_object":
                case "start_patrol_around_object":
                {
                    if (!TryResolveTarget(intention, out var target, out error))
                        return false;

                    string? detail = null;
                    if (string.Equals(action, "interact_with_object", System.StringComparison.Ordinal))
                        detail = $"interaction={intention.Value<string>("interaction") ?? "unknown"}";
                    else if (string.Equals(action, "start_follow_object", System.StringComparison.Ordinal) ||
                             string.Equals(action, "start_patrol_around_object", System.StringComparison.Ordinal))
                        detail = $"distance={intention.Value<int?>("distance")?.ToString() ?? "default"}";

                    task = BuildTargetedPlaceholderSequence(action, intention, target!, detail);
                    return true;
                }

                case "become_pack_leader":
                case "leave_pack":
                case "start_follow_pack_leader":
                case "start_follow_on_leash":
                case "stop_current_mode":
                {
                    task = BuildPlaceholderAction(action, intention);
                    return true;
                }

                case "start_exploring":
                {
                    task = new Task_Sequence(new IAgentTask[]
                    {
                        new Task_RandomNearbyMove(radiusCells: 2),
                        new Task_Sniff(null),
                        BuildPlaceholderAction(action, intention)
                    });
                    return true;
                }

                case "start_patrol_room":
                {
                    task = new Task_Sequence(new IAgentTask[]
                    {
                        new Task_RandomNearbyMove(radiusCells: 2),
                        new Task_Sniff(null),
                        BuildPlaceholderAction(action, intention)
                    });
                    return true;
                }

                case "start_stay":
                {
                    task = new Task_Sequence(new IAgentTask[]
                    {
                        new Task_Emote("stay"),
                        new Task_Wait(2.0f),
                        BuildPlaceholderAction(action, intention)
                    });
                    return true;
                }

                case "nap":
                {
                    float duration = Mathf.Clamp(intention.Value<int?>("duration") ?? 5, 1, 300);
                    task = new Task_Sequence(new IAgentTask[]
                    {
                        new Task_Emote("sleepy"),
                        BuildPlaceholderAction(action, intention, detail: $"duration={duration:0}"),
                        new Task_Wait(duration)
                    });
                    return true;
                }

                case "wait":
                {
                    int duration = Mathf.Clamp(intention.Value<int?>("duration") ?? 1, 0, 30);
                    task = new Task_Wait(duration);
                    return true;
                }

                default:
                    error = $"Action \"{action}\" is not implemented in the current V3 mapper.";
                    return false;
            }
        }

        private static bool TryResolveTarget(JObject intention, out WorldObject? target, out string? error)
        {
            target = null;
            error = null;

            string? targetIdRaw = intention.Value<string>("target_id");
            if (!TryParseTargetId(targetIdRaw, out int targetId))
            {
                error = $"target_id \"{targetIdRaw}\" is not a valid entity id.";
                return false;
            }

            Dir.Instance.worldObjectRegistry.TryGet(targetId, out var resolvedTarget);
            if (resolvedTarget == null)
            {
                error = $"target_id {targetId} matched no objects.";
                return false;
            }

            target = resolvedTarget;
            return true;
        }

        private static bool TryBuildDoorTask(
            string action,
            JObject intention,
            out IAgentTask? task,
            out string? error)
        {
            task = null;
            error = null;

            string? targetIdRaw = intention.Value<string>("target_id");
            if (!TryParseTargetId(targetIdRaw, out int doorId))
            {
                error = $"door target_id \"{targetIdRaw}\" is not a valid door id.";
                return false;
            }

            if (!TryResolveDoor(doorId, out DoorLookupInfo doorInfo, out error))
                return false;

            if (string.Equals(action, "go_through_door", System.StringComparison.Ordinal))
            {
                task = new Task_GoThroughDoor(doorId);
                return true;
            }

            task = BuildDoorPlaceholderSequence(action, intention, doorInfo);
            return true;
        }

        private static bool TryResolveDoor(int doorId, out DoorLookupInfo doorInfo, out string? error)
        {
            doorInfo = default;
            error = null;

            if (Dir.Instance == null || Dir.Instance.gen == null || Dir.Instance.gen.rooms == null)
            {
                error = "door lookup failed because the dungeon generator is unavailable.";
                return false;
            }

            if (!DoorIdUtility.TryGetDoorInfo(Dir.Instance.gen.rooms, doorId, out doorInfo))
            {
                error = $"doorId {doorId} matched no room doors.";
                return false;
            }

            return true;
        }

        private static IAgentTask BuildDoorPlaceholderSequence(
            string action,
            JObject intention,
            DoorLookupInfo doorInfo)
        {
            return new Task_Sequence(new IAgentTask[]
            {
                new Task_MoveToCell(doorInfo.cell.x, doorInfo.cell.y),
                BuildDoorPlaceholderAction(action, intention, doorInfo)
            });
        }

        private static IAgentTask BuildTargetedPlaceholderSequence(
            string action,
            JObject intention,
            WorldObject target,
            string? detail = null)
        {
            return new Task_Sequence(new IAgentTask[]
            {
                new Task_MoveToObject(target),
                BuildPlaceholderAction(action, intention, target, detail)
            });
        }

        private static Task_PlaceholderAction BuildDoorPlaceholderAction(
            string action,
            JObject intention,
            DoorLookupInfo doorInfo)
        {
            string reasoning = intention.Value<string>("reasoning") ?? "No reasoning provided.";
            string targetSummary =
                $"doorId={doorInfo.doorId}, room={doorInfo.roomId}, cell=[{doorInfo.cell.x},{doorInfo.cell.y}], direction={DirFlagsEx.ToLongName(doorInfo.direction)}, throughCell=[{doorInfo.throughCell.x},{doorInfo.throughCell.y}], targetRoom={doorInfo.targetRoomId}";
            return new Task_PlaceholderAction(action, reasoning, targetSummary, detail: "door");
        }

        private static Task_PlaceholderAction BuildPlaceholderAction(
            string action,
            JObject intention,
            WorldObject? target = null,
            string? detail = null)
        {
            string reasoning = intention.Value<string>("reasoning") ?? "No reasoning provided.";
            string? targetSummary = target != null ? $"{target.DisplayName}#{target.ObjectId}" : null;
            return new Task_PlaceholderAction(action, reasoning, targetSummary, detail);
        }

        private static bool TryParseTargetId(string? targetIdRaw, out int targetId)
        {
            targetId = -1;

            if (string.IsNullOrWhiteSpace(targetIdRaw))
                return false;

            string trimmed = targetIdRaw.Trim();
            if (int.TryParse(trimmed, out targetId))
                return true;

            int colonIndex = trimmed.LastIndexOf(':');
            if (colonIndex >= 0 && colonIndex + 1 < trimmed.Length)
                return int.TryParse(trimmed.Substring(colonIndex + 1), out targetId);

            return false;
        }

        private static int MapBarkIntentToVolume(string? barkIntent)
        {
            switch ((barkIntent ?? "").Trim())
            {
                case "social":
                    return 4;
                case "found":
                    return 6;
                case "suspicious":
                case "need_help":
                    return 7;
                case "alert_pack":
                case "threat":
                    return 9;
                default:
                    return 6;
            }
        }

        private static WalkMode ParseWalkMode(string mode)
        {
            switch ((mode ?? "").Trim().ToLowerInvariant())
            {
                case "walk":
                    return WalkMode.Walk;
                case "run":
                    return WalkMode.Run;
                case "sneak":
                    return WalkMode.Sneak;
                case "cautious":
                    return WalkMode.Cautious;
                case "crawl":
                    return WalkMode.Crawl;
                default:
                    return WalkMode.Walk;
            }
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

                    task = new Task_MoveToCell(cellX, cellY);
                    return true;
                }

                case "move_to_object":
                {
                    // NOTE: Maybe should be a string instead of int.  Lookup function is by int.
                    //
                    //string targetEntityId = (string)(parameters.Value<string?>("targetEntityId") ?? "");
                    //if (targetEntityId.Length > 20)   // chop end if too long
                    //    targetEntityId = targetEntityId.Substring(0, 20);
                    //targetEntityId = targetEntityId.Trim();     // clean up whitespace
                    
                    int targetEntityId = (int)(parameters.Value<int?>("targetEntityId") ?? -1);
                    
                    WorldObject targetEntity;
                    Dir.Instance.worldObjectRegistry.TryGet(targetEntityId, out targetEntity);
                    if (targetEntity==null)
                    {
                        error = $"move_to_object parameters.targetEntityId {targetEntityId} matched no objects.";
                        return false;
                    }                    
                    task = new Task_MoveToObject(targetEntity);
                    return true;
                }

                case "sniff":
                {
                    float durationSeconds = (float)(parameters.Value<double?>("stopRadius") ?? 0.35);
                    durationSeconds = Mathf.Clamp(durationSeconds, 0.05f, 2.0f);

                    task = new Task_Sniff(null);  //TODO: wants HashSet<string>
                    return true;
                }

                case "bark":
                {
                    int volume = (int)(parameters.Value<int?>("volume") ?? 8);
                    volume = Mathf.Clamp(volume, 1, 10);

                    task = new Task_Bark(volume);
                    return true;
                }

                case "emote":
                {
                    string type = (string)(parameters.Value<string?>("emote") ?? "");
                    if (type.Length > 20)   // chop end if too long
                        type = type.Substring(0, 20);
                    type = type.Trim();     // clean up whitespace
                    
                    task = new Task_Emote(type);
                    return true;
                }

                default:
                    error = $"Unsupported add_task.parameters.task \"{taskName}\" (v1 supports wait, move_to_cell).";
                    return false;
            }
        }
    }
}
