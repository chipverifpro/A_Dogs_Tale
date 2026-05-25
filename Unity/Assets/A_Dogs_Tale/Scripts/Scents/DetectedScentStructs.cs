#nullable enable
using System;
using UnityEngine;

namespace DogGame.Modules
{
    public enum ScentMedium { Air, Ground }

    [Serializable]
    public struct DetectedScent
    {
        public string scentKey;             // stable key (agent:123, Food:Steak, etc.)
        public ScentCategory category;
        public string scentName;

        public ScentMedium medium;          // Air vs Ground
        public float strength01;            // 0..1
        public Vector2Int cell;             // where detected (current cell)
        public float time;                  // Time.time stamp

        // Optional future hooks (matches your TODO direction)
        public int agentId;                 // -1 if not an agent source
        public bool ignored;
    }

    [Serializable]
    public sealed class SniffReport
    {
        public Vector2Int cell;
        public float time;

        public readonly System.Collections.Generic.List<DetectedScent> air = new();
        public readonly System.Collections.Generic.List<DetectedScent> ground = new();
    }
}