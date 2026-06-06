using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.Mathematics;
using InspectorTools;

/* 
LocationModule is the sensor.

It should not move the dog at all.
It should inform MotionModule and AgentMovementModule.

LocationModule can answer questions MotionModule cannot, such as:
	•	“What cell am I standing in?”
	•	“Is this walkable floor?”
	•	“Is the ground sloped?”
	•	“Am I on a staircase? Ramp?”
	•	“Should I perform a landing animation?”
	•	“What is the world y-offset for snapped ground height?”
	•	“What objects can I interact with from here?”
	•	“Am I inside a certain region or zone?”
	•	“Should the minimap show this spot?”
	•	“Is the dog’s current pose above/below ground?”
    */

namespace DogGame.Modules
{
    [InspectorNote("Sensory_Modules/Location Module", "Gets information about position, orientation, cell, tilt.  Some features also available directly in WorldModule.")]
    [DisallowMultipleComponent]
    public class LocationModule : WorldModule
    {
        #region Parameters
        private const string HoleArchetypeId = "PF_Floor_Hole";
        private const string MoundArchetypeId = "PF_Floor_Mound";
        private static readonly Dictionary<int, Dictionary<int, string>> knownAgentDescriptionsByRoom = new();

        private int lastLoggedRoomId = int.MinValue;

        public Vector3 pos3d_world => this.transform.position;
        public Vector3 pos3d_map => worldObject != null ? worldObject.WorldToMapPosition(pos3d_world) : pos3d_world;

        // Raw map-space Vector3 order matches world-space component order: x, height, row.
        // The scalar x/y/z accessors below remain semantic grid accessors: x, row, height.
        public float x_f => pos3d_map.x;
        public float y_f => pos3d_map.z;  // semantic grid row
        public float z_f => pos3d_map.y;  // semantic height alias
        public float height_f => z_f;

        public int x => Mathf.FloorToInt(x_f);
        public int y => Mathf.FloorToInt(y_f);
        public int z => Mathf.FloorToInt(z_f);
        public int height => z;
        private int heightSteps => MapHeightToHeightSteps(z_f);

        public Vector3 pos3d_f => pos3d_map;
        public Vector3Int pos3d => new(x, z, y);
        public Vector2 pos2_f => new(x_f, y_f);
        public Vector2Int pos2 => new(x, y);

        public bool DisplayRoomSnapshot = false;
        
        #endregion
        #region Cell
        public Cell cell
        {
            get
            {
                if (dir == null || dir.gen == null || !dir.gen.buildComplete || dir.gen.hf == null)
                    return null;

                return dir.gen.GetCellFromHf(x, y, heightSteps, 50);
            }
        }

        #endregion
        #region Tilt & Yaw
        /// <summary>
        /// Full world rotation (includes yaw + pitch/roll tilt).
        /// If you only want "tilt without yaw", see TiltNoYaw below.
        /// </summary>
        public quaternion tilt => (quaternion)transform.rotation;

        /// <summary>
        /// Facing direction in degrees, 0 = north (+mapY = +worldZ), clockwise.
        /// Computed from the transform forward projected onto the ground plane.
        /// </summary>
        public float yawDeg
        {
            get
            {
                Vector3 forward = transform.forward;
                forward.y = 0f; // remove vertical component

                if (forward.sqrMagnitude < 1e-8f)
                    return 0f;

                forward.Normalize();

                // 0° when forward points to +worldZ (map +Y).
                // Clockwise means +worldX should be +90°.
                float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

                // Normalize to [0, 360)
                if (yaw < 0f) yaw += 360f;

                return yaw;
            }
        }

        public float yawRad => yawDeg * Mathf.Deg2Rad;

        /// <summary>
        /// Optional helper: tilt rotation with yaw removed (pitch/roll only).
        /// Useful if you want "Up on slope" independent of facing direction.
        /// </summary>
        public quaternion TiltNoYaw
        {
            get
            {
                // Remove yaw by premultiplying inverse yaw rotation.
                float yaw = yawDeg;
                Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
                Quaternion tiltOnly = transform.rotation * Quaternion.Inverse(yawRot);
                return (quaternion)tiltOnly;
            }
        }
        #endregion
        #region LogRooms
        public override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);

            if (!ShouldLogRoomTransitions())
                return;

