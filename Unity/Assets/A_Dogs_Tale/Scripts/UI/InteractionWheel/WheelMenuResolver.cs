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
            MenuWheelPageCapacity pageCapacity)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (target == null) throw new ArgumentNullException(nameof(target));

            var context = new WheelContext(actor, target, worldPoint);

            // Step 1: collect raw options (currently just interactionModule, later more providers)
            List<WheelOption> raw = CollectRawOptions(context);

            // Step 2: normalize
            List<WheelOption> normalized = Normalize(raw);

            // Step 3: bundle into pages
            List<List<WheelOption>> pages = BuildPages(context, normalized, pageCapacity);

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

        private static List<List<WheelOption>> BuildPages(WheelContext context, List<WheelOption> normalized, MenuWheelPageCapacity pageCapacity)
        {
            int innerCapacity = Math.Max(1, pageCapacity.InnerRingCapacity);
            int outerCapacity = Math.Max(0, pageCapacity.OuterRingCapacity);

            if (normalized.Count == 0)
                return new List<List<WheelOption>> { new List<WheelOption>() };

            var explicitInner = new List<WheelOption>(normalized.Count);
            var explicitOuter = new List<WheelOption>(normalized.Count);
            var automatic = new List<WheelOption>(normalized.Count);

            for (int i = 0; i < normalized.Count; i++)
            {
                WheelOption option = normalized[i];
                option.navigationPageIndex = -1;
                option.resolvedRingPlacement = WheelOptionRingPlacement.Auto;

                switch (option.ringPlacement)
                {
                    case WheelOptionRingPlacement.Inner:
                        explicitInner.Add(option);
                        break;
                    case WheelOptionRingPlacement.Outer:
                        explicitOuter.Add(option);
                        break;
                    default:
                        automatic.Add(option);
                        break;
                }
            }

            int innerIndex = 0;
            int outerIndex = 0;
            int autoIndex = 0;
            var pages = new List<List<WheelOption>>();

            while (innerIndex < explicitInner.Count || outerIndex < explicitOuter.Count || autoIndex < automatic.Count)
            {
                bool hasBackLink = pages.Count > 0;
                int availableInner = innerCapacity;
                int availableOuter = outerCapacity;

                if (hasBackLink)
                {
                    if (availableOuter > 0)
                        availableOuter -= 1;
                    else
                        availableInner = Math.Max(0, availableInner - 1);
                }

                var pageInner = new List<WheelOption>(innerCapacity);
                var pageOuter = new List<WheelOption>(Math.Max(outerCapacity, 1));

                while (pageInner.Count < availableInner && innerIndex < explicitInner.Count)
                {
                    WheelOption option = explicitInner[innerIndex++];
                    option.resolvedRingPlacement = WheelOptionRingPlacement.Inner;
                    pageInner.Add(option);
                }

                while (pageOuter.Count < availableOuter && outerIndex < explicitOuter.Count)
                {
                    WheelOption option = explicitOuter[outerIndex++];
                    option.resolvedRingPlacement = WheelOptionRingPlacement.Outer;
                    pageOuter.Add(option);
                }

                while (pageInner.Count < availableInner && autoIndex < automatic.Count)
                {
                    WheelOption option = automatic[autoIndex++];
                    option.resolvedRingPlacement = WheelOptionRingPlacement.Inner;
                    pageInner.Add(option);
                }

                while (pageOuter.Count < availableOuter && autoIndex < automatic.Count)
                {
                    WheelOption option = automatic[autoIndex++];
                    option.resolvedRingPlacement = outerCapacity > 0
                        ? WheelOptionRingPlacement.Outer
                        : WheelOptionRingPlacement.Inner;

                    if (outerCapacity > 0)
                        pageOuter.Add(option);
                    else
                        pageInner.Add(option);
                }

                bool hasMoreLink =
                    innerIndex < explicitInner.Count ||
                    outerIndex < explicitOuter.Count ||
                    autoIndex < automatic.Count;

                if (hasMoreLink)
                {
                    if (outerCapacity > 0)
                    {
                        int outerContentBudget = outerCapacity - (hasBackLink ? 1 : 0);
                        if (pageOuter.Count >= outerContentBudget)
                            PushLastOptionBack(pageOuter, ref outerIndex, ref autoIndex);

                        pageOuter.Add(CreateMoreOption(context, pages.Count + 1, WheelOptionRingPlacement.Outer));
                    }
                    else
                    {
                        int innerContentBudget = innerCapacity - (hasBackLink ? 1 : 0);
                        if (pageInner.Count >= innerContentBudget)
                            PushLastOptionBack(pageInner, ref innerIndex, ref autoIndex);

                        pageInner.Add(CreateMoreOption(context, pages.Count + 1, WheelOptionRingPlacement.Inner));
                    }
                }

                if (hasBackLink)
                {
                    if (outerCapacity > 0)
                        pageOuter.Insert(0, CreateBackOption(context, pages.Count - 1, WheelOptionRingPlacement.Outer));
                    else
                        pageInner.Insert(0, CreateBackOption(context, pages.Count - 1, WheelOptionRingPlacement.Inner));
                }

                var page = new List<WheelOption>(pageInner.Count + pageOuter.Count);
                page.AddRange(pageInner);
                page.AddRange(pageOuter);
                pages.Add(page);
            }

            return pages;
        }

        private static void PushLastOptionBack(
            List<WheelOption> pageOptions,
            ref int explicitIndex,
            ref int autoIndex)
        {
            if (pageOptions.Count == 0)
                return;

            WheelOption option = pageOptions[pageOptions.Count - 1];
            pageOptions.RemoveAt(pageOptions.Count - 1);

            if (option.ringPlacement == WheelOptionRingPlacement.Auto)
            {
                autoIndex = Math.Max(0, autoIndex - 1);
                option.resolvedRingPlacement = WheelOptionRingPlacement.Auto;
                return;
            }

            explicitIndex = Math.Max(0, explicitIndex - 1);
            option.resolvedRingPlacement = WheelOptionRingPlacement.Auto;
        }

        private static WheelOption CreateMoreOption(WheelContext context, int targetPageIndex, WheelOptionRingPlacement ringPlacement)
        {
            return new WheelOption
            {
                id = MoreOptionId,
                label = "More…",
                hint = "Show additional options",
                disabledHint = "",
                isVisible = true,
                isEnabled = true,
                sortPriority = int.MinValue,
                ringPlacement = ringPlacement,
                resolvedRingPlacement = ringPlacement,
                navigationPageIndex = targetPageIndex,
                callback = null
            };
        }

        private static WheelOption CreateBackOption(WheelContext context, int targetPageIndex, WheelOptionRingPlacement ringPlacement)
        {
            return new WheelOption
            {
                id = BackOptionId,
                label = "Back",
                hint = "Return to previous options",
                disabledHint = "",
                isVisible = true,
                isEnabled = true,
                sortPriority = int.MaxValue,
                ringPlacement = ringPlacement,
                resolvedRingPlacement = ringPlacement,
                navigationPageIndex = targetPageIndex,
                callback = null
            };
        }
    }
}
