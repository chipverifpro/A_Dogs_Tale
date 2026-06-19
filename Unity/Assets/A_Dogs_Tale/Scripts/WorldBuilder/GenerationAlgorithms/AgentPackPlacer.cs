using System;
using System.Collections;
using System.Collections.Generic;
using DogGame.Modules;
using UnityEngine;

[Serializable]
public class AgentPackPlacementList
{
    public string packName = "Generated Pack";
    public List<GameObject> agentPrefabs = new();
}

public class AgentPackPlacer : MonoBehaviour
{
    [Header("Agent Packs")]
    [Tooltip("Each entry becomes one pack. The first prefab placed in the entry becomes the pack leader.")]
    public List<AgentPackPlacementList> agentPacks = new();

    [Header("Free Agents")]
    [Tooltip("Agents placed into the world but left out of any pack.")]
    public List<GameObject> freeAgentPrefabs = new();

    [Header("Placement")]
    public int maxAttemptsPerAgent = 30;
    public float baseYOffset = 0f;

    private Dir dir;
    private readonly HashSet<string> placedPrefabKeys = new(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        dir = Dir.Instance;
    }

    private IEnumerator Start()
    {
        if (dir == null || dir.gen == null)
            yield break;

        if (!dir.gen.buildComplete)
            yield return new WaitUntil(() => dir.gen.buildComplete);

        yield return new WaitUntil(() => dir.packManager != null && dir.packManager.packs != null);

        PlaceAllAgents();
    }

    public void PlaceAllAgents()
    {
        if (dir == null || dir.gen == null || dir.packManager == null)
        {
            Debug.LogError("AgentPackPlacer: missing ObjectDirectory, DungeonGenerator, or PackManager.", this);
            return;
        }

        if (dir.gen.rooms == null || dir.gen.rooms.Count == 0)
        {
            Debug.LogWarning("AgentPackPlacer: No rooms available in generator.", this);
            return;
        }

        placedPrefabKeys.Clear();
        PlaceConfiguredPacks();
        PlaceFreeAgents();
        PlaceRequiredAgents();
        RefreshPlayerPackCameraTarget();
    }

    private void PlaceConfiguredPacks()
    {
        if (agentPacks == null)
            return;

        for (int packIndex = 0; packIndex < agentPacks.Count; packIndex++)
        {
            AgentPackPlacementList packList = agentPacks[packIndex];
            if (packList == null || packList.agentPrefabs == null || packList.agentPrefabs.Count == 0)
                continue;

            string packName = string.IsNullOrWhiteSpace(packList.packName)
                ? $"Generated Pack {packIndex + 1}"
                : packList.packName;

            Pack pack = null;
            for (int agentIndex = 0; agentIndex < packList.agentPrefabs.Count; agentIndex++)
            {
                GameObject prefab = packList.agentPrefabs[agentIndex];
                if (prefab == null)
                    continue;

                WorldObject agent = TryPlaceAgentPrefab(prefab);
                if (agent == null)
                    continue;

                if (pack == null)
                    pack = CreatePackForName(packName, agent);
                else
                    pack.AddMember(agent, setAsLeader: false);

                RecordPlacedPrefab(prefab);
            }
        }
    }

    private Pack CreatePackForName(string packName, WorldObject leader)
    {
        Pack pack = IsHerdPackName(packName)
            ? dir.packManager.CreateNewHerd(packName, leader)
            : dir.packManager.CreateNewPack(packName, leader);

        ApplyHerdPackSettingsIfNeeded(packName, pack);
        return pack;
    }

