using System;
using System.Collections.Generic;
using DogGame.Modules;
using UnityEngine;

public static class ItemPlacementOverrides
{
    public readonly struct ItemPlaceRule
    {
        public readonly string displayName;
        public readonly PlacementRoomTypeFlags allowedRooms;
        public readonly bool hasAllowedRooms;
        public readonly bool mustPlace;

        public ItemPlaceRule(
            string displayName,
            PlacementRoomTypeFlags allowedRooms,
            bool hasAllowedRooms,
            bool mustPlace)
        {
            this.displayName = displayName;
            this.allowedRooms = allowedRooms;
            this.hasAllowedRooms = hasAllowedRooms;
            this.mustPlace = mustPlace;
        }
    }

    private static readonly Dictionary<string, ItemPlaceRule> rulesByPrefabKey =
        new(StringComparer.OrdinalIgnoreCase);

    static ItemPlacementOverrides()
    {
        ConfigureItemPlacementRules();
    }

    private static void ConfigureItemPlacementRules()
    {
        rulesByPrefabKey.Clear();

        // Example:
        // AddItemPlace(Prefab: "Key", DisplayName: "Gold Key", AllowedRooms: PlacementRoomTypeFlags.Bathroom | PlacementRoomTypeFlags.Utility);
        AddItemPlace(Prefab: "Key", DisplayName: "Gold Key", AllowedRooms: PlacementRoomTypeFlags.Bathroom | PlacementRoomTypeFlags.Utility);
        
        // This would go under Quests to make sure necessary elements exist in world.
        // SetMustPlace(Prefab: "Key", MustPlace: true);
        SetMustPlace(Prefab: "Key", MustPlace: true);

    }

    private static void AddItemPlace(string Prefab, string DisplayName, PlacementRoomTypeFlags AllowedRooms)
    {
        if (string.IsNullOrWhiteSpace(Prefab))
            return;

        string prefabKey = NormalizePrefabKey(Prefab);
        if (string.IsNullOrWhiteSpace(prefabKey))
            return;

        bool mustPlace = rulesByPrefabKey.TryGetValue(prefabKey, out ItemPlaceRule existing) && existing.mustPlace;
        rulesByPrefabKey[prefabKey] = new ItemPlaceRule(DisplayName, AllowedRooms, hasAllowedRooms: true, mustPlace);
    }

    private static void SetMustPlace(string Prefab, bool MustPlace)
    {
        if (string.IsNullOrWhiteSpace(Prefab))
            return;

        string prefabKey = NormalizePrefabKey(Prefab);
        if (string.IsNullOrWhiteSpace(prefabKey))
            return;

        if (rulesByPrefabKey.TryGetValue(prefabKey, out ItemPlaceRule existing))
        {
            rulesByPrefabKey[prefabKey] = new ItemPlaceRule(
                existing.displayName,
                existing.allowedRooms,
                existing.hasAllowedRooms,
                MustPlace);
            return;
        }

        rulesByPrefabKey[prefabKey] = new ItemPlaceRule(
            displayName: null,
            allowedRooms: PlacementRoomTypeFlags.None,
            hasAllowedRooms: false,
            mustPlace: MustPlace);
    }

    public static IEnumerable<KeyValuePair<string, ItemPlaceRule>> MustPlaceRules
    {
        get
        {
            foreach (KeyValuePair<string, ItemPlaceRule> entry in rulesByPrefabKey)
            {
                if (entry.Value.mustPlace)
                    yield return entry;
            }
        }
    }

    public static bool TryGetRule(GameObject prefabOrInstance, out ItemPlaceRule rule)
    {
        rule = default;
        if (prefabOrInstance == null)
            return false;

        return TryGetRule(prefabOrInstance.name, out rule);
    }

    public static bool TryGetRule(string prefabName, out ItemPlaceRule rule)
    {
        return rulesByPrefabKey.TryGetValue(NormalizePrefabKey(prefabName), out rule);
    }

    public static PlacementRoomTypeFlags GetAllowedRooms(GameObject prefab, PlacementModule placement)
    {
        if (TryGetRule(prefab, out ItemPlaceRule rule) && rule.hasAllowedRooms)
            return rule.allowedRooms;

        return placement != null ? placement.allowedRooms : PlacementRoomTypeFlags.None;
    }

    public static bool AllowsRoom(GameObject prefab, PlacementModule placement, PlacementRoomTypeFlags roomFlags)
    {
        PlacementRoomTypeFlags allowedRooms = GetAllowedRooms(prefab, placement);
        if (allowedRooms == PlacementRoomTypeFlags.Any)
            return true;

        if (allowedRooms == PlacementRoomTypeFlags.None)
            return false;

        return (allowedRooms & roomFlags) != 0;
    }

    public static void ApplyToInstance(GameObject instance)
    {
        ApplyToInstance(instance, null);
    }

    public static void ApplyToInstance(GameObject instance, params string[] prefabNames)
    {
        if (instance == null)
            return;

        if (!TryGetRule(prefabNames, out ItemPlaceRule rule) &&
            !TryGetRule(instance, out rule))
            return;

        WorldObject worldObject = instance.GetComponent<WorldObject>();
        if (worldObject != null && !string.IsNullOrWhiteSpace(rule.displayName))
            worldObject.changeDisplayName(rule.displayName);

        PlacementModule placement = instance.GetComponent<PlacementModule>();
        if (placement == null)
            placement = instance.GetComponentInChildren<PlacementModule>();

        if (placement != null && rule.hasAllowedRooms)
            placement.allowedRooms = rule.allowedRooms;
    }

    private static bool TryGetRule(string[] prefabNames, out ItemPlaceRule rule)
    {
        rule = default;
        if (prefabNames == null)
            return false;

        for (int i = 0; i < prefabNames.Length; i++)
        {
            if (TryGetRule(prefabNames[i], out rule))
                return true;
        }

        return false;
    }

    public static string GetPrefabKey(GameObject prefabOrInstance)
    {
        return prefabOrInstance != null ? NormalizePrefabKey(prefabOrInstance.name) : string.Empty;
    }

    public static string GetPrefabKey(string prefabName)
    {
        return NormalizePrefabKey(prefabName);
    }

    private static string NormalizePrefabKey(string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
            return string.Empty;

        string key = prefabName.Trim();
        const string cloneSuffix = "(Clone)";
        if (key.EndsWith(cloneSuffix, StringComparison.Ordinal))
            key = key.Substring(0, key.Length - cloneSuffix.Length).TrimEnd();

        int slashIndex = key.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < key.Length - 1)
            key = key.Substring(slashIndex + 1);

        const string prefabExtension = ".prefab";
        if (key.EndsWith(prefabExtension, StringComparison.OrdinalIgnoreCase))
            key = key.Substring(0, key.Length - prefabExtension.Length);

        return key;
    }
}
