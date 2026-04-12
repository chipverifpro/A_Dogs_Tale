using System;
using System.Collections.Generic;
using DogGame.Modules;
using UnityEngine;

/* usage examples:

When the dog finds the source object and sniffs it, call:
registry.MarkScentIdentified(source, "Hot Dog");

Later training calls:
registry.MarkScentTrained(source, extraSensitivityBoost: 0.5f);

*/

public enum ScentCategory
{
    Unknown = 0,
    Dog,
    OtherAnimal,
    Human,
    Food,
    Machine,
    Plant,
    Environment     // e.g. water, swamp, etc.
}

public enum ScentFamiliarity
{
    New = 0,        // Smelled indirectly; dog only knows the category
    Scented,        // Dog has followed it, but not yet seen the source object up close
    Identified,     // Dog has sniffed the source object; knows the specific thing (e.g., Hot Dog)
    Trained         // Explicitly trained on this scent; boosted sensitivity
}

[Serializable]
public struct ScentDetection
{
    public ScentSource scentSource;
    public float airStrength;
    public float groundStrength;
    public float combinedStrength;
}

public class ScentRegistry : MonoBehaviour
{
    [Header("Reference to Global Dir")]
    public Dir dir;

    [Header("All Scent Sources")]
    public List<ScentSource> allScentSources = new List<ScentSource>();

    // Internal map for fast lookup by agentId
    private readonly Dictionary<int, ScentSource> _byAgentId = new Dictionary<int, ScentSource>();
    private ScentSource _selectedTargetScent;

    [Header("Category Base Colors")]
    public Color dogBaseColor         = new Color(0.3f, 0.8f, 1.0f, 1.0f);  // cyan-ish
    public Color otherAnimalBaseColor = new Color(0.7f, 0.5f, 0.2f, 1.0f);  // brown-ish
    public Color humanBaseColor       = new Color(0.3f, 0.3f, 1.0f, 1.0f);  // blue
    public Color foodBaseColor        = new Color(1.0f, 0.2f, 0.2f, 1.0f);  // red
    public Color machineBaseColor     = new Color(0.7f, 0.7f, 0.7f, 1.0f);  // gray
    public Color plantBaseColor       = new Color(0.3f, 1.0f, 0.3f, 1.0f);  // green
    public Color environmentBaseColor = new Color(0.2f, 0.6f, 0.9f, 1.0f);  // blue-green
    public Color unknownBaseColor     = new Color(1.0f, 1.0f, 1.0f, 1.0f);  // white

    [Header("Per-Source Color Randomization")]
    [Tooltip("Max hue variation around the category color (0–0.5).")]
    [Range(0f, 0.25f)]
    public float hueJitter = 0.05f;

    [Tooltip("Scale range for saturation (1 ± this).")]
    [Range(0f, 0.5f)]
    public float saturationJitter = 0.1f;

    [Tooltip("Scale range for value/brightness (1 ± this).")]
    [Range(0f, 0.5f)]
    public float valueJitter = 0.1f;

    private void Awake()
    {
        RebuildLookup();
    }

    public void RebuildLookup()
    {
        _byAgentId.Clear();

        foreach (var scentSource in allScentSources)
        {
            if (scentSource == null) continue;

            if (!_byAgentId.ContainsKey(scentSource.agentId))
            {
                _byAgentId.Add(scentSource.agentId, scentSource);
            }
        }
    }

    public ScentSource SelectedTargetScent => _selectedTargetScent;

    public string SelectedTargetScentKey => BuildScentKey(_selectedTargetScent);

    public string BuildScentKey(ScentSource scentSource)
    {
        if (scentSource == null)
            return string.Empty;

        if (scentSource.agentId >= 0)
            return $"agent:{scentSource.agentId}";

        string name = string.IsNullOrWhiteSpace(scentSource.scentName)
            ? scentSource.category.ToString()
            : scentSource.scentName.Trim();

        return $"{scentSource.category}:{name}";
    }

    public ScentSource SetSelectedTargetScent(ScentSource scentSource)
    {
        if (scentSource == null)
        {
            _selectedTargetScent = null;
            return null;
        }

        _selectedTargetScent = RegisterScentSource(scentSource);
        return _selectedTargetScent;
    }

