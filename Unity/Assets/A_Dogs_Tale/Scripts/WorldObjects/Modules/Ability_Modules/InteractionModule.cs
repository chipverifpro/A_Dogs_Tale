#nullable enable
using System;
using System.Collections.Generic;
using DogGame.Modules;
using UnityEngine;

namespace DogGame.UI.InteractionWheel
{
    /// <summary>
    /// Per-object interaction definitions (Inspector-populated) that can be turned into wheel options.
    /// Lives on the TARGET WorldObject as: target.interactionModule (may be null).
    /// </summary>
    public sealed class InteractionModule : WorldModule, IWheelOptionProvider
    {
        [Serializable]
        public sealed class Entry
        {
            [Header("Identity")]
            public string id = "";                 // Stable option id; used for de-dup later
            public string actionKey = "";          // Key used to bind a callback at runtime
            public int sortPriority = 0;

            [Header("Display")]
            public string label = "";
            [TextArea] public string hint = "";
            [TextArea] public string disabledHint = "";
            public Sprite? icon;

            [Header("State")]
            public bool isVisible = true;
            public bool isEnabled = true;
        }

        [Header("Inspector-defined interactions for this WorldObject")]
        [SerializeField] private List<Entry> entries = new();

        // Runtime binding: actionKey -> callback
        private readonly Dictionary<string, Action<WheelContext>> boundActions = new();

        public void BindAction(string actionKey, Action<WheelContext> callback)
        {
            if (string.IsNullOrWhiteSpace(actionKey))
            {
                Debug.LogWarning("[InteractionModule] BindAction called with empty actionKey.", this);
                return;
            }

            if (callback == null)
            {
                Debug.LogWarning($"[InteractionModule] BindAction '{actionKey}' callback is null.", this);
                return;
            }

            boundActions[actionKey] = callback;
        }

        public void UnbindAction(string actionKey)
        {
            if (string.IsNullOrWhiteSpace(actionKey))
                return;

            boundActions.Remove(actionKey);
        }

        public void BuildWheelOptions(WheelContext context, List<WheelOption> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                Entry entry = entries[entryIndex];
                if (entry == null) continue;
                if (!entry.isVisible) continue;

                Action<WheelContext>? resolvedCallback = ResolveCallbackOrNull(entry);

                // If it's enabled but callback missing, we have two choices:
                // A) disable it with a wiring hint
                // B) keep enabled but log when clicked
                //
                // I recommend A so you catch setup issues immediately during testing.
                bool isActuallyEnabled = entry.isEnabled && resolvedCallback != null;

                var option = new WheelOption
                {
                    id = entry.id,
                    sortPriority = entry.sortPriority,
                    label = entry.label,
                    hint = entry.hint,
                    disabledHint = isActuallyEnabled
                        ? entry.disabledHint
                        : (string.IsNullOrWhiteSpace(entry.disabledHint)
                            ? $"'{entry.actionKey}' not bound."
                            : entry.disabledHint),
                    icon = entry.icon,
                    isVisible = true,
                    isEnabled = isActuallyEnabled,
                    callback = resolvedCallback
                };

                results.Add(option);
            }
        }

        private Action<WheelContext>? ResolveCallbackOrNull(Entry entry)
        {
            if (!entry.isEnabled) return null;
            string bindingKey = !string.IsNullOrWhiteSpace(entry.actionKey)
                ? entry.actionKey
                : entry.id;

            if (string.IsNullOrWhiteSpace(bindingKey)) return null;

            if (boundActions.TryGetValue(bindingKey, out var callback))
                return callback;

            return null;
        }

        protected override void Awake()
        {
            base.Awake();
            BindAction("Quest.Get", ctx => Debug.Log("Quest.Get fired for " + ctx.target.name));
            BindAction("Follow_nose", ctx => Debug.Log("Follow_nose fired for " + ctx.target.name));
            BindAction("sound_bark", ctx => Debug.Log("sound_bark fired for " + ctx.target.name));
            BindAction("scent_sniff", ctx => Debug.Log("scent_sniff fired for " + ctx.target.name));
            BindAction("Dig.Up", ctx => Debug.Log("Dig.Up fired for " + ctx.target.name));
            BindAction("Dig.Bury", ctx => Debug.Log("Dig.Bury fired for " + ctx.target.name));
            BindAction("Dig.Hole", ctx => Debug.Log("Dig.Hole fired for " + ctx.target.name));
            BindAction("Item.Drop", ctx => Debug.Log("Item.Drop fired for " + ctx.target.name));
        
        }

/*
        public void Start()
        {
            // Debug 1
            var options = WheelOptionCollector.CollectFromTarget(Dir.Instance.playerPack.packLeader, worldObject);
            Debug.Log($"Start InteractionModule {worldObject.DisplayName}: {options.Count} options:");
            foreach (var opt in options) Debug.Log(opt.ToString());

            // Debug 2
            int maxPrimaryOptions = 8; // parameter later
            var menuModel = WheelMenuResolver.CreateWheelMenu(Dir.Instance.playerPack.packLeader, worldObject, worldPoint: null, maxPrimaryOptions);
            Debug.Log($"Wheel menu for target={worldObject.DisplayName}: pages={menuModel.pages.Count}");
            for (int p = 0; p < menuModel.pages.Count; p++)
            {
                Debug.Log($"  Page {p}:");
                foreach (var opt in menuModel.pages[p])
                    Debug.Log($"    - {opt}");
            } 



            // Debug 3
            WheelMenuModel model = WheelMenuResolver.CreateWheelMenu(
                actor: Dir.Instance.playerPack.packLeader,
                target: worldObject,
                worldPoint: null,
                maxPrimaryOptions: 8
            );
            MenuWheelUIController menuWheelUIController = FindFirstObjectByType<MenuWheelUIController>(UnityEngine.FindObjectsInactive.Include);
            menuWheelUIController.OpenMenuWheel(model, overrideTimeScale: 0f);
        }
    */
    }
}
