using System.ComponentModel;
using UnityEngine;

namespace DogGame.Modules
{
    public class ImmobileDecisionModule : AgentDecisionModuleBase
    {
        public override AgentDecisionType DecisionType => AgentDecisionType.Immobile;

        public override void Initialize(AgentModule agentController)
        {
            base.Initialize(agentController);
        }

        private int debugDoubleTick = -1;
        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            //Debug.Log($"ImmobileDecisionModule {worldObject.DisplayName}: Tick {deltaTime}");
        }

        public override void BeginDecisionModule(bool resume=false)
        {
            
        }
        public override void EndDecisionModule()
        {
            
        }
    }
}