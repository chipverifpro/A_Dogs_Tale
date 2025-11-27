namespace DogGame.AI
{
    public abstract class AgentDecisionModuleBase
    {
        protected AgentController agent;
        protected AgentMovementModule movement;
        protected AgentSensesModule senses;
        protected AgentPackMemberModule packMember;
        protected AgentBlackboardView blackboard;

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