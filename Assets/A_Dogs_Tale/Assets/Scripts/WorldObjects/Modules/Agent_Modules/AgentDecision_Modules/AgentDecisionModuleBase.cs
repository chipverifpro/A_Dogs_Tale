using UnityEngine;

// ----- ABSTRACT BASE CLASS -----

namespace DogGame.AI
{
    public abstract class AgentDecisionModuleBase : WorldModule
    {
        //protected AgentModule agent;
        //protected AgentMovementModule movement;
        //protected AgentSensesModule senses;
        //protected AgentPackMemberModule packMember;
        //protected AgentBlackboardView blackboard;

        public abstract AgentDecisionType DecisionType { get; }
        protected AgentModule agentModule;

        public virtual void Initialize(AgentModule agentModuleOwner)
        {
            agentModule = agentModuleOwner;

            //agent = agentController;
            //movement = agent.movement;
            //senses = agent.senses;
            //packMember = agent.packMember;
            //blackboard = agent.blackboard;
        }

        public override void Tick(float deltaTime)
        {
            Debug.Log($"AgentDecisionModuleBase {worldObject.DisplayName}: Tick {deltaTime}");
        }
    }
}