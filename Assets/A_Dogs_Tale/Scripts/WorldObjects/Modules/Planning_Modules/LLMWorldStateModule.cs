#nullable enable
using System;
using System.Collections.Generic;
using DogGame.LLM.Policy;
using DogGame.Modules;
using Unity.Tutorials.Core.Editor;
using UnityEngine;
using UnityEngine.AI;

namespace DogGame.LLM.Agent
{
    /// <summary>
    /// Dynamic, self-updating context for the LLM.
    /// This module should not require per-agent babysitting: it should populate itself
    /// from game systems (player, combat, perception, etc.).
    ///
    /// Start simple: you (or other systems) can set the public fields directly.
    /// Later you can add RefreshFromGameSystems() to auto-fill from your WorldObject framework.
    /// </summary>
    public sealed class LLMWorldStateModule : WorldModule
    {
        public string positionContext = "";
        public string doorsContext = "";
        public int maxDoors = 5;

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

        // ---------------------

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

            positionContext = BuildPositionContextBlock();
            LimitAndAddBlock(contextBlocks, positionContext);
            //TryAddBlock(contextBlocks, "CONTEXT: Nearby", nearbySummary);
            //TryAddBlock(contextBlocks, "CONTEXT: Status", statusSummary);
            //TryAddBlock(contextBlocks, "CONTEXT: Goals", goalsSummary);
            //TryAddBlock(contextBlocks, "CONTEXT: Recent Events", recentEventsSummary);
        }

        private void LimitAndAddBlock(List<string> contextBlocks, string context)
        {
            if (string.IsNullOrWhiteSpace(context))
                return;

            // chop text to maximum size
            if (context.Length>maxCharsPerBlock)
                context = context.Substring(0, maxCharsPerBlock);

            // eliminate leading/trailing spaces
            context.Trim();

            contextBlocks.Add(context);
        }

//        private void TryAddBlock(List<string> blocks, string title, string body)
//        {
//            if (string.IsNullOrWhiteSpace(body))
//                return;
//
//            string trimmedBody = body.Trim();
//
//            if (maxCharsPerBlock > 0 && trimmedBody.Length > maxCharsPerBlock)
//                trimmedBody = trimmedBody.Substring(0, maxCharsPerBlock) + "…";
//
//            blocks.Add($"{title}\n{trimmedBody}");
//        }

        // --------------------------------------------------------------------
        // Optional expansion point:
        // Add a method you can call from a manager/perception system each tick,
        // or from Update() at a throttled rate.
        // --------------------------------------------------------------------

        /// <summary>
        /// Optional hook: populate fields from your game's systems (player, combat, perception).
        /// Not implemented yet because it depends on your project's architecture.
        /// </summary>
        public void RefreshFromGameSystems()
        {
            // Example future steps:
            // - Find player position and set distanceToPlayerMeters
            // - Pull combat state from your CombatModule
            // - Build nearbySummary from sensed entities list
            // - Summarize health/stamina/status into statusSummary
        }

        #region VisionContext
        public string GetVisionContextBlock()
        {
            // However you fetch modules in your project:
            // - worldObject.GetModule<VisionPerceptionModule>()
            // - GetComponent<VisionPerceptionModule>()
            // - worldObject.visionPerceptionModule
            var sb = new System.Text.StringBuilder(1024);
            //Debug.Log($"{worldObject.DisplayName} GetVisionContextBlock");

            var vision = worldObject.visionPerceptionModule;
            if (vision == null) return "";

            var events = vision.GetPerceptionEvents();
            Debug.Log($"{worldObject.DisplayName} vision events.count={events.Count}");
            if (events == null || events.Count == 0) return "";

            // Convert to compact lines
            var lines = events.ToLLMLines(maxVisionContextLines);
            if (lines.Count == 0) return "";

            sb.AppendLine("Vision:");
            for (int i = 0; i < lines.Count; i++)
                sb.Append(" - ").AppendLine(lines[i]);
            
            return sb.ToString();
        }
        #endregion

        #region PositionContext
            
