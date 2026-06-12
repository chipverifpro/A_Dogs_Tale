using System;
using System.IO;
using DogGame.LLM;
using DogGame.Modules;
using UnityEngine;
using static DungeonSettings;

/// <summary>
/// Stores a small set of user-adjustable runtime settings and reapplies them on launch.
/// </summary>
public static class PersistentGameSettings
{
    private const string PlayerPrefsKey = "A_Dogs_Tale.PersistentGameSettings";
    private const string TouchscreenJoystickVisibleJsonField = "\"touchscreenJoystickVisible\"";
    private const string DigitalJoystickJsonField = "\"digitalJoystick\"";
    private const string ShowCarriedModeJsonField = "\"showCarriedMode\"";
    private const string ButtonSizeJsonField = "\"buttonSize\"";
    private const string AndroidFullscreenJsonField = "\"androidFullscreenEnabled\"";
    private const string MusicEnabledJsonField = "\"musicEnabled\"";
    private const string MusicVolumeJsonField = "\"musicVolume\"";
    private const string SfxEnabledJsonField = "\"sfxEnabled\"";
    private const string SfxVolumeJsonField = "\"sfxVolume\"";
    private const string UiEnabledJsonField = "\"uiEnabled\"";
    private const string UiVolumeJsonField = "\"uiVolume\"";
    private const string MistralEnabledJsonField = "\"mistralEnabled\"";
    private const string LocalQwenEnabledJsonField = "\"localQwenEnabled\"";
    private const string LocalGemmaEnabledJsonField = "\"localGemmaEnabled\"";
    private const string LocalMistralEnabledJsonField = "\"localMistralEnabled\"";
    private const float MinScentSimulationTimeStep = 0.1f;
    private const float MaxScentSimulationTimeStep = 1.0f;
    public const float MinButtonSize = 40f;
    public const float MaxButtonSize = 250f;
    public const float DefaultButtonSize = 176f;
    public const int GraphicsLevelLow = 1985;
    public const int GraphicsLevelMedium = 1990;
    public const int GraphicsLevelHigh = 1995;
    public const string DefaultChatGptModelName = "gpt-4.1-nano";
    public const string DefaultGeminiModelName = "gemini-3.1-flash-lite";
    public const string DefaultMistralModelName = "ministral-3b-2512";
    public const string DefaultLocalQwenModelName = "qwen3:0.6b";
    public const string DefaultLocalGemmaModelName = "gemma3:270m";
    public const string DefaultLocalMistralModelName = "ministral-3:3b-instruct-2512-q4_K_M";

    public enum MapType
    {
        House = 0,
        Yard = 1,
        DogPark = 2,
        Forest = 3,
        Castle = 4,
    }

    public enum ShowCarriedMode
    {
        All = 0,
        PackOnly = 1,
        None = 2,
    }

    [Serializable]
    public class Data
    {
        public MapType mapType = MapType.House;
        public bool chatGptEnabled = true;
        public bool geminiEnabled = true;
        public bool mistralEnabled = false;
        public bool ollamaEnabled = true;
        public bool localQwenEnabled = true;
        public bool localGemmaEnabled = false;
        public bool localMistralEnabled = false;
        public string chatGptModelName = DefaultChatGptModelName;
        public string geminiModelName = DefaultGeminiModelName;
        public string mistralModelName = DefaultMistralModelName;
        public string localQwenModelName = DefaultLocalQwenModelName;
        public string localGemmaModelName = DefaultLocalGemmaModelName;
        public string localMistralModelName = DefaultLocalMistralModelName;
        public float scentSimulationTimeStep = MinScentSimulationTimeStep;
        public int graphicsLevel = GraphicsLevelHigh;
        public bool wallpaperEnabled = true;
        public bool musicEnabled = true;
        public float musicVolume = 1f;
        public bool sfxEnabled = true;
        public float sfxVolume = 1f;
        public bool uiEnabled = true;
        public float uiVolume = 1f;
        public bool touchscreenJoystickVisible = true;
        public bool digitalJoystick = false;
        public ShowCarriedMode showCarriedMode = ShowCarriedMode.All;
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
        {
            inputAdapter.SetMobileJoystickVisiblePreference(data.touchscreenJoystickVisible);
            inputAdapter.SetDigitalMobileJoystickPreference(data.digitalJoystick);
        }

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
                cfg.packedRoomTheme = PackedRoomTheme_e.House;
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
                cfg.packedRoomTheme = PackedRoomTheme_e.Park;
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
                cfg.packedRoomTheme = PackedRoomTheme_e.Park;
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
                cfg.packedRoomTheme = PackedRoomTheme_e.Park;
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
                cfg.packedRoomTheme = PackedRoomTheme_e.House;
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
        {
            inputAdapter.SetMobileJoystickVisiblePreference(normalized.touchscreenJoystickVisible);
            inputAdapter.SetDigitalMobileJoystickPreference(normalized.digitalJoystick);
        }