    public bool TryResolveScentSource(string scentKey, out ScentSource scentSource)
    {
        scentSource = null;

        if (string.IsNullOrWhiteSpace(scentKey))
            return false;

        RefreshKnownScentSources();

        if (scentKey.StartsWith("agent:", StringComparison.Ordinal) &&
            int.TryParse(scentKey.AsSpan(6), out int agentId))
        {
            scentSource = GetScentSource(agentId);
            return scentSource != null;
        }

        for (int i = 0; i < allScentSources.Count; i++)
        {
            ScentSource candidate = allScentSources[i];
            if (candidate == null)
                continue;

            if (string.Equals(BuildScentKey(candidate), scentKey, StringComparison.Ordinal))
            {
                scentSource = candidate;
                return true;
            }
        }

        return false;
    }

    public List<ScentSource> GetAvailableScentSources()
    {
        RefreshKnownScentSources();

        var results = new List<ScentSource>();
        var seenAgentIds = new HashSet<int>();

        for (int i = 0; i < allScentSources.Count; i++)
        {
            ScentSource scentSource = allScentSources[i];
            if (scentSource == null)
                continue;

            if (scentSource.agentId < 0)
                continue;

            if (!seenAgentIds.Add(scentSource.agentId))
                continue;

            results.Add(scentSource);
        }

        results.Sort(CompareScentSources);
        return results;
    }

    private int CompareScentSources(ScentSource a, ScentSource b)
    {
        string aName = GetDisplayName(a);
        string bName = GetDisplayName(b);

        int nameCompare = string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
        if (nameCompare != 0)
            return nameCompare;

        int categoryCompare = a.category.CompareTo(b.category);
        if (categoryCompare != 0)
            return categoryCompare;

        return a.agentId.CompareTo(b.agentId);
    }

    private string GetDisplayName(ScentSource scentSource)
    {
        if (scentSource == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(scentSource.scentName))
            return scentSource.scentName.Trim();

        if (scentSource.agent != null && !string.IsNullOrWhiteSpace(scentSource.agent.DisplayName))
            return scentSource.agent.DisplayName.Trim();

        return scentSource.category.ToString();
    }

    private void RefreshKnownScentSources()
    {
        dir ??= Dir.Instance;
        RegisterEmitterScentSources();
        RegisterActiveCellScentSources();
        RebuildLookup();
    }

    private void RegisterEmitterScentSources()
    {
        ScentEmitterModule[] emitters = Resources.FindObjectsOfTypeAll<ScentEmitterModule>();
        for (int i = 0; i < emitters.Length; i++)
        {
            ScentEmitterModule emitter = emitters[i];
            if (emitter == null || emitter.gameObject == null || !emitter.gameObject.scene.IsValid())
                continue;

            RegisterScentSource(emitter.normalScentSource);
            RegisterScentSource(emitter.onDemandScentSource);

            if (emitter.durationScentSources == null)
                continue;

            for (int dssIndex = 0; dssIndex < emitter.durationScentSources.Count; dssIndex++)
            {
                DurationScentSource durationScent = emitter.durationScentSources[dssIndex];
                if (durationScent == null)
                    continue;

                RegisterScentSource(durationScent.scentSource);
            }
        }
    }

    private void RegisterActiveCellScentSources()
    {
        if (dir == null || dir.scents == null || dir.worldObjectRegistry == null)
            return;

        List<Cell> scentCells = dir.scents.cellsContainingScents;
        if (scentCells == null)
            return;

        for (int cellIndex = 0; cellIndex < scentCells.Count; cellIndex++)
        {
            Cell cell = scentCells[cellIndex];
            if (cell == null || cell.scents == null)
                continue;

            for (int scentIndex = 0; scentIndex < cell.scents.Count; scentIndex++)
            {
                ScentInCell scentInCell = cell.scents[scentIndex];
                if (scentInCell == null)
                    continue;

                if (scentInCell.agentId < 0)
                    continue;

                WorldObject agent = dir.scents.GetAgentFromAgentId(scentInCell.agentId);
                ScentCategory category = InferCategoryFromAgent(agent);
                string displayName = agent != null ? agent.DisplayName : null;

                GetOrCreateScentSource(
                    scentInCell.agentId,
                    agent,
                    category,
                    defaultName: displayName);
            }
        }
    }