    private static bool IsHerdPackName(string packName)
    {
        return !string.IsNullOrWhiteSpace(packName) &&
               packName.IndexOf("herd", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ApplyHerdPackSettingsIfNeeded(string packName, Pack pack)
    {
        if (pack == null || !IsHerdPackName(packName))
            return;

        pack.SetFormation(FormationsEnum.Herd);
        pack.leadershipType = AgentDecisionType.Herd;
        pack.followerType = AgentDecisionType.Herd;
        pack.SetPackFollowChain();
    }

    private void PlaceFreeAgents()
    {
        if (freeAgentPrefabs == null)
            return;

        foreach (GameObject prefab in freeAgentPrefabs)
        {
            if (prefab == null)
                continue;

            WorldObject agent = TryPlaceAgentPrefab(prefab);
            if (agent == null)
                continue;

            MakeFreeAgent(agent);
            RecordPlacedPrefab(prefab);
        }
    }

    private void PlaceRequiredAgents()
    {
        foreach (KeyValuePair<string, AgentPlacementOverrides.AgentPlaceRule> entry in AgentPlacementOverrides.MustPlaceRules)
        {
            string prefabKey = entry.Key;
            if (string.IsNullOrWhiteSpace(prefabKey) || HasPlacedPrefab(prefabKey))
                continue;

            GameObject prefab = FindPlacementPrefabByKey(prefabKey);
            if (prefab == null)
            {
                Debug.LogWarning($"AgentPackPlacer: must-place agent '{prefabKey}' could not be found in agent lists or agent Resources.", this);
                continue;
            }

            WorldObject agent = TryPlaceAgentPrefab(prefab);
            if (agent == null)
            {
                Debug.LogWarning($"AgentPackPlacer: failed to place required agent '{prefabKey}'.", this);
                continue;
            }

            MakeFreeAgent(agent);
            RecordPlacedPrefab(prefab);
        }
    }

    private WorldObject TryPlaceAgentPrefab(GameObject prefab)
    {
        if (prefab == null)
            return null;

        PlacementModule placement = prefab.GetComponentInChildren<PlacementModule>();
        if (placement == null)
        {
            Debug.LogWarning($"AgentPackPlacer: agent prefab '{prefab.name}' has no PlacementModule.", prefab);
            return null;
        }

        foreach (Room room in dir.gen.rooms)
        {
            if (!IsUsablePlacementRoom(room))
                continue;

            if (!AgentPlacementOverrides.AllowsRoom(prefab, placement, room.placementTypes))
                continue;

            WorldObject agent = TryPlaceAgentInRoom(room, prefab, placement);
            if (agent != null)
                return agent;
        }

        foreach (Room room in dir.gen.rooms)
        {
            if (!IsUsablePlacementRoom(room))
                continue;

            WorldObject agent = TryPlaceAgentInRoom(room, prefab, placement);
            if (agent != null)
                return agent;
        }

        return null;
    }

    private WorldObject TryPlaceAgentInRoom(Room room, GameObject prefab, PlacementModule placement)
    {
        if (!GeneratedObjectPlacementUtility.TryPlaceOne(room, prefab, placement, baseYOffset, maxAttemptsPerAgent, out GameObject instance))
            return null;

        AgentPlacementOverrides.ApplyToInstance(instance, prefab.name);

        WorldObject worldObject = instance.GetComponent<WorldObject>();
        if (worldObject == null)
            worldObject = instance.AddComponent<WorldObject>();

        worldObject.CreateModulesIfNeeded(ModuleFlagsTemplates.FullAgent);
        worldObject.RegisterIfNeeded();

        return worldObject;
    }

    private void MakeFreeAgent(WorldObject agent)
    {
        if (agent == null)
            return;

        if (agent.packMemberModule != null && agent.packMemberModule.currentPack != null)
            agent.packMemberModule.LeaveCurrentPack();

        if (dir.packManager.FreeAgentsParent != null)
            agent.transform.SetParent(dir.packManager.FreeAgentsParent.transform, worldPositionStays: true);
    }

    private void RefreshPlayerPackCameraTarget()
    {
        WorldObject leader = dir != null && dir.playerPack != null ? dir.playerPack.packLeader : null;
        if (leader == null)
        {
            Debug.LogWarning("AgentPackPlacer: Cannot assign camera target because PlayerPack has no leader.", this);
            return;
        }

        if (leader.appearanceModule == null)
            leader.CreateModulesIfNeeded(ModuleFlags.appearanceModule);

        if (leader.appearanceModule == null)
        {
            Debug.LogWarning($"AgentPackPlacer: Cannot assign camera target because '{leader.DisplayName}' has no AppearanceModule.", leader);
            return;
        }

        leader.appearanceModule.SetCameraFollow();
    }

    private GameObject FindPlacementPrefabByKey(string prefabKey)
    {
        GameObject prefab = FindPrefabByKey(freeAgentPrefabs, prefabKey);
        if (prefab != null)
            return prefab;

        if (agentPacks != null)
        {
            foreach (AgentPackPlacementList packList in agentPacks)
            {
                prefab = FindPrefabByKey(packList != null ? packList.agentPrefabs : null, prefabKey);
                if (prefab != null)
                    return prefab;
            }
        }

        return Resources.Load<GameObject>($"Prefabs/Agents/{prefabKey}")
            ?? Resources.Load<GameObject>($"Prefabs/PackDogs/{prefabKey}")
            ?? Resources.Load<GameObject>($"Prefabs/ChatGPT_Prefabs/ChatGPT_Agents/{prefabKey}")
            ?? FindResourcePrefabByKey("Prefabs/Agents", prefabKey)
            ?? FindResourcePrefabByKey("Prefabs/PackDogs", prefabKey)
            ?? FindResourcePrefabByKey("Prefabs/ChatGPT_Prefabs/ChatGPT_Agents", prefabKey);
    }

    private static GameObject FindPrefabByKey(List<GameObject> prefabs, string prefabKey)
    {
        if (prefabs == null)
            return null;

        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null &&
                string.Equals(AgentPlacementOverrides.GetPrefabKey(prefab), prefabKey, StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }
        }

        return null;
    }

    private static GameObject FindResourcePrefabByKey(string resourcesPath, string prefabKey)
    {
        GameObject[] prefabs = Resources.LoadAll<GameObject>(resourcesPath);
        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null &&
                string.Equals(AgentPlacementOverrides.GetPrefabKey(prefab), prefabKey, StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }
        }

        return null;
    }

    private bool HasPlacedPrefab(string prefabKey)
    {
        if (placedPrefabKeys.Contains(prefabKey))
            return true;

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (registry == null)
            return false;

        foreach (WorldObject worldObject in registry.GetAllObjects())
        {
            if (worldObject == null)
                continue;

            if (string.Equals(AgentPlacementOverrides.GetPrefabKey(worldObject.gameObject), prefabKey, StringComparison.OrdinalIgnoreCase))
                return true;

            SavePrefabId savePrefabId = worldObject.GetComponent<SavePrefabId>();
            if (savePrefabId == null)
                continue;

            if (string.Equals(AgentPlacementOverrides.GetPrefabKey(savePrefabId.PrefabId), prefabKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(AgentPlacementOverrides.GetPrefabKey(savePrefabId.ResourcesPath), prefabKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(AgentPlacementOverrides.GetPrefabKey(savePrefabId.AssetPath), prefabKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void RecordPlacedPrefab(GameObject prefab)
    {
        string prefabKey = AgentPlacementOverrides.GetPrefabKey(prefab);
        if (!string.IsNullOrWhiteSpace(prefabKey))
            placedPrefabKeys.Add(prefabKey);
    }

    private static bool IsUsablePlacementRoom(Room room)
    {
        return room != null && room.cells != null && room.cells.Count > 0;
    }
}
