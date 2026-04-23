using System;
using DogGame.LLM;
using UnityEngine;

/// <summary>
/// Stores a small set of user-adjustable runtime settings and reapplies them on launch.
/// </summary>
public static class PersistentGameSettings
{
    private const string PlayerPrefsKey = "A_Dogs_Tale.PersistentGameSettings";
    private const float MinScentSimulationTimeStep = 0.1f;
    private const float MaxScentSimulationTimeStep = 1.0f;

    [Serializable]
    public class Data
    {
        public bool chatGptEnabled = true;
        public bool geminiEnabled = true;
        public bool ollamaEnabled = true;
        public float scentSimulationTimeStep = MinScentSimulationTimeStep;
        public bool wallpaperEnabled = true;
    }

    public static Data GetCurrentOrSaved()
    {
        if (TryLoad(out Data loaded))
            return loaded;

        return CaptureCurrentRuntimeSettings();
    }

    public static void SaveAndApply(Data data)
    {
        Data normalized = Normalize(data);
        PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(normalized));
        PlayerPrefs.Save();
        ApplyToRuntime(normalized);
    }

    public static void ApplySavedToScheduler(LLMWorldScheduler scheduler)
    {
        if (scheduler == null || !TryLoad(out Data data))
            return;

        scheduler.llmVendorAndModel = BuildVendorMask(data);
    }

    public static void ApplySavedToScentAirGround(ScentAirGround scentAirGround)
    {
        if (scentAirGround == null || !TryLoad(out Data data))
            return;

        scentAirGround.SimulationTimeStep = data.scentSimulationTimeStep;
    }

    public static void ApplySavedToDungeonGenerator(DungeonGenerator dungeonGenerator)
    {
        if (dungeonGenerator == null || !TryLoad(out Data data))
            return;

        dungeonGenerator.ApplyWallpaperOnWallTiles = data.wallpaperEnabled;
    }

    public static void ApplyToRuntime(Data data)
    {
        Data normalized = Normalize(data);

        LLMWorldScheduler scheduler = GetScheduler();
        if (scheduler != null)
        {
            scheduler.llmVendorAndModel = BuildVendorMask(normalized);
            scheduler.OnValidate();
        }

        ScentAirGround scentAirGround = GetScentAirGround();
        if (scentAirGround != null)
            scentAirGround.SimulationTimeStep = normalized.scentSimulationTimeStep;

        DungeonGenerator dungeonGenerator = GetDungeonGenerator();
        if (dungeonGenerator != null)
            dungeonGenerator.ApplyWallpaperOnWallTiles = normalized.wallpaperEnabled;
    }

    private static bool TryLoad(out Data data)
    {
        if (!PlayerPrefs.HasKey(PlayerPrefsKey))
        {
            data = null;
            return false;
        }

        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            data = null;
            return false;
        }

        data = JsonUtility.FromJson<Data>(json);
        if (data == null)
            return false;

        data = Normalize(data);
        return true;
    }

    private static Data CaptureCurrentRuntimeSettings()
    {
        Data data = new Data();

        LLMWorldScheduler scheduler = GetScheduler();
        if (scheduler != null)
        {
            data.chatGptEnabled = HasAnyVendorEnabled(scheduler.llmVendorAndModel, LLMVendor.OpenAI);
            data.geminiEnabled = HasAnyVendorEnabled(scheduler.llmVendorAndModel, LLMVendor.Gemini);
            data.ollamaEnabled = HasAnyVendorEnabled(scheduler.llmVendorAndModel, LLMVendor.Ollama);
        }

        ScentAirGround scentAirGround = GetScentAirGround();
        if (scentAirGround != null)
            data.scentSimulationTimeStep = scentAirGround.SimulationTimeStep;

        DungeonGenerator dungeonGenerator = GetDungeonGenerator();
        if (dungeonGenerator != null)
            data.wallpaperEnabled = dungeonGenerator.ApplyWallpaperOnWallTiles;

        return Normalize(data);
    }

    private static Data Normalize(Data data)
    {
        if (data == null)
            data = new Data();

        data.scentSimulationTimeStep = Mathf.Clamp(
            Mathf.Round(data.scentSimulationTimeStep * 10f) / 10f,
            MinScentSimulationTimeStep,
            MaxScentSimulationTimeStep);

        return data;
    }

    private static bool HasAnyVendorEnabled(LLMVendorAndModel mask, LLMVendor vendor)
    {
        return (mask & GetVendorMask(vendor)) != LLMVendorAndModel.None;
    }

    private static LLMVendorAndModel BuildVendorMask(Data data)
    {
        LLMVendorAndModel mask = LLMVendorAndModel.None;

        if (data.chatGptEnabled)
            mask |= GetVendorMask(LLMVendor.OpenAI);
        if (data.geminiEnabled)
            mask |= GetVendorMask(LLMVendor.Gemini);
        if (data.ollamaEnabled)
            mask |= GetVendorMask(LLMVendor.Ollama);

        return mask;
    }

    private static LLMVendorAndModel GetVendorMask(LLMVendor vendor)
    {
        LLMVendorAndModel mask = LLMVendorAndModel.None;

        foreach (LLMVendorAndModel value in Enum.GetValues(typeof(LLMVendorAndModel)))
        {
            if (value == LLMVendorAndModel.None)
                continue;

            if (GetVendor(value) == vendor)
                mask |= value;
        }

        return mask;
    }

    private static LLMVendor GetVendor(LLMVendorAndModel value)
    {
        string name = value.ToString();

        if (name.StartsWith("OpenAI_", StringComparison.Ordinal))
            return LLMVendor.OpenAI;
        if (name.StartsWith("Gemini_", StringComparison.Ordinal))
            return LLMVendor.Gemini;
        if (name.StartsWith("Ollama_", StringComparison.Ordinal))
            return LLMVendor.Ollama;

        return LLMVendor.None;
    }

    private static LLMWorldScheduler GetScheduler()
    {
        return Dir.Instance?.llmWorldScheduler
            ?? UnityEngine.Object.FindFirstObjectByType<LLMWorldScheduler>(FindObjectsInactive.Include);
    }

    private static ScentAirGround GetScentAirGround()
    {
        return Dir.Instance?.scentAirGround
            ?? Dir.Instance?.scents
            ?? UnityEngine.Object.FindFirstObjectByType<ScentAirGround>(FindObjectsInactive.Include);
    }

    private static DungeonGenerator GetDungeonGenerator()
    {
        return Dir.Instance?.gen
            ?? UnityEngine.Object.FindFirstObjectByType<DungeonGenerator>(FindObjectsInactive.Include);
    }
}