    private ScentSource RegisterScentSource(ScentSource scentSource)
    {
        if (scentSource == null)
            return null;

        if (scentSource.agent != null && string.IsNullOrWhiteSpace(scentSource.scentName))
            scentSource.scentName = scentSource.agent.DisplayName;

        if (scentSource.agentId >= 0 && _byAgentId.TryGetValue(scentSource.agentId, out ScentSource existing))
        {
            MergeScentSource(existing, scentSource);
            return existing;
        }

        if (scentSource.agentId >= 0)
        {
            for (int i = 0; i < allScentSources.Count; i++)
            {
                ScentSource existingSource = allScentSources[i];
                if (existingSource == null)
                    continue;

                if (existingSource.agentId != scentSource.agentId)
                    continue;

                MergeScentSource(existingSource, scentSource);
                _byAgentId[scentSource.agentId] = existingSource;
                return existingSource;
            }
        }

        if (!allScentSources.Contains(scentSource))
            allScentSources.Add(scentSource);

        if (scentSource.agentId >= 0)
            _byAgentId[scentSource.agentId] = scentSource;

        return scentSource;
    }

    private void MergeScentSource(ScentSource target, ScentSource source)
    {
        if (target == null || source == null || ReferenceEquals(target, source))
            return;

        if (target.agent == null && source.agent != null)
            target.agent = source.agent;

        if (target.category == ScentCategory.Unknown && source.category != ScentCategory.Unknown)
            target.category = source.category;

        if (string.IsNullOrWhiteSpace(target.scentName) && !string.IsNullOrWhiteSpace(source.scentName))
            target.scentName = source.scentName;

        if (target.categoryColor == default && source.categoryColor != default)
            target.categoryColor = source.categoryColor;

        if (target.sourceAirColor == default && source.sourceAirColor != default)
            target.sourceAirColor = source.sourceAirColor;

        if (target.sourceGroundColor == default && source.sourceGroundColor != default)
            target.sourceGroundColor = source.sourceGroundColor;

        target.airDepositRate = Mathf.Max(target.airDepositRate, source.airDepositRate);
        target.groundDepositRate = Mathf.Max(target.groundDepositRate, source.groundDepositRate);
        target.sensitivityBoost = Mathf.Max(target.sensitivityBoost, source.sensitivityBoost);

        if (string.IsNullOrWhiteSpace(target.persistentId) && !string.IsNullOrWhiteSpace(source.persistentId))
            target.persistentId = source.persistentId;
    }

    private ScentCategory InferCategoryFromAgent(WorldObject agent)
    {
        if (agent == null)
            return ScentCategory.Unknown;

        string displayName = agent.DisplayName ?? agent.name ?? string.Empty;
        string normalized = displayName.Replace(" ", string.Empty).ToLowerInvariant();

        if (normalized.Contains("dog") ||
            normalized.Contains("pug") ||
            normalized.Contains("corgi") ||
            normalized.Contains("chihuahua") ||
            normalized.Contains("shepherd") ||
            normalized.Contains("cur"))
        {
            return ScentCategory.Dog;
        }

        return ScentCategory.Unknown;
    }

    #region Core API

    /// <summary>
    /// Get the ScentSource for a given agent. If missing, create a new entry with
    /// broad category knowledge and a randomized color within that category.
    /// </summary>
    public ScentSource GetOrCreateScentSource(
        int agentId,
        WorldObject agent,
        ScentCategory category,
        string defaultName = null,
        float airDepositRate = 1.0f,
        float groundDepositRate = 0.1f)
    {
        if (_byAgentId.TryGetValue(agentId, out var existing))
        {
            return existing;
        }

        var scentSource = new ScentSource
        {
            agentId = agentId,
            agent = agent,
            category = category,
            scentName = string.IsNullOrEmpty(defaultName) ? category.ToString() : defaultName,
            categoryColor = GetCategoryBaseColor(category),
            airDepositRate = airDepositRate,
            groundDepositRate = groundDepositRate,
            familiarity = ScentFamiliarity.New,
            sensitivityBoost = 1.0f,
            persistentId = $"agent_{category}_{agentId}"
        };

        // get two shades of the category color for air and ground.
        scentSource.sourceAirColor = GenerateSourceColor(scentSource.categoryColor);
        scentSource.sourceGroundColor = GenerateSourceColor(scentSource.categoryColor);

        allScentSources.Add(scentSource);
        _byAgentId[agentId] = scentSource;

        return scentSource;
    }

    // stripped down version of above function, we assume it has already been created.
    public ScentSource GetScentSource(int agentId)
    {
        if (_byAgentId.TryGetValue(agentId, out var existing))
        {
            return existing;
        }
        Debug.LogWarning($"GetScentSource(agentId={agentId}) returned null)");
        return null;
    }

