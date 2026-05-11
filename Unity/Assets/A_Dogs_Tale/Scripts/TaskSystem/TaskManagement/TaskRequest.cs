#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using DogGame.LLM;
using DogGame.Modules;
using UnityEngine;

namespace DogGame.Tasks
{
    public readonly struct TaskRequest
    {
        public readonly IAgentTask Task;
        public readonly int Priority;           // 0–100
        public readonly bool CanInterrupt;
        public readonly bool ResumePrevious;
        public readonly bool ClearStackOnStart;
        public readonly TaskSource Source;
        public readonly string? Tag;
        public readonly string? OriginRequestId;

        public TaskRequest(
            IAgentTask task,
            int priority,
            TaskSource source,
            bool canInterrupt = true,
            bool resumePrevious = false,
            bool clearStackOnStart = false,
            string? tag = null,
            string? originRequestId = null)
        {
            Task = task;
            Priority = priority;
            Source = source;
            CanInterrupt = canInterrupt;
            ResumePrevious = resumePrevious;
            ClearStackOnStart = clearStackOnStart;
            Tag = tag;
            OriginRequestId = originRequestId;
        }
    }

    [Serializable]
    public sealed class TaskExecutorSaveData
    {
        public TaskRequestSaveData? currentRequest;
        public bool currentTaskStarted;
        public float currentRequestElapsed;
        public string? lastFailureReason;
        public List<TaskRequestSaveData> suspendedRequests = new();
    }

    [Serializable]
    public sealed class TaskRequestSaveData
    {
        public AgentTaskSaveData? task;
        public int priority;
        public bool canInterrupt;
        public bool resumePrevious;
        public bool clearStackOnStart;
        public int source;
        public string? tag;
        public string? originRequestId;

        public static TaskRequestSaveData? FromRequest(TaskRequest request)
        {
            AgentTaskSaveData? taskData = AgentTaskSaveData.FromTask(request.Task);
            if (taskData == null || taskData.unsupported)
                return null;

            return new TaskRequestSaveData
            {
                task = taskData,
                priority = request.Priority,
                canInterrupt = request.CanInterrupt,
                resumePrevious = request.ResumePrevious,
                clearStackOnStart = request.ClearStackOnStart,
                source = (int)request.Source,
                tag = request.Tag,
                originRequestId = request.OriginRequestId
            };
        }

        public bool TryToRequest(out TaskRequest request)
        {
            request = default;
            if (task == null || !task.TryToTask(out IAgentTask restoredTask))
                return false;

            request = new TaskRequest(
                task: restoredTask,
                priority: priority,
                source: (TaskSource)source,
                canInterrupt: canInterrupt,
                resumePrevious: resumePrevious,
                clearStackOnStart: clearStackOnStart,
                tag: tag,
                originRequestId: originRequestId);
            return true;
        }
    }

    [Serializable]
    public sealed class AgentTaskSaveData
    {
        public string taskType = "";
        public string debugName = "";
        public bool unsupported;
        public int intValue;
        public int intValue2;
        public int intValue3;
        public float floatValue;
        public float floatValue2;
        public float floatValue3;
        public string stringValue = "";
        public string stringValue2 = "";
        public string stringValue3 = "";
        public bool boolValue;
        public List<AgentTaskSaveData> children = new();

        public static AgentTaskSaveData? FromTask(IAgentTask task)
        {
            if (task == null)
                return null;

            AgentTaskSaveData data = new()
            {
                taskType = task.GetType().Name,
                debugName = task.DebugName
            };

            switch (task)
            {
                case Task_MoveToLocation:
                    data.floatValue = GetField<float>(task, "mapX");
                    data.floatValue2 = GetField<float>(task, "mapY");
                    return data;
                case Task_MoveToCell:
                    data.intValue = GetField<int>(task, "cellX");
                    data.intValue2 = GetField<int>(task, "cellY");
                    return data;
                case Task_MoveToObject:
                    data.intValue = GetField<WorldObject>(task, "target")?.ObjectId ?? -1;
                    return data;
                case Task_Wait:
                    data.floatValue = Mathf.Max(0f, GetField<float>(task, "remainingSeconds"));
                    return data;
                case Task_Bark:
                    data.floatValue = GetField<int>(task, "volume");
                    return data;
                case Task_Emote:
                    data.stringValue = GetField<string>(task, "emote") ?? "";
                    return data;
                case Task_SetWalkMode:
                    data.intValue = (int)GetField<WalkMode>(task, "walkMode");
                    return data;
                case Task_GoThroughDoor:
                    data.intValue = GetField<int>(task, "doorId");
                    data.intValue2 = (int)GetField<WalkMode>(task, "walkMode");
                    return data;
                case Task_RunLua:
                    CaptureRunLua(task, data);
                    return data;
                case Task_ScentFollowLua:
                    IAgentTask? innerTask = GetField<IAgentTask>(task, "innerTask");
                    AgentTaskSaveData? innerData = innerTask != null ? FromTask(innerTask) : null;
                    if (innerData != null)
                    {
                        data.children.Add(innerData);
                        return data;
                    }
                    break;
                case Task_RandomNearbyMove:
                    data.intValue = GetField<int>(task, "radiusCells");
                    return data;
                case Task_Sequence:
                    CaptureSequence(task, data);
                    return data;
            }

            data.unsupported = true;
            return data;
        }

