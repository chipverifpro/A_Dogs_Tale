using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    [SerializeField] private string wallpaperResourcesPath = "Sprites/Wallpaper";
    [SerializeField] private string wallpaperResourcesPath_mirror = "Sprites/Wallpaper_Mirror";

    private readonly Dictionary<int, Texture2D> roomWallpaperByRoomIndex = new();
    private readonly Dictionary<int, Texture2D> roomWallpaperMirrorByRoomIndex = new();
    private readonly Dictionary<int, string> roomWallpaperKeyByRoomIndex = new();
    private readonly Dictionary<string, WallpaperRoomRule> wallpaperRoomRulesByWallpaperKey = new(System.StringComparer.OrdinalIgnoreCase);
    private Texture2D[] cachedWallpaperTextures;
    private Texture2D[] cachedWallpaperTexturesMirror;
    private Dictionary<string, Texture2D> cachedWallpaperTexturesByKey;
    private Dictionary<string, Texture2D> cachedWallpaperTexturesMirrorByKey;
    private string[] cachedWallpaperSelectionKeys;

    [Header("Wall Appearance")]
    [Tooltip("Enable applying wallpaper textures to generated wall tiles.")]
    [SerializeField] private bool applyWallpaperOnWallTiles = true;

    public bool ApplyWallpaperOnWallTiles
    {
        get => applyWallpaperOnWallTiles;
        set => applyWallpaperOnWallTiles = value;
    }

    private sealed class WallpaperRoomRule
    {
        public readonly string label;
        public readonly PlacementRoomTypeFlags allowedRooms;

        public WallpaperRoomRule(string label, PlacementRoomTypeFlags allowedRooms)
        {
            this.label = label;
            this.allowedRooms = allowedRooms;
        }
    }

    private void ConfigureWallpaperRoomRules()
    {
        wallpaperRoomRulesByWallpaperKey.Clear();

        // Add code-only wallpaper room mappings here. Name is the wallpaper asset filename/key.
        // Example:
        // SetWallPaper("WP_ForestBirch", PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.YARD);
        SetWallPaper("WP_AbstractTriangleSquare", PlacementRoomTypeFlags.INDOOR);
        SetWallPaper("WP_BambooMat",        PlacementRoomTypeFlags.INDOOR);
        SetWallPaper("WP_BambooMatGreen",   PlacementRoomTypeFlags.INDOOR | PlacementRoomTypeFlags.OUTDOOR);
        SetWallPaper("WP_BambooSticks",     PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.YARD);
        SetWallPaper("WP_BambooTrees",      PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.YARD);
        SetWallPaper("WP_Beach",            PlacementRoomTypeFlags.OUTDOOR);
        SetWallPaper("WP_Brick1",           PlacementRoomTypeFlags.INDOOR | PlacementRoomTypeFlags.OUTDOOR);
        SetWallPaper("WP_Cars",             PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Cavern1",          PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Cavern2",          PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Cavern3",          PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Circuit1",         PlacementRoomTypeFlags.Utility | PlacementRoomTypeFlags.Library | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Clouds",           PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_ColorfulAbstractSquares", PlacementRoomTypeFlags.INDOOR);
        SetWallPaper("WP_CuteAnimals",      PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_DeepSpace2",       PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Library | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Dinosaur1",        PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Library | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Dinosaurs2",       PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Library | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Dinosaurs3",       PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Library | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Dunes",            PlacementRoomTypeFlags.OUTDOOR);
        SetWallPaper("WP_Fireplace",        PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Library | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_FirTrees",         PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.Wooded);
        SetWallPaper("WP_FlowersBelowChairRail", PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_ForestAspen",      PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.Wooded);
        SetWallPaper("WP_ForestBirch",      PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.Wooded);
        SetWallPaper("WP_ForestPine",       PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.Wooded);
        SetWallPaper("WP_Geometric1",       PlacementRoomTypeFlags.INDOOR);
        SetWallPaper("WP_Geometric2",       PlacementRoomTypeFlags.INDOOR);
        SetWallPaper("WP_Geometric3",       PlacementRoomTypeFlags.INDOOR);
        SetWallPaper("WP_GreenWithChairRail", PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_HexTiles",         PlacementRoomTypeFlags.Kitchen | PlacementRoomTypeFlags.Bathroom | PlacementRoomTypeFlags.Utility | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_IntricateMat",     PlacementRoomTypeFlags.INDOOR);
        SetWallPaper("WP_JungleFlowers",    PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.Garden | PlacementRoomTypeFlags.Wooded);
        SetWallPaper("WP_JungleWall",       PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.Garden | PlacementRoomTypeFlags.Wooded);
        SetWallPaper("WP_Laser",            PlacementRoomTypeFlags.Utility | PlacementRoomTypeFlags.Library | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Library1",         PlacementRoomTypeFlags.Library | PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Library2",         PlacementRoomTypeFlags.Library | PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Monkeys",          PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.Wooded);
        SetWallPaper("WP_Park1",            PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.YARD);
        SetWallPaper("WP_Park2",            PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.YARD);
        SetWallPaper("WP_PineTrees",        PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.Wooded);
        SetWallPaper("WP_PipesAndValves",   PlacementRoomTypeFlags.Utility | PlacementRoomTypeFlags.Kitchen | PlacementRoomTypeFlags.Bathroom);
        SetWallPaper("WP_Plumeria",         PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.Garden);
        SetWallPaper("WP_Puppies1",         PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Puppies2",         PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Robots1",          PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Utility | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Robots2",          PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Utility | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_SandstoneWall",    PlacementRoomTypeFlags.INDOOR | PlacementRoomTypeFlags.OUTDOOR);
        SetWallPaper("WP_Space1",           PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Library | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Spaceships1",      PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Library | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Spaceships2",      PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Library | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Spat",             PlacementRoomTypeFlags.INDOOR);
        SetWallPaper("WP_Steampunk",        PlacementRoomTypeFlags.Library | PlacementRoomTypeFlags.Utility | PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_TanTextured1",     PlacementRoomTypeFlags.INDOOR);
        SetWallPaper("WP_TanTextured2",     PlacementRoomTypeFlags.INDOOR);
        SetWallPaper("WP_TeddyBears1",      PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_TeddyBears2",      PlacementRoomTypeFlags.Bedroom | PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_TexturedTan",      PlacementRoomTypeFlags.INDOOR);
        SetWallPaper("WP_Tile1",            PlacementRoomTypeFlags.Kitchen | PlacementRoomTypeFlags.Bathroom | PlacementRoomTypeFlags.Utility | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_Tile2",            PlacementRoomTypeFlags.Kitchen | PlacementRoomTypeFlags.Bathroom | PlacementRoomTypeFlags.Utility | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_TileGreyAndTan",   PlacementRoomTypeFlags.Kitchen | PlacementRoomTypeFlags.Bathroom | PlacementRoomTypeFlags.Utility | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_TileWithFlowers",  PlacementRoomTypeFlags.Kitchen | PlacementRoomTypeFlags.Bathroom | PlacementRoomTypeFlags.Garden | PlacementRoomTypeFlags.Generic);
        SetWallPaper("WP_VineWall",         PlacementRoomTypeFlags.OUTDOOR | PlacementRoomTypeFlags.Garden | PlacementRoomTypeFlags.PicnicStructure);

    }

    private void SetWallPaper(string wallpaperName, PlacementRoomTypeFlags allowedRooms)
    {
        string wallpaperKey = NormalizeWallpaperRuleKey(wallpaperName);
        if (string.IsNullOrEmpty(wallpaperKey))
        {
            Debug.LogWarning("Ignoring wallpaper room rule with empty wallpaper name.", this);
            return;
        }

        wallpaperRoomRulesByWallpaperKey[wallpaperKey] = new WallpaperRoomRule(wallpaperKey, allowedRooms);
    }

    private void SetWallpaper(string wallpaperName, PlacementRoomTypeFlags allowedRooms)
    {
        SetWallPaper(wallpaperName, allowedRooms);
    }

    private static string NormalizeWallpaperRuleKey(string wallpaperName)
    {
        return string.IsNullOrWhiteSpace(wallpaperName) ? string.Empty : wallpaperName.Trim();
    }

    private Texture2D GetWallpaperTextureForRoom(int roomIndex, bool useMirror = false)
    {
        Dictionary<int, Texture2D> wallpaperCacheByRoom = useMirror
            ? roomWallpaperMirrorByRoomIndex
            : roomWallpaperByRoomIndex;

        if (wallpaperCacheByRoom.TryGetValue(roomIndex, out Texture2D cachedWallpaper))
            return cachedWallpaper;

        string[] wallpaperKeys = GetAvailableWallpaperKeysForRoom(roomIndex);
        if (wallpaperKeys == null || wallpaperKeys.Length == 0)
            return null;

        if (!roomWallpaperKeyByRoomIndex.TryGetValue(roomIndex, out string wallpaperKey))
        {
            int wallpaperIndex = GetDeterministicWallpaperIndex(roomIndex, wallpaperKeys.Length);
            wallpaperKey = wallpaperKeys[wallpaperIndex];
            roomWallpaperKeyByRoomIndex[roomIndex] = wallpaperKey;
        }

        Texture2D selectedWallpaper = ResolveWallpaperTextureForKey(wallpaperKey, useMirror);
        wallpaperCacheByRoom[roomIndex] = selectedWallpaper;
        return selectedWallpaper;
    }

    private string[] GetAvailableWallpaperKeys()
    {
        EnsureWallpaperTextureCache();
        return cachedWallpaperSelectionKeys;
    }

    private string[] GetAvailableWallpaperKeysForRoom(int roomIndex)
    {
        string[] allWallpaperKeys = GetAvailableWallpaperKeys();
        if (allWallpaperKeys == null || allWallpaperKeys.Length == 0)
            return allWallpaperKeys;

        Room room = roomIndex >= 0 && rooms != null && roomIndex < rooms.Count
            ? rooms[roomIndex]
            : null;
        if (room == null)
            return allWallpaperKeys;

        List<string> compatibleWallpaperKeys = new();
        for (int keyIndex = 0; keyIndex < allWallpaperKeys.Length; keyIndex++)
        {
            string wallpaperKey = allWallpaperKeys[keyIndex];
            if (AllowsWallpaperInRoom(wallpaperKey, room.placementTypes))
                compatibleWallpaperKeys.Add(allWallpaperKeys[keyIndex]);
        }

        return compatibleWallpaperKeys.ToArray();
    }

    private bool AllowsWallpaperInRoom(string wallpaperKey, PlacementRoomTypeFlags roomFlags)
    {
        if (string.IsNullOrWhiteSpace(wallpaperKey) ||
            !wallpaperRoomRulesByWallpaperKey.TryGetValue(wallpaperKey, out WallpaperRoomRule rule))
        {
            return true;
        }

        if (rule.allowedRooms == PlacementRoomTypeFlags.Any)
            return true;

        if (rule.allowedRooms == PlacementRoomTypeFlags.None)
            return false;

        return (rule.allowedRooms & roomFlags) != 0;
    }

    private int GetDeterministicWallpaperIndex(int roomIndex, int wallpaperCount)
    {
        unchecked
        {
            int seed = cfg != null ? cfg.seed : 0;
            int hash = seed;
            hash = (hash * 397) ^ roomIndex;
            hash ^= unchecked((int)0x9e3779b9u);
            if (hash < 0)
                hash = ~hash;

            return wallpaperCount > 0 ? hash % wallpaperCount : 0;
        }
    }
}
