#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.Reactions
{
    /// <summary>TaskSpec helpers: short, predictable, LLM-friendly.</summary>
    public static class TS
    {
        public static TaskSpec Bark(int volume10) =>
            TaskSpecBuilder.Task("bark").Arg("volume10", volume10).Build();

        public static TaskSpec Wait(float seconds) =>
            TaskSpecBuilder.Task("wait").Arg("seconds", seconds).Build();

        public static TaskSpec Sniff(float seconds = 1.0f) =>
            TaskSpecBuilder.Task("sniff").Arg("seconds", seconds).Build();

        public static TaskSpec Emote(string id) =>
            TaskSpecBuilder.Task("emote").Arg("id", id).Build();

        public static TaskSpec MoveToLocation(bool useEventWorldPos = true, float stopRadius = 0.6f) =>
            TaskSpecBuilder.Task("move_to_event_location")
                .Arg("useEventWorldPos", useEventWorldPos)
                .Arg("stopRadius", stopRadius)
                .Build();

        public static TaskSpec Try(TaskSpec tryTask, TaskSpec onFail) =>
            new TaskSpec("try", new Dictionary<string, object>
            {
                ["try"] = tryTask,
                ["fail"] = onFail
            });

        public static TaskSpec Sequence(params TaskSpec[] tasks) =>
            new TaskSpec("sequence", new Dictionary<string, object> { ["tasks"] = tasks });

        public static TaskSpec Timeout(float seconds, TaskSpec inner) =>
            new TaskSpec("timeout", new Dictionary<string, object>
            {
                ["seconds"] = seconds,
                ["inner"] = inner
            });

        /// <summary>
        /// Choose exactly one option at runtime and execute it to completion.
        /// </summary>
        public static TaskSpec Random(params TaskSpec[] options) =>
            new TaskSpec(name: "random", new Dictionary<string, object> { ["options"] = options });
        
        /// <summary>
        /// Move toward the PerceptionEvent.WorldPos.
        /// Common for vision/scent investigation.
        /// </summary>
        public static TaskSpec MoveToEvent(float stopRadius = 0.6f) =>
            TaskSpecBuilder.Task("move_to_event")
                .Arg("stopRadius", stopRadius)
                .Build();

        /// <summary>
        /// Move to a specific world location.
        /// </summary>
        public static TaskSpec MoveToLocation(Vector3 worldPos, float stopRadius = 0.6f) =>
            TaskSpecBuilder.Task("move_to_location")
                .Arg("x", worldPos.x)
                .Arg("y", worldPos.y)
                .Arg("z", worldPos.z)
                .Arg("stopRadius", stopRadius)
                .Build();

        /// <summary>
        /// Move to the perceived target object (vision-based).
        /// </summary>
        public static TaskSpec MoveToTarget(float stopRadius = 0.8f) =>
            TaskSpecBuilder.Task("move_to_target")
                .Arg("stopRadius", stopRadius)
                .Build();

        /// <summary>
        /// Small exploratory movement around current position.
        /// </summary>
        public static TaskSpec RandomNearbyMove(float radius = 1.5f) =>
            TaskSpecBuilder.Task("random_nearby_move")
                .Arg("radius", radius)
                .Build();

        /// <summary>
        /// Set movement style. Accepts: "walk","run","sneak","crawl","backpedal","strafe"
        /// (case-insensitive). Unknown => "walk".
        /// </summary>
        public static TaskSpec SetWalkMode(string mode) =>
            TaskSpecBuilder.Task("set_walk_mode")
                .Arg("mode", mode)
                .Build();

        /// <summary>
        /// Rotate to face the current PerceptionEvent.Target (vision), or no-op fail if missing.
        /// </summary>
        public static TaskSpec FaceTarget(float toleranceDeg = 6f, float maxSeconds = 1.0f) =>
            TaskSpecBuilder.Task("face_target")
                .Arg("toleranceDeg", toleranceDeg)
                .Arg("maxSeconds", maxSeconds)
                .Build();

        /// <summary>
        /// Move toward the vision target until it is actually visible (LOS + optional FOV),
        /// or until timeout. Useful for "go until you can see it".
        /// </summary>
        public static TaskSpec MoveUntilSeen(
            float stopRadius = 1.0f,
            float maxSeconds = 4.0f,
            float viewRadius = 12.0f,
            float fovDeg = 160.0f,
            bool requireFov = true) =>
            TaskSpecBuilder.Task("move_until_seen")
                .Arg("stopRadius", stopRadius)
                .Arg("maxSeconds", maxSeconds)
                .Arg("viewRadius", viewRadius)
                .Arg("fovDeg", fovDeg)
                .Arg("requireFov", requireFov)
                .Build();

        /// <summary>
        /// Push a high-level goal/hint into the blackboard.
        /// Phase-1: sets Blackboard string "goal.current".
        /// </summary>
        public static TaskSpec PushGoal(string goalId, bool overwrite = true) =>
            TaskSpecBuilder.Task("push_goal")
                .Arg("goalId", goalId)
                .Arg("overwrite", overwrite)
                .Build();

        public static TaskSpec Call(string routineId) =>
            TaskSpecBuilder.Task("routine_call")
                .Arg("id", routineId)
                .Build();
            
        // These take no explicit WorldObject parameter because routines/rules
        // typically act on the current Vision target or a blackboard-selected item.
        //
        // Why no parameter here? Because your DSL/LLM is way more reliable if it
        // can say “take the current target” rather than needing object references. 
        // We’ll resolve the item from:
        //	•	PerceptionEvent.Vision.Target if present
        //	•	otherwise a blackboard slot like item.target
        //
        // (If you really want explicit item IDs later, we can add take_item { objectId: 123 }.)

        public static TaskSpec TakeItem() =>
            TaskSpecBuilder.Task("take_item").Build();

        public static TaskSpec DropItem() =>
            TaskSpecBuilder.Task("drop_item").Build();

        public static TaskSpec BuryItem(float depthMeters = 0.15f) =>
            TaskSpecBuilder.Task("bury_item")
                .Arg("depthMeters", depthMeters)
                .Build();

    }
}

/*
What the LLM can now safely generate:

Example: scent investigation
.DoTasks(
    TS.Bark(6),
    TS.MoveToEvent(0.7f),
    TS.Sniff(1.2f)
)

Example: vision-based chase
.DoTasks(
    TS.Emote("alert"),
    TS.MoveToTarget(1.0f)
)

Example: curious wander
.DoTask(
    TS.RandomNearbyMove(2.0f)
)

Example: Chase until visible, then bark:
.DoTasks(
    TS.SetWalkMode("run"),
    TS.MoveUntilSeen(stopRadius: 1.0f, maxSeconds: 6.0f),
    TS.FaceTarget(6f, 1.0f),
    TS.Bark(7)
)

Example: Investigate smell, but stay sneaky:
.DoTasks(
    TS.PushGoal("investigate_food"),
    TS.SetWalkMode("sneak"),
    TS.MoveToEvent(0.7f),
    TS.Sniff(1.2f)
)
*/