    public Color GetCategoryBaseColor(ScentCategory category)
    {
        switch (category)
        {
            case ScentCategory.Dog:          return dogBaseColor;
            case ScentCategory.OtherAnimal:  return otherAnimalBaseColor;
            case ScentCategory.Human:        return humanBaseColor;
            case ScentCategory.Food:         return foodBaseColor;
            case ScentCategory.Machine:      return machineBaseColor;
            case ScentCategory.Plant:        return plantBaseColor;
            case ScentCategory.Environment:  return environmentBaseColor;
            case ScentCategory.Unknown:
            default:                         return unknownBaseColor;
        }
    }

    /// <summary>
    /// Generate a per-source color close to the category color:
    /// small jitter in HSV space so all food is 'red-ish' but distinguishable.
    /// </summary>
    public Color GenerateSourceColor(Color categoryColor)
    {
        Color.RGBToHSV(categoryColor, out float h, out float s, out float v);

        float hueOffset = UnityEngine.Random.Range(-hueJitter, hueJitter);
        float satScale  = 1f + UnityEngine.Random.Range(-saturationJitter, saturationJitter);
        float valScale  = 1f + UnityEngine.Random.Range(-valueJitter, valueJitter);

        float newH = Mathf.Repeat(h + hueOffset, 1f);
        float newS = Mathf.Clamp01(s * satScale);
        float newV = Mathf.Clamp01(v * valScale);

        return Color.HSVToRGB(newH, newS, newV);
    }

    #endregion

    #region Familiarity / Training

    /// <summary>
    /// Called when a dog first notices the scent (e.g., via a sniff command).
    /// Moves from New -> Scented if appropriate.
    /// </summary>
    public void MarkScentScented(ScentSource scentSource)
    {
        if (scentSource == null) return;

        if (scentSource.familiarity == ScentFamiliarity.New)
        {
            scentSource.familiarity = ScentFamiliarity.Scented;
        }
    }

    /// <summary>
    /// Called when the dog finds the actual source object and sniffs it.
    /// Updates the name from generic ("Food") to specific ("Hot Dog").
    /// </summary>
    public void MarkScentIdentified(ScentSource scentSource, string specificName)
    {
        if (scentSource == null) return;

        scentSource.scentName = string.IsNullOrEmpty(specificName) 
            ? scentSource.scentName 
            : specificName;

        if (scentSource.familiarity < ScentFamiliarity.Identified)
        {
            scentSource.familiarity = ScentFamiliarity.Identified;
        }
    }

    /// <summary>
    /// Called when the pack is explicitly trained on this scent.
    /// Boosts sensitivity so dogs pick it up more easily (e.g., lower thresholds).
    /// </summary>
    public void MarkScentTrained(ScentSource scentSource, float extraSensitivityBoost = 0.5f)
    {
        if (scentSource == null) return;

        scentSource.familiarity = ScentFamiliarity.Trained;
        scentSource.sensitivityBoost = Mathf.Max(1.0f, scentSource.sensitivityBoost + extraSensitivityBoost);
    }

    /// <summary>
    /// Returns overall sensitivity multiplier for a given scent.
    /// You can factor this into detection thresholds in the dog AI.
    /// </summary>
    public float GetSensitivityMultiplier(ScentSource scentSource)
    {
        if (scentSource == null) return 1.0f;
        return scentSource.sensitivityBoost;
    }

    #endregion

    #region Sniff UI & Overlay Stubs

    /// <summary>
    /// Collect scents present in the given cell, sorted strongest->weakest.
    /// This will be called by the 'sniff' command to populate a list for the player.
    /// </summary>
    public List<ScentDetection> CollectScentsAtCell(Cell cell, ScentAirGround scentSystem)
    {
        // TODO: Implement integration with your ScentSystem & Cell layout:
        // - For the given cell, find all agentIds contributing scent there.
        // - Map agentId -> ScentSource via this registry.
        // - Compute airStrength / groundStrength / combinedStrength.
        // For now, just return an empty list stub.

        var results = new List<ScentDetection>();

        // Example (pseudocode, replace with your actual sampling):
        
        foreach (var contributingAgent in cell.scents)
        {
            WorldObject agent = dir.scentAirGround.GetAgentFromAgentId(contributingAgent.agentId);
            var scentSource = GetOrCreateScentSource(contributingAgent.agentId, agent:agent, ScentCategory.Unknown);
            float air = contributingAgent.airIntensity;
            float ground = contributingAgent.groundIntensity;
            float combined = ground * 0.7f + air * 0.3f;

            results.Add(new ScentDetection
            {
                scentSource = scentSource,
                airStrength = air,
                groundStrength = ground,
                combinedStrength = combined
            });
        }
        

        // Sort strongest to weakest by combined strength
        results.Sort((a, b) => b.combinedStrength.CompareTo(a.combinedStrength));

        return results;
    }

