#nullable enable
using System;
using UnityEngine;
using DogGame.LLM;
using DogGame.Tasks;
using DogGame.Modules;

namespace DogGame.Reactions
{
    public static class TaskSpecFactory
    {
        public static bool TryBuildTask(TaskSpec spec, WorldObject observer, PerceptionEvent e, out IAgentTask? task, out string? error)
        {
            task = null;
            error = null;

            string name = spec.Name.Trim().ToLowerInvariant();

            // Leaf tasks
            if (name == "bark")
            {
                int volume10 = GetInt(spec, "volume10", 5);
                volume10 = Mathf.Clamp(volume10, 1, 10);
                task = new Task_Bark(volume10);
                return true;
            }

            if (name == "wait")
            {
                float seconds = GetFloat(spec, "seconds", 0.2f);
                seconds = Mathf.Clamp(seconds, 0f, 30f);
                task = new Task_Wait(seconds);
                return true;
            }

            if (name == "sniff")
            {
                float seconds = GetFloat(spec, "seconds", 1.0f);
                seconds = Mathf.Clamp(seconds, 0.05f, 10f);
                //TaskSpec context = new(observer.DisplayName);
                task = new Task_Sniff(null);    //TODO: Wants a HashSet<string>
                return true;
            }

            if (name == "emote")
            {
                string id = GetString(spec, "id", "look");
                task = new Task_Emote(id);
                return true;
            }

            if (name == "move_to_event_location")
            {
                float stopRadius = GetFloat(spec, "stopRadius", 0.08f);
                stopRadius = Mathf.Clamp(stopRadius, 0.05f, .08f);

                // Uses the event’s world position as destination
                task = new Task_MoveToLocation(e.WorldPos.x, e.WorldPos.z, stopRadius);
                return true;
            }

            // Combinators
            if (name == "sequence")
            {
                if (!TryGetTaskSpecArray(spec, "tasks", out var specs, out error))
                    return false;

                var tasks = new IAgentTask[specs.Length];
                for (int i = 0; i < specs.Length; i++)
                {
                    if (!TryBuildTask(specs[i], observer, e, out var built, out var err))
                    {
                        error = $"sequence[{i}] failed: {err}";
                        return false;
                    }
                    tasks[i] = built!;
                }

                task = new Task_Sequence(tasks);
                return true;
            }

            if (name == "try")
            {
                if (!TryGetTaskSpec(spec, "try", out var trySpec, out error)) return false;
                if (!TryGetTaskSpec(spec, "fail", out var failSpec, out error)) return false;

                if (!TryBuildTask(trySpec, observer, e, out var tryTask, out var err1))
                {
                    error = $"try.try failed: {err1}";
                    return false;
                }

                if (!TryBuildTask(failSpec, observer, e, out var failTask, out var err2))
                {
                    error = $"try.fail failed: {err2}";
                    return false;
                }

                task = new Task_Try(tryTask!, failTask!);
                return true;
            }

            if (name == "timeout")
            {
                float seconds = GetFloat(spec, "seconds", 2.0f);
                seconds = Mathf.Clamp(seconds, 0.05f, 30f);

                if (!TryGetTaskSpec(spec, "inner", out var innerSpec, out error))
                    return false;

                if (!TryBuildTask(innerSpec, observer, e, out var innerTask, out var innerErr))
                {
                    error = $"timeout.inner failed: {innerErr}";
                    return false;
                }

                task = new Task_Timeout(innerTask!, seconds);
                return true;
            }

            if (name == "random")
            {
                if (!TryGetTaskSpecArray(spec, "tasks", out var specs, out error))
                    return false;

                var tasks = new IAgentTask[specs.Length];
                for (int i = 0; i < specs.Length; i++)
                {
                    if (!TryBuildTask(specs[i], observer, e, out var built, out var err))
                    {
                        error = $"random[{i}] failed: {err}";
                        return false;
                    }
                    tasks[i] = built!;
                }

                task = new Task_Random(tasks);
                return true;
            }

            // ---------------- MOVEMENT TASKS ----------------

            if (name == "move_to_event")
            {
                float stopRadius = GetFloat(spec, "stopRadius", 0.08f);
                stopRadius = Mathf.Clamp(stopRadius, 0.05f, 0.08f);

                task = new Task_MoveToLocation(e.WorldPos.x, e.WorldPos.z, stopRadius);
                return true;
            }

            if (name == "move_to_location")
            {
                float x = GetFloat(spec, "x", observer.transform.position.x);
                float y = GetFloat(spec, "y", observer.transform.position.y);
                float z = GetFloat(spec, "z", observer.transform.position.z);
                float stopRadius = GetFloat(spec, "stopRadius", 0.08f);

                stopRadius = Mathf.Clamp(stopRadius, 0.05f, 0.08f);

                task = new Task_MoveToLocation(x, z, stopRadius);
                return true;
            }

            if (name == "move_to_target")
            {
                if (!e.Vision.HasValue)
                {
                    error = "move_to_target requires a Vision perception event.";
                    return false;
                }

                var target = e.Target;
                if (target == null)
                {
                    error = "Vision event has no target WorldObject.";
                    return false;
                }

                float stopRadius = GetFloat(spec, "stopRadius", 0.8f);
                stopRadius = Mathf.Clamp(stopRadius, 0.05f, 3.0f);

                task = new Task_MoveToObject(target, stopRadius);
                return true;
            }

            if (name == "random_nearby_move")
            {
                float radius = GetFloat(spec, "radius", 1.5f);
                radius = Mathf.Clamp(radius, 0.25f, 10f);

                task = new Task_RandomNearbyMove((int)radius);
                return true;
            }

            // ---------------- WALK MODE ----------------
            if (name == "set_walk_mode")
            {
                string mode = GetString(spec, "mode", "walk");
                task = new Task_SetWalkMode(ParseWalkMode(mode));
                return true;
            }

            // ---------------- FACE TARGET ----------------
            if (name == "face_target")
            {
                if (!e.Vision.HasValue || e.Target == null)
                {
                    error = "face_target requires a Vision event with a Target.";
                    return false;
                }

                float tol = GetFloat(spec, "toleranceDeg", 6f);
                float maxS = GetFloat(spec, "maxSeconds", 1.0f);
                tol = Mathf.Clamp(tol, 0.5f, 45f);
                maxS = Mathf.Clamp(maxS, 0.05f, 10f);

                task = new Task_FaceTarget(e.Target!, tol, maxS);
                return true;
            }

            // ---------------- MOVE UNTIL SEEN ----------------
            if (name == "move_until_seen")
            {
                if (!e.Vision.HasValue || e.Target == null)
                {
                    error = "move_until_seen requires a Vision event with a Target.";
                    return false;
                }

                float stopRadius = GetFloat(spec, "stopRadius", 1.0f);
                float maxSeconds = GetFloat(spec, "maxSeconds", 4.0f);
                float viewRadius = GetFloat(spec, "viewRadius", 12.0f);
                float fovDeg = GetFloat(spec, "fovDeg", 160.0f);
                bool requireFov = GetBool(spec, "requireFov", true);

                stopRadius = Mathf.Clamp(stopRadius, 0.05f, 5.0f);
                maxSeconds = Mathf.Clamp(maxSeconds, 0.05f, 30f);
                viewRadius = Mathf.Clamp(viewRadius, 0.5f, 100f);
                fovDeg = Mathf.Clamp(fovDeg, 10f, 360f);

                task = new Task_MoveUntilSeen(
                    target: e.Target!,
                    stopRadius: stopRadius,
                    maxSeconds: maxSeconds,
                    viewRadius: viewRadius,
                    fovDeg: fovDeg,
                    requireFov: requireFov
                );
                return true;
            }

            // ---------------- PUSH GOAL ----------------
            if (name == "push_goal")
            {
                string goalId = GetString(spec, "goalId", "");
                if (string.IsNullOrWhiteSpace(goalId))
                {
                    error = "push_goal requires goalId.";
                    return false;
                }

                bool overwrite = GetBool(spec, "overwrite", true);
                task = new Task_PushGoal(goalId.Trim(), overwrite);
                return true;
            }

            // ---------------- ITEM TASKS ----------------
            if (name == "take_item")
            {
                task = new Task_TakeItem();
                return true;
            }

            if (name == "drop_item")
            {
                task = new Task_DropItem();
                return true;
            }

            if (name == "bury_item")
            {
                float depth = GetFloat(spec, "depthMeters", 0.15f);
                //depth = UnityEngine.Mathf.Clamp(depth, 0.01f, 1.0f);

                task = new Task_BuryItem(depth);
                return true;
            }

            error = $"Unknown TaskSpec name '{spec.Name}'.";
            return false;
        }

