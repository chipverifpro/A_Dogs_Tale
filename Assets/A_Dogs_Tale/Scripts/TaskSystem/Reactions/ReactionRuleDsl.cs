#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using DogGame.LLM;
using DogGame.Tasks;
using DogGame.Modules;

namespace DogGame.Reactions
{
    /// <summary>
    /// Builds a list of ReactionRules in a structured, LLM-friendly fluent DSL.
    /// Rules remain "pure": they return TaskRequests; ReactionModule submits them.
    /// </summary>
    public sealed class RuleSetBuilder
    {
        private readonly List<ReactionRule> rules = new();

        public static RuleSetBuilder Create() => new RuleSetBuilder();

        public RuleBuilder Rule(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Rule name required.", nameof(name));

            return new RuleBuilder(this, name.Trim());
        }

        internal void AddBuiltRule(ReactionRule rule) => rules.Add(rule);

        public IReadOnlyList<ReactionRule> Build() => rules;
    }

    /// <summary>
    /// Fluent builder for a single ReactionRule.
    /// Avoids arbitrary lambdas by providing common predicates and knobs.
    /// </summary>
    public sealed class RuleBuilder
    {
        private readonly RuleSetBuilder owner;
        private readonly string name;

        // Match criteria (ANDed)
        private PerceptionSense? sense;
        private PerceptionEventType? type;

        private ScentCategory? scentCategory;
        private float? minInterest;
        private float? minNovelty;
        private float? minStrength;

        // Scoring (simple weighted sum; easy for LLMs)
        private float weightInterest = 1.0f;
        private float weightNovelty = 0.25f;
        private float weightStrength = 0.0f;

        // Request metadata
        private int priority = 50;
        private TaskSource source = TaskSource.Reaction;
        private bool canInterrupt = true;
        private bool resumePrevious = false;
        private bool clearStackOnStart = false;
        private string? tag;

        // Cooldown
        private float cooldownSeconds = 0f;
        private CooldownMode cooldownMode = CooldownMode.ByRule;
        private string? cooldownPrefix;

        // Action = sequence of tasks
        private readonly List<IAgentTask> sequenceTasks = new();
        private bool actionDefined;

        internal RuleBuilder(RuleSetBuilder owner, string name)
        {
            this.owner = owner;
            this.name = name;
        }

        private readonly List<TaskSpec> sequenceSpecs = new();

        public RuleBuilder DoTask(TaskSpec spec)
        {
            sequenceSpecs.Add(spec);
            actionDefined = true;
            return this;
        }

        public RuleBuilder DoTasks(params TaskSpec[] specs)
        {
            sequenceSpecs.Clear();
            for (int i = 0; i < specs.Length; i++)
                sequenceSpecs.Add(specs[i]);
            actionDefined = true;
            return this;
        }

        // ----- MATCH / WHEN -----

        public RuleBuilder WhenSense(PerceptionSense v) { sense = v; return this; }
        public RuleBuilder WhenType(PerceptionEventType v) { type = v; return this; }

        public RuleBuilder WhenScentCategory(ScentCategory v)
        {
            scentCategory = v;
            // convenience: if you set scent criteria, assume sense is scent unless overridden
            sense ??= PerceptionSense.Scent;
            return this;
        }

        public RuleBuilder MinInterest(float v) { minInterest = v; return this; }
        public RuleBuilder MinNovelty(float v) { minNovelty = v; return this; }
        public RuleBuilder MinStrength(float v) { minStrength = v; return this; }

        // ----- SCORING -----

        public RuleBuilder ScoreWeights(float interest = 1.0f, float novelty = 0.25f, float strength = 0.0f)
        {
            weightInterest = interest;
            weightNovelty = novelty;
            weightStrength = strength;
            return this;
        }

        // ----- REQUEST METADATA -----

        public RuleBuilder Priority(int v) { priority = Mathf.Clamp(v, 0, 100); return this; }
        public RuleBuilder Source(TaskSource v) { source = v; return this; }
        public RuleBuilder CanInterrupt(bool v) { canInterrupt = v; return this; }
        public RuleBuilder ResumePrevious(bool v) { resumePrevious = v; return this; }
        public RuleBuilder ClearStackOnStart(bool v) { clearStackOnStart = v; return this; }
        public RuleBuilder Tag(string v) { tag = v; return this; }

        // ----- COOLDOWNS -----

        public enum CooldownMode
        {
            ByRule,
            ByScentKey,
            ByTargetObjectId
        }

        public RuleBuilder Cooldown(float seconds, CooldownMode mode = CooldownMode.ByRule, string? keyPrefix = null)
        {
            cooldownSeconds = Mathf.Max(0f, seconds);
            cooldownMode = mode;
            cooldownPrefix = keyPrefix;
            return this;
        }

        // ----- ACTION -----

        /// <summary>Adds a task to the rule's sequence (executed in order).</summary>
        public RuleBuilder Do(IAgentTask task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            sequenceTasks.Add(task);
            actionDefined = true;
            return this;
        }

