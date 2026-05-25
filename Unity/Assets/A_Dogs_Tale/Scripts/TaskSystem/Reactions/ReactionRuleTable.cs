#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using DogGame.Tasks;
using DogGame.Modules;

namespace DogGame.Reactions
{
    /// <summary>One reaction rule: match -> score -> build a task request.</summary>
    public sealed class ReactionRule
    {
        public readonly string Name;

        /// <summary>Fast filter. If false, rule is not considered.</summary>
        public readonly Func<WorldObject, PerceptionEvent, bool> Match;

        /// <summary>How desirable this reaction is vs other rules (higher wins).</summary>
        public readonly Func<WorldObject, PerceptionEvent, float> Score;

        /// <summary>Build the task request (sequence, priority, resume policy, etc.).</summary>
        public readonly Func<WorldObject, PerceptionEvent, TaskRequest> Build;

        /// <summary>Optional cooldown key generator (null means no cooldown).</summary>
        public readonly Func<WorldObject, PerceptionEvent, string?> CooldownKey;

        /// <summary>Cooldown duration for this rule (seconds). 0 disables.</summary>
        public readonly float CooldownSeconds;

        public ReactionRule(
            string name,
            Func<WorldObject, PerceptionEvent, bool> match,
            Func<WorldObject, PerceptionEvent, float> score,
            Func<WorldObject, PerceptionEvent, TaskRequest> build,
            float cooldownSeconds = 0.0f,
            Func<WorldObject, PerceptionEvent, string?>? cooldownKey = null)
        {
            Name = name;
            Match = match;
            Score = score;
            Build = build;
            CooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            CooldownKey = cooldownKey ?? ((_, __) => name); // default: rule name
        }
    }

    /// <summary>
    /// Holds a set of rules and selects the best rule for a given event.
    /// Also provides simple per-rule cooldowns (by key).
    /// </summary>
    public sealed class ReactionRuleTable
    {
        private readonly List<ReactionRule> rules = new();
        private readonly Dictionary<string, float> cooldownUntilTime = new();

        public void Add(ReactionRule rule) => rules.Add(rule);

        public bool TrySelectBestRule(WorldObject observer, PerceptionEvent e, out ReactionRule? bestRule, out float bestScore)
        {
            bestRule = null;
            bestScore = float.NegativeInfinity;

            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (!rule.Match(observer, e))
                    continue;

                // Cooldown check
                if (rule.CooldownSeconds > 0f)
                {
                    string? key = rule.CooldownKey(observer, e);
                    if (!string.IsNullOrEmpty(key) && cooldownUntilTime.TryGetValue(key, out float until) && Time.time < until)
                        continue;
                }

                float s = rule.Score(observer, e);
                if (s > bestScore)
                {
                    bestScore = s;
                    bestRule = rule;
                }
            }

            return bestRule != null;
        }

        public void ArmCooldown(WorldObject observer, PerceptionEvent e, ReactionRule rule)
        {
            if (rule.CooldownSeconds <= 0f)
                return;

            string? key = rule.CooldownKey(observer, e);
            if (string.IsNullOrEmpty(key))
                return;

            cooldownUntilTime[key] = Time.time + rule.CooldownSeconds;
        }
    }
}