using System;
using System.Collections.Generic;
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
public class ScentSource
{
    // Unique key that ties this scent to your ScentSystem / emitters.
    public int agentId = -1;

    // Broad category: Dog, Human, Food, Machine, etc.
    public ScentCategory category = ScentCategory.Unknown;

    // Display name:
    // - Category-level initially (e.g., "Food")
    // - Specific after identification (e.g., "Hot Dog")
    public string scentName;

    // Base category color (e.g., all Food ≈ shades of red)
    public Color categoryColor;

    // Individualized color for this particular source (e.g., slightly different red hue).
    public Color sourceAirColor;
    public Color sourceGroundColor;

    // Default deposit / emission rates for this scent source
    // (can be fed directly into AddScentToCell or used as multipliers).
    public float airDepositRate = 1.0f;
    public float groundDepositRate = 0.1f;

    // How familiar the pack is with this scent.
    public ScentFamiliarity familiarity = ScentFamiliarity.New;

    // Sensitivity multiplier: >1.0 when trained, applied when dogs sniff for this scent.
    public float sensitivityBoost = 1.0f;

    // Optional: persistent ID for saving/loading (if you want something beyond agentId).
    public string persistentId;
}

[Serializable]
public struct ScentDetection
{
    public ScentSource source;
    public float airStrength;
    public float groundStrength;
    public float combinedStrength;
}

public class ScentRegistry : MonoBehaviour
{
    [Header("Reference to Global Directory")]
    public ObjectDirectory dir;

    [Header("Known Scents (Pack-Wide)")]
    public List<ScentSource> knownScents = new List<ScentSource>();

    // Internal map for fast lookup by agentId
    private readonly Dictionary<int, ScentSource> _byAgentId = new Dictionary<int, ScentSource>();

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

        foreach (var source in knownScents)
        {
            if (source == null) continue;

            if (!_byAgentId.ContainsKey(source.agentId))
            {
                _byAgentId.Add(source.agentId, source);
            }
        }
    }

    #region Core API

    /// <summary>
    /// Get the ScentSource for a given agent. If missing, create a new entry with
    /// broad category knowledge and a randomized color within that category.
    /// </summary>
    public ScentSource GetOrCreateScentSource(
        int agentId,
        ScentCategory category,
        string defaultName = null,
        float airDepositRate = 1.0f,
        float groundDepositRate = 0.1f)
    {
        if (_byAgentId.TryGetValue(agentId, out var existing))
        {
            return existing;
        }

        var source = new ScentSource
        {
            agentId = agentId,
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
        source.sourceAirColor = GenerateSourceColor(source.categoryColor);
        source.sourceGroundColor = GenerateSourceColor(source.categoryColor);

        knownScents.Add(source);
        _byAgentId[agentId] = source;

        return source;
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
    public void MarkScentScented(ScentSource source)
    {
        if (source == null) return;

        if (source.familiarity == ScentFamiliarity.New)
        {
            source.familiarity = ScentFamiliarity.Scented;
        }
    }

    /// <summary>
    /// Called when the dog finds the actual source object and sniffs it.
    /// Updates the name from generic ("Food") to specific ("Hot Dog").
    /// </summary>
    public void MarkScentIdentified(ScentSource source, string specificName)
    {
        if (source == null) return;

        source.scentName = string.IsNullOrEmpty(specificName) 
            ? source.scentName 
            : specificName;

        if (source.familiarity < ScentFamiliarity.Identified)
        {
            source.familiarity = ScentFamiliarity.Identified;
        }
    }

    /// <summary>
    /// Called when the pack is explicitly trained on this scent.
    /// Boosts sensitivity so dogs pick it up more easily (e.g., lower thresholds).
    /// </summary>
    public void MarkScentTrained(ScentSource source, float extraSensitivityBoost = 0.5f)
    {
        if (source == null) return;

        source.familiarity = ScentFamiliarity.Trained;
        source.sensitivityBoost = Mathf.Max(1.0f, source.sensitivityBoost + extraSensitivityBoost);
    }

    /// <summary>
    /// Returns overall sensitivity multiplier for a given scent.
    /// You can factor this into detection thresholds in the dog AI.
    /// </summary>
    public float GetSensitivityMultiplier(ScentSource source)
    {
        if (source == null) return 1.0f;
        return source.sensitivityBoost;
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
        /*
        foreach (var contributingAgent in cell.contributingAgents)
        {
            var source = GetOrCreateScentSource(contributingAgent.agentId, contributingAgent.category);
            float air = contributingAgent.airIntensity;
            float ground = contributingAgent.groundIntensity;
            float combined = ground * 0.7f + air * 0.3f;

            results.Add(new ScentDetection
            {
                source = source,
                airStrength = air,
                groundStrength = ground,
                combinedStrength = combined
            });
        }
        */

        // Sort strongest to weakest by combined strength
        results.Sort((a, b) => b.combinedStrength.CompareTo(a.combinedStrength));

        return results;
    }

    /// <summary>
    /// Called by the UI when the player clicks a scent in the list.
    /// Should trigger the visualization overlay for that specific scent.
    /// </summary>
    public void ActivateScentOverlay(ScentSource source = null)
    {
        //if (source == null)
        //{
        //    return;
        //}
        dir.scents.ActivateOverlayForSource(source);
    }

    /// <summary>
    /// Called when leaving the scent overlay mode.
    /// </summary>
    public void DeactivateScentOverlay()
    {
        // TODO: Restore default visualization mode (all scents, or none, etc.).
    }

    #endregion

    #region Strong Scent Notifications & Distraction Hooks

    /// <summary>
    /// Called when a particularly strong scent is present in the current cell,
    /// even if the player isn't actively sniffing.
    /// Should trigger a BottomBanner notice and may influence dog behavior.
    /// </summary>
    public void NotifyStrongScent(ScentSource source, float strength)
    {
        if (source == null) return;

        // TODO: Hook up to BottomBanner:
        // BottomBanner.ShowMessage($"Strong {source.category} scent: {source.scentName}");

        // TODO: Hook up to dog AI:
        // - Some dogs may lose concentration and wander toward this scent
        //   if strength * sensitivityBoost crosses a per-dog threshold.
    }

    /// <summary>
    /// Helper to decide whether a scent is 'distracting' enough for a given dog.
    /// You might call this from your dog behavior tree / state machine.
    /// </summary>
    public bool IsDistractingScentForDog(ScentSource source, float strength, float dogBaseThreshold)
    {
        if (source == null) return false;

        float effectiveStrength = strength * GetSensitivityMultiplier(source);
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