        // ---------- Helpers ----------
        private static int GetInt(TaskSpec spec, string key, int defaultValue)
        {
            if (spec.Args.TryGetValue(key, out var obj))
            {
                if (obj is int i) return i;
                if (obj is float f) return Mathf.RoundToInt(f);
                if (obj is double d) return (int)Math.Round(d);
                if (obj is string s && int.TryParse(s, out int parsed)) return parsed;
            }
            return defaultValue;
        }

        private static float GetFloat(TaskSpec spec, string key, float defaultValue)
        {
            if (spec.Args.TryGetValue(key, out var obj))
            {
                if (obj is float f) return f;
                if (obj is double d) return (float)d;
                if (obj is int i) return i;
                if (obj is string s && float.TryParse(s, out float parsed)) return parsed;
            }
            return defaultValue;
        }

        private static string GetString(TaskSpec spec, string key, string defaultValue)
        {
            if (spec.Args.TryGetValue(key, out var obj))
            {
                if (obj is string s) return s;
            }
            return defaultValue;
        }

        private static bool GetBool(TaskSpec spec, string key, bool defaultValue)
        {
            if (spec.Args.TryGetValue(key, out var obj))
            {
                if (obj is bool b) return b;
                if (obj is int i) return i != 0;
                if (obj is float f) return Mathf.Abs(f) > 0.0001f;
                if (obj is string s && bool.TryParse(s, out bool parsed)) return parsed;
            }
            return defaultValue;
        }