    /// <summary>
    /// Called by the UI when the player clicks a scent in the list.
    /// Should trigger the visualization overlay for that specific scent.
    /// </summary>
    public void ActivateScentOverlay(ScentSource scentSource = null)
    {
        if (dir == null || dir.scents == null)
        {
            Debug.LogError("ScentRegistry.ActivateScentOverlay called without Dir.scents assigned.");
            return;
        }

        ScentSource resolvedSource = scentSource ?? ResolveDefaultOverlaySource();
        dir.scents.ActivateOverlayForSource(resolvedSource);
    }

    /// <summary>
    /// Called when leaving the scent overlay mode.
    /// </summary>
    public void DeactivateScentOverlay()
    {
        if (dir == null || dir.scents == null)
        {
            Debug.LogError("ScentRegistry.DeactivateScentOverlay called without Dir.scents assigned.");
            return;
        }

        dir.scents.ActivateOverlayForSource(null);
    }

    private ScentSource ResolveDefaultOverlaySource()
    {
        WorldObject playerLeader = dir != null && dir.playerPack != null
            ? dir.playerPack.packLeader
            : null;

        if (playerLeader != null && playerLeader.ObjectId > 0)
        {
            return GetOrCreateScentSource(
                playerLeader.ObjectId,
                playerLeader,
                ScentCategory.Dog,
                defaultName: playerLeader.DisplayName);
        }

        int currentAgentId = dir != null && dir.scents != null ? dir.scents.currentAgentId : -1;
        if (currentAgentId > 0)
        {
            WorldObject agent = dir != null && dir.scents != null
                ? dir.scents.GetAgentFromAgentId(currentAgentId)
                : null;
            if (agent != null)
            {
                return GetOrCreateScentSource(
                    currentAgentId,
                    agent,
                    ScentCategory.Dog,
                    defaultName: agent.DisplayName);
            }
        }

        Debug.LogWarning("ScentRegistry could not resolve a default scent overlay source.");
        return null;
    }

    #endregion

    #region Strong Scent Notifications & Distraction Hooks

    /// <summary>
    /// Called when a particularly strong scent is present in the current cell,
    /// even if the player isn't actively sniffing.
    /// Should trigger a BottomBanner notice and may influence dog behavior.
    /// </summary>
    public void NotifyStrongScent(ScentSource scentSource, float strength)
    {
        if (scentSource == null) return;

        // TODO: Hook up to BottomBanner:
        // BottomBanner.ShowMessage($"Strong {scentSource.category} scent: {scentSource.scentName}");

        // TODO: Hook up to dog AI:
        // - Some dogs may lose concentration and wander toward this scent
        //   if strength * sensitivityBoost crosses a per-dog threshold.
    }

    /// <summary>
    /// Helper to decide whether a scent is 'distracting' enough for a given dog.
    /// You might call this from your dog behavior tree / state machine.
    /// </summary>
    public bool IsDistractingScentForDog(ScentSource scentSource, float strength, float dogBaseThreshold)
    {
        if (scentSource == null) return false;

        float effectiveStrength = strength * GetSensitivityMultiplier(scentSource);
        return effectiveStrength >= dogBaseThreshold;
    }

    #endregion

    #region Persistence Stubs

    /// <summary>
    /// Save known scent data for the pack. You can serialize to JSON, binary, etc.
    /// </summary>
    public void SaveScentKnowledge(string saveId)
    {
        // TODO: Implement save logic:
        // - Serialize 'knownScents' list to file / PlayerPrefs / custom save system.
    }

    /// <summary>
    /// Load known scent data for the pack and rebuild lookup tables.
    /// </summary>
    public void LoadScentKnowledge(string saveId)
    {
        // TODO: Implement load logic:
        // - Deserialize into 'knownScents'
        // - Call RebuildLookup()
    }

    #endregion
}
