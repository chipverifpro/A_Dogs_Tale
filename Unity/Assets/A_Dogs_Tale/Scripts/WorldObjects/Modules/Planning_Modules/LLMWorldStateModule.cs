#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using DogGame.LLM.Policy;
using DogGame.Modules;
using UnityEngine;
using UnityEngine.AI;
using InspectorTools;

        // This is the dynamic context provider for LLM agents. Today it mainly
        //   contributes leash text, position/room/door context, vision summaries,
        //   and queued task observations. 
        //   It is used by LLMConfigModule, 
        //   by the sidecar /world_state endpoint in UnitySidecarInboundServer.cs (line 204), 
        //   by exploration logic for door discovery in ExploreDecisionModule.cs (line 466), 
        //   and by TaskExecutor.cs (line 100) to store task reports as observations.

        // LLMWorldStateModule is active, but several of its “auto-populate” fields like
        //   distanceToPlayerMeters, isInCombat, isQuestCritical, and the summary strings
        //   appear to have read sites but no write sites in the repo. So the module is real,
        //   but parts of it are still unfinished.

namespace DogGame.LLM.Agent
{
    /// <summary>
    /// Dynamic, self-updating context for the LLM.
    /// This module should not require per-agent babysitting: it should populate itself
    /// from game systems (player, combat, perception, etc.).
    /// </summary>
    [InspectorNote("Planning_Modules/LLM World State Module", "Dynamic self-updating context for LLM.  It should populate itself (dynamic context).")]
    [DisallowMultipleComponent]
    public sealed class LLMWorldStateModule : WorldModule
    {
        public string positionContext = "";
        public string doorsContext = "";
        public int maxDoors = 5;

        public string leashContext = "";

        [Header("Perception / Signals (auto-populate at runtime)")]
        [Tooltip("Approx distance to player in meters.")]
        public float distanceToPlayerMeters = 999f;

        [Tooltip("True if this agent is currently engaged or threatened.")]
        public bool isInCombat = false;

        [Tooltip("True if this agent is quest/story critical (should think harder).")]
        public bool isQuestCritical = false;

        [Tooltip("True if the player is explicitly focusing this agent (selected/targeted/looked-at).")]
        public bool isPlayerFocusingThisNpc = false;

        [Tooltip("Rough nearby entity count used as a complexity signal.")]
        public int nearbyEntityCount = 0;

        [Header("Context Summaries (auto-populate at runtime)")]
        [TextArea(3, 12)]
        [Tooltip("Short bullet-ish summary of nearby entities/objects/hazards.")]
        public string nearbySummary = "";

        [TextArea(2, 10)]
        [Tooltip("Short summary of agent state: health, stamina, status effects, inventory highlights.")]
        public string statusSummary = "";

        [TextArea(2, 10)]
        [Tooltip("Short summary of current goals/intent framing, if you want it injected as context.")]
        public string goalsSummary = "";

        [TextArea(2, 10)]
        [Tooltip("Very recent events relevant to this agent, if any.")]
        public string recentEventsSummary = "";

        [Header("Context Controls")]
        [Tooltip("Caps the length of each summary block to reduce prompt bloat.")]
        [Range(100, 4000)]
        public int maxCharsPerBlock = 800;

        // ----- Location context -----
        [Header("Agent Location (auto-populate)")]
        [Tooltip("Agent world-space position (meters). Auto-filled from transform.")]
        public Vector3 agentWorldPosition;

        [Tooltip("Agent Cell, if known.")]
        public Cell? agentCell;

        [Tooltip("True if agentCell is valid this frame.")]
        public bool hasAgentCell = false;

        public int suggestedTravelRadius = 8;

        [Tooltip("Suggested nearby target cells (reachable/preferred) if known. Keep small (<=8).")]
        public Rect tgt = new();

        [SerializeField] private int maxVisionContextLines = 6;

        /// <summary>
        /// Build the inputs used by SophisticationPolicy.
        /// </summary>
        public SophisticationPolicy.Inputs BuildSophisticationInputs(bool isBoss)
        {
            return new SophisticationPolicy.Inputs
            {
                distanceToPlayerMeters = distanceToPlayerMeters,
                isInCombat = isInCombat,
                isQuestCritical = isQuestCritical,
                isBoss = isBoss,
                isPlayerFocusingThisNpc = isPlayerFocusingThisNpc,
                nearbyEntityCount = Mathf.Max(0, nearbyEntityCount)
            };
        }

        /// <summary>
        /// Add dynamic context blocks to the prompt.
        /// Keep these factual and compact (perception, state, goals), not "plans".
        /// </summary>
        public void AddContextBlocks(List<string> contextBlocks)
        {
            if (contextBlocks == null) throw new ArgumentNullException(nameof(contextBlocks));

            leashContext = LeashSystem.MyLeashToLLM(observer: worldObject);
            LimitAndAddBlock(contextBlocks, leashContext);

            positionContext = BuildPositionContextBlock();
            LimitAndAddBlock(contextBlocks, positionContext);

            // Future optional blocks:
            // LimitAndAddBlock(contextBlocks, nearbySummary);
            // LimitAndAddBlock(contextBlocks, statusSummary);
            // LimitAndAddBlock(contextBlocks, goalsSummary);
            // LimitAndAddBlock(contextBlocks, recentEventsSummary);
        }

