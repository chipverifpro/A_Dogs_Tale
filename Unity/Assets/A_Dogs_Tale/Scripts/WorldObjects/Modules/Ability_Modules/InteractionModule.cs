#nullable enable
using System;
using System.Collections.Generic;
using DogGame.LLM;
using DogGame.Modules;
using DogGame.Tasks;
using DogGame.World;
using Unity.InferenceEngine;
using UnityEngine;
using InspectorTools;

namespace DogGame.UI.InteractionWheel
{
    /// <summary>
    /// Per-object interaction definitions (Inspector-populated) that can be turned into wheel options.
    /// Lives on the TARGET WorldObject as: target.interactionModule (may be null).
    /// </summary>
    [InspectorNote("Ability_Modules/Interaction Module", "Custom buttons for Interaction Wheel Menu.")]
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

        [Header("Button Group Enables")]
        bool includeModeButtons = true;
        bool includePackButtons = true;
        bool includeMoveButtons = true;
        bool includeInventoryButtons = true;
        bool includeDigButtons = true;
        bool includeScentButtons = true;
        bool includeSoundButtons = true;
        bool includeDoorButtons = true;

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

            BuildDefaultEntriesIfNeeded();
            UpdateEntryStates(context);

            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                Entry entry = entries[entryIndex];
                if (entry == null) continue;
                if (!entry.isVisible) continue;

                Action<WheelContext>? resolvedCallback = ResolveCallbackOrNull(entry);

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
        public enum ActionCategory
        {
            Hearing,
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

        public bool RegisterAction(
            string actionName,
            string actionCategory,
            EnableState enable = EnableState.enabled
        )
        {
            return true;
        }