        /// <summary>Convenience: set/replace the whole action with an explicit sequence.</summary>
        public RuleBuilder DoSequence(params IAgentTask[] tasks)
        {
            sequenceTasks.Clear();
            if (tasks != null)
            {
                for (int i = 0; i < tasks.Length; i++)
                    if (tasks[i] != null) sequenceTasks.Add(tasks[i]);
            }
            actionDefined = true;
            return this;
        }

        // ----- FINALIZE -----

        public RuleSetBuilder End()
        {
            if (!actionDefined || sequenceTasks.Count == 0)
                throw new InvalidOperationException($"Rule '{name}' has no action. Use Do(...) or DoSequence(...).");

            // Build match predicate from structured criteria.
            bool Match(WorldObject observer, PerceptionEvent e)
            {
                if (sense.HasValue && e.Sense != sense.Value) return false;
                if (type.HasValue && e.Type != type.Value) return false;

                if (minInterest.HasValue && e.Interest01 < minInterest.Value) return false;
                if (minNovelty.HasValue && e.Novelty01 < minNovelty.Value) return false;
                if (minStrength.HasValue && e.Strength01 < minStrength.Value) return false;

                if (scentCategory.HasValue)
                {
                    if (!e.Scent.HasValue) return false;
                    if (e.Scent.Value.Category != scentCategory.Value) return false;
                }

                return true;
            }

            float Score(WorldObject observer, PerceptionEvent e)
            {
                return (e.Interest01 * weightInterest)
                     + (e.Novelty01 * weightNovelty)
                     + (e.Strength01 * weightStrength);
            }

            TaskRequest Build(WorldObject observer, PerceptionEvent e)
            {
                // Convert specs -> tasks now (pure; no submits here).
                var tasks = new IAgentTask[sequenceSpecs.Count];

                for (int i = 0; i < sequenceSpecs.Count; i++)
                {
                    if (!TaskSpecFactory.TryBuildTask(sequenceSpecs[i], observer, e, out var built, out var err))
                        throw new Exception($"Rule '{name}' task[{i}] build failed: {err}");
                        // (If you prefer: don’t throw; instead return a “noop” request or log + return Task_Wait(0).)
                    tasks[i] = built!;
                }
                // Always wrap multi-step behaviors into ONE request (atomic priority/interrupt).
                var seq = new Task_Sequence(sequenceTasks.ToArray());

                return new TaskRequest(
                    task: seq,
                    priority: priority,
                    source: source,
                    canInterrupt: canInterrupt,
                    resumePrevious: resumePrevious,
                    clearStackOnStart: clearStackOnStart,
                    tag: tag ?? name
                );
            }

            string? CooldownKey(WorldObject observer, PerceptionEvent e)
            {
                if (cooldownSeconds <= 0f)
                    return null;

                string prefix = string.IsNullOrEmpty(cooldownPrefix) ? name : cooldownPrefix!;

                return cooldownMode switch
                {
                    CooldownMode.ByRule => prefix,

                    CooldownMode.ByScentKey =>
                        e.Scent.HasValue ? $"{prefix}:Scent:{e.Scent.Value.ScentKey}" : prefix,

                    CooldownMode.ByTargetObjectId =>
                        (e.Target != null) ? $"{prefix}:Target:{e.Target.ObjectId}" : prefix,

                    _ => prefix
                };
            }

            owner.AddBuiltRule(new ReactionRule(
                name: name,
                match: Match,
                score: Score,
                build: Build,
                cooldownSeconds: cooldownSeconds,
                cooldownKey: CooldownKey
            ));

            return owner;
        }
    }
}

/* Example usage (LLM-friendly output)

This is the kind of code you can tell the LLM to generate verbatim:

using DogGame.AI.Reactions;

ruleTable = new ReactionRuleTable();

var rules = RuleSetBuilder.Create()
    .Rule("FoodSmell_BarkAndSniff")
        .WhenSense(PerceptionSense.Scent)
        .WhenType(PerceptionEventType.NewSmell)
        .WhenScentCategory(ScentCategory.Food)
        .MinInterest(0.25f)
        .Priority(80)
        .ResumePrevious(true)
        .Cooldown(1.0f, RuleBuilder.CooldownMode.ByScentKey)
        .DoTasks(
            TS.Bark(10),
            TS.Try(
                TS.Sniff(1.0f),
                TS.Wait(0.1f)))
    .End()
    .Build();

for (int i = 0; i < rules.Count; i++)
    ruleTable.Add(rules[i]);
*/

/*
The instruction you give to the LLM

Tell it something like:

“Generate reaction rules using the RuleBuilder DSL only.
Allowed methods: Rule(name), WhenSense, WhenType, WhenScentCategory, 
MinInterest, Priority, ResumePrevious, ClearStackOnStart, 
Cooldown(seconds, mode), DoSequence(tasks...), End().”

That constraint is what makes it reliable.
*/