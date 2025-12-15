using UnityEngine;

namespace DogGame.AI
{
    [CreateAssetMenu(menuName = "DogsTale/AI/Motivations/Pack Loyalty Tuning")]
    public class PackLoyaltyTuning : ScriptableObject
    {
        [Header("Distance Response")]
        [Tooltip("Distance (meters) where separation starts to matter.")]
        public float comfortRadiusMeters = 4f;

        [Tooltip("Distance (meters) where loyalty becomes urgent.")]
        public float maxRadiusMeters = 12f;

        [Header("Urgency")]
        [Range(0f, 5f)] public float baseWeight = 1.5f;
        [Range(0f, 1f)] public float activationThreshold = 0.35f;

        [Header("Distress")]
        [Range(0f, 2f)] public float distressMultiplier = 1.25f;

        [Header("Training Effects")]
        [Range(0f, 1f)] public float maxTrainingSuppression = 0.6f; // focus/obedience can suppress
    
    }
}