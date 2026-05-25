#nullable enable
// LLMConfigModuleSections_Character.cs (line 9): unused. 
// Nothing instantiates CharacterSection, nothing serializes it into scenes/prefabs,
// and the only related prompt helper is commented out in PromptBlocks.CharacterPersonaBlock.cs (line 8).

/*
namespace DogGame.LLM.Agent
{
    [Serializable]
    public sealed class CharacterSection
    {
        [Header("Manual Overrides (optional)")]
        [Tooltip("If set, this archetype string is always used (overrides random selection).")]
        public string archetype = "";

        [Tooltip("If set, this short background is always used (overrides random selection).")]
        [TextArea(2, 6)]
        public string background = "";

        [Tooltip("If non-empty, these goals are always included (in addition to random goals if enabled).")]
        public List<string> forcedGoals = new();

        [Tooltip("If non-empty, these quirks are always included (overrides random quirks if 'overrideRandomQuirks' is true).")]
        public List<string> forcedQuirks = new();

        [Tooltip("If true and forcedQuirks has entries, random quirks are replaced (not appended).")]
        public bool overrideRandomQuirks = true;

        [Header("Random Pools")]
        [Tooltip("Pool of possible archetypes if 'archetype' is empty.")]
        public List<string> archetypePool = new()
        {
            "Loyal protector",
            "Curious scout",
            "Anxious sentinel",
            "Playful troublemaker",
            "Stoic veteran"
        };

        [Tooltip("Pool of possible short backgrounds if 'background' is empty.")]
        [TextArea(2, 6)]
        public List<string> backgroundPool = new()
        {
            "Grew up guarding a quiet home and distrusts sudden noises.",
            "Was once a stray; learned to survive by reading people fast.",
            "Raised around children; protective but easily distracted by play.",
            "Trained for patrols; methodical and watchful."
        };

        [Tooltip("Pool of possible goals (the LLM uses these as 'what I care about').")]
        public List<string> goalsPool = new()
        {
            "Keep the pack safe.",
            "Investigate unfamiliar scents.",
            "Stay near the human unless danger is far away.",
            "Avoid open conflict unless necessary.",
            "Maintain territory boundaries."
        };

        [Tooltip("Pool of possible quirks (small behavioral flavor).")]
        public List<string> quirksPool = new()
        {
            "Sniffs first before acting.",
            "Hesitates at thresholds/doorways.",
            "Dislikes loud metallic clanks.",
            "Circles once before settling.",
            "Stops to listen when uncertain."
        };

        [Header("Randomization Controls")]
        public bool enableRandom = true;

        [Range(0, 5)] public int randomGoalCount = 2;
        [Range(0, 5)] public int randomQuirkCount = 2;

        [Tooltip("If true, chosen random values are cached (stable across play).")]
        public bool lockAfterSpawn = true;

        [NonSerialized] private bool cached;
        [NonSerialized] private string cachedArchetype = "";
        [NonSerialized] private string cachedBackground = "";
        [NonSerialized] private List<string> cachedGoals = new();
        [NonSerialized] private List<string> cachedQuirks = new();

        public void ResetCache()
        {
            cached = false;
            cachedArchetype = "";
            cachedBackground = "";
            cachedGoals.Clear();
            cachedQuirks.Clear();
        }

        public CharacterBuild Build(string seedString)
        {
            if (lockAfterSpawn && cached)
            {
                return new CharacterBuild
                {
                    archetype = cachedArchetype,
                    background = cachedBackground,
                    goals = new List<string>(cachedGoals),
                    quirks = new List<string>(cachedQuirks)
                };
            }

            var rng = new SeededRng(seedString);

            // Archetype / background
            string finalArchetype = !string.IsNullOrWhiteSpace(archetype)
                ? archetype.Trim()
                : PickOneOrEmpty(archetypePool, rng);

            string finalBackground = !string.IsNullOrWhiteSpace(background)
                ? background.Trim()
                : PickOneOrEmpty(backgroundPool, rng);

            // Goals
            var goals = new List<string>();
            if (forcedGoals.Count > 0)
                goals.AddRange(CleanList(forcedGoals));

            if (enableRandom && randomGoalCount > 0)
                goals.AddRange(PickManyUnique(goalsPool, randomGoalCount, rng, exclude: goals));

            // Quirks
            var quirks = new List<string>();
            bool useForcedOnly = overrideRandomQuirks && forcedQuirks.Count > 0;

            if (forcedQuirks.Count > 0)
                quirks.AddRange(CleanList(forcedQuirks));

            if (!useForcedOnly && enableRandom && randomQuirkCount > 0)
                quirks.AddRange(PickManyUnique(quirksPool, randomQuirkCount, rng, exclude: quirks));

            // Cache
            if (lockAfterSpawn)
            {
                cached = true;
                cachedArchetype = finalArchetype;
                cachedBackground = finalBackground;
                cachedGoals = new List<string>(goals);
                cachedQuirks = new List<string>(quirks);
            }

            return new CharacterBuild
            {
                archetype = finalArchetype,
                background = finalBackground,
                goals = goals,
                quirks = quirks
            };
        }

        private static string PickOneOrEmpty(List<string> pool, SeededRng rng)
        {
            if (pool == null || pool.Count == 0) return "";
            int index = rng.NextInt(0, pool.Count);
            return (pool[index] ?? "").Trim();
        }

        private static List<string> PickManyUnique(List<string> pool, int count, SeededRng rng, List<string>? exclude)
        {
            var result = new List<string>();
            if (pool == null || pool.Count == 0 || count <= 0) return result;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (exclude != null)
                foreach (var e in exclude) if (!string.IsNullOrWhiteSpace(e)) seen.Add(e.Trim());

            // Fisher–Yates style sampling
            var candidates = new List<string>();
            foreach (var s in pool)
            {
                var t = (s ?? "").Trim();
                if (t.Length == 0) continue;
                if (seen.Contains(t)) continue;
                candidates.Add(t);
            }

            int take = Mathf.Min(count, candidates.Count);
            for (int i = 0; i < take; i++)
            {
                int j = rng.NextInt(i, candidates.Count);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                result.Add(candidates[i]);
            }

            return result;
        }

        private static List<string> CleanList(List<string> src)
        {
            var cleaned = new List<string>();
            for (int i = 0; i < src.Count; i++)
            {
                var s = (src[i] ?? "").Trim();
                if (s.Length > 0) cleaned.Add(s);
            }
            return cleaned;
        }
    }

    public struct CharacterBuild
    {
        public string archetype;
        public string background;
        public List<string> goals;
        public List<string> quirks;
    }

    /// <summary>Deterministic RNG from a string seed (stable per agent).</summary>
    public readonly struct SeededRng
    {
        private readonly System.Random random;

        public SeededRng(string seed)
        {
            random = new System.Random(StableHash(seed));
        }

        public int NextInt(int minInclusive, int maxExclusive) => random.Next(minInclusive, maxExclusive);

        private static int StableHash(string s)
        {
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < (s?.Length ?? 0); i++)
                    hash = hash * 31 + s![i];
                return hash;
            }
        }
    }
}
*/