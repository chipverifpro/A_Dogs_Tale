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
    private const string TouchscreenJoystickVisibleJsonField = "\"touchscreenJoystickVisible\"";
    private const string ButtonSizeJsonField = "\"buttonSize\"";
    private const string AndroidFullscreenJsonField = "\"androidFullscreenEnabled\"";
    private const float MinScentSimulationTimeStep = 0.1f;
    private const float MaxScentSimulationTimeStep = 1.0f;
    public const float MinButtonSize = 40f;
    public const float MaxButtonSize = 250f;
    public const float DefaultButtonSize = 176f;
    public const int GraphicsLevelLow = 1985;
    public const int GraphicsLevelMedium = 1990;
    public const int GraphicsLevelHigh = 1995;

    public enum MapType
    {
        House = 0,
        Yard = 1,
        DogPark = 2,
        Forest = 3,
        Castle = 4,
    }

    [Serializable]
    public class Data
    {
        public MapType mapType = MapType.House;
        public bool chatGptEnabled = true;
        public bool geminiEnabled = true;
        public bool ollamaEnabled = true;
        public float scentSimulationTimeStep = MinScentSimulationTimeStep;
        public int graphicsLevel = GraphicsLevelHigh;
        public bool wallpaperEnabled = true;
        public bool touchscreenJoystickVisible = true;
        public float buttonSize = DefaultButtonSize;
        public bool androidFullscreenEnabled = false;
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

        ApplyGraphicsLevelToDungeonGenerator(dungeonGenerator, data.graphicsLevel);
        ApplySavedMapToDungeonGenerator(dungeonGenerator);
    }

    public static void ApplySavedToInputAdapter(NewInputAdapter inputAdapter)
    {
        Data data = GetCurrentOrSaved();

        if (inputAdapter != null)
            inputAdapter.SetMobileJoystickVisiblePreference(data.touchscreenJoystickVisible);

        ApplyAndroidDisplayMode(data);
    }

    public static void ApplySavedGraphicsToDungeonGenerator(DungeonGenerator dungeonGenerator)
    {
        if (dungeonGenerator == null || !TryLoad(out Data data))
            return;

        ApplyGraphicsLevelToDungeonGenerator(dungeonGenerator, data.graphicsLevel);
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
                cfg.usePerlin = false;
                cfg.perlinWavelength = 0.25f;
                cfg.perlin2Wavelength = 0.01f;
                cfg.perlin2Amplitude = 1f;
                cfg.perlinThreshold = 0.45f;
                cfg.maxElevation = 0;
                cfg.perlinFloorHeights = 0;
                cfg.cellularFillPercent = 45;
                cfg.useDiagonalCorners = false;
                cfg.mapHeight = 25;                 // small house map
                cfg.mapWidth = 25;
                break;
            case MapType.Yard:
                cfg.RoomAlgorithm = DungeonSettings.RoomAlgorithm_e.Scatter_NoOverlap;
                cfg.generateOverlappingRooms = false;
                cfg.useCellularAutomata = false;
                cfg.useScatterRooms = true;
                cfg.usePerlin = false;
                cfg.usePackedRooms = false;
                cfg.useDiagonalCorners = false;
                cfg.maxElevation = 0;
//                cfg.perlinFloorHeights = 50;
//                cfg.cellularFillPercent = 45;
                cfg.mapHeight = 50;
                cfg.mapWidth = 50;
                cfg.useScatterRooms = true;
                cfg.roomAttempts = 10;
                cfg.roomsMax = 5;
                cfg.minRoomSize = 5;
                cfg.maxRoomSize = 15;
                cfg.generateOverlappingRooms = true;
                cfg.MergeScatteredRooms = true;
                cfg.allowVerticalStacking = false;
                cfg.minVerticalStackHeight = 5;  // less than this results in merged rooms
                cfg.ovalRooms = false;
                break;
            case MapType.DogPark:
                cfg.RoomAlgorithm = DungeonSettings.RoomAlgorithm_e.CellularAutomataPerlin;
                cfg.generateOverlappingRooms = false;
                cfg.useCellularAutomata = true;
                cfg.useScatterRooms = false;
                cfg.usePerlin = true;
                cfg.perlinWavelength = 0.25f;
                cfg.perlin2Wavelength = 0.01f;
                cfg.perlin2Amplitude = .5f;
                cfg.perlinThreshold = 0.55f;
                cfg.usePackedRooms = false;
                cfg.useDiagonalCorners = true;
                cfg.maxElevation = 100;
                cfg.perlinFloorHeights = 50;
                cfg.cellularFillPercent = 45;
                cfg.mapHeight = 50;
                cfg.mapWidth = 50;
                break;
            case MapType.Forest:
                cfg.RoomAlgorithm = DungeonSettings.RoomAlgorithm_e.CellularAutomataPerlin;
                cfg.generateOverlappingRooms = false;
                cfg.useCellularAutomata = true;
                cfg.useScatterRooms = false;
                cfg.usePerlin = true;
                cfg.perlinWavelength = 0.25f;
                cfg.perlin2Wavelength = 0.01f;
                cfg.perlin2Amplitude = 1f;
                cfg.perlinThreshold = 0.45f;
                cfg.usePackedRooms = false;
                cfg.useDiagonalCorners = true;
                cfg.maxElevation = 100;
                cfg.perlinFloorHeights = 50;
                cfg.cellularFillPercent = 45;
                cfg.mapHeight = 75;
                cfg.mapWidth = 75;
                break;
            case MapType.Castle:
                cfg.RoomAlgorithm = DungeonSettings.RoomAlgorithm_e.PackedRooms;
                cfg.useCellularAutomata = false;
                cfg.useScatterRooms = false;
                cfg.usePackedRooms = true;
                cfg.usePerlin = false;
                cfg.perlinWavelength = 0.25f;
                cfg.perlin2Wavelength = 0.01f;
                cfg.perlin2Amplitude = 1f;
                cfg.perlinThreshold = 0.45f;
                cfg.maxElevation = 0;
                cfg.perlinFloorHeights = 0;
                cfg.cellularFillPercent = 45;
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
            ApplyGraphicsLevelToDungeonGenerator(dungeonGenerator, normalized.graphicsLevel);

        NewInputAdapter inputAdapter = GetInputAdapter();
        if (inputAdapter != null)
            inputAdapter.SetMobileJoystickVisiblePreference(normalized.touchscreenJoystickVisible);

        ApplyAndroidDisplayMode(normalized);
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

        bool hasTouchscreenJoystickVisible = json.Contains(TouchscreenJoystickVisibleJsonField, StringComparison.Ordinal);
        bool hasButtonSize = json.Contains(ButtonSizeJsonField, StringComparison.Ordinal);
        bool hasAndroidFullscreen = json.Contains(AndroidFullscreenJsonField, StringComparison.Ordinal);
        data = JsonUtility.FromJson<Data>(json);
        if (data == null)
            return false;

        if (!hasTouchscreenJoystickVisible)
            data.touchscreenJoystickVisible = true;
        if (!hasButtonSize)
            data.buttonSize = DefaultButtonSize;
        if (!hasAndroidFullscreen)
            data.androidFullscreenEnabled = false;

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

        NewInputAdapter inputAdapter = GetInputAdapter();
        if (inputAdapter != null)
            data.touchscreenJoystickVisible = inputAdapter.MobileJoystickVisiblePreference;

        DungeonGenerator dungeonGenerator = GetDungeonGenerator();
        if (dungeonGenerator != null)
        {
            data.wallpaperEnabled = dungeonGenerator.ApplyWallpaperOnWallTiles;
            data.graphicsLevel = data.wallpaperEnabled ? GraphicsLevelHigh : GraphicsLevelLow;
        }

        return Normalize(data);
    }

    public static bool IsAndroidDevice()
    {
        return Application.platform == RuntimePlatform.Android;
    }

    public static bool ShouldShowAndroidDisplayModeSetting()
    {
        return IsAndroidDevice() || Application.isEditor;
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

        if (data.graphicsLevel == 0)
            data.graphicsLevel = data.wallpaperEnabled ? GraphicsLevelHigh : GraphicsLevelLow;

        data.graphicsLevel = SnapGraphicsLevel(data.graphicsLevel);
        data.wallpaperEnabled = data.graphicsLevel >= GraphicsLevelMedium;
        data.buttonSize = SnapButtonSize(data.buttonSize);

        return data;
    }

    private static void ApplyAndroidDisplayMode(Data data)
    {
        if (!ShouldShowAndroidDisplayModeSetting())
            return;

        Screen.fullScreen = Normalize(data).androidFullscreenEnabled;
    }

    public static float SnapButtonSize(float value)
    {
        return Mathf.Clamp(Mathf.Round(value), MinButtonSize, MaxButtonSize);
    }

    public static int SnapGraphicsLevel(float value)
    {
        if (value < (GraphicsLevelLow + GraphicsLevelMedium) * 0.5f)
            return GraphicsLevelLow;

        if (value < (GraphicsLevelMedium + GraphicsLevelHigh) * 0.5f)
            return GraphicsLevelMedium;

        return GraphicsLevelHigh;
    }

    public static string GetGraphicsLevelLabel(int graphicsLevel)
    {
        switch (SnapGraphicsLevel(graphicsLevel))
        {
            case GraphicsLevelLow:
                return "1985";
            case GraphicsLevelMedium:
                return "1990";
            case GraphicsLevelHigh:
            default:
                return "1995";
        }
    }

    private static void ApplyGraphicsLevelToDungeonGenerator(DungeonGenerator dungeonGenerator, int graphicsLevel)
    {
        if (dungeonGenerator == null)
            return;

        int snappedLevel = SnapGraphicsLevel(graphicsLevel);
        DungeonSettings cfg = dungeonGenerator.cfg;

        dungeonGenerator.ApplyWallpaperOnWallTiles = snappedLevel >= GraphicsLevelMedium;

        switch (snappedLevel)
        {
            case GraphicsLevelLow:
                dungeonGenerator.checkerFloorStrength = 0f;
                dungeonGenerator.ConfigureSurfaceOptimization(mergeFlatSurfaceTiles: true, mergeContinuousWalls: true);
                if (cfg != null)
                {
                    cfg.enableTiltedTiles = false;
                    cfg.tiltFloorTilesMaxAngle = 0;
                }
                ApplyRenderResolutionScale(0.5f);
                QualitySettings.antiAliasing = 0;
                QualitySettings.lodBias = 0.6f;
                break;

            case GraphicsLevelMedium:
                dungeonGenerator.checkerFloorStrength = 0.15f;
                dungeonGenerator.ConfigureSurfaceOptimization(mergeFlatSurfaceTiles: true, mergeContinuousWalls: true);
                if (cfg != null)
                {
                    cfg.enableTiltedTiles = true;
                    cfg.tiltFloorTilesMaxAngle = 20;
                }
                ApplyRenderResolutionScale(0.75f);
                QualitySettings.antiAliasing = 2;
                QualitySettings.lodBias = 1f;
                break;

            case GraphicsLevelHigh:
            default:
                dungeonGenerator.checkerFloorStrength = 0.25f;
                dungeonGenerator.ConfigureSurfaceOptimization(mergeFlatSurfaceTiles: false, mergeContinuousWalls: false);
                if (cfg != null)
                {
                    cfg.enableTiltedTiles = true;
                    cfg.tiltFloorTilesMaxAngle = 45;
                }
                ApplyRenderResolutionScale(1f);
                QualitySettings.antiAliasing = 4;
                QualitySettings.lodBias = 2f;
                break;
        }
    }

    private static void ApplyRenderResolutionScale(float scale)
    {
        float clampedScale = Mathf.Clamp(scale, 0.5f, 1f);
        ScalableBufferManager.ResizeBuffers(clampedScale, clampedScale);
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

    private static NewInputAdapter GetInputAdapter()
    {
        return UnityEngine.Object.FindFirstObjectByType<NewInputAdapter>(FindObjectsInactive.Include);
    }
}
