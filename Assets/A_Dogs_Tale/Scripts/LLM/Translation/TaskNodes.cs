#nullable enable
using System;
using System.Collections.Generic;

namespace DogGame.LLM.Translation
{
    public static class TaskNodes
    {
        public static TaskNode Sequence(params TaskNode[] children)
        {
            var node = new TaskNode { taskTypeName = "Task_Sequence" };
            if (children != null) node.children.AddRange(children);
            return node;
        }

        public static TaskNode Try(params TaskNode[] children)
        {
            var node = new TaskNode { taskTypeName = "Task_Try" };
            if (children != null) node.children.AddRange(children);
            return node;
        }

        public static TaskNode WaitSeconds(float seconds, string? note = null)
        {
            var node = new TaskNode { taskTypeName = "Task_Wait" };
            node.parameters["durationSeconds"] = seconds;
            if (!string.IsNullOrWhiteSpace(note)) node.parameters["note"] = note;
            return node;
        }

        public static TaskNode PushGoal(string goal, double? horizonSeconds = null)
        {
            var node = new TaskNode { taskTypeName = "Task_PushGoal" };
            node.parameters["goal"] = goal;
            if (horizonSeconds.HasValue) node.parameters["horizonSeconds"] = horizonSeconds.Value;
            return node;
        }

        public static TaskNode Emote(string kind)
        {
            var node = new TaskNode { taskTypeName = "Task_Emote" };
            node.parameters["kind"] = kind;
            return node;
        }

        public static TaskNode Bark(string intensity = "normal")
        {
            var node = new TaskNode { taskTypeName = "Task_Bark" };
            node.parameters["intensity"] = intensity;
            return node;
        }

        public static TaskNode FaceTarget(string targetId)
        {
            var node = new TaskNode { taskTypeName = "Task_FaceTarget" };
            node.parameters["targetId"] = targetId;
            return node;
        }

        public static TaskNode MoveToObject(string objectId)
        {
            var node = new TaskNode { taskTypeName = "Task_MoveToObject" };
            node.parameters["objectId"] = objectId;
            return node;
        }

        public static TaskNode MoveToLocation(string location)
        {
            var node = new TaskNode { taskTypeName = "Task_MoveToLocation" };
            node.parameters["location"] = location;
            return node;
        }

        public static TaskNode MoveToCell(int x, int y)
        {
            var node = new TaskNode { taskTypeName = "Task_MoveToCell" };
            node.parameters["x"] = x;
            node.parameters["y"] = y;
            return node;
        }

        public static TaskNode Sniff(string? focus = null)
        {
            var node = new TaskNode { taskTypeName = "Task_Sniff" };
            if (!string.IsNullOrWhiteSpace(focus)) node.parameters["focus"] = focus;
            return node;
        }

        public static TaskNode FollowScentTrail(string? scentType = null)
        {
            var node = new TaskNode { taskTypeName = "Task_FollowScentTrail" };
            if (!string.IsNullOrWhiteSpace(scentType)) node.parameters["scentType"] = scentType;
            return node;
        }

        public static TaskNode Abort(string? reason = null)
        {
            var node = new TaskNode { taskTypeName = "Task_Abort" };
            if (!string.IsNullOrWhiteSpace(reason)) node.parameters["reason"] = reason;
            return node;
        }

        public static TaskNode RequestLLMPlan(string request)
        {
            var node = new TaskNode { taskTypeName = "Task_RequestLLMPlan" };
            node.parameters["request"] = request;
            return node;
        }
    }
}