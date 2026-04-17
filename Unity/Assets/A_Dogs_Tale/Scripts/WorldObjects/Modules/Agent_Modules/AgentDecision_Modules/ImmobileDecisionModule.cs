using System.ComponentModel;
using UnityEngine;
using InspectorTools;

namespace DogGame.Modules
{
    [InspectorNote("AgentDecision_Modules/Immobile Decision Module", "Agent stands still.")]
    public class ImmobileDecisionModule : AgentDecisionModuleBase
    {
        public override AgentDecisionType DecisionType => AgentDecisionType.Immobile;

        public override void Initialize(AgentModule agentController)
        {
            base.Initialize(agentController);
        }

        #region Tick

        private int debugDoubleTick = -1;
        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            //Debug.Log($"ImmobileDecisionModule {worldObject.DisplayName}: Tick {deltaTime}");
        }

        #endregion
        
        public override void BeginDecisionModule(bool resume=false)
        {
            StopMovementIntent();
        }
        public override void EndDecisionModule()
        {
            StopMovementIntent();
        }
    }
}
