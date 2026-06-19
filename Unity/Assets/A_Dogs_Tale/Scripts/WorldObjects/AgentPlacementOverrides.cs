using System;
using System.Collections.Generic;
using DogGame.Modules;
using UnityEngine;

public static class AgentPlacementOverrides
{
    public readonly struct AgentPlaceRule
    {
        public readonly string displayName;
        public readonly PlacementRoomTypeFlags allowedRooms;
        public readonly bool hasAllowedRooms;
        public readonly bool mustPlace;

        public AgentPlaceRule(
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

    private static readonly Dictionary<string, AgentPlaceRule> rulesByPrefabKey =
        new(StringComparer.OrdinalIgnoreCase);

    static AgentPlacementOverrides()
    {
        ConfigureAgentPlacementRules();
    }

    private static void ConfigureAgentPlacementRules()
    {
        rulesByPrefabKey.Clear();

        // Quest-driven examples. These are hard-coded until quest data owns placement needs.
        AddAgentPlace(Prefab: "Elder_Female_A", DisplayName: "Old Lady", AllowedRooms: PlacementRoomTypeFlags.Living | PlacementRoomTypeFlags.Generic | PlacementRoomTypeFlags.Outdoor);
        SetMustPlace(Prefab: "Elder_Female_A", MustPlace: true);
    }

    private static void AddAgentPlace(string Prefab, string DisplayName, PlacementRoomTypeFlags AllowedRooms)
    {
        if (string.IsNullOrWhiteSpace(Prefab))
            return;

        string prefabKey = NormalizePrefabKey(Prefab);
        if (string.IsNullOrWhiteSpace(prefabKey))
            return;

        bool mustPlace = rulesByPrefabKey.TryGetValue(prefabKey, out AgentPlaceRule existing) && existing.mustPlace;
        rulesByPrefabKey[prefabKey] = new AgentPlaceRule(DisplayName, AllowedRooms, hasAllowedRooms: true, mustPlace);
    }

    private static void SetMustPlace(string Prefab, bool MustPlace)
    {
        if (string.IsNullOrWhiteSpace(Prefab))
            return;

        string prefabKey = NormalizePrefabKey(Prefab);
        if (string.IsNullOrWhiteSpace(prefabKey))
            return;

        if (rulesByPrefabKey.TryGetValue(prefabKey, out AgentPlaceRule existing))
        {
            rulesByPrefabKey[prefabKey] = new AgentPlaceRule(
                existing.displayName,
                existing.allowedRooms,
                existing.hasAllowedRooms,
                MustPlace);
            return;
        }

        rulesByPrefabKey[prefabKey] = new AgentPlaceRule(
            displayName: null,
            allowedRooms: PlacementRoomTypeFlags.None,
            hasAllowedRooms: false,
            mustPlace: MustPlace);
    }

    public static IEnumerable<KeyValuePair<string, AgentPlaceRule>> MustPlaceRules
    {
        get
        {
            foreach (KeyValuePair<string, AgentPlaceRule> entry in rulesByPrefabKey)
            {
                if (entry.Value.mustPlace)
                    yield return entry;
            }
        }
    }

    public static bool TryGetRule(GameObject prefabOrInstance, out AgentPlaceRule rule)
    {
        rule = default;
        if (prefabOrInstance == null)
            return false;

        return TryGetRule(prefabOrInstance.name, out rule);
    }

    public static bool TryGetRule(string prefabName, out AgentPlaceRule rule)
    {
        return rulesByPrefabKey.TryGetValue(NormalizePrefabKey(prefabName), out rule);
    }

    public static PlacementRoomTypeFlags GetAllowedRooms(GameObject prefab, PlacementModule placement)
    {
        if (TryGetRule(prefab, out AgentPlaceRule rule) && rule.hasAllowedRooms)
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

    public static void ApplyToInstance(GameObject instance, params string[] prefabNames)
    {
        if (instance == null)
            return;

        if (!TryGetRule(prefabNames, out AgentPlaceRule rule) &&
            !TryGetRule(instance, out rule))
        {
            return;
        }

        WorldObject worldObject = instance.GetComponent<WorldObject>();
        if (worldObject != null && !string.IsNullOrWhiteSpace(rule.displayName))
            worldObject.changeDisplayName(rule.displayName);

        PlacementModule placement = instance.GetComponent<PlacementModule>();
        if (placement == null)
            placement = instance.GetComponentInChildren<PlacementModule>();

        if (placement != null && rule.hasAllowedRooms)
            placement.allowedRooms = rule.allowedRooms;
    }

    public static string GetPrefabKey(GameObject prefabOrInstance)
    {
        return prefabOrInstance != null ? NormalizePrefabKey(prefabOrInstance.name) : string.Empty;
    }

    public static string GetPrefabKey(string prefabName)
    {
        return NormalizePrefabKey(prefabName);
    }

    private static bool TryGetRule(string[] prefabNames, out AgentPlaceRule rule)
    {
        rule = default;
        if (prefabNames == null)
            return false;

        foreach (string prefabName in prefabNames)
        {
            if (TryGetRule(prefabName, out rule))
                return true;
        }

        return false;
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
