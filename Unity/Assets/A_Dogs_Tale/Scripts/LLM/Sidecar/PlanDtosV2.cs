using System;
using System.Collections.Generic;

namespace DogGame.LLM
{
    // ------------------------------------------------------------
    // REQUEST
    // ------------------------------------------------------------

    [Serializable]
    public sealed class PlanRequestV2
    {
        public string schema = "plan_request_v2";
        public string agent_id = "";

        public TriggerV2 trigger = new();
        public WorldStateV2 world_state = new();
        public ConstraintsV2 constraints = new();
    }

    [Serializable]
    public sealed class TriggerV2
    {
        public string type = "";
        public float[] location;
        public float intensity;
    }

    [Serializable]
    public sealed class WorldStateV2
    {
        public float[] position;
        public int pack_members_nearby;
    }

    [Serializable]
    public sealed class ConstraintsV2
    {
        public int max_plan_steps = 4;
        public int max_latency_ms = 1000;
    }

    // ------------------------------------------------------------
    // RESPONSE
    // ------------------------------------------------------------

    [Serializable]
    public sealed class PlanResponseV2
    {
        public string schema;
        public List<PlanIntentionV2> intentions;
        public DebugInfoV2 debug;
    }

    [Serializable]
    public sealed class PlanIntentionV2
    {
        public string type;
        public int count;
        public float seconds;
    }

    [Serializable]
    public sealed class DebugInfoV2
    {
        public string model;
        public int latency_ms;
        public string request_schema;
        public string trigger_type;
    }

    // ------------------------------------------------------------
    // SCHEMA PROBE
    // ------------------------------------------------------------

    [Serializable]
    public sealed class SchemaProbeV2
    {
        public string schema;
    }
}