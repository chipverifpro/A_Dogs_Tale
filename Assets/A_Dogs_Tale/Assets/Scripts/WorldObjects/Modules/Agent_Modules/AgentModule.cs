using UnityEngine;
using System.Linq;

// ----- ABSTRACT BASE CLASS -----

namespace DogGame.AI
{
    [RequireComponent(typeof(AgentMovementModule))]
    [RequireComponent(typeof(AgentSensesModule))]
    [RequireComponent(typeof(AgentPackMemberModule))]
    [RequireComponent(typeof(BlackboardModule))]
    [RequireComponent(typeof(AgentDecisionModuleBase))]
    public abstract class AgentModule : WorldModule
    {
        [Header("Debug / Identity")]
        public string agentName = "Unnamed Agent";

        [Header("Agent Specific Modules")]
        // Agent Specific modules (most build on other modules):
        public AgentMovementModule agentMovementModule { get; protected set; }
        public AgentPackMemberModule agentPackMemberModule { get; protected set; }
        public AgentSensesModule agentSensesModule { get; protected set; }

        [Header("Customized Module Views")]
        public AgentBlackboardView agentBlackboard;
        
        [Header("Initial Decision Type")]
        public AgentDecisionType initialDecisionType = AgentDecisionType.Wanderer;

        public AgentDecisionModuleBase currentDecisionModule;
        private AgentDecisionModuleBase[] allDecisionModules;


        protected override void Awake()
        {
            base.Awake();

            // Find all decision modules attached to this agent
            allDecisionModules = GetComponents<AgentDecisionModuleBase>();

            foreach (var module in allDecisionModules)
            {
                module.Initialize(this);
                module.enabled = false; // start disabled; we'll enable the active one
                Debug.Log($"[AgentModule {agentName}] Found decision module: {module.GetType().Name} ({module.DecisionType})", this);
            }

            // Pick the initial module
            SwitchDecisionModule(initialDecisionType);
        }

        protected override void Update()
        {
            base.Update();
            Debug.Log($"AgentModule.Update {agentName}: currentDecisionModule={currentDecisionModule}");
        }

        // Tick is called by WorldObject, pass it along to the current DecisionModule
        public override void Tick(float deltaTime)
        {
            Debug.Log($"AgentModule {worldObject.DisplayName}: Tick {deltaTime}");
            
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
            Debug.Log($"SwitchDecisionModule {agentName}: decisionType = {decisionType}", this);

            // Disable the current one if any
            if (currentDecisionModule != null)
            {
                currentDecisionModule.enabled = false;
            }

            // Find a module with matching DecisionType
            var nextModule = allDecisionModules
                .FirstOrDefault(m => m.DecisionType == decisionType);

            // Fallback: if not found, use any Wanderer, or first module
            if (nextModule == null)
            {
                nextModule = allDecisionModules
                    .FirstOrDefault(m => m.DecisionType == AgentDecisionType.Wanderer)
                    ?? allDecisionModules.FirstOrDefault();
            }

            currentDecisionModule = nextModule;

            if (currentDecisionModule != null)
            {
                currentDecisionModule.enabled = true;
                Debug.Log($"[AgentModule {agentName}] Switched to module {currentDecisionModule.GetType().Name}", this);
            }
            else
            {
                Debug.LogWarning($"[AgentModule {agentName}] No decision module found to switch to!", this);
            }
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