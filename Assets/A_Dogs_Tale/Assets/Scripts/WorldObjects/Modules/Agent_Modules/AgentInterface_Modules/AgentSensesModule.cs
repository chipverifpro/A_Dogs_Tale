using UnityEngine;

namespace DogGame.AI
{
    public class AgentSensesModule : AgentModule
    {
        //[Header("Sub-sensors")]
        //public ScentSensor scentSensor;
        //public VisionSensor visionSensor;
        //public HearingSensor hearingSensor;

        protected override void Awake()
        {
            // Use accesses like this instead...???
            // worldObject.Vision
        }
    }

    [System.Serializable]
    public struct ScentSample
    {
        public Vector3 position;
        public float intensity;
        public string scentTag;
    }

    public class ScentSensor : AgentModule
    {
        // TODO: hook into your scent field / grid system.

        public bool TryGetStrongestInterestingScent(out ScentSample scentSample)
        {
            // TODO: query your scent map / physics.
            scentSample = default;
            return false;
        }
    }

    public class VisionSensor : AgentModule
    {
        public float visionRange = 10f;
        public float visionAngle = 120f;

        // TODO: Add visible targets list, raycasts, etc.
    }

    public class HearingSensor : AgentModule
    {
        public float hearingRadius = 8f;
        // TODO: Add hooks for noise events.
    }
}