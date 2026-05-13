using System;
using System.IO;
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

    public enum MapType
    {
        House = 0,
        Yard = 1,
        DogPark = 2,
        Forest = 3,
        Castle = 4
    }

    [Serializable]
    public class Data
    {
        public MapType mapType = MapType.House;
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

    public static string GetSelectedMapPresetName(string subFolder = "DungeonConfigs")
    {
        Data data = GetCurrentOrSaved();
        string presetName = GetMapPresetName(data.mapType);
        if (MapPresetExists(presetName, subFolder))
            return presetName;

        string fallbackName = GetMapPresetName(MapType.House);
        if (data.mapType != MapType.House)
            Debug.LogWarning($"Selected map preset '{presetName}' was not found. Falling back to '{fallbackName}'.");

        return fallbackName;
    }

    public static string GetMapPresetName(MapType mapType)
    {
        switch (mapType)
        {
            case MapType.Yard:
                return "02_Yard";
            case MapType.DogPark:
                return "03_Dog_Park";
            case MapType.Forest:
                return "04_Forest";
            case MapType.Castle:
                return "05_Castle";
            case MapType.House:
            default:
                return "01_House_Tutorial";
        }
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
        ApplySavedMapToDungeonGenerator(dungeonGenerator);
    }

    public static void ApplySavedMapToDungeonGenerator(DungeonGenerator dungeonGenerator)
    {
        DungeonSettings cfg = dungeonGenerator.cfg;
        Data data = GetCurrentOrSaved();
        switch (data.mapType)
        {
            case MapType.House:
                cfg.RoomAlgorithm = DungeonSettings.RoomAlgorithm_e.PackedRooms;
                cfg.useCellularAutomata = false;
                cfg.useScatterRooms = false;
                cfg.usePackedRooms = true;
                cfg.useDiagonalCorners = false;
                cfg.mapHeight = 25;                 // small house map
                cfg.mapWidth = 25;
                break;
            case MapType.Yard:
                break;
            case MapType.DogPark:
                break;
            case MapType.Forest:
                cfg.RoomAlgorithm = DungeonSettings.RoomAlgorithm_e.CellularAutomataPerlin;
                cfg.generateOverlappingRooms = false;
                cfg.useCellularAutomata = true;
                cfg.useScatterRooms = false;
                cfg.usePerlin = true;
                cfg.usePackedRooms = false;
                cfg.useDiagonalCorners = true;
                break;
            case MapType.Castle:
                cfg.RoomAlgorithm = DungeonSettings.RoomAlgorithm_e.PackedRooms;
                cfg.useCellularAutomata = false;
                cfg.useScatterRooms = false;
                cfg.usePackedRooms = true;
                cfg.useDiagonalCorners = false;
                cfg.mapHeight = 100;                 // big house map
                cfg.mapWidth = 100;
                break;                
        }
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

        if (!Enum.IsDefined(typeof(MapType), data.mapType))
            data.mapType = MapType.House;

        data.scentSimulationTimeStep = Mathf.Clamp(
            Mathf.Round(data.scentSimulationTimeStep * 10f) / 10f,
            MinScentSimulationTimeStep,
            MaxScentSimulationTimeStep);

        return data;
    }

    private static bool MapPresetExists(string presetName, string subFolder)
    {
        if (string.IsNullOrWhiteSpace(presetName))
            return false;

        string folder = Path.Combine(Application.persistentDataPath, subFolder);
        string path = Path.Combine(folder, presetName + ".json");
        return File.Exists(path);
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