        private static WalkMode ParseWalkMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return WalkMode.Walk;

            switch (mode.Trim().ToLowerInvariant())
            {
                case "none": return WalkMode.None;
                case "walk": return WalkMode.Walk;
                case "run": return WalkMode.Run;
                case "sneak": return WalkMode.Sneak;
                case "crawl": return WalkMode.Crawl;
                case "backpedal":
                case "backpeddal": // common misspelling
                    return WalkMode.Backpedal;
                case "strafe": return WalkMode.Strafe;
                default: return WalkMode.Walk;
            }
        }

        private static bool TryGetTaskSpec(TaskSpec spec, string key, out TaskSpec value, out string? error)
        {
            error = null;
            value = default;

            if (!spec.Args.TryGetValue(key, out var obj))
            {
                error = $"Missing '{key}' in TaskSpec '{spec.Name}'.";
                return false;
            }

            if (obj is TaskSpec ts)
            {
                value = ts;
                return true;
            }

            error = $"'{key}' in TaskSpec '{spec.Name}' must be a TaskSpec.";
            return false;
        }

        private static bool TryGetTaskSpecArray(TaskSpec spec, string key, out TaskSpec[] value, out string? error)
        {
            error = null;
            value = Array.Empty<TaskSpec>();

            if (!spec.Args.TryGetValue(key, out var obj))
            {
                error = $"Missing '{key}' in TaskSpec '{spec.Name}'.";
                return false;
            }

            if (obj is TaskSpec[] arr)
            {
                value = arr;
                return true;
            }

            error = $"'{key}' in TaskSpec '{spec.Name}' must be TaskSpec[].";
            return false;
        }

        private static bool TryResolveItem(
            TaskContext context,
            PerceptionEvent? evt,
            out WorldObject? item,
            out string? error)
        {
            error = null;
            item = null;

            // Prefer vision target if present
            if (evt.HasValue && evt.Value.Vision.HasValue && evt.Value.Target != null)
            {
                item = evt.Value.Target;
                return true;
            }

            // Otherwise look for blackboard selection
            if (context.Blackboard.TryGetInt("item.targetId", out int id))
            {
                if (WorldObjectRegistry.Instance && WorldObjectRegistry.Instance.TryGet(id, out var wo))
                {
                    item = wo;
                    return true;
                }
                error = $"Blackboard item.targetId={id} not found in registry.";
                return false;
            }

            error = "No item resolved (need Vision.Target or blackboard item.targetId).";
            return false;
        }

        private static bool TryResolveCarriedItem(
            TaskContext context,
            out WorldObject? item,
            out string? error)
        {
            error = null;
            item = null;

            if (!context.Blackboard.TryGetInt("item.carriedId", out int id) || id <= 0)
            {
                error = "No carried item (blackboard item.carriedId missing).";
                return false;
            }

            if (!WorldObjectRegistry.Instance || !WorldObjectRegistry.Instance.TryGet(id, out var wo) || wo == null)
            {
                error = $"Carried item id={id} missing from registry.";
                return false;
            }

            item = wo;
            return true;
        }
    }
}