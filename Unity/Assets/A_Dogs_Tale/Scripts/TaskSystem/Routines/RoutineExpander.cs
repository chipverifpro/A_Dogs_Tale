#nullable enable
using System;
using DogGame.Reactions;

namespace DogGame.Routines
{
    /// <summary>
    /// Replaces TaskSpec nodes named "routine_call" by inlining the referenced routine body.
    /// v1: args substitution is minimal; we just inline.
    /// </summary>
    public static class RoutineExpander
    {
        public static bool TryExpand(
            TaskSpec root,
            RoutineLibrary lib,
            int maxDepth,
            int maxNodes,
            out TaskSpec expanded,
            out string? error)
        {
            error = null;
            int nodeCount = 0;

            bool ExpandRec(TaskSpec spec, int depth, out TaskSpec outSpec, out string? err)
            {
                err = null;
                outSpec = spec;

                nodeCount++;
                if (nodeCount > maxNodes)
                {
                    err = $"Routine expansion exceeded maxNodes={maxNodes}.";
                    return false;
                }

                if (depth > maxDepth)
                {
                    err = $"Routine expansion exceeded maxDepth={maxDepth}.";
                    return false;
                }

                // Inline routine_call
                if (string.Equals(spec.Name, "routine_call", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetString(spec, "id", out var id, out err))
                        return false;

                    if (!lib.TryGet(id, out var def))
                    {
                        err = $"routine_call references unknown routine '{id}'.";
                        return false;
                    }

                    // Inline the referenced body (and expand it too)
                    return ExpandRec(def.Body, depth + 1, out outSpec, out err);
                }

                // Expand combinators that contain child TaskSpecs
                if (string.Equals(spec.Name, "sequence", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetTaskSpecArray(spec, "tasks", out var tasks, out err))
                        return false;

                    var newTasks = new TaskSpec[tasks.Length];
                    for (int i = 0; i < tasks.Length; i++)
                    {
                        if (!ExpandRec(tasks[i], depth + 1, out newTasks[i], out err))
                            return false;
                    }

                    outSpec = TS.Sequence(newTasks);
                    return true;
                }

                if (string.Equals(spec.Name, "try", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetTaskSpec(spec, "try", out var a, out err)) return false;
                    if (!TryGetTaskSpec(spec, "fail", out var b, out err)) return false;

                    if (!ExpandRec(a, depth + 1, out var a2, out err)) return false;
                    if (!ExpandRec(b, depth + 1, out var b2, out err)) return false;

                    outSpec = TS.Try(a2, b2);
                    return true;
                }

                if (string.Equals(spec.Name, "timeout", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetTaskSpec(spec, "inner", out var inner, out err)) return false;
                    if (!ExpandRec(inner, depth + 1, out var inner2, out err)) return false;

                    float seconds = 2.0f;
                    if (spec.Args.TryGetValue("seconds", out var obj) && obj is float f) seconds = f;

                    outSpec = TS.Timeout(seconds, inner2);
                    return true;
                }

                // Leaf tasks: no change
                return true;
            }

            if (!ExpandRec(root, depth: 0, out expanded, out error))
                return false;

            return true;
        }

        // --- minimal arg helpers (TaskSpec uses object bag) ---
        private static bool TryGetString(TaskSpec spec, string key, out string value, out string? error)
        {
            error = null;
            value = "";

            if (!spec.Args.TryGetValue(key, out var obj))
            {
                error = $"TaskSpec '{spec.Name}' missing '{key}'.";
                return false;
            }

            if (obj is string s && !string.IsNullOrWhiteSpace(s))
            {
                value = s;
                return true;
            }

            error = $"TaskSpec '{spec.Name}' arg '{key}' must be a non-empty string.";
            return false;
        }

        private static bool TryGetTaskSpec(TaskSpec spec, string key, out TaskSpec value, out string? error)
        {
            error = null;
            value = default;

            if (!spec.Args.TryGetValue(key, out var obj) || obj is not TaskSpec ts)
            {
                error = $"TaskSpec '{spec.Name}' arg '{key}' must be a TaskSpec.";
                return false;
            }

            value = ts;
            return true;
        }

        private static bool TryGetTaskSpecArray(TaskSpec spec, string key, out TaskSpec[] value, out string? error)
        {
            error = null;
            value = Array.Empty<TaskSpec>();

            if (!spec.Args.TryGetValue(key, out var obj) || obj is not TaskSpec[] arr)
            {
                error = $"TaskSpec '{spec.Name}' arg '{key}' must be TaskSpec[].";
                return false;
            }

            value = arr;
            return true;
        }
    }
}