using UnityEngine;
using System.Collections.Generic;
using System;

[System.Serializable]
public class ScentInCell             // Everything about ONE scent at one location
{
    public int agentId;
    public Agent agent;             // pointer to the agent

    // Airborne scent
    public float airIntensity;     // current airborne scent strength
    public float airNextDelta;     // next airborne value during decay/spread calc
    public float airLastVisualized = -1f; // for determining whether to bother updating visual cloud
    public int airGOindex = -1;  // index into airborne visual (fog, etc.)

    // Ground (surface) scent
    public float groundIntensity;      // current ground scent strength
    public float groundNextDelta;      // next ground value during decay/spread calc
    public float groundLastVisualized = -1f; // for determining whether to bother updating visual cloud
    public int groundGOindex = -1;   // index into ground visual (if any)
}