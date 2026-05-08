using System;
using System.Collections.Generic;
using UnityEngine;

public static class SpriteServer
{
    struct SpriteReference
    {
        public SpriteReference(string spriteSheet, int index)
        {
            SpriteSheet = spriteSheet;
            Index = index;
        }

        public string SpriteSheet { get; }
        public int Index { get; }
    }

    static readonly Dictionary<string, string> spriteSheetResourcePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Senses",                "Sprites/SensesSymbolsColor_v4" },
        { "SensesSymbolsColor_v4", "Sprites/SensesSymbolsColor_v4" },
        { "DogEmojiSheetA",        "Sprites/DogEmojiSheetA" },
        { "DogEmojiSheetB",        "Sprites/DogEmojiSheetB" },
        { "DogEmojiSheetC",        "Sprites/DogEmojiSheetC" },
        { "InventoryActionsSheetA","Sprites/InventoryActionsSheetA" },
        { "MapsSpriteSheet",       "Sprites/MapsSpriteSheet" },
        { "ArrowsSpriteSheetA",    "Sprites/ArrowsSpriteSheetA" },
        { "TakeItemSpriteSheetA",  "Sprites/TakeItemSpriteSheetA" },
        { "SpriteSheet_Modes_V3",  "Sprites/SpriteSheet_Modes_V3" },
        { "Speeds",                "Sprites/Speeds" },
        { "PlayAndPause_Dual",     "Sprites/PlayAndPause_Dual" },
        { "DigHoleSpriteA",        "Sprites/DigHoleSpriteA" },
        { "TricksSpritesheet_B",   "Sprites/TricksSpritesheet_B" }
    };

    static readonly Dictionary<string, SpriteReference> knownSprites = new Dictionary<string, SpriteReference>(StringComparer.OrdinalIgnoreCase)
    {
        { NormalizeLookupKey("Inventory"),                new SpriteReference("InventoryActionsSheetA", 2) },
        { NormalizeLookupKey("InventoryButton"),          new SpriteReference("InventoryActionsSheetA", 2) },
        { NormalizeLookupKey("InventoryActionsSheetA_2"), new SpriteReference("InventoryActionsSheetA", 2) },
        { NormalizeLookupKey("UseItem"),    new SpriteReference("InventoryActionsSheetA", 0) },
        { NormalizeLookupKey("EatItem"),    new SpriteReference("InventoryActionsSheetA", 1) },
        { NormalizeLookupKey("GiveItem"),   new SpriteReference("InventoryActionsSheetA", 2) },
        { NormalizeLookupKey("TradeItem"),  new SpriteReference("InventoryActionsSheetA", 3) },
        { NormalizeLookupKey("DropItem"),   new SpriteReference("InventoryActionsSheetA", 4) },
        { NormalizeLookupKey("PickUpItem"), new SpriteReference("InventoryActionsSheetA", 5) },

        { NormalizeLookupKey("BuildProgress"),      new SpriteReference("MapsSpriteSheet", 1) },
        { NormalizeLookupKey("MapBuildProgress"),   new SpriteReference("MapsSpriteSheet", 1) },
        { NormalizeLookupKey("TreasureMap"),        new SpriteReference("MapsSpriteSheet", 0) },
        { NormalizeLookupKey("BlueprintsA"),        new SpriteReference("MapsSpriteSheet", 1) },
        { NormalizeLookupKey("TreasureMapReader"),  new SpriteReference("MapsSpriteSheet", 2) },
        { NormalizeLookupKey("TreasureMapHardHat"), new SpriteReference("MapsSpriteSheet", 3) },
        { NormalizeLookupKey("BlueprintsB"),        new SpriteReference("MapsSpriteSheet", 4) },
        { NormalizeLookupKey("TreasureMapCompass"), new SpriteReference("MapsSpriteSheet", 5) },

        { NormalizeLookupKey("Scent"),   new SpriteReference("Senses", -1) },
        { NormalizeLookupKey("Smell"),   new SpriteReference("Senses", -1) },
        { NormalizeLookupKey("Notice3"), new SpriteReference("Senses", 0) },
        { NormalizeLookupKey("Notice2"), new SpriteReference("Senses", 1) },
        { NormalizeLookupKey("Notice1"), new SpriteReference("Senses", 2) },
        { NormalizeLookupKey("Notice0"), new SpriteReference("Senses", 3) },
        { NormalizeLookupKey("Sound3"),  new SpriteReference("Senses", 4) },
        { NormalizeLookupKey("Sound2"),  new SpriteReference("Senses", 5) },
        { NormalizeLookupKey("Sound1"),  new SpriteReference("Senses", 6) },
        { NormalizeLookupKey("Sound0"),  new SpriteReference("Senses", 7) },
        { NormalizeLookupKey("Vision3"), new SpriteReference("Senses", 8) },
        { NormalizeLookupKey("Vision2"), new SpriteReference("Senses", 9) },
        { NormalizeLookupKey("Vision1"), new SpriteReference("Senses", 10) },
        { NormalizeLookupKey("Vision0"), new SpriteReference("Senses", 11) },
        { NormalizeLookupKey("Smell3"),  new SpriteReference("Senses", 12) },
        { NormalizeLookupKey("Smell2"),  new SpriteReference("Senses", 13) },
        { NormalizeLookupKey("Smell1"),  new SpriteReference("Senses", 14) },
        { NormalizeLookupKey("Smell0"),  new SpriteReference("Senses", 15) },
        
        { NormalizeLookupKey("Play"),   new SpriteReference("PlayAndPause_Dual", 0) },
        { NormalizeLookupKey("Pause"),  new SpriteReference("PlayAndPause_Dual", 1) },
        
        { NormalizeLookupKey("Sneak"), new SpriteReference("Speeds", 0) },
        { NormalizeLookupKey("Walk"),  new SpriteReference("Speeds", 1) },
        { NormalizeLookupKey("Run"),   new SpriteReference("Speeds", 2) },

        { NormalizeLookupKey("Player"),        new SpriteReference("Modes", 0) },
        { NormalizeLookupKey("Follow"),        new SpriteReference("Modes", 1) },
        { NormalizeLookupKey("Explore"),       new SpriteReference("Modes", 2) },
        { NormalizeLookupKey("Hold"),          new SpriteReference("Modes", 3) },
        { NormalizeLookupKey("Wander"),        new SpriteReference("Modes", 4) },
        { NormalizeLookupKey("LLMControlled"), new SpriteReference("Modes", 5) },
        
        { NormalizeLookupKey("DigHole"),   new SpriteReference("DigHoleSpriteA", 0) },
        { NormalizeLookupKey("DigButton"), new SpriteReference("DigHoleSpriteA", 0) },
        
        { NormalizeLookupKey("Fetch"),     new SpriteReference("TricksSpritesheet_B", 0) },
        { NormalizeLookupKey("Stay"),      new SpriteReference("TricksSpritesheet_B", 1) },
        { NormalizeLookupKey("Come"),      new SpriteReference("TricksSpritesheet_B", 2) },
        { NormalizeLookupKey("DropIt"),    new SpriteReference("TricksSpritesheet_B", 3) },
        { NormalizeLookupKey("LeaveIt"),   new SpriteReference("TricksSpritesheet_B", 4) },
        { NormalizeLookupKey("Heel"),      new SpriteReference("TricksSpritesheet_B", 5) },
        { NormalizeLookupKey("Quiet"),     new SpriteReference("TricksSpritesheet_B", 6) },
        { NormalizeLookupKey("Bark"),      new SpriteReference("TricksSpritesheet_B", 7) },
        { NormalizeLookupKey("GoodDog"),   new SpriteReference("TricksSpritesheet_B", 8) },
        { NormalizeLookupKey("BadDog"),    new SpriteReference("TricksSpritesheet_B", 9) },
        { NormalizeLookupKey("FindIt"),    new SpriteReference("TricksSpritesheet_B", 10) },
        { NormalizeLookupKey("RollOver"),  new SpriteReference("TricksSpritesheet_B", 11) },
        { NormalizeLookupKey("Sit"),       new SpriteReference("TricksSpritesheet_B", 12) },
        { NormalizeLookupKey("Down"),      new SpriteReference("TricksSpritesheet_B", 13) },
        { NormalizeLookupKey("Release"),   new SpriteReference("TricksSpritesheet_B", 14) }
    };

    static readonly Dictionary<string, Dictionary<int, Sprite>> spritesBySheet = new Dictionary<string, Dictionary<int, Sprite>>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Dictionary<string, Sprite>> spritesByNameBySheet = new Dictionary<string, Dictionary<string, Sprite>>(StringComparer.OrdinalIgnoreCase);

    public static Sprite SpriteLookup(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
            return null;

        string trimmedName = spriteName.Trim();
        string lookupKey = NormalizeLookupKey(trimmedName);

        if (knownSprites.TryGetValue(lookupKey, out SpriteReference knownReference))
        {
            if (knownReference.Index >= 0)
                return SpriteSheetLookup(knownReference.SpriteSheet, knownReference.Index);

            if (lookupKey == NormalizeLookupKey("Scent") || lookupKey == NormalizeLookupKey("Smell"))
                return SpriteLookup("Sense_Smell_None") ?? SpriteLookup("Sense_Smell_Low") ?? SpriteLookup("Sense_Alert_None");
        }

        if (TryGetEmojiSprite(trimmedName, out Sprite emojiSprite, out _))
            return emojiSprite;

        if (trimmedName.StartsWith("Sense_", StringComparison.OrdinalIgnoreCase))
            return SpriteSheetLookupByName("Senses", trimmedName);

        return SpriteSheetLookup(trimmedName);
    }

    public static Sprite SpriteSheetLookup(string spriteSheet, int index = -1)
    {
        if (string.IsNullOrWhiteSpace(spriteSheet))
            return null;

        string sheetName = spriteSheet.Trim();
        if (index < 0 && TrySplitSpriteSheetIndex(sheetName, out string parsedSheetName, out int parsedIndex))
        {
            sheetName = parsedSheetName;
            index = parsedIndex;
        }

        if (index < 0)
            return SpriteSheetLookupByName(sheetName, spriteSheet.Trim());

        Dictionary<int, Sprite> lookup = GetSpritesByIndex(sheetName);
        return lookup.TryGetValue(index, out Sprite sprite) ? sprite : null;
    }

    public static bool TryGetEmojiSprite(string emote, out Sprite sprite, out string displayName)
    {
        sprite = null;
        displayName = FormatSpriteDisplayName(emote);

        if (!TryResolveEmojiEntry(emote, out DogEmojiEntry entry))
            return false;

        displayName = entry.Name;
        sprite = SpriteSheetLookup($"DogEmojiSheet{entry.SheetId}", entry.SpriteIndex);
        return sprite != null;
    }

    public static Dictionary<int, Sprite> GetSpriteSheet(string spriteSheet)
    {
        return new Dictionary<int, Sprite>(GetSpritesByIndex(spriteSheet));
    }

    public static int GetSpriteSheetIndex(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
            return -1;

        int separatorIndex = spriteName.LastIndexOf('_');
        if (separatorIndex < 0 || separatorIndex >= spriteName.Length - 1)
            return -1;

        return int.TryParse(spriteName.Substring(separatorIndex + 1), out int index)
            ? index
            : -1;
    }

    public static string NormalizeResourcePath(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return string.Empty;

        resourcePath = resourcePath.Trim().Replace("\\", "/");

        int extensionIndex = resourcePath.LastIndexOf(".", StringComparison.Ordinal);
        if (extensionIndex >= 0)
            resourcePath = resourcePath.Substring(0, extensionIndex);

        const string resourcesToken = "/Resources/";
        int resourcesIndex = resourcePath.IndexOf(resourcesToken, StringComparison.OrdinalIgnoreCase);
        if (resourcesIndex >= 0)
            resourcePath = resourcePath.Substring(resourcesIndex + resourcesToken.Length);

        return resourcePath.Trim('/');
    }

    static Sprite SpriteSheetLookupByName(string spriteSheet, string spriteName)
    {
        Dictionary<string, Sprite> lookup = GetSpritesByName(spriteSheet);
        return lookup.TryGetValue(spriteName, out Sprite sprite) ? sprite : null;
    }

    static Dictionary<int, Sprite> GetSpritesByIndex(string spriteSheet)
    {
        string resourcePath = ResolveSpriteSheetResourcePath(spriteSheet);
        if (spritesBySheet.TryGetValue(resourcePath, out Dictionary<int, Sprite> lookup))
            return lookup;

        LoadSpriteSheet(resourcePath);
        return spritesBySheet.TryGetValue(resourcePath, out lookup)
            ? lookup
            : new Dictionary<int, Sprite>();
    }

    static Dictionary<string, Sprite> GetSpritesByName(string spriteSheet)
    {
        string resourcePath = ResolveSpriteSheetResourcePath(spriteSheet);
        if (spritesByNameBySheet.TryGetValue(resourcePath, out Dictionary<string, Sprite> lookup))
            return lookup;

        LoadSpriteSheet(resourcePath);
        return spritesByNameBySheet.TryGetValue(resourcePath, out lookup)
            ? lookup
            : new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    }

    static void LoadSpriteSheet(string resourcePath)
    {
        string normalizedPath = NormalizeResourcePath(resourcePath);
        Dictionary<int, Sprite> spritesByIndex = new Dictionary<int, Sprite>();
        Dictionary<string, Sprite> spritesByName = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        Sprite[] sprites = Resources.LoadAll<Sprite>(normalizedPath);
        Sprite firstSprite = null;

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;

            if (firstSprite == null)
                firstSprite = sprite;

            spritesByName[sprite.name] = sprite;

            int index = GetSpriteSheetIndex(sprite.name);
            if (index >= 0)
                spritesByIndex[index] = sprite;
        }

        if (!spritesByIndex.ContainsKey(0) && firstSprite != null)
            spritesByIndex[0] = firstSprite;

        spritesBySheet[normalizedPath] = spritesByIndex;
        spritesByNameBySheet[normalizedPath] = spritesByName;
    }

    static string ResolveSpriteSheetResourcePath(string spriteSheet)
    {
        if (string.IsNullOrWhiteSpace(spriteSheet))
            return string.Empty;

        string normalized = NormalizeResourcePath(spriteSheet);
        if (spriteSheetResourcePaths.TryGetValue(normalized, out string resourcePath))
            return resourcePath;

        string fileName = normalized;
        int slashIndex = fileName.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < fileName.Length - 1)
            fileName = fileName.Substring(slashIndex + 1);

        if (spriteSheetResourcePaths.TryGetValue(fileName, out resourcePath))
            return resourcePath;

        return normalized.Contains("/")
            ? normalized
            : $"Sprites/{normalized}";
    }

    static bool TrySplitSpriteSheetIndex(string spriteName, out string spriteSheet, out int index)
    {
        spriteSheet = spriteName;
        index = -1;

        int separatorIndex = spriteName.LastIndexOf('_');
        if (separatorIndex < 0 || separatorIndex >= spriteName.Length - 1)
            return false;

        if (!int.TryParse(spriteName.Substring(separatorIndex + 1), out index))
            return false;

        spriteSheet = spriteName.Substring(0, separatorIndex);
        return true;
    }

    static bool TryResolveEmojiEntry(string emote, out DogEmojiEntry entry)
    {
        string normalized = NormalizeLookupKey(emote);
        string aliased = ResolveEmojiAlias(normalized);

        for (int i = 0; i < DogEmojiCatalog.Entries.Length; i++)
        {
            DogEmojiEntry candidate = DogEmojiCatalog.Entries[i];
            if (NormalizeLookupKey(candidate.EntryId) == normalized ||
                NormalizeLookupKey(candidate.Name) == aliased)
            {
                entry = candidate;
                return true;
            }
        }

        entry = default;
        return false;
    }

    static string ResolveEmojiAlias(string normalized)
    {
        switch (normalized)
        {
            case "friendly":
                return "happy";
            case "fearful":
                return "afraid";
            case "tilthead":
                return "curious";
            case "dig":
                return "determined";
            case "stay":
                return "alert";
            case "setuptrap":
                return "sneaky";
            case "scratch":
                return "annoyed";
            default:
                return normalized;
        }
    }

    static string NormalizeLookupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim()
            .ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty);
    }

    static string FormatSpriteDisplayName(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
            return "Sprite";

        string spaced = spriteName.Trim().Replace("_", " ").Replace("-", " ");
        return char.ToUpperInvariant(spaced[0]) + (spaced.Length > 1 ? spaced.Substring(1) : string.Empty);
    }
}