        // ========== Position Context ===========
        public string BuildPositionContextBlock()
        {
            string roomName;
            string roomType;
            RectInt worldBounds = new(0,0, dir.cfg.mapWidth,dir.cfg.mapHeight);
            RectInt roomBounds;
            RectInt radiusBounds;

            RectInt clipRect;
            RectInt tgt;

            string roomContext = ""; // description of the room

            // get location and cell so we can look up room.
            Vector3 agentWorldPosition = worldObject.pos3d_world;   // guaranteed valid
            Cell? cell = worldObject.locationModule?.cell;          // possibly null

            // get suggested move rectangle, later clip this to room or map limits
            radiusBounds = GetRadiusBounds(agentWorldPosition, suggestedTravelRadius);
            
            // identify the room and it's particulars..
            if (cell!=null)
            {
                Room room = dir.gen.rooms[cell.room_number];
                roomName = room.name;
                roomType = $"{room.placementTypes}";
                // these may be redundant...
                if (room.isOutdoor) roomType += ", Outdoor";
                if (room.isCorridor) roomType += ", Corridor";

                roomBounds = room.bounds;

                if (!TryIntersect(worldBounds, roomBounds, out clipRect))
                    clipRect=worldBounds; // on failure, use entire map.
                
                if (!roomName.IsNullOrEmpty())
                    roomContext = $" in room \"{roomName}\" of type \"{roomType}\" and size {roomBounds.width},{roomBounds.height}. ";
                else
                    roomContext = $" in room type \"{roomType}\" and size {roomBounds.width},{roomBounds.height}. ";
                
                //   identify door locations.
                BuildDoorsList(agentWorldPosition, room, radiusBounds, maxDoors);
            
            } 
            else
            {
                // no Cell/Room so don't describe room, and clip only to map.
                clipRect = worldBounds;
                roomContext = "";
            }
            
            // bounds are radius around agentWorldPosition clipped by room and map
            if(!TryIntersect(radiusBounds, clipRect, out tgt))
                tgt = radiusBounds;     // if no overlap, just use local radius without clip

            string visionContext = GetVisionContextBlock();
            // expansion suggestions:
            // create summary for LLM:
            positionContext = $"[{worldObject.DisplayName}] CONTEXT: Agent position:"
                + $" [{agentWorldPosition.x:0.0}, {agentWorldPosition.z:0.0}]."
                + $" floor height={agentWorldPosition.y:0.0}"
                + roomContext
                + doorsContext 
                + visionContext
                + $" suggested move_to bounded by rectangle [x in {tgt.x} .. {tgt.x+tgt.width-1}, y in {tgt.y} .. {tgt.y+tgt.height-1}]";
            Debug.Log(positionContext);
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

        public string BuildDoorsList(Vector3 worldPos, Room room, RectInt radiusBounds, int maxDoors)
        {
            Debug.Log($"BuildDoorsList: {room.cells.Count}");
            doorsContext = "";
            List<FoundDoor> foundDoors = new();
            foreach (Cell c in room.cells)
            {
                if (c.doors != DirFlags.None)
                {
                    if (!radiusBounds.Contains(c.pos))
                        continue;
                    foreach (DirFlags dir in DirFlagsEx.AllCardinals)
                    {
                        if ((c.doors & dir) != 0)
                        {
                            Debug.Log($"Found door @ {c.pos}");
                            
                            FoundDoor door = new FoundDoor
                            {
                                pos = c.pos,
                                distSqr = Vector3.SqrMagnitude(worldPos - c.pos3d_world),
                                direction = dir,
                                open = false, // future capability
                            };
                            door.IsOpen = door.open ? "Open" : "Closed";

                            foundDoors.Add(door);
                        }
                    }
                }
            }
            //Debug.Log($"{foundDoors.Count} doors nearby: ");
            if (foundDoors.Count==0) return "";

            // Sort nearest first
            foundDoors.Sort((a, b) => a.distSqr.CompareTo(b.distSqr));

            // Truncate to maxDoors
            if (maxDoors > 0 && foundDoors.Count > maxDoors)
            {
                foundDoors.RemoveRange(maxDoors, foundDoors.Count - maxDoors);
            }
            doorsContext = $"{foundDoors.Count} room exits nearby: ";
            foreach (FoundDoor foundDoor in foundDoors)
            {
                doorsContext += $"Door to {DirFlagsEx.ToLongName(foundDoor.direction)} is {foundDoor.IsOpen} at [{foundDoor.pos.x},{foundDoor.pos.y}]; ";
            }
            doorsContext.Trim();    // eliminate trailing space
            //Debug.Log(doorsContext);
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

        // gets a rectangular radius from a position, and clips it to bounds (world/room/etc)
        public RectInt GetRadiusBounds(Vector3 centerWorldPos, int radius)
        {
            RectInt tgt = new();

            // build target around center
            tgt.x = Mathf.FloorToInt(centerWorldPos.x) - radius;
            tgt.y = Mathf.FloorToInt(centerWorldPos.z) - radius; // z is used in world position
            tgt.width = radius * 2 + 1;
            tgt.height = radius * 2 + 1;
            return tgt;
        }

        #endregion
        
        // ======================================
    
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
        #endregion
    }
}