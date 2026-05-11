using UnityEngine;
using DogGame.Attributes;
using System;
using System.Collections.Generic;

[Serializable]
public class LLMDebugEntry
{
    public string agentId;      // NEW: replaces WorldObject

    public string requestId;

    public float Time_Request;
    [JsonPreview(260f)]
    public string LLM_Request;

    public float Time_Response;
    public float DeltaTime_Response;
    [JsonPreview(260f)]
    public string LLM_Response;

    public bool wasStale;

    public bool savedPendingResponse;
    [NonSerialized] public bool pendingResponse;
}

[Serializable]
public class LLMDebugAgentLog
{
    public string agentId;
    public List<LLMDebugEntry> entries = new();

    [NonSerialized]
    public Dictionary<string, LLMDebugEntry> pendingByRequestId = new();
}

/// <summary>
/// A little monitor to display the LLM input/output packets in the Unity inspector.
/// Stores multiple request/response exchanges grouped by agent.
/// </summary>
public class LLMDebugMonitor : MonoBehaviour
{
    [Serializable]
    public sealed class SaveData
    {
        public int maxNumberOfLLMLogsPerAgent;
        public List<LLMDebugAgentLog> logsByAgent = new();
    }

    [Header("History")]
    [Min(1)]
    public int maxNumberOfLLMLogsPerAgent = 5;

    [SerializeField]
    private List<LLMDebugAgentLog> logsByAgent = new();

    private readonly Dictionary<string, LLMDebugAgentLog> agentIdToLog = new();
/*
    [Header("Request (latest)")]
    public float Time_Request;
    [JsonPreview(260f)]
    public string LLM_Request;
    [JsonPreview(260f)]
    public string LLM_Request_Input;

    [Header("Response (latest)")]
    public float Time_Response;
    [JsonPreview(260f)]
    public string LLM_Response;
*/
    void Awake()
    {
        RebuildLookup();
        //();
    }

    void OnValidate()
    {
        if (maxNumberOfLLMLogsPerAgent < 1) maxNumberOfLLMLogsPerAgent = 1;
    }

/*
    private void ClearLatestFields()
    {
        LLM_Request = "";
        LLM_Request_Input = "";
        LLM_Response = "";
        Time_Request = 0f;
        Time_Response = 0f;
    }
*/
    private void RebuildLookup()
    {
        agentIdToLog.Clear();

        // Remove null agent entries to keep things clean (optional but nice).
        for (int i = logsByAgent.Count - 1; i >= 0; i--)
        {
            if (logsByAgent[i] == null || logsByAgent[i].agentId == null)
                logsByAgent.RemoveAt(i);
        }

        foreach (var log in logsByAgent)
        {
            if (log.agentId == null) continue;
            string id = log.agentId;
            if (!agentIdToLog.ContainsKey(id))
                agentIdToLog.Add(id, log);
        }
    }

    private LLMDebugAgentLog GetOrCreateAgentLog(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            agentId = "<unknown>";

        if (agentIdToLog.TryGetValue(agentId, out var log))
            return log;

        log = new LLMDebugAgentLog
        {
            agentId = agentId,
            entries = new List<LLMDebugEntry>(),
            pendingByRequestId = new Dictionary<string, LLMDebugEntry>()
        };

        logsByAgent.Add(log);
        agentIdToLog[agentId] = log;
        return log;
    }

    /*
    private void TrimAgentLog(LLMDebugAgentLog log)
    {
        if (log == null) return;

        int max = Mathf.Max(1, maxNumberOfLLMLogsPerAgent);
        if (log.entries == null) log.entries = new List<LLMDebugEntry>();

        if (log.pendingByRequestId == null)
            log.pendingByRequestId = new Dictionary<string, LLMDebugEntry>();

        if (log.entries.Count <= max) return;

        for (int i = log.entries.Count - 1; i >= max; i--)
        {
            var removed = log.entries[i];

            if (removed != null &&
                removed.pendingResponse &&
                !string.IsNullOrWhiteSpace(removed.requestId))
            {
                if (log.pendingByRequestId.TryGetValue(removed.requestId, out var pendingEntry) &&
                    ReferenceEquals(pendingEntry, removed))
                {
                    log.pendingByRequestId.Remove(removed.requestId);
                }
            }

            log.entries.RemoveAt(i);
        }
    }
    */

/*
    public void DebugLLMRequest_Input(string request, WorldObject agent)
    {
        // Keep latest-only field (as you had)
        LLM_Request_Input = request ?? "";

        // Optional: if you want to store input packets too, we can add another field to LLMDebugEntry.
    }
*/

