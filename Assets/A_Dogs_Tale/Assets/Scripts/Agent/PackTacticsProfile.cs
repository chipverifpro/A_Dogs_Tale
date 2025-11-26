using UnityEngine;

namespace DogGame.AI
{
    public enum PackFormationType
    {
        LooseSwarm,
        TightSwarm,
        Line,
        Wedge,
        PairWalk,
    }

    public enum PackAggressionLevel
    {
        VeryPassive,
        Cautious,
        Neutral,
        Aggressive,
        Berserk
    }

    [CreateAssetMenu(menuName = "DogGame/Pack Tactics Profile")]
    public class PackTacticsProfile : ScriptableObject
    {
        public string profileName = "Default Pack Tactics";

        [Header("Group Behavior")]
        public PackFormationType formationType = PackFormationType.LooseSwarm;
        public PackAggressionLevel aggressionLevel = PackAggressionLevel.Neutral;
        public float cohesionRadius = 3f;
        public float preferredEngageDistance = 4f;

        [Header("Retreat / Loyalty")]
        public float retreatHealthFraction = 0.2f;
        public float minLoyaltyToHoldGround = 0.5f;

        [Header("Targeting")]
        public bool preferSameTargetAsLeader = true;
        public bool protectLeaderIfThreatened = true;
        public bool avoidAreaOfEffect = true;

        // TODO: Extend as needed.
    }
}