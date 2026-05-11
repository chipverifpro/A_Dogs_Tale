using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public partial class DungeonGenerator
{
    private const int MapSaveVersion = 1;
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

            BottomBanner.Show($"Map saved to {savePath}");
            Debug.Log($"[MapSaveSystem] Saved map to {savePath}", this);
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

        buildComplete = true;
        regenerateCoroutine = null;

        if (dir != null && dir.scentAirGround != null)
            dir.scentAirGround.StartScentSimulation();

        BottomBanner.Show($"Map loaded from {savePath}");
        Debug.Log($"[MapSaveSystem] Loaded map from {savePath}", this);
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

    [Serializable]
    private sealed class MapSaveData
    {
        public int version;
        public string createdUtc;
        public int mapWidth;
        public int mapHeight;
        public List<RoomDto> rooms = new();

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

            return data;
        }

        public List<Room> ToRooms()
        {
            List<Room> restoredRooms = new();
            foreach (RoomDto room in rooms)
                restoredRooms.Add(room.ToRoom());
            return restoredRooms;
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
