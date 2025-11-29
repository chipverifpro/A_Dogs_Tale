using UnityEngine;

namespace DogGame.AI
{
    [RequireComponent(typeof(AgentMovementModule))]
    [RequireComponent(typeof(AgentSensesModule))]
    [RequireComponent(typeof(AgentPackMemberModule))]
    [RequireComponent(typeof(BlackboardModule))]
    
    public class AgentModule : MonoBehaviour
    {
        [Header("Debug / Identity")]
        public string agentName = "Unnamed Agent";

        [Header("Initial Decision Type")]
        public AgentDecisionType initialDecisionType = AgentDecisionType.Wanderer;

        [HideInInspector] public AgentMovementModule movement;
        [HideInInspector] public AgentSensesModule senses;
        [HideInInspector] public AgentPackMemberModule packMember;
        [HideInInspector] public AgentBlackboardView blackboard;
        [HideInInspector] public BlackboardModule bb_raw;

        private AgentDecisionModuleBase currentDecisionModule;

        private void Awake()
        {
            movement = GetComponent<AgentMovementModule>();
            senses = GetComponent<AgentSensesModule>();
            packMember = GetComponent<AgentPackMemberModule>();
            bb_raw = GetComponent<BlackboardModule>();

            if (bb_raw == null)
            {
                bb_raw = GetComponent<BlackboardModule>();
                blackboard = new AgentBlackboardView(bb_raw);
            }
        }

        private void Start()
        {
            SwitchDecisionModule(initialDecisionType);
        }

        private void Update()
        {
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