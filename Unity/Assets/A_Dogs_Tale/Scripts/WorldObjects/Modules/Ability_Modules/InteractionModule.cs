#nullable enable
using System;
using System.Collections.Generic;
using DogGame.Modules;
using Unity.InferenceEngine;
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

        public enum EnableState { disabled, enabled, unavailable };
        public enum ActionCategory { Hearing,
                                     Knowledge,
                                     ScentPerception,
                                     VisionPerception,
                                     Motion,
                                     Appearance,
                                     NoiseMaker,
                                     ScentEmitter,
                                     AgentState,
                                     TaskList,
                                     WorldState,
                                   };
        
        public bool RegisterAction( string actionName,
                                    string actionCategory, // for sorting
                                    //function callback,
                                    EnableState enable = EnableState.enabled
                                    )
        {
            return true;
        }

        private bool TryGetTargetAgent(WheelContext ctx, out WorldObject target)
        {
            if (ctx==null)
            {
                target=new();
                Debug.LogError("[InteractionModule] Mode action fired without a context.", this);
                return false;
            }
            target = ctx.target;
            if (target == null)
            {
                target = new();
                Debug.LogWarning("[InteractionModule] Mode action fired without a target.", this);
                return false;
            }

            if (target.agentModule == null)
                target.CreateModulesIfNeeded(ModuleFlagsTemplates.FullAgent);

            if (target.agentModule == null)
            {
                Debug.LogWarning($"[InteractionModule] {target.name} has no AgentModule; cannot change mode.", this);
                return false;
            }

            return true;
        }

        private void SetTargetMode(WheelContext ctx, AgentDecisionType decisionType)
        {
            if (!TryGetTargetAgent(ctx, out WorldObject target))
                return;

            target.agentModule.SwitchDecisionModule(decisionType);
            Debug.Log($"[InteractionModule] Set {target.DisplayName} to mode {decisionType}.");
        }

        private Vector3 GetInteractionWorldPoint(WheelContext ctx, WorldObject target)
        {
            if (ctx != null && ctx.worldPoint.HasValue)
                return ctx.worldPoint.Value;

            if (target.locationModule != null)
                return target.locationModule.pos3d_world;

            return target.transform.position;
        }

        private void SetTargetWalkMode(WheelContext ctx, WalkMode walkMode)
        {
            if (!TryGetTargetAgent(ctx, out WorldObject target))
                return;

            if (target.agentMovementModule == null || target.motionModule == null)
                target.CreateModulesIfNeeded(ModuleFlags.agentMovementModule | ModuleFlags.motionModule);

            if (target.agentMovementModule == null || target.motionModule == null)
            {
                Debug.LogWarning($"[InteractionModule] {target.DisplayName} cannot change walk mode to {walkMode}; missing movement modules.", this);
                return;
            }

            target.agentMovementModule.SetWalkMode(walkMode);
            Debug.Log($"[InteractionModule] Set {target.DisplayName} walk mode to {walkMode}.");
        }

        private void HandlePackJoin(WheelContext ctx)
        {
            if (!TryGetTargetAgent(ctx, out WorldObject target))
                return;

            WorldObject instigator = ctx?.actor != null ? ctx.actor : target;
            GameMode gameMode = GameInputRouter.Instance != null ? GameInputRouter.Instance.currentGameMode : GameMode.Explore;
            Vector3 hitPoint = GetInteractionWorldPoint(ctx!, target);

            var activateContext = new ActivateContext(
                userIsInstigator: false,
                instigator: instigator,
                target: target,
                gameMode: gameMode,
                hitPoint: hitPoint,
                promoteTarget: true);

            ActivateResult result = target.Activate(activateContext, new ActivateRequest(ActivateKind.RequestToJoinPack));
            if (!string.IsNullOrWhiteSpace(result.message))
                Debug.Log($"[InteractionModule] Pack.Join on {target.DisplayName}: {result.kind} ({result.message})");
            else
                Debug.Log($"[InteractionModule] Pack.Join on {target.DisplayName}: {result.kind}");
        }

        private void HandlePackLeave(WheelContext ctx)
        {
            if (!TryGetTargetAgent(ctx, out WorldObject target))
                return;

            if (target.packMemberModule == null)
            {
                Debug.LogWarning($"[InteractionModule] {target.DisplayName} has no PackMemberModule; cannot leave pack.", this);
                return;
            }

            Pack previousPack = target.packMemberModule.currentPack;
            bool leftPack = target.packMemberModule.LeaveCurrentPack();
            if (!leftPack)
            {
                string packName = previousPack != null ? previousPack.packName : "(none)";
                Debug.Log($"[InteractionModule] Pack.Leave on {target.DisplayName} failed for {packName}.");
                return;
            }

            target.agentModule.SwitchDecisionModule(AgentDecisionType.Wanderer);
            string oldPackName = previousPack != null ? previousPack.packName : "(none)";
            Debug.Log($"[InteractionModule] {target.DisplayName} left pack {oldPackName} and is now wandering.");
        }

        private void HandlePackLead(WheelContext ctx)
        {
            if (!TryGetTargetAgent(ctx, out WorldObject target))
                return;

            if (target.packMemberModule == null || target.packMemberModule.currentPack == null)
            {
                Debug.LogWarning($"[InteractionModule] {target.DisplayName} is not in a pack; cannot become leader.", this);
                return;
            }

            target.packMemberModule.RequestBecomeLeader();
            Debug.Log($"[InteractionModule] {target.DisplayName} is now leading pack {target.packMemberModule.currentPack.packName}.");
        }

        private void SetTargetPlayerMode(WheelContext ctx)
        {
            if (!TryGetTargetAgent(ctx, out WorldObject target))
                return;

            if (target==null || target.packMemberModule==null)
                return;

            Pack? targetPack = target.packMemberModule != null ? target.packMemberModule.currentPack : null;
            WorldObject[] worldObjects = FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int index = 0; index < worldObjects.Length; index++)
            {
                WorldObject candidate = worldObjects[index];
                if (candidate == null || candidate == target || candidate.agentModule == null)
                    continue;

                AgentDecisionModuleBase currentDecisionModule = candidate.agentModule.currentDecisionModule;
                if (currentDecisionModule == null || currentDecisionModule.DecisionType != AgentDecisionType.Player)
                    continue;

                Pack? candidatePack = candidate.packMemberModule != null ? candidate.packMemberModule.currentPack : null;
                AgentDecisionType fallbackMode =
                    targetPack != null && candidatePack == targetPack
                        ? AgentDecisionType.Follower
                        : AgentDecisionType.Wanderer;

                candidate.agentModule.SwitchDecisionModule(fallbackMode);
                Debug.Log($"[InteractionModule] Set previous player {candidate.DisplayName} to mode {fallbackMode}.");
            }

            target.agentModule.SwitchDecisionModule(AgentDecisionType.Player);
            Debug.Log($"[InteractionModule] Set {target.DisplayName} to mode Player.");
        }

        protected override void Awake()
        {
            base.Awake();
            // Quests
            BindAction("Quest.Get", ctx => Debug.Log("Quest.Get fired for " + ctx.target.name));
            // Sound
            BindAction("Sound.Bark", ctx => Debug.Log("Sound.Bark fired for " + ctx.target.name));
            // ScentPerception
            BindAction("Scent.Sniff", ctx => Debug.Log("Scent.Sniff fired for " + ctx.target.name));
            BindAction("Scent.Deposit", ctx => Debug.Log("Scent.Deposit fired for " + ctx.target.name));
            BindAction("Scent.Follow", ctx => Debug.Log("Scent.Follow fired for " + ctx.target.name));
            // Dig
            BindAction("Dig.Up", ctx => Debug.Log("Dig.Up fired for " + ctx.target.name));
            BindAction("Dig.Bury", ctx => Debug.Log("Dig.Bury fired for " + ctx.target.name));
            BindAction("Dig.Hole", ctx => Debug.Log("Dig.Hole fired for " + ctx.target.name));
            // Inventory
            BindAction("Item.Drop", ctx => Debug.Log("Item.Drop fired for " + ctx.target.name));
            BindAction("Item.Get", ctx => Debug.Log("Item.Get fired for " + ctx.target.name));
            // Movement
            BindAction("Move.Sneak", ctx => SetTargetWalkMode(ctx, WalkMode.Sneak));
            BindAction("Move.Run", ctx => SetTargetWalkMode(ctx, WalkMode.Run));
            BindAction("Move.Walk", ctx => SetTargetWalkMode(ctx, WalkMode.Walk));

            // Pack Member
            BindAction("Pack.Leave", HandlePackLeave);
            BindAction("Pack.Join", HandlePackJoin);
            BindAction("Pack.Lead", HandlePackLead);
            BindAction("Pack.Formation", ctx => Debug.Log("Pack.Formation fired for " + ctx.target.name));
            
            // Mode
            BindAction("Mode.Explore", ctx => SetTargetMode(ctx, AgentDecisionType.Explorer));
            BindAction("Mode.Wander", ctx => SetTargetMode(ctx, AgentDecisionType.Wanderer));
            BindAction("Mode.Player", SetTargetPlayerMode);
            BindAction("Mode.Follow", ctx => SetTargetMode(ctx, AgentDecisionType.Follower));
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
