using UnityEngine;

namespace DogGame.AI
{
    [RequireComponent(typeof(AgentMovementModule))]
    [RequireComponent(typeof(AgentSensesModule))]
    [RequireComponent(typeof(AgentPackMemberModule))]
    [RequireComponent(typeof(BlackboardModule))]
    [RequireComponent(typeof(AgentDecisionModuleBase))]
    public class AgentModule : WorldModule
    {
        [Header("Debug / Identity")]
        public string agentName = "Unnamed Agent";

        [Header("Agent Specific Modules")]
        // Agent Specific modules (most build on other modules):
        public AgentMovementModule agentMovementModule { get; private set; }
        public AgentPackMemberModule agentPackMemberModule { get; private set; }
        public AgentSensesModule agentSensesModule { get; private set; }

        [Header("Customized Module Views")]
        public AgentBlackboardView agentBlackboard;
        private AgentDecisionModuleBase currentDecisionModule;

        [Header("Initial Decision Type")]
        public AgentDecisionType initialDecisionType = AgentDecisionType.Wanderer;



        protected override void Awake()
        {
            base.Awake();

            agentMovementModule   = GetComponent<AgentMovementModule>();
            agentPackMemberModule = GetComponent<AgentPackMemberModule>();
            agentSensesModule     = GetComponent<AgentSensesModule>();


        }

        private void OnEnable()
        {
//            if (worldObject.blackboardModule != null)
//            {
//                agentBlackboard = new AgentBlackboardView(worldObject.blackboardModule);
//            }

//            SwitchDecisionModule(initialDecisionType);
        }

        protected override void Update()
        {
            base.Update();

            float deltaTime = Time.deltaTime;

            if (currentDecisionModule != null)
            {
                currentDecisionModule.Tick(deltaTime);
            }
        }

        /// <summary>
        /// Switch decision module at runtime using enum.
        /// </summary>
        public void SwitchDecisionModule(AgentDecisionType decisionType)
        {
            // You can replace this simple switch later with a more data-driven factory.
            currentDecisionModule = decisionType switch
            {
                AgentDecisionType.Player    => new PlayerDecisionModule(),
                AgentDecisionType.Follower  => new FollowerDecisionModule(),
                AgentDecisionType.Wanderer  => new WandererDecisionModule(),
                _                           => new WandererDecisionModule()
            };

            currentDecisionModule.Initialize(this);
        }

        /// <summary>
        /// Generic helper if you want to set modules directly from code.
        /// </summary>
        public void SetDecisionModule(AgentDecisionModuleBase decisionModule)
        {
            currentDecisionModule = decisionModule;
            currentDecisionModule.Initialize(this);
        }

        /// <summary>
        /// Convenience for pack-based switching, e.g. when NPC joins player pack.
        /// </summary>
        public void BecomeFollower()
        {
            SwitchDecisionModule(AgentDecisionType.Follower);
        }

        public void BecomePlayerControlled()
        {
            SwitchDecisionModule(AgentDecisionType.Player);
        }

        public void BecomeWanderer()
        {
            SwitchDecisionModule(AgentDecisionType.Wanderer);
        }
    }

    public enum AgentDecisionType
    {
        Player,
        Follower,
        Wanderer,
        // Add more: Predator, Boss, Civilian, Summoned, etc.
    }
}