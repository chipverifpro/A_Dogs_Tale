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

        public override void Tick(float deltaTime)
        {
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