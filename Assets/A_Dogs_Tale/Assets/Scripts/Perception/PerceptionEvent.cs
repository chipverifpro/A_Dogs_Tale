#nullable enable
using UnityEngine;

namespace DogGame.AI.Perception
{
    public enum PerceptionEventType
    {
        NewSmell,
        SmellStrengthChanged
    }

    public readonly struct PerceptionEvent
    {
        public readonly PerceptionEventType Type;
        public readonly Vector3 WorldPos;
        public readonly string ScentKey;
        public readonly ScentCategory Category;
        public readonly string ScentName;
        public readonly float Strength01;   // 0..1 (normalized/clamped)
        public readonly float Novelty01;    // 0..1
        public readonly float Interest01;   // 0..1

        public PerceptionEvent(
            PerceptionEventType type,
            Vector3 worldPos,
            string scentKey,
            ScentCategory category,
            string scentName,
            float strength01,
            float novelty01,
            float interest01)
        {
            Type = type;
            WorldPos = worldPos;
            ScentKey = scentKey;
            Category = category;
            ScentName = scentName;
            Strength01 = strength01;
            Novelty01 = novelty01;
            Interest01 = interest01;
        }
    }
}