        private bool TryGetTargetAgent(WheelContext ctx, out WorldObject target)
        {
            if (ctx == null)
            {
                target = new();
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

        private bool TryGetTargetWorldObject(WheelContext? ctx, out WorldObject target)
        {
            if (ctx == null)
            {
                target = new();
                Debug.LogError("[InteractionModule] Action fired without a context.", this);
                return false;
            }

            target = ctx.target;
            if (target == null)
            {
                target = new();
                Debug.LogWarning("[InteractionModule] Action fired without a target.", this);
                return false;
            }

            return true;
        }

        private bool TryGetDoorModule(WheelContext? ctx, out WorldObject target, out DoorModule? doorModule)
        {
            doorModule = null;

            if (!TryGetTargetWorldObject(ctx, out target))
                return false;

            doorModule = target.GetComponent<DoorModule>();
            if (doorModule == null)
            {
                Debug.LogWarning($"[InteractionModule] {target.DisplayName} has no DoorModule.", this);
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

            if (target == null || target.packMemberModule == null)
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

        private void HandleDoorOpen(WheelContext ctx)
        {
            if (!TryGetDoorModule(ctx, out WorldObject target, out DoorModule? doorModule) || doorModule == null)
                return;

            bool opened = doorModule.OpenDoor();
            Debug.Log(opened
                ? $"[InteractionModule] Opened door on {target.DisplayName}."
                : $"[InteractionModule] Could not open door on {target.DisplayName}.");
        }

        private void HandleDoorClose(WheelContext ctx)
        {
            if (!TryGetDoorModule(ctx, out WorldObject target, out DoorModule? doorModule) || doorModule == null)
                return;

            bool closed = doorModule.CloseDoor();
            Debug.Log(closed
                ? $"[InteractionModule] Closed door on {target.DisplayName}."
                : $"[InteractionModule] Could not close door on {target.DisplayName}.");
        }

        private void HandleBark(WheelContext ctx)
        {
            if (ctx == null || ctx.actor == null || ctx.target == null)
            {
                Debug.LogWarning("[InteractionModule] Scent.Follow fired without a valid actor/target.", this);
                return;
            }

            WorldObject actor = ctx.actor;

            if (actor.taskController == null)
            {
                Debug.LogWarning($"[InteractionModule] {actor.DisplayName} has no TaskController; cannot bark.", this);
                return;
            }

            actor.taskController.EnqueueTask(
                task: new Task_Bark(volume:5),
                priority: 80,
                source: TaskSource.Player,
                applyMode: LLMApplyMode.Interrupt,
                tag: $"interaction_bark",
                front: true);

            Debug.Log($"[InteractionModule] {actor.DisplayName} enqueued bark.");

        }
        private void HandleScentFollow(WheelContext ctx)
        {
            if (ctx == null || ctx.actor == null || ctx.target == null)
            {
                Debug.LogWarning("[InteractionModule] Scent.Follow fired without a valid actor/target.", this);
                return;
            }

            WorldObject actor = ctx.actor;
            WorldObject target = ctx.target;

            if (actor.taskController == null)
            {
                Debug.LogWarning($"[InteractionModule] {actor.DisplayName} has no TaskController; cannot follow scent.", this);
                return;
            }

            string scentKey = $"agent:{target.ObjectId}";
            actor.taskController.EnqueueTask(
                task: new Task_ScentFollowLua(scentKey: scentKey, medium: ScentMedium.Ground),
                priority: 80,
                source: TaskSource.Player,
                applyMode: LLMApplyMode.Interrupt,
                tag: $"interaction_scent_follow:{scentKey}",
                front: true);

            Debug.Log($"[InteractionModule] {actor.DisplayName} following scent '{scentKey}' from target {target.DisplayName}.");
        }

        private bool defaultEntriesBuilt = false;

        private void BuildDefaultEntriesIfNeeded()
        {
            if (defaultEntriesBuilt)
                return;

            defaultEntriesBuilt = true;

            bool HasEntry(string id)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i] != null && entries[i].id == id)
                        return true;
                }
                return false;
            }

            void Add(string id, string actionKey, string label, int sortPriority)
            {
                if (HasEntry(id))
                    return;

                entries.Add(new Entry
                {
                    id = id,
                    actionKey = actionKey,
                    label = label,
                    sortPriority = sortPriority,
                    isVisible = true,
                    isEnabled = true
                });
            }

            int basePriority = 10;

            // ===== Movement =====
            if (includeMoveButtons)
            {
                Add("Move.Sneak", "Move.Sneak", "Sneak", basePriority);
                Add("Move.Run",   "Move.Run",   "Run",   basePriority);
                basePriority += 10;
            }

            // ===== Pack =====
            if (includePackButtons)
            {
                Add("Pack.Join",  "Pack.Join",  "Join Pack",  basePriority);
                Add("Pack.Leave", "Pack.Leave", "Leave Pack", basePriority);
                Add("Pack.Lead",  "Pack.Lead",  "Lead Pack",  basePriority);
                basePriority += 10;
            }

            // ===== Inventory =====
            if (includeInventoryButtons)
            {
                Add("Item.Get",  "Item.Get",  "Pick Up", basePriority);
                Add("Item.Drop", "Item.Drop", "Drop",    basePriority);
                basePriority += 10;
            }

            // ===== Dig =====
            if (includeDigButtons)
            {
                Add("Dig.Hole", "Dig.Hole", "Dig Hole", basePriority);
                Add("Dig.Up",   "Dig.Up",   "Dig Up",   basePriority);
                Add("Dig.Bury", "Dig.Bury", "Bury",     basePriority);
                basePriority += 10;
            }

            // ===== Scent =====
            if (includeScentButtons)
            {
                Add("Scent.Sniff",   "Scent.Sniff",   "Sniff",        basePriority);
                Add("Scent.Follow",  "Scent.Follow",  "Follow Scent", basePriority);
                Add("Scent.Deposit", "Scent.Deposit", "Mark",         basePriority);
                basePriority += 10;
            }

            // ===== Sound =====
            if (includeSoundButtons)
            {
                Add("Sound.Bark", "Sound.Bark", "Bark", basePriority);
                basePriority += 10;
            }

            // ===== Door =====
            if (includeDoorButtons)
            {
                Add("Door.Open",  "Door.Open",  "Open Door",  basePriority);
                Add("Door.Close", "Door.Close", "Close Door", basePriority);
                basePriority += 10;
            }

            // ===== Mode =====
            if (includeModeButtons)
            {
                Add("Mode.Player", "Mode.Player", "Take Control", basePriority);
                basePriority += 10;
            }
        }