        AudioPlayer audioPlayer = GetAudioPlayer();
        if (audioPlayer != null)
            audioPlayer.ApplySoundSettings(
                normalized.musicEnabled,
                normalized.musicVolume,
                normalized.sfxEnabled,
                normalized.sfxVolume,
                normalized.uiEnabled,
                normalized.uiVolume);

        ApplyShowCarriedModeToContainers();
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
        bool hasDigitalJoystick = json.Contains(DigitalJoystickJsonField, StringComparison.Ordinal);
        bool hasShowCarriedMode = json.Contains(ShowCarriedModeJsonField, StringComparison.Ordinal);
        bool hasButtonSize = json.Contains(ButtonSizeJsonField, StringComparison.Ordinal);
        bool hasAndroidFullscreen = json.Contains(AndroidFullscreenJsonField, StringComparison.Ordinal);
        bool hasMusicEnabled = json.Contains(MusicEnabledJsonField, StringComparison.Ordinal);
        bool hasMusicVolume = json.Contains(MusicVolumeJsonField, StringComparison.Ordinal);
        bool hasSfxEnabled = json.Contains(SfxEnabledJsonField, StringComparison.Ordinal);
        bool hasSfxVolume = json.Contains(SfxVolumeJsonField, StringComparison.Ordinal);
        bool hasUiEnabled = json.Contains(UiEnabledJsonField, StringComparison.Ordinal);
        bool hasUiVolume = json.Contains(UiVolumeJsonField, StringComparison.Ordinal);
        bool hasMistralEnabled = json.Contains(MistralEnabledJsonField, StringComparison.Ordinal);
        bool hasLocalQwenEnabled = json.Contains(LocalQwenEnabledJsonField, StringComparison.Ordinal);
        bool hasLocalGemmaEnabled = json.Contains(LocalGemmaEnabledJsonField, StringComparison.Ordinal);
        bool hasLocalMistralEnabled = json.Contains(LocalMistralEnabledJsonField, StringComparison.Ordinal);
        data = JsonUtility.FromJson<Data>(json);
        if (data == null)
            return false;

        if (!hasTouchscreenJoystickVisible)
            data.touchscreenJoystickVisible = true;
        if (!hasDigitalJoystick)
            data.digitalJoystick = false;
        if (!hasShowCarriedMode)
            data.showCarriedMode = ShowCarriedMode.All;
        if (!hasButtonSize)
            data.buttonSize = DefaultButtonSize;
        if (!hasAndroidFullscreen)
            data.androidFullscreenEnabled = false;
        if (!hasMusicEnabled)
            data.musicEnabled = true;
        if (!hasMusicVolume)
            data.musicVolume = 1f;
        if (!hasSfxEnabled)
            data.sfxEnabled = true;
        if (!hasSfxVolume)
            data.sfxVolume = 1f;
        if (!hasUiEnabled)
            data.uiEnabled = true;
        if (!hasUiVolume)
            data.uiVolume = 1f;
        if (!hasMistralEnabled)
            data.mistralEnabled = false;
        if (!hasLocalQwenEnabled)
            data.localQwenEnabled = data.ollamaEnabled;
        if (!hasLocalGemmaEnabled)
            data.localGemmaEnabled = false;
        if (!hasLocalMistralEnabled)
            data.localMistralEnabled = false;

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
            data.mistralEnabled = HasModelEnabled(scheduler.llmVendorAndModel, LLMVendorAndModel.Mistral_mistral_small_latest);
            data.localQwenEnabled = HasModelEnabled(scheduler.llmVendorAndModel, LLMVendorAndModel.Ollama_Qwen2_5_1_5b);
            data.localGemmaEnabled = HasModelEnabled(scheduler.llmVendorAndModel, LLMVendorAndModel.Ollama_Gemma3);
            data.localMistralEnabled = HasModelEnabled(scheduler.llmVendorAndModel, LLMVendorAndModel.Ollama_Mistral);
            data.ollamaEnabled = data.localQwenEnabled || data.localGemmaEnabled || data.localMistralEnabled;
        }

        ScentAirGround scentAirGround = GetScentAirGround();
        if (scentAirGround != null)
            data.scentSimulationTimeStep = scentAirGround.SimulationTimeStep;

        NewInputAdapter inputAdapter = GetInputAdapter();
        if (inputAdapter != null)
        {
            data.touchscreenJoystickVisible = inputAdapter.MobileJoystickVisiblePreference;
            data.digitalJoystick = inputAdapter.DigitalMobileJoystickPreference;
        }

        DungeonGenerator dungeonGenerator = GetDungeonGenerator();
        if (dungeonGenerator != null)
        {
            data.wallpaperEnabled = dungeonGenerator.ApplyWallpaperOnWallTiles;
            data.graphicsLevel = data.wallpaperEnabled ? GraphicsLevelHigh : GraphicsLevelLow;
        }

