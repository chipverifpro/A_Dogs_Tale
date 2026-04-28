using System;
using System.Collections.Generic;
using DogGame.Modules;
using UnityEngine;

namespace DogGame.Lua
{
    public enum Severity
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public enum EventCategory
    {
        None = 0,
        Perception,         // Combined: Agent Nearby, Agent Gone
        PerceptionSeen,     // Saw, approaching, disappeared, emote seen
        PerceptionHeard,    // Footsteps, Bark, words heard
        PerceptionSmelled,  // New Strong Scent
        LLM_Request,        // Requested from LLM
        CommandReceived,    // Received command(s) from LLM, Lua
        CommandExecuted,    // Began executing command from LLM, Lua (Dig, Goto)
        DecisionModule,     // Switched DecisionModule (Explore, Patrol, Player)
        LLMExplanation,     // LLM Thought
        Item,               // Get, Drop, Eat, Trade
        Identified,         // Agent
        Emote,
        Bark,
        Speak,              // Human words / Translated
        PackMembership,     // Joined, Left, Leader
        PackEvent,          // LostSight, In Trouble, ...
        Room,               // Enter room type, door open/close
        Tracking,           // Begin, Found, Lost
        Time,               // Wait period started/finished
    }

    [Serializable]
    public class MemoryEventEntry
    {
        public float gameTime;
        public int frameCount;
        public Severity severity;
        public EventCategory category;
        public string text;
    }

    public class MemoryState
    {
        // Track current events
        public string lastDogSeen        = "";
        public string lastFoodFound      = "";
        public string lastThreatSeen     = "";
        public string lastBarkHeard      = "";

        public float lastDogSeenTime        = -1f;
        public float lastFoodFoundTime      = -1f;
        public float lastThreatSeenTime     = -1f;
        public float lastBarkHeardTime      = -1f;

        public bool newDogSeen           = false;
        public bool newSoundHeard        = false;
        public bool newScentDetected     = false;

        // Store a log of everything sensed/done
        public List<MemoryEventEntry> eventHistory = new();
        public IReadOnlyList<MemoryEventEntry> EventHistory => eventHistory;

        // handy shortcuts
        public WorldObject worldObject;
        public AgentState  state;

        public void InitState(WorldObject worldObject, AgentState state)
        {
            this.worldObject = worldObject;
            this.state = state;
        }

        public void UpdateState(Detail detail)
        {
            // Placeholder for event-history integration.
        }

        public MemoryEventEntry AddEntry(string text, Severity severity, EventCategory category)
        {
            MemoryEventEntry entry = new()
            {
                gameTime = Time.time,
                frameCount = Time.frameCount,
                severity = severity,
                category = category,
                text = text ?? string.Empty
            };

            eventHistory.Add(entry);
            return entry;
        }

        public string ConvertEventToString(MemoryEventEntry entry)
        {
            string result = "";
            if (entry.severity != Severity.None)
                result += $"[{entry.severity}] ";
            result += entry.text;
            return result;
        }


        public void Tick(float interval)
        {
            newDogSeen = false;
            newSoundHeard = false;
            newScentDetected = false;
        }
    }
}

/* Example event history:

--Hunger level = high.
--Begin command Explore.
--Saw non-packmember dog approaching.  AgentID=5
--Heard footsteps approaching.
--Smelled new dog unknown.
--Found and took item "plunger".
--Ate item "plunger".
--Hunger level = none.
--Found and took item "Potted Cactus"
--LLM thought: "We saw and heard a dog approaching, we should investigate."
--LLM command received:  Goto AgentID=5
--LLM command received: Emote Curious
--Command executed Goto AgentID=5
--New dog AgentID=5 identified as Poodle named Fluffy
--AgentID=5 emoted "Friendly"
--Emoted "Curious"
--LLM Request generated.
--LLM thought: "We discovered a friendly dog.  Ask it to join our pack."
--LLM command received: Request AgentID=5 to join our pack.
--Command executed: Request AgentID=5 to join our pack.
--AgentID=5 joined our pack.
--Inventory of AgentID=5 is a "Bone".
--Traded item "Potted Cactus" with AgentID=5 "Bone"
--Eat "Bone".

*/