            Cell currentCell = cell;
            int currentRoomId = currentCell != null ? currentCell.room_number : -1;
            if (currentRoomId == lastLoggedRoomId)
                return;

            if (lastLoggedRoomId == int.MinValue)
            {
                lastLoggedRoomId = currentRoomId;
                if (currentRoomId >= 0)
                    LogRoomSnapshot("Entered", currentRoomId);
                return;
            }

            if (lastLoggedRoomId >= 0)
                LogRoomSnapshot("Left", lastLoggedRoomId);

            lastLoggedRoomId = currentRoomId;

            if (currentRoomId >= 0)
                LogRoomSnapshot("Entered", currentRoomId);
        }

        private bool ShouldLogRoomTransitions()
        {
            if (!Application.isPlaying || worldObject == null || dir == null || dir.gen == null || !dir.gen.buildComplete)
                return false;

            return worldObject.Kind == WorldObjectKind.Agent || worldObject.agentModule != null;
        }

        private void LogRoomSnapshot(string action, int roomId)
        {
            string agentName = worldObject != null ? worldObject.DisplayName : name;
            string roomName = ResolveRoomName(roomId);
            RoomSnapshot snapshot = BuildRoomSnapshot(roomId);
//            BottomBanner.LogMessageWithIcon(
//                BannerSense.Vision,
//                BannerLevel.Low,
//                $"[{agentName}] {action} {roomName}. {snapshot.ToLogText()}",
//                "MapsSpriteSheet_2",
//                true);
            if (DisplayRoomSnapshot)
                Debug.Log($"[{agentName}] {action} {roomName}. {snapshot.ToLogText()}");
        }

        #endregion
        #region BuildSnap

        private RoomSnapshot BuildRoomSnapshot(int roomId)
        {
            RoomSnapshot snapshot = new RoomSnapshot();
            HashSet<int> currentAgentIds = new HashSet<int>();
            WorldMemoryModule memory = worldObject != null ? worldObject.worldMemoryModule : null;

            WorldObjectRegistry registry = WorldObjectRegistry.Instance;
            if (registry != null)
            {
                foreach (WorldObject candidate in registry.GetAllObjects())
                {
                    if (!IsVisibleRoomObject(candidate, roomId))
                        continue;

                    if (IsAgent(candidate))
                    {
                        if (candidate == worldObject || IsPackmate(candidate))
                            continue;

                        memory?.RecordSeenObject(candidate);
                        memory?.RecordContainerContents(candidate);
                        snapshot.agents.Add(DescribeAgent(candidate));
                        if (candidate.ObjectId > 0)
                            currentAgentIds.Add(candidate.ObjectId);
                        continue;
                    }

                    if (IsHeldByAnotherWorldObject(candidate))
                        continue;

                    if (IsContainer(candidate))
                    {
                        memory?.RecordSeenObject(candidate);
                        memory?.RecordContainerContents(candidate);
                        snapshot.containers.Add(DescribeContainer(candidate));
                    }
                    else if (candidate.Kind == WorldObjectKind.Item)
                    {
                        memory?.RecordSeenObject(candidate);
                        snapshot.items.Add(candidate.DisplayName);
                    }
                }
            }

            AddHoleDescriptions(roomId, snapshot.holes);
            AddPreviouslyContainedAgents(roomId, currentAgentIds, snapshot.previouslyContainedAgents);
            RememberCurrentAgents(roomId, currentAgentIds);

            snapshot.Sort();
            return snapshot;
        }

        private bool IsVisibleRoomObject(WorldObject candidate, int roomId)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
                return false;

            int candidateRoomId = ResolveObjectRoomId(candidate);
            return candidateRoomId == roomId;
        }

        private int ResolveObjectRoomId(WorldObject candidate)
        {
            if (candidate == null)
                return -1;

            if (candidate.locationModule != null)
            {
                Cell candidateCell = candidate.locationModule.cell;
                return candidateCell != null ? candidateCell.room_number : -1;
            }

            if (dir == null || dir.gen == null || !dir.gen.buildComplete || dir.gen.hf == null)
                return -1;

            Vector3 mapPos = candidate.pos3d_map;
            int mapX = Mathf.FloorToInt(mapPos.x);
            int mapY = Mathf.FloorToInt(mapPos.z);
            int mapZ = MapHeightToHeightSteps(mapPos.y);
            Cell cellAtObject = dir.gen.GetCellFromHf(mapX, mapY, mapZ, 50);
            return cellAtObject != null ? cellAtObject.room_number : -1;
        }

        private int MapHeightToHeightSteps(float mapY)
        {
            float unitHeight = dir != null && dir.cfg != null
                ? Mathf.Max(0.0001f, dir.cfg.unitHeight)
                : 1f;
            return Mathf.RoundToInt(mapY / unitHeight);
        }

        private static bool IsAgent(WorldObject obj)
        {
            return obj != null && (obj.Kind == WorldObjectKind.Agent || obj.agentModule != null);
        }

        private bool IsPackmate(WorldObject candidate)
        {
            if (worldObject == null || candidate == null || candidate == worldObject)
                return false;

            Pack selfPack = worldObject.packMemberModule != null ? worldObject.packMemberModule.currentPack : null;
            Pack candidatePack = candidate.packMemberModule != null ? candidate.packMemberModule.currentPack : null;
            return selfPack != null && candidatePack == selfPack;
        }

        private bool IsPackmateObjectId(int objectId)
        {
            if (objectId <= 0 || worldObject == null || worldObject.packMemberModule == null)
                return false;

            Pack selfPack = worldObject.packMemberModule.currentPack;
            if (selfPack == null || selfPack.packAgentList == null)
                return false;

            for (int i = 0; i < selfPack.packAgentList.Count; i++)
            {
                WorldObject packMember = selfPack.packAgentList[i];
                if (packMember != null && packMember != worldObject && packMember.ObjectId == objectId)
                    return true;
            }

            return false;
        }

        private static bool IsContainer(WorldObject obj)
        {
            return obj != null && !IsAgent(obj) &&
                   (obj.Kind == WorldObjectKind.Container || obj.containerModule != null);
        }

        private static bool IsHeldByAnotherWorldObject(WorldObject obj)
        {
            if (obj == null || obj.transform.parent == null)
                return false;

            WorldObject parentWorldObject = obj.transform.parent.GetComponentInParent<WorldObject>();
            return parentWorldObject != null && parentWorldObject != obj;
        }

        #endregion
        #region Describe

        private string DescribeAgent(WorldObject agent)
        {
            string carrying = DescribeHeldItems(agent);
            return string.IsNullOrEmpty(carrying)
                ? agent.DisplayName
                : $"{agent.DisplayName} (carrying {carrying})";
        }

        private string DescribeContainer(WorldObject container)
        {
            ContainerModule containerModule = container.containerModule;
            if (containerModule == null)
                return container.DisplayName;

            if (!containerModule.CanAccessContents(out _))
                return $"{container.DisplayName} (contents unknown)";

            string contents = DescribeHeldItems(container);
            return string.IsNullOrEmpty(contents)
                ? $"{container.DisplayName} (empty)"
                : $"{container.DisplayName} (contains {contents})";
        }

        private static string DescribeHeldItems(WorldObject owner)
        {
            if (owner == null || owner.containerModule == null || owner.containerModule.HeldItemCount <= 0)
                return string.Empty;

            List<string> names = new List<string>();
            IReadOnlyList<WorldObject> heldItems = owner.containerModule.HeldItems;
            for (int i = 0; i < heldItems.Count; i++)
            {
                WorldObject item = heldItems[i];
                if (item != null)
                    names.Add(item.DisplayName);
            }

            names.Sort();
            return string.Join(", ", names);
        }

        private void AddHoleDescriptions(int roomId, List<string> holes)
        {
            if (dir == null || dir.elementStore == null)
                return;

            ElementLayer floorLayer = dir.elementStore.GetLayer(ElementLayerKind.Floor);
            if (floorLayer == null || floorLayer.instances == null)
                return;

            int openHoleCount = 0;
            int filledHoleCount = 0;
            for (int i = 0; i < floorLayer.instances.Count; i++)
            {
                ElementInstanceData instance = floorLayer.instances[i];
                if (instance == null || instance.roomIndex != roomId)
                    continue;

                if (instance.archetypeId == HoleArchetypeId)
                {
                    openHoleCount++;
                    holes.Add($"Hole{openHoleCount}");
                }
                else if (instance.archetypeId == MoundArchetypeId)
                {
                    filledHoleCount++;
                    holes.Add($"FilledHole{filledHoleCount}");
                }
            }
        }

        private void AddPreviouslyContainedAgents(int roomId, HashSet<int> currentAgentIds, List<string> previousAgents)
        {
            if (!knownAgentDescriptionsByRoom.TryGetValue(roomId, out Dictionary<int, string> knownAgents))
                return;

            foreach (KeyValuePair<int, string> entry in knownAgents)
            {
                if (worldObject != null && entry.Key == worldObject.ObjectId)
                    continue;

                if (IsPackmateObjectId(entry.Key))
                    continue;

                WorldObjectRegistry registry = WorldObjectRegistry.Instance;
                if (registry != null && registry.TryGet(entry.Key, out WorldObject agent) && IsPackmate(agent))
                    continue;

                if (!currentAgentIds.Contains(entry.Key))
                    previousAgents.Add(entry.Value);
            }
        }

        #endregion
        #region Remember
        
        private void RememberCurrentAgents(int roomId, HashSet<int> currentAgentIds)
        {
            if (!knownAgentDescriptionsByRoom.TryGetValue(roomId, out Dictionary<int, string> knownAgents))
            {
                knownAgents = new Dictionary<int, string>();
                knownAgentDescriptionsByRoom[roomId] = knownAgents;
            }

            WorldObjectRegistry registry = WorldObjectRegistry.Instance;
            if (registry == null)
                return;

            foreach (int agentId in currentAgentIds)
            {
                if (registry.TryGet(agentId, out WorldObject agent) && agent != null)
                    knownAgents[agentId] = DescribeAgent(agent);
            }
        }

        private string ResolveRoomName(int roomId)
        {
            if (roomId < 0 || dir == null || dir.gen == null || dir.gen.rooms == null || roomId >= dir.gen.rooms.Count)
                return $"Room {roomId}";

            Room room = dir.gen.rooms[roomId];
            if (room == null)
                return $"Room {roomId}";

            if (!string.IsNullOrWhiteSpace(room.name) && !IsRawGeneratedRoomName(room.name, roomId))
                return room.name.Trim();

            return ResolveSemanticRoomName(dir.gen.rooms, roomId);
        }

        private string ResolveSemanticRoomName(List<Room> rooms, int roomId)
        {
            if (rooms == null || roomId < 0 || roomId >= rooms.Count || rooms[roomId] == null)
                return $"Room {roomId}";

            DungeonSettings settings = dir != null
                ? dir.cfg != null ? dir.cfg : dir.gen != null ? dir.gen.cfg : null
                : null;

            string label = RoomUseAssigner.GetRoomLabel(rooms[roomId], settings);
            int count = 0;
            for (int i = 0; i <= roomId && i < rooms.Count; i++)
            {
                Room candidate = rooms[i];
                if (candidate == null)
                    continue;

                if (RoomUseAssigner.GetRoomLabel(candidate, settings) == label)
                    count++;
            }

            return $"{label} {Mathf.Max(1, count)}";
        }

        private static bool IsRawGeneratedRoomName(string roomName, int roomId)
        {
            if (string.IsNullOrWhiteSpace(roomName))
                return true;

            string trimmed = roomName.Trim();
            return trimmed == $"Room {roomId}" || trimmed == $"Room {roomId + 1}";
        }

        #endregion
        #region RoomSnapshot

        private sealed class RoomSnapshot
        {
            public readonly List<string> agents = new List<string>();
            public readonly List<string> items = new List<string>();
            public readonly List<string> containers = new List<string>();
            public readonly List<string> holes = new List<string>();
            public readonly List<string> previouslyContainedAgents = new List<string>();

            public void Sort()
            {
                agents.Sort();
                items.Sort();
                containers.Sort();
                holes.Sort();
                previouslyContainedAgents.Sort();
            }

            public string ToLogText()
            {
                StringBuilder builder = new StringBuilder();
                AppendList(builder, "agents", agents);
                AppendList(builder, "items", items);
                AppendList(builder, "containers", containers);
                AppendList(builder, "holes", holes);
                AppendList(builder, "previously contained", previouslyContainedAgents);
                return builder.Length > 0 ? builder.ToString() : "room appears empty.";
            }

            private static void AppendList(StringBuilder builder, string label, List<string> values)
            {
                if (values == null || values.Count == 0)
                    return;

                if (builder.Length > 0)
                    builder.Append(' ');

                builder.Append(label);
                builder.Append(" = ");
                builder.Append(string.Join(", ", values));
                builder.Append('.');
            }
        }

        #endregion
    }
}
