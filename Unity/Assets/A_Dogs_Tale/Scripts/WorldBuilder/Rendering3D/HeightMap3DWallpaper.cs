using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    [SerializeField] private string wallpaperResourcesPath = "Sprites/Wallpaper";
    [SerializeField] private string wallpaperResourcesPath_mirror = "Sprites/Wallpaper_Mirror";

    private readonly Dictionary<int, Texture2D> roomWallpaperByRoomIndex = new();
    private readonly Dictionary<int, Texture2D> roomWallpaperMirrorByRoomIndex = new();
    private readonly Dictionary<int, string> roomWallpaperKeyByRoomIndex = new();
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

    private Texture2D GetWallpaperTextureForRoom(int roomIndex, bool useMirror = false)
    {
        Dictionary<int, Texture2D> wallpaperCacheByRoom = useMirror
            ? roomWallpaperMirrorByRoomIndex
            : roomWallpaperByRoomIndex;

        if (wallpaperCacheByRoom.TryGetValue(roomIndex, out Texture2D cachedWallpaper))
            return cachedWallpaper;

        string[] wallpaperKeys = GetAvailableWallpaperKeys();
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
