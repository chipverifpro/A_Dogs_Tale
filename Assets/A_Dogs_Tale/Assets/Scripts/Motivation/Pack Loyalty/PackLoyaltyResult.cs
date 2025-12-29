using UnityEngine;

namespace DogGame.AI
{
    public enum PackLoyaltyDirective
    {
        None,
        ReturnToPackCentroid,
        FollowLeader,
        AssistDistressedPackmate
    }

    public struct PackLoyaltyResult
    {
        public float urge01;                   // 0..1
        public bool isActive;                  // urge above threshold
        public PackLoyaltyDirective directive;
        public Vector3 targetLocation;         // where to move (centroid/leader/etc.)
        public IAgentHandle targetAgent;       // leader or distressed member
        public string debugReason;
    }
}