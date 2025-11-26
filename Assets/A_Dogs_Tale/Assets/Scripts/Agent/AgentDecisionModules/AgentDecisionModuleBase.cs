namespace DogGame.AI
{
    public abstract class AgentDecisionModuleBase
    {
        protected AgentController agent;
        protected AgentMovement movement;
        protected AgentSenses senses;
        protected AgentPackMember packMember;
        protected AgentBlackboard blackboard;

        public virtual void Initialize(AgentController agentController)
        {
            agent = agentController;
            movement = agent.movement;
            senses = agent.senses;
            packMember = agent.packMember;
            blackboard = agent.blackboard;
        }

        public abstract void Tick(float deltaTime);
    }
}