using UnityEngine;

namespace DogGame.AI
{
    /// <summary>
    /// Shared scratchpad for decisions. Later this can grow into a full blackboard.
    /// </summary>
    [System.Serializable]
    public class AgentBlackboard
    {
        public Transform currentTarget;
        public Vector3 lastKnownTargetPosition;
        public bool hasTarget;

        public Vector3 lastInterestingScentPosition;
        public bool hasInterestingScent;

        public float fearLevel;
        public float curiosityLevel;
        public float hungerLevel;
        public float loyaltyLevel;

        // TODO: Add more as needed: dominance, energy, mood, etc.
    }
}