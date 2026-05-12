using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DogGame.LLM;
using DogGame.LLM.Agent;
using DogGame.Modules;
using UnityEngine;

public partial class DungeonGenerator
{
    private const int MapSaveVersion = 7;
    private const string SaveDirectoryName = "DogsTaleSaves";
    private const string SingleMapSaveFilename = "dogs_tale_map_slot.json";

    public static string SingleMapSavePath
    {
        get
        {
            string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, SaveDirectoryName, SingleMapSaveFilename);
        }
    }

    public static bool SingleMapSaveExists => File.Exists(SingleMapSavePath);

    public void SaveCurrentMapToSingleSlot()
    {
        try
        {
            if (rooms == null || rooms.Count == 0)
            {
                BottomBanner.Show("No map is available to save.");
                Debug.LogWarning("[MapSaveSystem] Save skipped because the room list is empty.", this);
                return;
            }

            MapSaveData saveData = MapSaveData.FromGenerator(this);
            string json = JsonUtility.ToJson(saveData, prettyPrint: true);
            string savePath = SingleMapSavePath;
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(savePath, json);

            BottomBanner.Show($"Map and WorldObjects saved to {savePath}");
            Debug.Log($"[MapSaveSystem] Saved map and WorldObjects to {savePath}", this);
        }
        catch (Exception ex)
        {
            BottomBanner.Show("Map save failed. See console for details.");
            Debug.LogError($"[MapSaveSystem] Save failed: {ex}", this);
        }
    }

    public void LoadMapFromSingleSlot()
    {
        if (regenerateCoroutine != null)
        {
            StopCoroutine(regenerateCoroutine);
            regenerateCoroutine = null;
        }

        regenerateCoroutine = StartCoroutine(LoadMapFromSingleSlotCoroutine());
    }

    private IEnumerator LoadMapFromSingleSlotCoroutine()
    {
        string savePath = SingleMapSavePath;

        if (!File.Exists(savePath))
        {
            BottomBanner.Show($"No map save found at {savePath}");
            Debug.LogWarning($"[MapSaveSystem] Load skipped because no save exists at {savePath}", this);
            regenerateCoroutine = null;
            yield break;
        }

        buildComplete = false;
        BottomBanner.Show("Loading saved map...");

        MapSaveData saveData;
        try
        {
            string json = File.ReadAllText(savePath);
            saveData = JsonUtility.FromJson<MapSaveData>(json);
        }
        catch (Exception ex)
        {
            BottomBanner.Show("Map load failed. See console for details.");
            Debug.LogError($"[MapSaveSystem] Could not read map save at {savePath}: {ex}", this);
            buildComplete = true;
            regenerateCoroutine = null;
            yield break;
        }

        if (saveData == null || saveData.version <= 0 || saveData.rooms == null)
        {
            BottomBanner.Show("Map load failed: save file is invalid.");
            Debug.LogError($"[MapSaveSystem] Invalid map save at {savePath}", this);
            buildComplete = true;
            regenerateCoroutine = null;
            yield break;
        }

        ApplyMapSaveData(saveData);
        yield return StartCoroutine(Build3DFromRooms(tm: null));

        DrawMapByRooms(rooms);
        UpdateCellGridFromRooms(rooms);
        PrepareHeightfield();

        Dictionary<int, WorldObject> restoredWorldObjects = ApplySavedWorldObjects(saveData.worldObjects);
        ApplySavedPacks(saveData.packs, restoredWorldObjects);
        ApplySavedContainers(saveData.worldObjects, restoredWorldObjects);
        ApplySavedScentPhysics(saveData.scentPhysics, restoredWorldObjects);
        ApplySavedBottomBannerMessages(saveData.bottomBanner);
        ApplySavedLLMDebugState(saveData.llmDebug);

        buildComplete = true;
        regenerateCoroutine = null;

        if (dir != null && dir.scentAirGround != null)
            dir.scentAirGround.StartScentSimulation(resetOverlayAgent: saveData.scentPhysics == null);

        ApplySavedLLMSchedulerState(saveData.llmScheduler);

        BottomBanner.Show($"Map and WorldObjects loaded from {savePath}");
        Debug.Log($"[MapSaveSystem] Loaded map and WorldObjects from {savePath}", this);
    }

    private void ApplyMapSaveData(MapSaveData saveData)
    {
        int width = Mathf.Max(1, saveData.mapWidth);
        int height = Mathf.Max(1, saveData.mapHeight);

        if (cfg != null)
        {
            cfg.mapWidth = width;
            cfg.mapHeight = height;
        }

        tilemap?.ClearAllTiles();
        tilemap_walls?.ClearAllTiles();
        tilemap_doors?.ClearAllTiles();

        if (dir != null && dir.warehouse != null)
            dir.warehouse.ClearAll();
        if (elementStore != null)
            elementStore.ClearInstances();

        rooms = saveData.ToRooms();
        map = new byte[width, height];
        mapHeights = new int[width, height];
        FillVoidToWalls(map);
        RebuildMapArraysFromRooms(width, height);

        hf = null;
        hf_valid = false;
        UpdateCellGridFromRooms(rooms);
        DrawMapByRooms(rooms);
    }

    private void RebuildMapArraysFromRooms(int width, int height)
    {
        foreach (Room room in rooms)
        {
            if (room == null || room.cells == null)
                continue;

            foreach (Cell cell in room.cells)
            {
                if (cell == null || cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height)
                    continue;

                map[cell.x, cell.y] = FLOOR;
                mapHeights[cell.x, cell.y] = cell.height;
            }
        }
    }

    private Dictionary<int, WorldObject> ApplySavedWorldObjects(List<WorldObjectDto> savedObjects)
    {
        Dictionary<int, WorldObject> restoredById = new();
        if (savedObjects == null)
            return restoredById;

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (registry == null)
        {
            Debug.LogWarning("[MapSaveSystem] Could not restore WorldObjects because no registry is available.", this);
            return restoredById;
        }

        foreach (WorldObjectDto savedObject in savedObjects)
        {
            if (savedObject == null || savedObject.objectId <= 0)
                continue;

            WorldObject worldObject = null;
            if (!registry.TryGet(savedObject.objectId, out worldObject) || worldObject == null)
                worldObject = CreateSavedWorldObject(savedObject);

            ApplySavedWorldObject(worldObject, savedObject);
            if (worldObject != null)
                restoredById[savedObject.objectId] = worldObject;
        }

        foreach (WorldObjectDto savedObject in savedObjects)
        {
            if (savedObject == null || savedObject.objectId <= 0 || savedObject.parentWorldObjectId <= 0)
                continue;

            if (!restoredById.TryGetValue(savedObject.objectId, out WorldObject child) || child == null)
                continue;
            if (!restoredById.TryGetValue(savedObject.parentWorldObjectId, out WorldObject parent) || parent == null)
                continue;

            child.transform.SetParent(parent.transform, worldPositionStays: true);
        }

        return restoredById;
    }

    private WorldObject CreateSavedWorldObject(WorldObjectDto savedObject)
    {
        GameObject go = InstantiateSavedPrefab(savedObject);
        if (go == null)
        {
            string objectName = string.IsNullOrWhiteSpace(savedObject.displayName)
                ? $"WorldObject_{savedObject.objectId}"
                : savedObject.displayName;

            go = new GameObject(objectName);
            go.SetActive(false);
        }
        else
        {
            go.SetActive(false);
        }

        WorldObject worldObject = go.GetComponent<WorldObject>();
        if (worldObject == null)
            worldObject = go.AddComponent<WorldObject>();

        WorldObjectRegistry.Instance?.Unregister(worldObject);
        worldObject.ApplySavedIdentity(
            savedObject.objectId,
            savedObject.displayName,
            (WorldObjectKind)savedObject.kind,
            (Species)savedObject.species,
            savedObject.breed);
        ApplySavedPrefabIdentity(worldObject, savedObject);

        return worldObject;
    }

    private GameObject InstantiateSavedPrefab(WorldObjectDto savedObject)
    {
        GameObject prefab = null;

        if (!string.IsNullOrWhiteSpace(savedObject.prefabResourcesPath))
            prefab = Resources.Load<GameObject>(savedObject.prefabResourcesPath);

#if UNITY_EDITOR
        if (prefab == null && !string.IsNullOrWhiteSpace(savedObject.prefabAssetPath))
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(savedObject.prefabAssetPath);
#endif

        if (prefab == null)
        {
            if (!string.IsNullOrWhiteSpace(savedObject.prefabId) ||
                !string.IsNullOrWhiteSpace(savedObject.prefabResourcesPath) ||
                !string.IsNullOrWhiteSpace(savedObject.prefabAssetPath))
            {
                Debug.LogWarning(
                    $"[MapSaveSystem] Could not load prefab for {savedObject.displayName} " +
                    $"prefabId='{savedObject.prefabId}' resourcesPath='{savedObject.prefabResourcesPath}' assetPath='{savedObject.prefabAssetPath}'. " +
                    "Using shell fallback.",
                    this);
            }

            return null;
        }

        return Instantiate(prefab);
    }

    private static void ApplySavedWorldObject(WorldObject worldObject, WorldObjectDto savedObject)
    {
        if (worldObject == null || savedObject == null)
            return;

        worldObject.ApplySavedIdentity(
            savedObject.objectId,
            savedObject.displayName,
            (WorldObjectKind)savedObject.kind,
            (Species)savedObject.species,
            savedObject.breed);
        ApplySavedPrefabIdentity(worldObject, savedObject);

        worldObject.sizeRadius = savedObject.sizeRadius;
        worldObject.weight = savedObject.weight;
        worldObject.immovable = savedObject.immovable;
        worldObject.adjustMapToWorld = savedObject.adjustMapToWorld.ToVector3();
        worldObject.transform.SetPositionAndRotation(
            savedObject.position.ToVector3(),
            savedObject.rotation.ToQuaternion());
        worldObject.transform.localScale = savedObject.localScale.ToVector3();

        if (TryParseModuleFlags(savedObject.moduleFlagsRaw, out ModuleFlags moduleFlags))
            worldObject.CreateModulesIfNeeded(moduleFlags);

        ApplySavedAgentState(worldObject, savedObject.agent);
        ApplySavedPlacementState(worldObject, savedObject.placement);
        ApplySavedContainerState(worldObject, savedObject.container);
        ApplySavedScentEmitterState(worldObject, savedObject.scentEmitter);
        ApplySavedMessageQueueState(worldObject, savedObject.messageQueues);
        ApplySavedLLMState(worldObject, savedObject.llmState);
        ApplySavedTaskControllerState(worldObject, savedObject.taskController);

        worldObject.agentMovementModule?.ClearDesiredMovement();
        worldObject.RegisterIfNeeded();
        worldObject.gameObject.SetActive(savedObject.activeSelf);
    }

    private static void ApplySavedPrefabIdentity(WorldObject worldObject, WorldObjectDto savedObject)
    {
        if (worldObject == null || savedObject == null)
            return;

        if (string.IsNullOrWhiteSpace(savedObject.prefabId) &&
            string.IsNullOrWhiteSpace(savedObject.prefabResourcesPath) &&
            string.IsNullOrWhiteSpace(savedObject.prefabAssetPath))
        {
            return;
        }

        SavePrefabId savePrefabId = worldObject.GetComponent<SavePrefabId>();
        if (savePrefabId == null)
            savePrefabId = worldObject.gameObject.AddComponent<SavePrefabId>();

        savePrefabId.SetPrefabIdentity(
            savedObject.prefabId,
            savedObject.prefabResourcesPath,
            savedObject.prefabAssetPath);
    }

    private static void ApplySavedAgentState(WorldObject worldObject, AgentDto agent)
    {
        if (worldObject == null || agent == null)
            return;

        if (worldObject.motionModule != null)
            worldObject.motionModule.SetWalkMode((WalkMode)agent.walkMode);

        if (worldObject.agentModule != null)
        {
            AgentDecisionType initialDecisionType = (AgentDecisionType)agent.initialDecisionType;
            if (initialDecisionType != AgentDecisionType.Undefined)
                worldObject.agentModule.initialDecisionType = initialDecisionType;
        }
    }

    private static void ApplySavedPlacementState(WorldObject worldObject, PlacementDto placement)
    {
        if (worldObject == null || placement == null || worldObject.placementModule == null)
            return;

        worldObject.placementModule.allowedRooms = (PlacementRoomTypeFlags)placement.allowedRooms;
        worldObject.placementModule.autoSizeFromMesh = placement.autoSizeFromMesh;
        worldObject.placementModule.sizeInCells = placement.sizeInCells.ToVector3();
        worldObject.placementModule.cellSize = placement.cellSize;
        worldObject.placementModule.edgeHint = (EdgeHint)placement.edgeHint;
        worldObject.placementModule.rotationRule = (RotationRule)placement.rotationRule;
        worldObject.placementModule.minClearCellsAround = placement.minClearCellsAround;
        worldObject.placementModule.mustTouchWall = placement.mustTouchWall;
        worldObject.placementModule.wallPadding = placement.wallPadding;
    }

    private static void ApplySavedContainerState(WorldObject worldObject, ContainerDto container)
    {
        if (worldObject == null || container == null || worldObject.containerModule == null)
            return;

        worldObject.containerModule.ApplySavedContainerState(
            container.itemCapacity,
            container.maxWeight,
            container.heldItemsVisible,
            container.heldHeight,
            container.isLocked,
            container.isClosed,
            container.autoPickupNearbyItems,
            container.pickupRadiusTiles,
            container.autoConfigureAgentCapacity,
            container.dogItemCapacity,
            container.humanItemCapacity);
    }

    private static void ApplySavedScentEmitterState(WorldObject worldObject, ScentEmitterDto scentEmitter)
    {
        if (worldObject == null || scentEmitter == null || worldObject.scentEmitterModule == null)
            return;

        scentEmitter.ApplyTo(worldObject.scentEmitterModule, worldObject);
    }

    private static void ApplySavedMessageQueueState(WorldObject worldObject, MessageQueueDto messageQueues)
    {
        if (worldObject == null || messageQueues == null)
            return;

        if (worldObject.llmWorldStateModule != null)
            worldObject.llmWorldStateModule.RestoreRecentObservations(messageQueues.llmRecentObservations);
    }

    private static void ApplySavedLLMState(WorldObject worldObject, LLMStateDto llmState)
    {
        if (worldObject == null || llmState == null)
            return;

        if (worldObject.llmConfigModule != null)
            worldObject.llmConfigModule.RestoreSaveData(llmState.config);
        if (worldObject.llmWorldStateModule != null)
            worldObject.llmWorldStateModule.RestoreSaveData(llmState.worldState);
        if (worldObject.llmThinkModule != null)
            worldObject.llmThinkModule.RestoreSaveData(llmState.think);
    }

    private static void ApplySavedTaskControllerState(WorldObject worldObject, TaskController.SaveData taskController)
    {
        if (worldObject == null || taskController == null || worldObject.taskController == null)
            return;

        worldObject.taskController.RestoreSaveData(taskController);
    }

    private static void ApplySavedContainers(List<WorldObjectDto> savedObjects, Dictionary<int, WorldObject> restoredById)
    {
        if (savedObjects == null || restoredById == null)
            return;

        foreach (WorldObjectDto savedObject in savedObjects)
        {
            if (savedObject == null || savedObject.container == null || savedObject.container.heldObjectIds == null)
                continue;
            if (!restoredById.TryGetValue(savedObject.objectId, out WorldObject containerObject) || containerObject == null)
                continue;
            if (containerObject.containerModule == null)
                continue;

            List<WorldObject> heldItems = new();
            foreach (int heldObjectId in savedObject.container.heldObjectIds)
            {
                if (restoredById.TryGetValue(heldObjectId, out WorldObject heldItem) && heldItem != null)
                    heldItems.Add(heldItem);
            }

            containerObject.containerModule.RestoreSavedContents(heldItems);
        }
    }

    private void ApplySavedPacks(List<PackDto> savedPacks, Dictionary<int, WorldObject> restoredById)
    {
        if (savedPacks == null || savedPacks.Count == 0 || restoredById == null)
            return;
        if (dir == null || dir.packManager == null)
        {
            Debug.LogWarning("[MapSaveSystem] Could not restore packs because PackManager is unavailable.", this);
            return;
        }

        PackManager packManager = dir.packManager;
        if (packManager.packs == null)
            packManager.packs = new List<Pack>();

        foreach (Pack pack in packManager.packs)
        {
            if (pack == null || pack.packAgentList == null)
                continue;

            foreach (WorldObject member in pack.packAgentList)
            {
                if (member != null && member.packMemberModule != null)
                    member.packMemberModule.currentPack = null;
            }
            pack.packAgentList.Clear();
        }

        foreach (PackDto savedPack in savedPacks)
        {
            if (savedPack == null || string.IsNullOrWhiteSpace(savedPack.packName))
                continue;

            Pack pack = FindOrCreatePackForLoad(packManager, savedPack);
            if (pack == null)
                continue;

            pack.dir = dir;
            pack.packName = savedPack.packName;
            pack.formation = (FormationsEnum)savedPack.formation;
            pack.leadershipType = (AgentDecisionType)savedPack.leadershipType;
            pack.followerType = (AgentDecisionType)savedPack.followerType;
            pack.formationSpacing = savedPack.formationSpacing;
            if (pack.packAgentList == null)
                pack.packAgentList = new List<WorldObject>();
            pack.packAgentList.Clear();

            if (savedPack.memberObjectIds == null)
                savedPack.memberObjectIds = new List<int>();

            foreach (int memberId in savedPack.memberObjectIds)
            {
                if (!restoredById.TryGetValue(memberId, out WorldObject member) || member == null)
                    continue;

                if (member.packMemberModule == null)
                    member.CreateModulesIfNeeded(ModuleFlags.packMemberModule);
                if (member.packMemberModule == null)
                    continue;

                pack.packAgentList.Add(member);
                member.packMemberModule.currentPack = pack;
                member.transform.SetParent(pack.transform, worldPositionStays: true);
            }

            if (!packManager.packs.Contains(pack))
                packManager.packs.Add(pack);

            if (savedPack.isPlayerPack)
            {
                packManager.playerPack = pack;
                dir.playerPack = pack;
            }

            if (pack.packAgentList.Count > 0)
                pack.SetPackFollowChain();
        }
    }

    private Pack FindOrCreatePackForLoad(PackManager packManager, PackDto savedPack)
    {
        for (int i = 0; i < packManager.packs.Count; i++)
        {
            Pack candidate = packManager.packs[i];
            if (candidate != null && candidate.packName == savedPack.packName)
                return candidate;
        }

        GameObject packObject = new GameObject(savedPack.packName);
        if (packManager.PackParentObject != null)
            packObject.transform.SetParent(packManager.PackParentObject.transform, worldPositionStays: false);

        Pack pack = packObject.AddComponent<Pack>();
        pack.dir = dir;
        pack.packName = savedPack.packName;
        pack.packAgentList = new List<WorldObject>();
        packManager.packs.Add(pack);
        return pack;
    }

    private void ApplySavedScentPhysics(ScentPhysicsDto scentPhysics, Dictionary<int, WorldObject> restoredById)
    {
        if (scentPhysics == null)
            return;
        if (dir == null || dir.scentAirGround == null)
        {
            Debug.LogWarning("[MapSaveSystem] Could not restore scent physics because ScentAirGround is unavailable.", this);
            return;
        }

        ScentAirGround scentAirGround = dir.scentAirGround;
        scentAirGround.StopScentSimulation();
        scentAirGround.ClearAllScentVisuals();

        ApplySavedScentAirGroundSettings(scentAirGround, scentPhysics.airGround);
        ApplySavedCellScents(scentPhysics.cells);
        scentAirGround.ScentCellsListCreate();
        ApplySavedScentRegistry(scentPhysics.registry, restoredById);

        if (dir.scentRegistry != null)
            dir.scentRegistry.RebuildLookup();
    }

    private static void ApplySavedScentAirGroundSettings(ScentAirGround scentAirGround, ScentAirGroundDto airGround)
    {
        if (scentAirGround == null || airGround == null)
            return;

        scentAirGround.currentAgentId = airGround.currentAgentId;
        scentAirGround.previousAgentIdVisualized = airGround.previousAgentIdVisualized;
        scentAirGround.airScentVisible = airGround.airScentVisible;
        scentAirGround.groundScentVisible = airGround.groundScentVisible;
        scentAirGround.airScentDepositRate = airGround.airScentDepositRate;
        scentAirGround.airDiffusionRate = airGround.airDiffusionRate;
        scentAirGround.airDecayRate = airGround.airDecayRate;
        scentAirGround.groundScentDepositRate = airGround.groundScentDepositRate;
        scentAirGround.groundDiffusionRate = airGround.groundDiffusionRate;
        scentAirGround.groundDecayRate = airGround.groundDecayRate;
        scentAirGround.airToGroundRate = airGround.airToGroundRate;
        scentAirGround.groundToAirRate = airGround.groundToAirRate;
        scentAirGround.SimulationTimeStep = airGround.simulationTimeStep;
        scentAirGround.runOnStart = airGround.runOnStart;
        scentAirGround.practically_zero = airGround.practicallyZero;
        scentAirGround.enableScentDiagnostics = airGround.enableScentDiagnostics;
        scentAirGround.scentDiagnosticsEveryNFrames = airGround.scentDiagnosticsEveryNFrames;
        scentAirGround.logScentReclamation = airGround.logScentReclamation;
        scentAirGround.scentVisualThreshold = airGround.scentVisualThreshold;
        scentAirGround.maxVisualIntensity = airGround.maxVisualIntensity;
        scentAirGround.airBaseColor = airGround.airBaseColor.ToColor();
        scentAirGround.groundBaseColor = airGround.groundBaseColor.ToColor();
    }

    private void ApplySavedScentRegistry(ScentRegistryDto registryDto, Dictionary<int, WorldObject> restoredById)
    {
        if (registryDto == null || dir == null || dir.scentRegistry == null)
            return;

        ScentRegistry registry = dir.scentRegistry;
        registry.allScentSources = new List<ScentSource>();
        if (registryDto.sources != null)
        {
            foreach (ScentSourceDto sourceDto in registryDto.sources)
            {
                if (sourceDto == null)
                    continue;

                ScentSource source = sourceDto.ToScentSource(restoredById);
                registry.allScentSources.Add(source);
            }
        }

        registry.RebuildLookup();

        if (!string.IsNullOrWhiteSpace(registryDto.selectedTargetScentKey) &&
            registry.TryResolveScentSource(registryDto.selectedTargetScentKey, out ScentSource selectedSource))
        {
            registry.SetSelectedTargetScent(selectedSource);
        }
        else
        {
            registry.SetSelectedTargetScent(null);
        }
    }

    private void ApplySavedCellScents(List<ScentCellDto> savedCells)
    {
        if (savedCells == null || dir == null || dir.gen == null)
            return;

        foreach (Room room in rooms)
        {
            if (room == null || room.cells == null)
                continue;

            foreach (Cell cell in room.cells)
            {
                if (cell != null)
                    cell.scents = null;
            }
        }

        foreach (ScentCellDto savedCell in savedCells)
        {
            if (savedCell == null || savedCell.scents == null || savedCell.scents.Count == 0)
                continue;

            Cell cell = dir.gen.GetCellFromHf(savedCell.x, savedCell.y, savedCell.height, threshold: 50);
            if (cell == null)
                cell = FindRoomCell(savedCell.x, savedCell.y, savedCell.height);
            if (cell == null)
                continue;

            cell.scents = new List<ScentInCell>();
            foreach (ScentInCellDto savedScent in savedCell.scents)
            {
                if (savedScent == null || savedScent.agentId < 0)
                    continue;

                cell.scents.Add(savedScent.ToScentInCell());
            }

            if (cell.scents.Count == 0)
                cell.scents = null;
        }
    }

    private Cell FindRoomCell(int x, int y, int height)
    {
        if (rooms == null)
            return null;

        foreach (Room room in rooms)
        {
            if (room == null || room.cells == null)
                continue;

            foreach (Cell cell in room.cells)
            {
                if (cell != null && cell.x == x && cell.y == y && cell.height == height)
                    return cell;
            }
        }

        return null;
    }

    private static void ApplySavedBottomBannerMessages(BottomBanner.SaveData bottomBanner)
    {
        if (bottomBanner == null)
            return;

        BottomBanner.RestoreSaveData(bottomBanner);
    }

    private void ApplySavedLLMSchedulerState(LLMWorldScheduler.SaveData llmScheduler)
    {
        if (llmScheduler == null)
            return;

        LLMWorldScheduler scheduler = dir != null ? dir.llmWorldScheduler : null;
        if (scheduler == null)
            scheduler = LLMWorldScheduler.Instance;
        if (scheduler == null)
        {
            Debug.LogWarning("[MapSaveSystem] Could not restore LLM scheduler state because no scheduler is available.", this);
            return;
        }

        scheduler.RestoreSaveData(llmScheduler);
    }

    private void ApplySavedLLMDebugState(LLMDebugMonitor.SaveData llmDebug)
    {
        if (llmDebug == null)
            return;

        LLMDebugMonitor monitor = dir != null ? dir.llmDebugMonitor : null;
        if (monitor == null && Dir.Instance != null)
            monitor = Dir.Instance.llmDebugMonitor;
        if (monitor == null)
            monitor = FindFirstObjectByType<LLMDebugMonitor>();

        if (monitor == null)
            return;

        monitor.RestoreSaveData(llmDebug);
    }

    private static bool TryParseModuleFlags(string raw, out ModuleFlags moduleFlags)
    {
        moduleFlags = ModuleFlags.none;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (!ulong.TryParse(raw, out ulong value))
            return false;

        moduleFlags = (ModuleFlags)value;
        return true;
    }

    [Serializable]
    private sealed class MapSaveData
    {
        public int version;
        public string createdUtc;
        public int mapWidth;
        public int mapHeight;
        public List<RoomDto> rooms = new();
        public List<WorldObjectDto> worldObjects = new();
        public List<PackDto> packs = new();
        public ScentPhysicsDto scentPhysics;
        public BottomBanner.SaveData bottomBanner;
        public LLMWorldScheduler.SaveData llmScheduler;
        public LLMDebugMonitor.SaveData llmDebug;

        public static MapSaveData FromGenerator(DungeonGenerator generator)
        {
            MapSaveData data = new()
            {
                version = MapSaveVersion,
                createdUtc = DateTime.UtcNow.ToString("o"),
                mapWidth = generator.cfg != null ? generator.cfg.mapWidth : 0,
                mapHeight = generator.cfg != null ? generator.cfg.mapHeight : 0,
                rooms = new List<RoomDto>()
            };

            foreach (Room room in generator.rooms)
                data.rooms.Add(RoomDto.FromRoom(room));

            WorldObjectRegistry registry = WorldObjectRegistry.Instance;
            if (registry != null)
            {
                data.worldObjects = new List<WorldObjectDto>();
                foreach (WorldObject worldObject in registry.GetAllObjects())
                {
                    if (worldObject == null || worldObject.ObjectId <= 0)
                        continue;

                    data.worldObjects.Add(WorldObjectDto.FromWorldObject(worldObject));
                }
            }

            PackManager packManager = Dir.Instance != null ? Dir.Instance.packManager : null;
            if (packManager != null && packManager.packs != null)
            {
                data.packs = new List<PackDto>();
                foreach (Pack pack in packManager.packs)
                {
                    if (pack == null)
                        continue;

                    data.packs.Add(PackDto.FromPack(pack));
                }
            }

            data.scentPhysics = ScentPhysicsDto.FromDir(generator.dir != null ? generator.dir : Dir.Instance);
            data.bottomBanner = BottomBanner.CaptureSaveData();
            data.llmScheduler = CaptureLLMScheduler(generator.dir != null ? generator.dir : Dir.Instance);
            data.llmDebug = CaptureLLMDebugMonitor(generator.dir != null ? generator.dir : Dir.Instance);

            return data;
        }

        public List<Room> ToRooms()
        {
            List<Room> restoredRooms = new();
            foreach (RoomDto room in rooms)
                restoredRooms.Add(room.ToRoom());
            return restoredRooms;
        }

        private static LLMWorldScheduler.SaveData CaptureLLMScheduler(Dir dir)
        {
            LLMWorldScheduler scheduler = dir != null ? dir.llmWorldScheduler : null;
            if (scheduler == null)
                scheduler = LLMWorldScheduler.Instance;

            return scheduler != null ? scheduler.CaptureSaveData() : null;
        }

        private static LLMDebugMonitor.SaveData CaptureLLMDebugMonitor(Dir dir)
        {
            LLMDebugMonitor monitor = dir != null ? dir.llmDebugMonitor : null;
            if (monitor == null && Dir.Instance != null)
                monitor = Dir.Instance.llmDebugMonitor;

            return monitor != null ? monitor.CaptureSaveData() : null;
        }
    }

    [Serializable]
    private sealed class PackDto
    {
        public string packName;
        public bool isPlayerPack;
        public int formation;
        public int leadershipType;
        public int followerType;
        public float formationSpacing;
        public List<int> memberObjectIds = new();

        public static PackDto FromPack(Pack pack)
        {
            PackDto dto = new()
            {
                packName = pack.packName,
                isPlayerPack = pack.isPlayerPack,
                formation = (int)pack.formation,
                leadershipType = (int)pack.leadershipType,
                followerType = (int)pack.followerType,
                formationSpacing = pack.formationSpacing,
                memberObjectIds = new List<int>()
            };

            if (pack.packAgentList != null)
            {
                foreach (WorldObject member in pack.packAgentList)
                {
                    if (member != null && member.ObjectId > 0)
                        dto.memberObjectIds.Add(member.ObjectId);
                }
            }

            return dto;
        }
    }

    [Serializable]
    private sealed class WorldObjectDto
    {
        public int objectId;
        public string displayName;
        public int kind;
        public int species;
        public string breed;
        public bool activeSelf;
        public Vector3Dto position;
        public QuaternionDto rotation;
        public Vector3Dto localScale;
        public Vector3Dto adjustMapToWorld;
        public float sizeRadius;
        public float weight;
        public bool immovable;
        public int parentWorldObjectId;
        public string prefabId;
        public string prefabResourcesPath;
        public string prefabAssetPath;
        public string moduleFlagsRaw;
        public string moduleFlagsNames;
        public AgentDto agent;
        public ContainerDto container;
        public PlacementDto placement;
        public ScentEmitterDto scentEmitter;
        public MessageQueueDto messageQueues;
        public LLMStateDto llmState;
        public TaskController.SaveData taskController;

        public static WorldObjectDto FromWorldObject(WorldObject worldObject)
        {
            ModuleFlags moduleFlags = ModuleFlagsFromWorldObject(worldObject);
            SavePrefabId savePrefabId = worldObject.GetComponent<SavePrefabId>();
            return new WorldObjectDto
            {
                objectId = worldObject.ObjectId,
                displayName = worldObject.DisplayName,
                kind = (int)worldObject.Kind,
                species = (int)worldObject.species,
                breed = worldObject.breed,
                activeSelf = worldObject.gameObject.activeSelf,
                position = Vector3Dto.FromVector3(worldObject.transform.position),
                rotation = QuaternionDto.FromQuaternion(worldObject.transform.rotation),
                localScale = Vector3Dto.FromVector3(worldObject.transform.localScale),
                adjustMapToWorld = Vector3Dto.FromVector3(worldObject.adjustMapToWorld),
                sizeRadius = worldObject.sizeRadius,
                weight = worldObject.weight,
                immovable = worldObject.immovable,
                parentWorldObjectId = FindParentWorldObjectId(worldObject),
                prefabId = savePrefabId != null ? savePrefabId.PrefabId : "",
                prefabResourcesPath = savePrefabId != null ? savePrefabId.ResourcesPath : "",
                prefabAssetPath = savePrefabId != null ? savePrefabId.AssetPath : "",
                moduleFlagsRaw = ((ulong)moduleFlags).ToString(),
                moduleFlagsNames = moduleFlags.ToString(),
                agent = AgentDto.FromWorldObject(worldObject),
                container = ContainerDto.FromWorldObject(worldObject),
                placement = PlacementDto.FromWorldObject(worldObject),
                scentEmitter = ScentEmitterDto.FromWorldObject(worldObject),
                messageQueues = MessageQueueDto.FromWorldObject(worldObject),
                llmState = LLMStateDto.FromWorldObject(worldObject),
                taskController = worldObject.taskController != null
                    ? worldObject.taskController.CaptureSaveData()
                    : null
            };
        }

        private static int FindParentWorldObjectId(WorldObject worldObject)
        {
            if (worldObject == null || worldObject.transform.parent == null)
                return -1;

            WorldObject parent = worldObject.transform.parent.GetComponentInParent<WorldObject>();
            if (parent == null || parent == worldObject || parent.ObjectId <= 0)
                return -1;

            return parent.ObjectId;
        }

        private static ModuleFlags ModuleFlagsFromWorldObject(WorldObject worldObject)
        {
            ModuleFlags flags = ModuleFlags.none;
            if (worldObject.hearingModule != null) flags |= ModuleFlags.hearingModule;
            if (worldObject.scentPerceptionModule != null) flags |= ModuleFlags.scentPerceptionModule;
            if (worldObject.visionPerceptionModule != null) flags |= ModuleFlags.visionPerceptionModule;
            if (worldObject.TasteModule != null) flags |= ModuleFlags.tasteModule;
            if (worldObject.worldMemoryModule != null) flags |= ModuleFlags.worldMemoryModule;
            if (worldObject.playerDecisionModule != null) flags |= ModuleFlags.playerDecisionModule;
            if (worldObject.followerDecisionModule != null) flags |= ModuleFlags.followerDecisionModule;
            if (worldObject.wandererDecisionModule != null) flags |= ModuleFlags.wanderDecisionModule;
            if (worldObject.immobileDecisionModule != null) flags |= ModuleFlags.immobileDecisionModule;
            if (worldObject.taskFollowerDecisionModule != null) flags |= ModuleFlags.taskFollowerDecisionModule;
            if (worldObject.exploreDecisionModule != null) flags |= ModuleFlags.exploreDecisionModule;
            if (worldObject.kineticModule != null) flags |= ModuleFlags.kineticModule;
            if (worldObject.agentModule != null) flags |= ModuleFlags.agentModule;
            if (worldObject.agentMovementModule != null) flags |= ModuleFlags.agentMovementModule;
            if (worldObject.packMemberModule != null) flags |= ModuleFlags.packMemberModule;
            if (worldObject.llmThinkModule != null) flags |= ModuleFlags.llmThinkModule;
            if (worldObject.reactionModule != null) flags |= ModuleFlags.reactionModule;
            if (worldObject.motivationModule != null) flags |= ModuleFlags.motivationModule;
            if (worldObject.activatorModule != null) flags |= ModuleFlags.activatorModule;
            if (worldObject.interactionModule != null) flags |= ModuleFlags.interactionModule;
            if (worldObject.motionModule != null) flags |= ModuleFlags.motionModule;
            if (worldObject.appearanceModule != null) flags |= ModuleFlags.appearanceModule;
            if (worldObject.noiseMakerModule != null) flags |= ModuleFlags.noiseMakerModule;
            if (worldObject.scentEmitterModule != null) flags |= ModuleFlags.scentEmitterModule;
            if (worldObject.blackboardModule != null) flags |= ModuleFlags.blackboardModule;
            if (worldObject.agentStateModule != null) flags |= ModuleFlags.agentStateModule;
            if (worldObject.taskListModule != null) flags |= ModuleFlags.taskListModule;
            if (worldObject.containerModule != null) flags |= ModuleFlags.containerModule;
            if (worldObject.llmConfigModule != null) flags |= ModuleFlags.llmConfigModule;
            if (worldObject.llmWorldStateModule != null) flags |= ModuleFlags.llmWorldStateModule;
            if (worldObject.fetchQuestModule != null) flags |= ModuleFlags.fetchQuestModule;
            if (worldObject.locationModule != null) flags |= ModuleFlags.locationModule;
            if (worldObject.placementModule != null) flags |= ModuleFlags.placementModule;
            if (worldObject.doorModule != null) flags |= ModuleFlags.doorModule;
            return flags;
        }
    }

    [Serializable]
    private sealed class AgentDto
    {
        public int currentDecisionType;
        public int initialDecisionType;
        public int walkMode;
        public bool isPackLeader;
        public string currentPackName;

        public static AgentDto FromWorldObject(WorldObject worldObject)
        {
            if (worldObject == null || worldObject.agentModule == null)
                return null;

            AgentDecisionType currentDecision = worldObject.agentModule.currentDecisionModule != null
                ? worldObject.agentModule.currentDecisionModule.DecisionType
                : AgentDecisionType.Undefined;

            return new AgentDto
            {
                currentDecisionType = (int)currentDecision,
                initialDecisionType = (int)worldObject.agentModule.initialDecisionType,
                walkMode = worldObject.motionModule != null ? (int)worldObject.motionModule.currentWalkMode : (int)WalkMode.None,
                isPackLeader = worldObject.packMemberModule != null && worldObject.packMemberModule.isLeader,
                currentPackName = worldObject.packMemberModule != null && worldObject.packMemberModule.currentPack != null
                    ? worldObject.packMemberModule.currentPack.packName
                    : ""
            };
        }
    }

    [Serializable]
    private sealed class ContainerDto
    {
        public int itemCapacity;
        public float maxWeight;
        public bool heldItemsVisible;
        public float heldHeight;
        public bool isLocked;
        public bool isClosed;
        public bool autoPickupNearbyItems;
        public float pickupRadiusTiles;
        public bool autoConfigureAgentCapacity;
        public int dogItemCapacity;
        public int humanItemCapacity;
        public List<int> heldObjectIds = new();

        public static ContainerDto FromWorldObject(WorldObject worldObject)
        {
            ContainerModule container = worldObject != null ? worldObject.containerModule : null;
            if (container == null)
                return null;

            ContainerDto dto = new()
            {
                itemCapacity = container.itemCapacity,
                maxWeight = container.maxWeight,
                heldItemsVisible = container.heldItemsVisible,
                heldHeight = container.heldHeight,
                isLocked = container.isLocked,
                isClosed = container.isClosed,
                autoPickupNearbyItems = container.autoPickupNearbyItems,
                pickupRadiusTiles = container.pickupRadiusTiles,
                autoConfigureAgentCapacity = container.autoConfigureAgentCapacity,
                dogItemCapacity = container.dogItemCapacity,
                humanItemCapacity = container.humanItemCapacity,
                heldObjectIds = new List<int>()
            };

            foreach (WorldObject heldItem in container.HeldItems)
            {
                if (heldItem != null && heldItem.ObjectId > 0)
                    dto.heldObjectIds.Add(heldItem.ObjectId);
            }

            return dto;
        }
    }

    [Serializable]
    private sealed class PlacementDto
    {
        public int allowedRooms;
        public bool autoSizeFromMesh;
        public Vector3Dto sizeInCells;
        public float cellSize;
        public int edgeHint;
        public int rotationRule;
        public int minClearCellsAround;
        public bool mustTouchWall;
        public float wallPadding;

        public static PlacementDto FromWorldObject(WorldObject worldObject)
        {
            PlacementModule placement = worldObject != null ? worldObject.placementModule : null;
            if (placement == null)
                return null;

            return new PlacementDto
            {
                allowedRooms = (int)placement.allowedRooms,
                autoSizeFromMesh = placement.autoSizeFromMesh,
                sizeInCells = Vector3Dto.FromVector3(placement.sizeInCells),
                cellSize = placement.cellSize,
                edgeHint = (int)placement.edgeHint,
                rotationRule = (int)placement.rotationRule,
                minClearCellsAround = placement.minClearCellsAround,
                mustTouchWall = placement.mustTouchWall,
                wallPadding = placement.wallPadding
            };
        }
    }

    [Serializable]
    private sealed class ScentEmitterDto
    {
        public float depositTimeLeft;
        public ScentSourceDto normalScentSource;
        public ScentSourceDto onDemandScentSource;
        public List<DurationScentSourceDto> durationScentSources = new();

        public static ScentEmitterDto FromWorldObject(WorldObject worldObject)
        {
            ScentEmitterModule emitter = worldObject != null ? worldObject.scentEmitterModule : null;
            if (emitter == null)
                return null;

            ScentEmitterDto dto = new()
            {
                depositTimeLeft = emitter.deposit_time_left,
                normalScentSource = ScentSourceDto.FromScentSource(emitter.normalScentSource),
                onDemandScentSource = ScentSourceDto.FromScentSource(emitter.onDemandScentSource),
                durationScentSources = new List<DurationScentSourceDto>()
            };

            if (emitter.durationScentSources != null)
            {
                foreach (DurationScentSource durationSource in emitter.durationScentSources)
                {
                    DurationScentSourceDto durationDto = DurationScentSourceDto.FromDurationScentSource(durationSource);
                    if (durationDto != null)
                        dto.durationScentSources.Add(durationDto);
                }
            }

            return dto;
        }

        public void ApplyTo(ScentEmitterModule emitter, WorldObject owner)
        {
            if (emitter == null)
                return;

            emitter.deposit_time_left = depositTimeLeft;
            emitter.normalScentSource = normalScentSource != null
                ? normalScentSource.ToScentSource(owner)
                : emitter.normalScentSource;
            emitter.onDemandScentSource = onDemandScentSource != null
                ? onDemandScentSource.ToScentSource(owner)
                : emitter.onDemandScentSource;
            emitter.durationScentSources = new List<DurationScentSource>();

            if (durationScentSources != null)
            {
                foreach (DurationScentSourceDto durationDto in durationScentSources)
                {
                    DurationScentSource durationSource = durationDto != null ? durationDto.ToDurationScentSource(owner) : null;
                    if (durationSource != null)
                        emitter.durationScentSources.Add(durationSource);
                }
            }
        }
    }

    [Serializable]
    private sealed class MessageQueueDto
    {
        public List<string> llmRecentObservations = new();

        public static MessageQueueDto FromWorldObject(WorldObject worldObject)
        {
            if (worldObject == null || worldObject.llmWorldStateModule == null)
                return null;

            return new MessageQueueDto
            {
                llmRecentObservations = worldObject.llmWorldStateModule.CaptureRecentObservations()
            };
        }
    }

    [Serializable]
    private sealed class LLMStateDto
    {
        public LLMConfigModule.SaveData config;
        public LLMWorldStateModule.SaveData worldState;
        public LLMThinkModule.SaveData think;

        public static LLMStateDto FromWorldObject(WorldObject worldObject)
        {
            if (worldObject == null)
                return null;

            bool hasLLMState =
                worldObject.llmConfigModule != null ||
                worldObject.llmWorldStateModule != null ||
                worldObject.llmThinkModule != null;
            if (!hasLLMState)
                return null;

            return new LLMStateDto
            {
                config = worldObject.llmConfigModule != null
                    ? worldObject.llmConfigModule.CaptureSaveData()
                    : null,
                worldState = worldObject.llmWorldStateModule != null
                    ? worldObject.llmWorldStateModule.CaptureSaveData()
                    : null,
                think = worldObject.llmThinkModule != null
                    ? worldObject.llmThinkModule.CaptureSaveData()
                    : null
            };
        }
    }

    [Serializable]
    private sealed class DurationScentSourceDto
    {
        public ScentSourceDto scentSource;
        public float duration;
        public float timeRemaining;

        public static DurationScentSourceDto FromDurationScentSource(DurationScentSource source)
        {
            if (source == null)
                return null;

            return new DurationScentSourceDto
            {
                scentSource = ScentSourceDto.FromScentSource(source.scentSource),
                duration = source.duration,
                timeRemaining = source.time_remaining
            };
        }

        public DurationScentSource ToDurationScentSource(WorldObject fallbackAgent)
        {
            return new DurationScentSource
            {
                scentSource = scentSource != null ? scentSource.ToScentSource(fallbackAgent) : null,
                duration = duration,
                time_remaining = timeRemaining
            };
        }
    }

    [Serializable]
    private sealed class ScentPhysicsDto
    {
        public ScentAirGroundDto airGround;
        public ScentRegistryDto registry;
        public List<ScentCellDto> cells = new();

        public static ScentPhysicsDto FromDir(Dir dir)
        {
            if (dir == null || dir.scentAirGround == null)
                return null;

            return new ScentPhysicsDto
            {
                airGround = ScentAirGroundDto.FromScentAirGround(dir.scentAirGround),
                registry = ScentRegistryDto.FromScentRegistry(dir.scentRegistry),
                cells = ScentCellDto.FromScentAirGround(dir.scentAirGround)
            };
        }
    }

    [Serializable]
    private sealed class ScentAirGroundDto
    {
        public int currentAgentId;
        public int previousAgentIdVisualized;
        public bool airScentVisible;
        public float airScentDepositRate;
        public float airDiffusionRate;
        public float airDecayRate;
        public bool groundScentVisible;
        public float groundScentDepositRate;
        public float groundDiffusionRate;
        public float groundDecayRate;
        public float airToGroundRate;
        public float groundToAirRate;
        public float simulationTimeStep;
        public bool runOnStart;
        public float practicallyZero;
        public bool enableScentDiagnostics;
        public int scentDiagnosticsEveryNFrames;
        public bool logScentReclamation;
        public float scentVisualThreshold;
        public float maxVisualIntensity;
        public ColorDto airBaseColor;
        public ColorDto groundBaseColor;

        public static ScentAirGroundDto FromScentAirGround(ScentAirGround scentAirGround)
        {
            if (scentAirGround == null)
                return null;

            return new ScentAirGroundDto
            {
                currentAgentId = scentAirGround.currentAgentId,
                previousAgentIdVisualized = scentAirGround.previousAgentIdVisualized,
                airScentVisible = scentAirGround.airScentVisible,
                airScentDepositRate = scentAirGround.airScentDepositRate,
                airDiffusionRate = scentAirGround.airDiffusionRate,
                airDecayRate = scentAirGround.airDecayRate,
                groundScentVisible = scentAirGround.groundScentVisible,
                groundScentDepositRate = scentAirGround.groundScentDepositRate,
                groundDiffusionRate = scentAirGround.groundDiffusionRate,
                groundDecayRate = scentAirGround.groundDecayRate,
                airToGroundRate = scentAirGround.airToGroundRate,
                groundToAirRate = scentAirGround.groundToAirRate,
                simulationTimeStep = scentAirGround.SimulationTimeStep,
                runOnStart = scentAirGround.runOnStart,
                practicallyZero = scentAirGround.practically_zero,
                enableScentDiagnostics = scentAirGround.enableScentDiagnostics,
                scentDiagnosticsEveryNFrames = scentAirGround.scentDiagnosticsEveryNFrames,
                logScentReclamation = scentAirGround.logScentReclamation,
                scentVisualThreshold = scentAirGround.scentVisualThreshold,
                maxVisualIntensity = scentAirGround.maxVisualIntensity,
                airBaseColor = ColorDto.FromColor(scentAirGround.airBaseColor),
                groundBaseColor = ColorDto.FromColor(scentAirGround.groundBaseColor)
            };
        }
    }

    [Serializable]
    private sealed class ScentRegistryDto
    {
        public string selectedTargetScentKey;
        public List<ScentSourceDto> sources = new();

        public static ScentRegistryDto FromScentRegistry(ScentRegistry registry)
        {
            if (registry == null)
                return null;

            ScentRegistryDto dto = new()
            {
                selectedTargetScentKey = registry.SelectedTargetScentKey,
                sources = new List<ScentSourceDto>()
            };

            if (registry.allScentSources != null)
            {
                foreach (ScentSource source in registry.allScentSources)
                {
                    ScentSourceDto sourceDto = ScentSourceDto.FromScentSource(source);
                    if (sourceDto != null)
                        dto.sources.Add(sourceDto);
                }
            }

            return dto;
        }
    }

    [Serializable]
    private sealed class ScentSourceDto
    {
        public int agentId;
        public int agentObjectId;
        public int category;
        public string scentName;
        public ColorDto categoryColor;
        public ColorDto sourceAirColor;
        public ColorDto sourceGroundColor;
        public float airDepositRate;
        public float groundDepositRate;
        public int familiarity;
        public float sensitivityBoost;
        public string persistentId;

        public static ScentSourceDto FromScentSource(ScentSource source)
        {
            if (source == null)
                return null;

            return new ScentSourceDto
            {
                agentId = source.agentId,
                agentObjectId = source.agent != null ? source.agent.ObjectId : -1,
                category = (int)source.category,
                scentName = source.scentName,
                categoryColor = ColorDto.FromColor(source.categoryColor),
                sourceAirColor = ColorDto.FromColor(source.sourceAirColor),
                sourceGroundColor = ColorDto.FromColor(source.sourceGroundColor),
                airDepositRate = source.airDepositRate,
                groundDepositRate = source.groundDepositRate,
                familiarity = (int)source.familiarity,
                sensitivityBoost = source.sensitivityBoost,
                persistentId = source.persistentId
            };
        }

        public ScentSource ToScentSource(Dictionary<int, WorldObject> worldObjects)
        {
            WorldObject agent = null;
            if (worldObjects != null && agentObjectId > 0)
                worldObjects.TryGetValue(agentObjectId, out agent);

            return ToScentSource(agent);
        }

        public ScentSource ToScentSource(WorldObject fallbackAgent)
        {
            WorldObject agent = fallbackAgent;
            if (agent == null && agentObjectId > 0 && WorldObjectRegistry.Instance != null)
                WorldObjectRegistry.Instance.TryGet(agentObjectId, out agent);

            return new ScentSource
            {
                agentId = agentId,
                agent = agent,
                category = (ScentCategory)category,
                scentName = scentName,
                categoryColor = categoryColor.ToColor(),
                sourceAirColor = sourceAirColor.ToColor(),
                sourceGroundColor = sourceGroundColor.ToColor(),
                airDepositRate = airDepositRate,
                groundDepositRate = groundDepositRate,
                familiarity = (ScentFamiliarity)familiarity,
                sensitivityBoost = sensitivityBoost,
                persistentId = persistentId
            };
        }
    }

    [Serializable]
    private sealed class ScentCellDto
    {
        public int x;
        public int y;
        public int height;
        public List<ScentInCellDto> scents = new();

        public static List<ScentCellDto> FromScentAirGround(ScentAirGround scentAirGround)
        {
            List<ScentCellDto> scentCells = new();
            if (scentAirGround == null || scentAirGround.cellsContainingScents == null)
                return scentCells;

            foreach (Cell cell in scentAirGround.cellsContainingScents)
            {
                ScentCellDto cellDto = FromCell(cell);
                if (cellDto != null)
                    scentCells.Add(cellDto);
            }

            return scentCells;
        }

        private static ScentCellDto FromCell(Cell cell)
        {
            if (cell == null || cell.scents == null || cell.scents.Count == 0)
                return null;

            ScentCellDto dto = new()
            {
                x = cell.x,
                y = cell.y,
                height = cell.height,
                scents = new List<ScentInCellDto>()
            };

            foreach (ScentInCell scent in cell.scents)
            {
                ScentInCellDto scentDto = ScentInCellDto.FromScentInCell(scent);
                if (scentDto != null)
                    dto.scents.Add(scentDto);
            }

            return dto.scents.Count > 0 ? dto : null;
        }
    }

    [Serializable]
    private sealed class ScentInCellDto
    {
        public int agentId;
        public float airIntensity;
        public float airNextDelta;
        public float groundIntensity;
        public float groundNextDelta;

        public static ScentInCellDto FromScentInCell(ScentInCell scent)
        {
            if (scent == null)
                return null;

            return new ScentInCellDto
            {
                agentId = scent.agentId,
                airIntensity = scent.airIntensity,
                airNextDelta = scent.airNextDelta,
                groundIntensity = scent.groundIntensity,
                groundNextDelta = scent.groundNextDelta
            };
        }

        public ScentInCell ToScentInCell()
        {
            return new ScentInCell
            {
                agentId = agentId,
                airIntensity = airIntensity,
                airNextDelta = airNextDelta,
                airLastVisualized = -1f,
                airGOindex = -1,
                groundIntensity = groundIntensity,
                groundNextDelta = groundNextDelta,
                groundLastVisualized = -1f,
                groundGOindex = -1
            };
        }
    }

    [Serializable]
    private sealed class RoomDto
    {
        public int myRoomNumber;
        public string name;
        public List<CellDto> cells = new();
        public List<DoorDto> doors = new();
        public ColorDto colorFloor;
        public List<int> neighbors = new();
        public bool isCorridor;
        public bool connectedToCorridor;
        public float ceilingHeight;
        public bool isOutdoor;
        public ColorDto colorCeiling;
        public int placementTypes;
        public int area;
        public RectIntDto bounds;

        public static RoomDto FromRoom(Room room)
        {
            RoomDto dto = new()
            {
                myRoomNumber = room.my_room_number,
                name = room.name,
                colorFloor = ColorDto.FromColor(room.colorFloor),
                neighbors = room.neighbors != null ? new List<int>(room.neighbors) : new List<int>(),
                isCorridor = room.isCorridor,
                connectedToCorridor = room.connectedToCorridor,
                ceilingHeight = room.ceilingHeight,
                isOutdoor = room.isOutdoor,
                colorCeiling = ColorDto.FromColor(room.colorCeiling),
                placementTypes = (int)room.placementTypes,
                area = room.area,
                bounds = RectIntDto.FromRect(room.bounds)
            };

            if (room.cells != null)
            {
                foreach (Cell cell in room.cells)
                    dto.cells.Add(CellDto.FromCell(cell));
            }

            if (room.doors != null)
            {
                foreach (Door door in room.doors)
                    dto.doors.Add(DoorDto.FromDoor(door));
            }

            return dto;
        }

        public Room ToRoom()
        {
            Room room = new()
            {
                my_room_number = myRoomNumber,
                name = name ?? "",
                cells = new List<Cell>(),
                doors = new List<Door>(),
                colorFloor = colorFloor.ToColor(),
                neighbors = neighbors != null ? new List<int>(neighbors) : new List<int>(),
                isCorridor = isCorridor,
                connectedToCorridor = connectedToCorridor,
                ceilingHeight = ceilingHeight,
                isOutdoor = isOutdoor,
                colorCeiling = colorCeiling.ToColor(),
                placementTypes = (PlacementRoomTypeFlags)placementTypes,
                area = area,
                bounds = bounds.ToRect()
            };

            if (cells != null)
            {
                foreach (CellDto cell in cells)
                    room.cells.Add(cell.ToCell());
            }

            if (doors != null)
            {
                foreach (DoorDto door in doors)
                    room.doors.Add(door.ToDoor());
            }

            room.cell_dictionary_room = new();
            return room;
        }
    }

    [Serializable]
    private sealed class CellDto
    {
        public int x;
        public int y;
        public int height;
        public int roomNumber;
        public int type;
        public int walls;
        public int doors;
        public ColorDto colorFloor;
        public QuaternionDto tiltFloor;
        public float travelCost;
        public bool isCorridor;

        public static CellDto FromCell(Cell cell)
        {
            return new CellDto
            {
                x = cell.x,
                y = cell.y,
                height = cell.height,
                roomNumber = cell.room_number,
                type = cell.type,
                walls = (int)cell.walls,
                doors = (int)cell.doors,
                colorFloor = ColorDto.FromColor(cell.colorFloor),
                tiltFloor = QuaternionDto.FromQuaternion(cell.tiltFloor),
                travelCost = cell.travel_cost,
                isCorridor = cell.isCorridor
            };
        }

        public Cell ToCell()
        {
            return new Cell(x, y, height)
            {
                room_number = roomNumber,
                type = type,
                walls = (DirFlags)walls,
                doors = (DirFlags)doors,
                colorFloor = colorFloor.ToColor(),
                tiltFloor = tiltFloor.ToQuaternion(),
                travel_cost = travelCost,
                isCorridor = isCorridor
            };
        }
    }

    [Serializable]
    private sealed class DoorDto
    {
        public int id;
        public int ownerRoomIndex;
        public DoorAnchorDto anchor;
        public int cellX;
        public int cellY;
        public int openDir;
        public int partnerDoorId;
        public int neighborRoomIndex;
        public int flags;
        public int material;
        public int style;
        public int hinge;
        public float openAngleDeg;
        public float openSpeed;
        public ColorDto color;
        public string keyTag;
        public int lockDifficulty;
        public int trapDifficulty;
        public string note;

        public static DoorDto FromDoor(Door door)
        {
            return new DoorDto
            {
                id = door.id,
                ownerRoomIndex = door.ownerRoomIndex,
                anchor = DoorAnchorDto.FromDoorAnchor(door.anchor),
                cellX = door.cell.x,
                cellY = door.cell.y,
                openDir = (int)door.openDir,
                partnerDoorId = door.partnerDoorId,
                neighborRoomIndex = door.neighborRoomIndex,
                flags = (int)door.flags,
                material = (int)door.material,
                style = (int)door.style,
                hinge = (int)door.hinge,
                openAngleDeg = door.openAngleDeg,
                openSpeed = door.openSpeed,
                color = ColorDto.FromColor(door.color),
                keyTag = door.keyTag,
                lockDifficulty = door.lockDifficulty,
                trapDifficulty = door.trapDifficulty,
                note = door.note
            };
        }

        public Door ToDoor()
        {
            return new Door
            {
                id = id,
                ownerRoomIndex = ownerRoomIndex,
                anchor = anchor.ToDoorAnchor(),
                cell = new Vector2Int(cellX, cellY),
                openDir = (Direction4)openDir,
                partnerDoorId = partnerDoorId,
                neighborRoomIndex = neighborRoomIndex,
                flags = (DoorFlags)flags,
                material = (DoorMaterial)material,
                style = (Door.DoorStyle)style,
                hinge = (Door.HingeSide)hinge,
                openAngleDeg = openAngleDeg,
                openSpeed = openSpeed,
                color = color.ToColor(),
                keyTag = keyTag ?? "",
                lockDifficulty = lockDifficulty,
                trapDifficulty = trapDifficulty,
                note = note ?? ""
            };
        }
    }

    [Serializable]
    private sealed class DoorAnchorDto
    {
        public int type;
        public int aEntryX;
        public int aEntryY;
        public int bEntryX;
        public int bEntryY;
        public int normal;
        public int wallStartX;
        public int wallStartY;
        public int throughDepthTiles;
        public int spanTiles;

        public static DoorAnchorDto FromDoorAnchor(DoorAnchor anchor)
        {
            return new DoorAnchorDto
            {
                type = (int)anchor.type,
                aEntryX = anchor.aEntry.x,
                aEntryY = anchor.aEntry.y,
                bEntryX = anchor.bEntry.x,
                bEntryY = anchor.bEntry.y,
                normal = (int)anchor.normal,
                wallStartX = anchor.wallStart.x,
                wallStartY = anchor.wallStart.y,
                throughDepthTiles = anchor.throughDepthTiles,
                spanTiles = anchor.spanTiles
            };
        }

        public DoorAnchor ToDoorAnchor()
        {
            return new DoorAnchor
            {
                type = (DoorAnchorType)type,
                aEntry = new Vector2Int(aEntryX, aEntryY),
                bEntry = new Vector2Int(bEntryX, bEntryY),
                normal = (Direction4)normal,
                wallStart = new Vector2Int(wallStartX, wallStartY),
                throughDepthTiles = throughDepthTiles,
                spanTiles = spanTiles
            };
        }
    }

    [Serializable]
    private struct ColorDto
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public static ColorDto FromColor(Color color)
        {
            return new ColorDto { r = color.r, g = color.g, b = color.b, a = color.a };
        }

        public Color ToColor()
        {
            return new Color(r, g, b, a);
        }
    }

    [Serializable]
    private struct Vector3Dto
    {
        public float x;
        public float y;
        public float z;

        public static Vector3Dto FromVector3(Vector3 value)
        {
            return new Vector3Dto { x = value.x, y = value.y, z = value.z };
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    [Serializable]
    private struct QuaternionDto
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public static QuaternionDto FromQuaternion(Quaternion rotation)
        {
            return new QuaternionDto { x = rotation.x, y = rotation.y, z = rotation.z, w = rotation.w };
        }

        public Quaternion ToQuaternion()
        {
            return new Quaternion(x, y, z, w);
        }
    }

    [Serializable]
    private struct RectIntDto
    {
        public int x;
        public int y;
        public int width;
        public int height;

        public static RectIntDto FromRect(RectInt rect)
        {
            return new RectIntDto { x = rect.x, y = rect.y, width = rect.width, height = rect.height };
        }

        public RectInt ToRect()
        {
            return new RectInt(x, y, width, height);
        }
    }
}
