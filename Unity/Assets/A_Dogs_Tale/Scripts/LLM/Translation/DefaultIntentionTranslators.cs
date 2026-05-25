#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DogGame.LLM.Translation
{
    public sealed class NoopTranslator : IIntentionTranslator
    {
        public PlanIntentionType Type => PlanIntentionType.noop;

        public List<TaskNode> Translate(PlanIntentionV1 intention)
        {
            // Noop maps to a tiny wait (or you could map to Task_RandomNearbyMove for ambient life).
            return new List<TaskNode> { TaskNodes.WaitSeconds(0.2f) };
        }
    }

    public sealed class SetGoalTranslator : IIntentionTranslator
    {
        public PlanIntentionType Type => PlanIntentionType.set_goal;

        public List<TaskNode> Translate(PlanIntentionV1 intention)
        {
            string goal = intention.Parameters?["goal"]?.Value<string>() ?? "";
            if (string.IsNullOrWhiteSpace(goal))
                return new List<TaskNode> { TaskNodes.WaitSeconds(0.2f, "Missing goal parameter") };

            double? horizon = null;
            if (intention.Parameters?["horizonSeconds"] != null &&
                (intention.Parameters["horizonSeconds"]!.Type == JTokenType.Integer ||
                 intention.Parameters["horizonSeconds"]!.Type == JTokenType.Float))
            {
                horizon = intention.Parameters.Value<double>("horizonSeconds");
            }

            return new List<TaskNode> { TaskNodes.PushGoal(goal, horizon) };
        }
    }

    public sealed class AddTaskTranslator : IIntentionTranslator
    {
        public PlanIntentionType Type => PlanIntentionType.add_task;

        public List<TaskNode> Translate(PlanIntentionV1 intention)
        {
            // Your LLM currently gives { task: "..." } as a string.
            // A safe first implementation is to feed that string into your existing Task_RequestLLMPlan
            // or build a tiny "do this" sequence later when you add a real task compiler.
            string task = intention.Parameters?["task"]?.Value<string>() ?? "";
            if (string.IsNullOrWhiteSpace(task))
                return new List<TaskNode> { TaskNodes.WaitSeconds(0.2f, "Missing task parameter") };

            // Minimal: request a more concrete plan, or interpret simple keywords.
            // You can replace this with a real compiler once you decide your task DSL.
            return new List<TaskNode>
            {
                TaskNodes.RequestLLMPlan(task)
            };
        }
    }

    public sealed class ProposeDialogueTranslator : IIntentionTranslator
    {
        public PlanIntentionType Type => PlanIntentionType.propose_dialogue;

        public List<TaskNode> Translate(PlanIntentionV1 intention)
        {
            string message = intention.Parameters?["message"]?.Value<string>() ?? "";
            if (string.IsNullOrWhiteSpace(message))
                return new List<TaskNode> { TaskNodes.WaitSeconds(0.2f, "Missing message parameter") };

            // You have Task_Emote. If it supports speech, great. If not, swap to Task_Bark/Task_Emote variants.
            // We'll encode speech as an emote with a "message" parameter.
            var speak = new TaskNode { taskTypeName = "Task_Emote" };
            speak.parameters["message"] = message;

            string? tone = intention.Parameters?["tone"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(tone)) speak.parameters["tone"] = tone;

            string? toEntityId = intention.Parameters?["toEntityId"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(toEntityId)) speak.parameters["toEntityId"] = toEntityId;

            return new List<TaskNode> { speak };
        }
    }

    public sealed class RequestObservationTranslator : IIntentionTranslator
    {
        public PlanIntentionType Type => PlanIntentionType.request_observation;

        public List<TaskNode> Translate(PlanIntentionV1 intention)
        {
            string request = intention.Parameters?["request"]?.Value<string>() ?? "";
            if (string.IsNullOrWhiteSpace(request))
                return new List<TaskNode> { TaskNodes.WaitSeconds(0.2f, "Missing request parameter") };

            // First pass: sniff + maybe move nearby.
            // You can get fancier later (MoveUntilSeen, FollowScentTrail, etc.)
            int radiusRooms = 0;
            if (intention.Parameters?["radiusRooms"] != null &&
                (intention.Parameters["radiusRooms"]!.Type == JTokenType.Integer))
            {
                radiusRooms = intention.Parameters.Value<int>("radiusRooms");
            }

            var nodes = new List<TaskNode>
            {
                TaskNodes.Sniff(focus: request)
            };

            // If radius > 0, do a small roam.
            if (radiusRooms > 0)
                nodes.Add(new TaskNode { taskTypeName = "Task_RandomNearbyMove", parameters = { ["radiusRooms"] = radiusRooms } });

            return nodes;
        }
    }

    public sealed class ProposeTrapTranslator : IIntentionTranslator
    {
        public PlanIntentionType Type => PlanIntentionType.propose_trap;

        public List<TaskNode> Translate(PlanIntentionV1 intention)
        {
            string trap = intention.Parameters?["trap"]?.Value<string>() ?? "";
            if (string.IsNullOrWhiteSpace(trap))
                return new List<TaskNode> { TaskNodes.WaitSeconds(0.2f, "Missing trap parameter") };

            var loc = intention.Parameters?["locationCell"] as JArray;
            if (loc == null || loc.Count != 2 || loc[0].Type != JTokenType.Integer || loc[1].Type != JTokenType.Integer)
                return new List<TaskNode> { TaskNodes.WaitSeconds(0.2f, "Invalid locationCell") };

            int x = loc[0].Value<int>();
            int y = loc[1].Value<int>();

            // You don't have an explicit "place trap" task in the list.
            // So first pass: move there and (optionally) bury item / emote.
            var move = TaskNodes.MoveToCell(x, y);

            var emote = TaskNodes.Emote("setup_trap");
            emote.parameters["trap"] = trap;

            string? trigger = intention.Parameters?["trigger"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(trigger)) emote.parameters["trigger"] = trigger;

            return new List<TaskNode>
            {
                TaskNodes.Sequence(move, emote)
            };
        }
    }

    public sealed class UpdateBeliefsTranslator : IIntentionTranslator
    {
        public PlanIntentionType Type => PlanIntentionType.update_beliefs;

        public List<TaskNode> Translate(PlanIntentionV1 intention)
        {
            // You likely have some belief/memory system.
            // For now: encode as a Task_SetBool / Task_SetBool-like pattern or a generic "emote/log" placeholder.
            var beliefs = intention.Parameters?["beliefs"] as JArray;
            if (beliefs == null || beliefs.Count == 0)
                return new List<TaskNode> { TaskNodes.WaitSeconds(0.2f, "Missing beliefs array") };

            var node = new TaskNode { taskTypeName = "Task_Emote" };
            node.parameters["kind"] = "update_beliefs";
            node.parameters["beliefs"] = beliefs.ToObject<object?>();

            return new List<TaskNode> { node };
        }
    }
}