        /// <summary>
        /// Build world state blocks for MCP / sidecar requests using a requested detail level.
        /// This is the entry point intended for get_world_state(detail).
        /// </summary>
        public List<string> BuildWorldStateBlocks(string detail = "normal")
        {
            List<string> contextBlocks = new();

            leashContext = LeashSystem.MyLeashToLLM(observer: worldObject);
            LimitAndAddBlock(contextBlocks, leashContext);

            positionContext = BuildPositionContextBlock(detail);
            LimitAndAddBlock(contextBlocks, positionContext);

            return contextBlocks;
        }

        /// <summary>
        /// Build a single compact text block for the current world state.
        /// This is the preferred return for Python get_world_state(detail).
        /// </summary>
        public string BuildWorldStateText(string detail = "normal")
        {
            List<string> blocks = BuildWorldStateBlocks(detail);

            if (blocks.Count == 0)
                return $"[{worldObject.DisplayName}] CONTEXT: No world state available.";

            StringBuilder stringBuilder = new(maxCharsPerBlock * Math.Max(1, blocks.Count));

            for (int index = 0; index < blocks.Count; index++)
            {
                if (index > 0)
                    stringBuilder.AppendLine();

                stringBuilder.Append(blocks[index]);
            }

            return stringBuilder.ToString().Trim();
        }

        private void LimitAndAddBlock(List<string> contextBlocks, string context)
        {
            if (string.IsNullOrWhiteSpace(context))
                return;

            if (context.Length > maxCharsPerBlock)
                context = context.Substring(0, maxCharsPerBlock);

            context = context.Trim();

            contextBlocks.Add(context);
        }

        /// <summary>
        /// Optional hook: populate fields from your game's systems.
        /// </summary>
        public void RefreshFromGameSystems()
        {
            // Future expansion point.
        }

        #region VisionContext
        public string GetVisionContextBlock()
        {
            var stringBuilder = new StringBuilder(1024);

            var vision = worldObject.visionPerceptionModule;
            if (vision == null)
                return "";

            var events = vision.GetPerceptionEvents();
            Debug.Log($"{worldObject.DisplayName} vision events.count={events.Count}");
            if (events == null || events.Count == 0)
                return "";

            var lines = events.ToLLMLines(maxVisionContextLines);
            if (lines.Count == 0)
                return "";

            stringBuilder.AppendLine("Vision:");
            for (int index = 0; index < lines.Count; index++)
                stringBuilder.Append(" - ").AppendLine(lines[index]);

            return stringBuilder.ToString();
        }
        #endregion

        #region PositionContext

        public string BuildPositionContextBlock(string detail = "normal")
        {
            string roomName;
            string roomType;
            RectInt worldBounds = new(0, 0, dir.cfg.mapWidth, dir.cfg.mapHeight);
            RectInt roomBounds;
            RectInt radiusBounds;

            RectInt clipRect;
            RectInt tgt;

            bool wantBrief = detail == "brief";
            bool wantDetailed = detail == "detailed";
            bool wantNormal = !(wantBrief || wantDetailed);

            string roomContext = "";

            Vector3 currentAgentWorldPosition = worldObject.pos3d_world;
            Vector3 currentAgentMapPosition = worldObject.pos3d_map;
            Cell? cell = worldObject.locationModule?.cell;

            radiusBounds = GetRadiusBounds(currentAgentMapPosition, suggestedTravelRadius);

            if (cell != null)
            {
                Room room = dir.gen.rooms[cell.room_number];
                roomName = room.name;
                roomType = $"{room.placementTypes}";

                if (room.isOutdoor)
                    roomType += ", Outdoor";
                if (room.isCorridor)
                    roomType += ", Corridor";

                roomBounds = room.bounds;

                if (!TryIntersect(worldBounds, roomBounds, out clipRect))
                    clipRect = worldBounds;

                if (!string.IsNullOrEmpty(roomName))
                    roomContext = $" in room {roomName} of type {roomType}";
                else
                    roomContext = $" in room type {roomType}";

                if (!wantBrief)
                    roomContext += $" and size {roomBounds.width},{roomBounds.height}. ";
                else
                    roomContext += ". ";

                maxDoors = wantBrief ? 2 : wantDetailed ? 8 : 4;
                BuildDoorsList(currentAgentMapPosition, room, radiusBounds, maxDoors);
            }
            else
            {
                clipRect = worldBounds;
                roomContext = "";
            }

            if (!TryIntersect(radiusBounds, clipRect, out tgt))
                tgt = radiusBounds;

            string visionContext = GetVisionContextBlock();

            if (wantBrief)
            {
                positionContext = $"[{worldObject.DisplayName}] CONTEXT: Agent position:"
                    + $" [{currentAgentWorldPosition.x:0}, {currentAgentWorldPosition.z:0}]."
                    + roomContext
                    + doorsContext
                    + visionContext;
            }

            if (wantNormal)
            {
                positionContext = $"[{worldObject.DisplayName}] CONTEXT: Agent position:"
                    + $" [{currentAgentWorldPosition.x:0.0}, {currentAgentWorldPosition.z:0.0}]."
                    + roomContext
                    + doorsContext
                    + visionContext;
            }

            if (wantDetailed)
            {
                positionContext = $"[{worldObject.DisplayName}] CONTEXT: Agent position:"
                    + $" [{currentAgentWorldPosition.x:0.0}, {currentAgentWorldPosition.z:0.0}]."
                    + $" floor height={currentAgentWorldPosition.y:0.0}"
                    + roomContext
                    + doorsContext
                    + visionContext
                    + $" suggested move_to bounded by rectangle [x in {tgt.x} .. {tgt.x + tgt.width - 1}, y in {tgt.y} .. {tgt.y + tgt.height - 1}]";
            }

            Debug.Log($"{detail} Context: {positionContext}");
            return positionContext;
        }

