using System;
using System.Collections.Generic;

[Serializable]
public class PlanResponseV2
{
    public string schema;
    public List<PlanIntention> intentions;
    public DebugInfo debug;
}

[Serializable]
public class PlanIntention
{
    public string type;

    public int count;
    public float seconds;
}

[Serializable]
public class DebugInfo
{
    public string model;
    public int latency_ms;
}