        public bool TryToTask(out IAgentTask task)
        {
            task = null!;
            if (unsupported)
                return false;

            switch (taskType)
            {
                case nameof(Task_MoveToLocation):
                    task = new Task_MoveToLocation(floatValue, floatValue2);
                    return true;
                case nameof(Task_MoveToCell):
                    task = new Task_MoveToCell(intValue, intValue2);
                    return true;
                case nameof(Task_MoveToObject):
                    if (WorldObjectRegistry.Instance == null ||
                        !WorldObjectRegistry.Instance.TryGet(intValue, out WorldObject target) ||
                        target == null)
                    {
                        return false;
                    }

                    task = new Task_MoveToObject(target);
                    return true;
                case nameof(Task_Wait):
                    task = new Task_Wait(floatValue);
                    return true;
                case nameof(Task_Bark):
                    task = new Task_Bark(floatValue);
                    return true;
                case nameof(Task_Emote):
                    task = new Task_Emote(stringValue);
                    return true;
                case nameof(Task_SetWalkMode):
                    task = new Task_SetWalkMode((WalkMode)intValue);
                    return true;
                case nameof(Task_GoThroughDoor):
                    task = new Task_GoThroughDoor(intValue, (WalkMode)intValue2);
                    return true;
                case nameof(Task_RunLua):
                    task = new Task_RunLua(
                        fileNameLua: stringValue,
                        entryFunction: stringValue2,
                        maxSeconds: floatValue,
                        scentKey: stringValue3,
                        scentMedium: (ScentMedium)intValue,
                        minThreshold: floatValue2,
                        visitRoomCenterBeforeBacktracking: boolValue);
                    return true;
                case nameof(Task_ScentFollowLua):
                    if (children != null && children.Count > 0 && children[0].TryToTask(out IAgentTask restoredInner))
                    {
                        task = restoredInner;
                        return true;
                    }
                    return false;
                case nameof(Task_RandomNearbyMove):
                    task = new Task_RandomNearbyMove(intValue);
                    return true;
                case nameof(Task_Sequence):
                    List<IAgentTask> restoredChildren = new();
                    if (children != null)
                    {
                        foreach (AgentTaskSaveData child in children)
                        {
                            if (child != null && child.TryToTask(out IAgentTask childTask))
                                restoredChildren.Add(childTask);
                        }
                    }

                    task = new Task_Sequence(restoredChildren);
                    return restoredChildren.Count > 0;
            }

            return false;
        }

        private static void CaptureRunLua(IAgentTask task, AgentTaskSaveData data)
        {
            data.stringValue = GetField<string>(task, "fileNameLua") ?? "";
            data.stringValue2 = GetField<string>(task, "entryFunction") ?? "tick";
            data.stringValue3 = GetField<string>(task, "scentKey") ?? "";
            data.floatValue = GetField<float>(task, "maxSeconds");
            data.floatValue2 = GetField<float>(task, "minThreshold");
            data.intValue = (int)GetField<ScentMedium>(task, "scentMedium");
            data.boolValue = GetField<bool>(task, "visitRoomCenterBeforeBacktracking");
        }

        private static void CaptureSequence(IAgentTask task, AgentTaskSaveData data)
        {
            List<IAgentTask> steps = GetField<List<IAgentTask>>(task, "steps") ?? new List<IAgentTask>();
            int stepIndex = Mathf.Clamp(GetField<int>(task, "stepIndex"), 0, Mathf.Max(0, steps.Count));
            data.intValue = stepIndex;

            for (int i = stepIndex; i < steps.Count; i++)
            {
                AgentTaskSaveData? child = FromTask(steps[i]);
                if (child != null && !child.unsupported)
                    data.children.Add(child);
            }
        }

        private static T? GetField<T>(object instance, string name)
        {
            FieldInfo? field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
                return default;

            object? value = field.GetValue(instance);
            if (value is T typed)
                return typed;

            return default;
        }
    }
}