        public struct FoundDoor
        {
            public Vector2Int pos;
            public float distSqr;
            public bool open;
            public string IsOpen;
            public DirFlags direction;
        }

        public List<FoundDoor> GetDoorsInRoom(Vector3 mapPos, Room room, RectInt radiusBounds, int maxDoors)
        {
            List<FoundDoor> foundDoors = new();

            foreach (Cell c in room.cells)
            {
                if (c.doors == DirFlags.None)
                    continue;

                if (!radiusBounds.Contains(c.pos))
                    continue;

                foreach (DirFlags dir in DirFlagsEx.AllCardinals)
                {
                    if ((c.doors & dir) == 0)
                        continue;

                    FoundDoor door = new FoundDoor
                    {
                        pos = c.pos,
                        distSqr = Vector3.SqrMagnitude(mapPos - c.center3d_f),
                        direction = dir,
                        open = false,
                    };
                    door.IsOpen = door.open ? "Open" : "Closed";

                    foundDoors.Add(door);
                }
            }

            foundDoors.Sort((a, b) => a.distSqr.CompareTo(b.distSqr));

            if (maxDoors > 0 && foundDoors.Count > maxDoors)
                foundDoors.RemoveRange(maxDoors, foundDoors.Count - maxDoors);

            return foundDoors;
        }

        public string BuildDoorsList(Vector3 mapPos, Room room, RectInt radiusBounds, int maxDoors)
        {
            //Debug.Log($"BuildDoorsList: {room.cells.Count}");
            doorsContext = "";
            List<FoundDoor> foundDoors = GetDoorsInRoom(mapPos, room, radiusBounds, maxDoors);

            if (foundDoors.Count == 0)
                return "";

            doorsContext = $"{foundDoors.Count} room exits nearby: ";
            foreach (FoundDoor foundDoor in foundDoors)
            {
                doorsContext += $"Door to {DirFlagsEx.ToLongName(foundDoor.direction)} is {foundDoor.IsOpen} at [{foundDoor.pos.x},{foundDoor.pos.y}]; ";
            }

            doorsContext = doorsContext.Trim();
            return doorsContext;
        }

        public static bool TryIntersect(RectInt a, RectInt b, out RectInt intersection)
        {
            int xMin = Mathf.Max(a.xMin, b.xMin);
            int yMin = Mathf.Max(a.yMin, b.yMin);
            int xMax = Mathf.Min(a.xMax, b.xMax);
            int yMax = Mathf.Min(a.yMax, b.yMax);

            if (xMax <= xMin || yMax <= yMin)
            {
                intersection = default;
                return false;
            }

            intersection = new RectInt(
                xMin,
                yMin,
                xMax - xMin,
                yMax - yMin
            );
            return true;
        }

        public RectInt GetRadiusBounds(Vector3 centerMapPos, int radius)
        {
            RectInt tgt = new();

            tgt.x = Mathf.FloorToInt(centerMapPos.x) - radius;
            tgt.y = Mathf.FloorToInt(centerMapPos.z) - radius;
            tgt.width = radius * 2 + 1;
            tgt.height = radius * 2 + 1;
            return tgt;
        }

        #endregion

        #region Observation

        private readonly Queue<string> recentObservations = new();
        private const int MaxObservations = 5;

        public void AddObservation(string jsonLine)
        {
            if (string.IsNullOrWhiteSpace(jsonLine))
                return;

            if (recentObservations.Count >= MaxObservations)
                recentObservations.Dequeue();

            recentObservations.Enqueue(jsonLine);
        }

        public IEnumerable<string> ConsumeObservations()
        {
            while (recentObservations.Count > 0)
                yield return recentObservations.Dequeue();
        }

        public List<string> CaptureRecentObservations()
        {
            return new List<string>(recentObservations);
        }

        public void RestoreRecentObservations(List<string> observations)
        {
            recentObservations.Clear();
            if (observations == null)
                return;

            int startIndex = Mathf.Max(0, observations.Count - MaxObservations);
            for (int i = startIndex; i < observations.Count; i++)
            {
                string observation = observations[i];
                if (!string.IsNullOrWhiteSpace(observation))
                    recentObservations.Enqueue(observation);
            }
        }
        #endregion
    }
}