        AudioPlayer audioPlayer = GetAudioPlayer();
        if (audioPlayer != null)
        {
            data.musicEnabled = audioPlayer.musicEnabled;
            data.musicVolume = audioPlayer.musicVolume;
            data.sfxEnabled = audioPlayer.sfxEnabled;
            data.sfxVolume = audioPlayer.sfxVolume;
            data.uiEnabled = audioPlayer.uiEnabled;
            data.uiVolume = audioPlayer.uiVolume;
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

        if (!Enum.IsDefined(typeof(ShowCarriedMode), data.showCarriedMode))
            data.showCarriedMode = ShowCarriedMode.All;

        data.scentSimulationTimeStep = Mathf.Clamp(
            Mathf.Round(data.scentSimulationTimeStep * 10f) / 10f,
            MinScentSimulationTimeStep,
            MaxScentSimulationTimeStep);

        if (data.graphicsLevel == 0)
            data.graphicsLevel = data.wallpaperEnabled ? GraphicsLevelHigh : GraphicsLevelLow;

        data.graphicsLevel = SnapGraphicsLevel(data.graphicsLevel);
        data.wallpaperEnabled = data.graphicsLevel >= GraphicsLevelMedium;
        data.musicVolume = Mathf.Clamp01(data.musicVolume);
        data.sfxVolume = Mathf.Clamp01(data.sfxVolume);
        data.uiVolume = Mathf.Clamp01(data.uiVolume);
        data.buttonSize = SnapButtonSize(data.buttonSize);
        data.ollamaEnabled = data.localQwenEnabled || data.localGemmaEnabled || data.localMistralEnabled;
        data.chatGptModelName = NormalizeModelName(data.chatGptModelName, DefaultChatGptModelName);
        data.geminiModelName = NormalizeModelName(data.geminiModelName, DefaultGeminiModelName);
        data.mistralModelName = NormalizeModelName(data.mistralModelName, DefaultMistralModelName);
        data.localQwenModelName = NormalizeModelName(data.localQwenModelName, DefaultLocalQwenModelName);
        data.localGemmaModelName = NormalizeModelName(data.localGemmaModelName, DefaultLocalGemmaModelName);
        data.localMistralModelName = NormalizeModelName(data.localMistralModelName, DefaultLocalMistralModelName);

        return data;
    }

    public static bool ShouldShowCarriedItemsForCarrier(WorldObject carrier)
    {
        return ShouldShowCarriedItemsForCarrier(carrier, GetCurrentOrSaved().showCarriedMode);
    }

    public static bool ShouldShowCarriedItemsForCarrier(WorldObject carrier, ShowCarriedMode mode)
    {
        switch (mode)
        {
            case ShowCarriedMode.None:
                return false;
            case ShowCarriedMode.PackOnly:
                return IsInPlayerPack(carrier);
            case ShowCarriedMode.All:
            default:
                return true;
        }
    }

    private static bool IsInPlayerPack(WorldObject carrier)
    {
        if (carrier == null || carrier.packMemberModule == null)
            return false;

        Dir dir = Dir.Instance;
        Pack playerPack = dir != null ? dir.playerPack : null;
        return playerPack != null && carrier.packMemberModule.currentPack == playerPack;
    }

    private static void ApplyShowCarriedModeToContainers()
    {
        WorldObjectRegistry registry = UnityEngine.Object.FindFirstObjectByType<WorldObjectRegistry>();
        if (registry == null)
            return;

        foreach (WorldObject worldObject in registry.GetAllObjects())
        {
            if (worldObject != null && worldObject.containerModule != null)
                worldObject.containerModule.RefreshHeldItems();
        }
    }

    private static string NormalizeModelName(string modelName, string defaultModelName)
    {
        return string.IsNullOrWhiteSpace(modelName) ? defaultModelName : modelName.Trim();
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
        if (data.mistralEnabled)
            mask |= LLMVendorAndModel.Mistral_mistral_small_latest;
        if (data.localQwenEnabled)
            mask |= LLMVendorAndModel.Ollama_Qwen2_5_1_5b;
        if (data.localGemmaEnabled)
            mask |= LLMVendorAndModel.Ollama_Gemma3;
        if (data.localMistralEnabled)
            mask |= LLMVendorAndModel.Ollama_Mistral;

        return mask;
    }

    private static bool HasModelEnabled(LLMVendorAndModel mask, LLMVendorAndModel model)
    {
        return (mask & model) != LLMVendorAndModel.None;
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
        if (name.StartsWith("Mistral_", StringComparison.Ordinal))
            return LLMVendor.Mistral;
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

    private static AudioPlayer GetAudioPlayer()
    {
        return Dir.Instance?.audioPlayer
            ?? AudioPlayer.Instance
            ?? UnityEngine.Object.FindFirstObjectByType<AudioPlayer>(FindObjectsInactive.Include);
    }
}
