using System;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    private void ResetWallpaperTextureCache()
    {
        roomWallpaperByRoomIndex.Clear();
        roomWallpaperMirrorByRoomIndex.Clear();
        roomWallpaperKeyByRoomIndex.Clear();
        cachedWallpaperTextures = null;
        cachedWallpaperTexturesMirror = null;
        cachedWallpaperTexturesByKey = null;
        cachedWallpaperTexturesMirrorByKey = null;
        cachedWallpaperSelectionKeys = null;
        ConfigureWallpaperRoomRules();
    }

    private Texture2D[] GetAvailableWallpaperTextures(bool useMirror = false)
    {
        EnsureWallpaperTextureCache();
        return useMirror ? cachedWallpaperTexturesMirror : cachedWallpaperTextures;
    }

    private void EnsureWallpaperTextureCache()
    {
        if (cachedWallpaperSelectionKeys != null)
            return;

        cachedWallpaperTexturesByKey = LoadWallpaperTexturesByKey(wallpaperResourcesPath);
        cachedWallpaperTexturesMirrorByKey = LoadWallpaperTexturesByKey(wallpaperResourcesPath_mirror);

        List<string> sharedKeys = new();
        foreach (KeyValuePair<string, Texture2D> entry in cachedWallpaperTexturesByKey)
        {
            if (cachedWallpaperTexturesMirrorByKey.ContainsKey(entry.Key))
                sharedKeys.Add(entry.Key);
        }

        sharedKeys.Sort(StringComparer.OrdinalIgnoreCase);

        List<string> selectionKeys = sharedKeys;
        if (selectionKeys.Count == 0)
        {
            selectionKeys = new List<string>(cachedWallpaperTexturesByKey.Keys);
            if (selectionKeys.Count == 0)
                selectionKeys.AddRange(cachedWallpaperTexturesMirrorByKey.Keys);

            selectionKeys.Sort(StringComparer.OrdinalIgnoreCase);

            if (cachedWallpaperTexturesByKey.Count > 0 && cachedWallpaperTexturesMirrorByKey.Count > 0)
                Debug.LogWarning("Wallpaper textures were found in both normal and mirror folders, but no filenames matched. Falling back to non-paired wallpaper selection.");
        }
        else if (sharedKeys.Count != cachedWallpaperTexturesByKey.Count || sharedKeys.Count != cachedWallpaperTexturesMirrorByKey.Count)
        {
            Debug.Log($"Wallpaper pairing found {sharedKeys.Count} filename matches between Resources/{wallpaperResourcesPath} and Resources/{wallpaperResourcesPath_mirror}. Unmatched files will be skipped so mirrored wallpapers stay paired correctly.");
        }

        cachedWallpaperSelectionKeys = selectionKeys.ToArray();
        cachedWallpaperTextures = BuildWallpaperTextureArray(cachedWallpaperSelectionKeys, useMirror: false);
        cachedWallpaperTexturesMirror = BuildWallpaperTextureArray(cachedWallpaperSelectionKeys, useMirror: true);

        if (cachedWallpaperSelectionKeys.Length == 0)
            Debug.LogWarning($"No wallpaper textures found at Resources/{wallpaperResourcesPath} or Resources/{wallpaperResourcesPath_mirror}.");
    }

    private Dictionary<string, Texture2D> LoadWallpaperTexturesByKey(string resourcesPath)
    {
        Dictionary<string, Texture2D> wallpapersByKey = new(StringComparer.OrdinalIgnoreCase);

        Sprite[] wallpaperSprites = Resources.LoadAll<Sprite>(resourcesPath);
        for (int spriteIndex = 0; spriteIndex < wallpaperSprites.Length; spriteIndex++)
        {
            Sprite sprite = wallpaperSprites[spriteIndex];
            Texture2D texture = sprite != null ? sprite.texture : null;
            string wallpaperKey = GetWallpaperTextureKey(sprite != null ? sprite.name : null, texture);
            if (texture == null || string.IsNullOrWhiteSpace(wallpaperKey) || wallpapersByKey.ContainsKey(wallpaperKey))
                continue;

            wallpapersByKey[wallpaperKey] = texture;
        }

        if (wallpapersByKey.Count == 0)
        {
            Texture2D[] loadedTextures = Resources.LoadAll<Texture2D>(resourcesPath);
            for (int textureIndex = 0; textureIndex < loadedTextures.Length; textureIndex++)
            {
                Texture2D texture = loadedTextures[textureIndex];
                string wallpaperKey = GetWallpaperTextureKey(texture != null ? texture.name : null, texture);
                if (texture == null || string.IsNullOrWhiteSpace(wallpaperKey) || wallpapersByKey.ContainsKey(wallpaperKey))
                    continue;

                wallpapersByKey[wallpaperKey] = texture;
            }
        }

        return wallpapersByKey;
    }

    private Texture2D[] BuildWallpaperTextureArray(string[] wallpaperKeys, bool useMirror)
    {
        List<Texture2D> wallpapers = new();
        for (int keyIndex = 0; keyIndex < wallpaperKeys.Length; keyIndex++)
        {
            Texture2D texture = ResolveWallpaperTextureForKey(wallpaperKeys[keyIndex], useMirror);
            if (texture != null)
                wallpapers.Add(texture);
        }

        return wallpapers.ToArray();
    }

    private Texture2D ResolveWallpaperTextureForKey(string wallpaperKey, bool useMirror)
    {
        if (string.IsNullOrWhiteSpace(wallpaperKey))
            return null;

        Dictionary<string, Texture2D> preferredTextures = useMirror
            ? cachedWallpaperTexturesMirrorByKey
            : cachedWallpaperTexturesByKey;
        Dictionary<string, Texture2D> fallbackTextures = useMirror
            ? cachedWallpaperTexturesByKey
            : cachedWallpaperTexturesMirrorByKey;

        if (preferredTextures != null && preferredTextures.TryGetValue(wallpaperKey, out Texture2D texture))
            return texture;

        if (fallbackTextures != null && fallbackTextures.TryGetValue(wallpaperKey, out texture))
            return texture;

        return null;
    }

    private static string GetWallpaperTextureKey(string assetName, Texture2D texture)
    {
        string wallpaperKey = !string.IsNullOrWhiteSpace(assetName)
            ? assetName.Trim()
            : (texture != null ? texture.name.Trim() : string.Empty);

        return wallpaperKey;
    }
}
