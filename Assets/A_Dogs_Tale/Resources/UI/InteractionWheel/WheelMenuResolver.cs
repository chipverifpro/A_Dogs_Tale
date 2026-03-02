#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.UI.InteractionWheel
{
    public static class WheelMenuResolver
    {
        // Reserved IDs so we don't collide with user-defined options.
        public const string MoreOptionId = "__more";
        public const string BackOptionId = "__back";

        /// <summary>
        /// Build final menu pages for the given actor/target.
        /// - Collect from target (Step 1)
        /// - Filter + de-dup + sort
        /// - Apply "More..." bundling if option count exceeds maxPrimaryOptions
        /// </summary>
        public static WheelMenuModel CreateWheelMenu(
            WorldObject actor,
            WorldObject target,
            Vector3? worldPoint,
            int maxPrimaryOptions)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (target == null) throw new ArgumentNullException(nameof(target));

            if (maxPrimaryOptions < 3)
            {
                // Needs room for at least 1 real option plus "More" (rare) plus comfortable spacing.
                maxPrimaryOptions = 3;
            }

            var context = new WheelContext(actor, target, worldPoint);

            // Step 1: collect raw options (currently just interactionModule, later more providers)
            List<WheelOption> raw = CollectRawOptions(context);

            // Step 2: normalize
            List<WheelOption> normalized = Normalize(raw);

            // Step 3: bundle into pages
            List<List<WheelOption>> pages = BuildPages(context, normalized, maxPrimaryOptions);

            return new WheelMenuModel(context, pages);
        }

        private static List<WheelOption> CollectRawOptions(WheelContext context)
        {
            var list = new List<WheelOption>(capacity: 12);

            // Only interactionModule for now (your architecture decision).
            InteractionModule? interactionModule = context.target.interactionModule;
            if (interactionModule != null)
                interactionModule.BuildWheelOptions(context, list);

            return list;
        }

        private static List<WheelOption> Normalize(List<WheelOption> raw)
        {
            // Filter invisible, blank IDs, de-dup by ID, then sort.
            var deduped = new Dictionary<string, WheelOption>(StringComparer.Ordinal);

            for (int i = 0; i < raw.Count; i++)
            {
                WheelOption option = raw[i];
                if (option == null) continue;

                if (!option.isVisible) continue;

                if (string.IsNullOrWhiteSpace(option.id))
                {
                    // Ignore invalid entries quietly; you can change to warning if preferred.
                    continue;
                }

                // Protect reserved IDs.
                if (option.id == MoreOptionId || option.id == BackOptionId)
                {
                    Debug.LogWarning($"[WheelMenuResolver] Option id '{option.id}' is reserved and will be ignored.");
                    continue;
                }

                if (!deduped.TryGetValue(option.id, out var existing))
                {
                    deduped[option.id] = option;
                    continue;
                }

                // If duplicates exist, keep the "better" one:
                // 1) enabled beats disabled
                // 2) higher priority wins
                // 3) otherwise keep the first
                bool optionBetter =
                    (option.isEnabled && !existing.isEnabled) ||
                    (option.isEnabled == existing.isEnabled && option.sortPriority > existing.sortPriority);

                if (optionBetter)
                    deduped[option.id] = option;
            }

            var normalized = new List<WheelOption>(deduped.Count);
            foreach (var kvp in deduped)
                normalized.Add(kvp.Value);

            // Sort: priority desc, then label asc, then id asc
            normalized.Sort(CompareOptions);

            return normalized;
        }

        private static int CompareOptions(WheelOption a, WheelOption b)
        {
            // Higher priority first
            int priorityCompare = b.sortPriority.CompareTo(a.sortPriority);
            if (priorityCompare != 0) return priorityCompare;

            // Then label
            int labelCompare = string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase);
            if (labelCompare != 0) return labelCompare;

            // Then id
            return string.Compare(a.id, b.id, StringComparison.Ordinal);
        }

        private static List<List<WheelOption>> BuildPages(WheelContext context, List<WheelOption> normalized, int maxPrimaryOptions)
        {
            // If fits, one page.
            if (normalized.Count <= maxPrimaryOptions)
                return new List<List<WheelOption>> { normalized };

            // We need "More..." on page 0. That consumes one slot.
            int page0RealSlots = Math.Max(1, maxPrimaryOptions - 1);

            var page0 = new List<WheelOption>(capacity: maxPrimaryOptions);
            for (int i = 0; i < page0RealSlots && i < normalized.Count; i++)
                page0.Add(normalized[i]);

            var overflow = new List<WheelOption>(capacity: Math.Max(0, normalized.Count - page0.Count));
            for (int i = page0.Count; i < normalized.Count; i++)
                overflow.Add(normalized[i]);

            // Add "More..." which switches to page 1.
            var more = CreateMoreOption(context);
            page0.Add(more);

            // Page 1 includes "Back" + overflow (and if overflow is still huge, we could paginate again later).
            var page1 = new List<WheelOption>(capacity: overflow.Count + 1)
            {
                CreateBackOption(context)
            };
            page1.AddRange(overflow);

            return new List<List<WheelOption>> { page0, page1 };
        }

        private static WheelOption CreateMoreOption(WheelContext context)
        {
            return new WheelOption
            {
                id = MoreOptionId,
                label = "More…",
                hint = "Show additional options",
                disabledHint = "",
                isVisible = true,
                isEnabled = true,
                sortPriority = int.MinValue, // keep it at the end after sorting; we add it manually anyway
                callback = null // UI handles page switching; callback not used
            };
        }

        private static WheelOption CreateBackOption(WheelContext context)
        {
            return new WheelOption
            {
                id = BackOptionId,
                label = "Back",
                hint = "Return to main options",
                disabledHint = "",
                isVisible = true,
                isEnabled = true,
                sortPriority = int.MaxValue, // want it first on page 1 (we insert it first anyway)
                callback = null // UI handles page switching
            };
        }
    }
}