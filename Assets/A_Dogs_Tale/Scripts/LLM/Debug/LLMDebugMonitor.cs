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

    private void TrimAgentLog(LLMDebugAgentLog log)
    {
        if (log == null) return;

        int max = Mathf.Max(1, maxNumberOfLLMLogsPerAgent);
        if (log.entries == null) log.entries = new List<LLMDebugEntry>();

        if (log.pendingByRequestId == null)
            log.pendingByRequestId = new Dictionary<string, LLMDebugEntry>();

        if (log.entries.Count <= max) return;

        // Remove oldest entries (end of list) until we're at capacity.
        for (int i = log.entries.Count - 1; i >= max; i--)
        {
            var removed = log.entries[i];

            // If we are trimming an entry that still has a pending response,
            // remove it from the pending dictionary too.
            if (removed != null &&
                removed.pendingResponse &&
                !string.IsNullOrWhiteSpace(removed.requestId))
            {
                // Only remove if the dictionary points to this exact entry (safe).
                if (log.pendingByRequestId.TryGetValue(removed.requestId, out var pendingEntry) &&
                    ReferenceEquals(pendingEntry, removed))
                {
                    log.pendingByRequestId.Remove(removed.requestId);
                }
            }

            log.entries.RemoveAt(i);
        }
    }

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

    // Optional helper if you want to clear all history via inspector context menu
    [ContextMenu("Clear All LLM Debug Logs")]
    public void ClearAllLogs()
    {
        logsByAgent.Clear();
        agentIdToLog.Clear();
        //ClearLatestFields();
    }
}