        private void UpdateEntryStates(WheelContext ctx)
        {
            if (ctx == null || ctx.target == null)
                return;

            WorldObject target = ctx.target;

            bool hasAgent = target.agentModule != null;
            bool hasPack = target.packMemberModule != null;
            bool inPack = hasPack && target.packMemberModule!.currentPack != null;

            bool hasScent = target.scentPerceptionModule != null;
            bool hasInventory = target.containerModule != null;

            DoorModule? doorModule = target.GetComponent<DoorModule>();
            bool hasDoor = doorModule != null;
            bool canOpenDoor = hasDoor && doorModule!.CanOpen();
            bool canCloseDoor = hasDoor && doorModule!.CanClose();
            bool doorLocked = hasDoor && doorModule!.IsLocked;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry == null)
                    continue;

                string key = entry.actionKey;

                entry.isVisible = true;
                entry.isEnabled = true;
                entry.hint = "";
                entry.disabledHint = "";

                // ===== PACK =====
                if (key == "Pack.Join")
                {
                    entry.isEnabled = hasAgent && !inPack;
                }
                else if (key == "Pack.Leave")
                {
                    entry.isEnabled = hasAgent && inPack;
                }
                else if (key == "Pack.Lead")
                {
                    entry.isEnabled = hasAgent && inPack;
                }

                // ===== SCENT =====
                else if (key.StartsWith("Scent."))
                {
                    entry.isVisible = hasScent;
                    entry.isEnabled = hasScent;
                }

                // ===== INVENTORY =====
                else if (key.StartsWith("Item."))
                {
                    entry.isVisible = hasInventory;
                    entry.isEnabled = hasInventory;
                }

                // ===== MOVEMENT =====
                else if (key.StartsWith("Move."))
                {
                    entry.isEnabled = hasAgent;
                }

                // ===== DOOR =====
                else if (key == "Door.Open")
                {
                    entry.isVisible = hasDoor;
                    entry.isEnabled = canOpenDoor;
                    entry.hint = "Swing the door open.";
                    if (doorLocked)
                        entry.disabledHint = "The door is locked.";
                    else if (hasDoor && !canOpenDoor)
                        entry.disabledHint = "The door is already open.";
                }
                else if (key == "Door.Close")
                {
                    entry.isVisible = hasDoor;
                    entry.isEnabled = canCloseDoor;
                    entry.hint = "Swing the door closed.";
                    if (hasDoor && !canCloseDoor)
                        entry.disabledHint = "The door is already closed.";
                }

                // ===== MODE =====
                else if (key == "Mode.Player")
                {
                    entry.isEnabled = hasAgent;
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();
            BindAllActions();
        }

        public void BindAllActions()
        {
            // Quests
            BindAction("Quest.Get", ctx => Debug.Log("Quest.Get fired for " + ctx.target.name));

            // Sound
            //BindAction("Sound.Bark", ctx => Debug.Log("Sound.Bark fired for " + ctx.target.name));
            BindAction("Sound.Bark", HandleBark);

            // ScentPerception
            BindAction("Scent.Sniff", ctx => Debug.Log("Scent.Sniff fired for " + ctx.target.name));
            BindAction("Scent.Deposit", ctx => Debug.Log("Scent.Deposit fired for " + ctx.target.name));
            BindAction("Scent.Follow", HandleScentFollow);

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

            // Door
            BindAction("Door.Open", HandleDoorOpen);
            BindAction("Door.Close", HandleDoorClose);

            // Mode
            BindAction("Mode.Explore", ctx => SetTargetMode(ctx, AgentDecisionType.Explorer));
            BindAction("Mode.Wander", ctx => SetTargetMode(ctx, AgentDecisionType.Wanderer));
            BindAction("Mode.Player", SetTargetPlayerMode);
            BindAction("Mode.Follow", ctx => SetTargetMode(ctx, AgentDecisionType.Follower));
        }
    }
}