    public void DebugLLMRequest(string request, string agentId, string requestId)
    {
        var log = GetOrCreateAgentLog(agentId);

        var entry = new LLMDebugEntry
        {
            agentId = agentId,
            requestId = requestId,

            Time_Request = Time.time,
            LLM_Request = request ?? "",

            savedPendingResponse = true,
            pendingResponse = true
        };

        log.entries.Insert(0, entry);
        log.pendingByRequestId[requestId] = entry;

        Trim(log);
    }

    public void DebugLLMResponse(string response, string agentId, string requestId, bool wasStale)
    {
        var log = GetOrCreateAgentLog(agentId);

        if (log.pendingByRequestId.TryGetValue(requestId, out var entry))
        {
            entry.Time_Response = Time.time;
            entry.DeltaTime_Response = entry.Time_Response - entry.Time_Request;
            entry.LLM_Response = response ?? "";
            entry.wasStale = wasStale;
            entry.savedPendingResponse = false;
            entry.pendingResponse = false;

            log.pendingByRequestId.Remove(requestId);
            return;
        }

        // Orphan response fallback
        log.entries.Insert(0, new LLMDebugEntry
        {
            agentId = agentId,
            requestId = requestId,
            Time_Response = Time.time,
            LLM_Response = response ?? "",
            wasStale = wasStale
        });

        Trim(log);
    }

    private void Trim(LLMDebugAgentLog log)
    {
        while (log.entries.Count > maxNumberOfLLMLogsPerAgent)
        {
            var removed = log.entries[^1];
            if (removed.pendingResponse && removed.requestId != null)
                log.pendingByRequestId.Remove(removed.requestId);

            log.entries.RemoveAt(log.entries.Count - 1);
        }
    }

    public SaveData CaptureSaveData()
    {
        SaveData data = new()
        {
            maxNumberOfLLMLogsPerAgent = maxNumberOfLLMLogsPerAgent,
            logsByAgent = new List<LLMDebugAgentLog>()
        };

        foreach (LLMDebugAgentLog log in logsByAgent)
        {
            if (log == null)
                continue;

            LLMDebugAgentLog logCopy = new()
            {
                agentId = log.agentId,
                entries = new List<LLMDebugEntry>()
            };

            if (log.entries != null)
            {
                foreach (LLMDebugEntry entry in log.entries)
                {
                    if (entry == null)
                        continue;

                    logCopy.entries.Add(new LLMDebugEntry
                    {
                        agentId = entry.agentId,
                        requestId = entry.requestId,
                        Time_Request = entry.Time_Request,
                        LLM_Request = entry.LLM_Request,
                        Time_Response = entry.Time_Response,
                        DeltaTime_Response = entry.DeltaTime_Response,
                        LLM_Response = entry.LLM_Response,
                        wasStale = entry.wasStale,
                        savedPendingResponse = entry.pendingResponse,
                        pendingResponse = entry.pendingResponse
                    });
                }
            }

            data.logsByAgent.Add(logCopy);
        }

        return data;
    }

    public void RestoreSaveData(SaveData data)
    {
        logsByAgent.Clear();
        agentIdToLog.Clear();

        if (data == null)
            return;

        maxNumberOfLLMLogsPerAgent = Mathf.Max(1, data.maxNumberOfLLMLogsPerAgent);
        if (data.logsByAgent != null)
        {
            foreach (LLMDebugAgentLog savedLog in data.logsByAgent)
            {
                if (savedLog == null)
                    continue;

                LLMDebugAgentLog log = new()
                {
                    agentId = savedLog.agentId,
                    entries = savedLog.entries != null ? new List<LLMDebugEntry>(savedLog.entries) : new List<LLMDebugEntry>(),
                    pendingByRequestId = new Dictionary<string, LLMDebugEntry>()
                };

                foreach (LLMDebugEntry entry in log.entries)
                {
                    if (entry != null)
                        entry.pendingResponse = entry.savedPendingResponse;
                }

                logsByAgent.Add(log);
            }
        }

        RebuildLookup();
        foreach (LLMDebugAgentLog log in logsByAgent)
        {
            log.pendingByRequestId = new Dictionary<string, LLMDebugEntry>();
            if (log.entries == null)
                continue;

            foreach (LLMDebugEntry entry in log.entries)
            {
                if (entry != null && entry.pendingResponse && !string.IsNullOrWhiteSpace(entry.requestId))
                    log.pendingByRequestId[entry.requestId] = entry;
            }
        }
    }

    // Optional helper if you want to clear all history via inspector context menu
    /*
    [ContextMenu("Clear All LLM Debug Logs")]
    public void ClearAllLogs()
    {
        logsByAgent.Clear();
        agentIdToLog.Clear();
    }
    */
}
