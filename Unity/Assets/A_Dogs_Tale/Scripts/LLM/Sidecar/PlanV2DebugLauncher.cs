using System;
using UnityEngine;

namespace DogGame.LLM
{
    public sealed class PlanV2DebugLauncher : MonoBehaviour
    {
        [SerializeField]
        private SidecarPlanClientV2 sidecarPlanClient;

        public String DebugPlan;
    }
}
