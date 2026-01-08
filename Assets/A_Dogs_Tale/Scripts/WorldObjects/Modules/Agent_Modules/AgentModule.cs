using UnityEngine;
using System.Linq;
using DogGame.AI;
using Unity.Tutorials.Core.Editor;

// ----- Not a BASE CLASS -----

namespace DogGame.Modules
{
    [RequireComponent(typeof(AgentMovementModule))]
    [RequireComponent(typeof(PackMemberModule))]
    [RequireComponent(typeof(BlackboardModule))]
    [RequireComponent(typeof(AgentDecisionModuleBase))]
    [RequireComponent(typeof(MotivationModule))]
    public class AgentModule : WorldModule
    {
        [Header("Debug / Identity")]
        public string agentName = "Unnamed Agent";

        //[Header("Agent Specific Modules")]
        // Agent Specific modules (most build on other modules):
        //public AgentMovementModule agentMovementModule { get; protected set; }
        //public PackMemberModule packMemberModule { get; protected set; }
        //public MotivationModule motivationModule { get; protected set; }

        [Header("Customized Module Views")]
        public AgentBlackboardView agentBlackboard;
        
        [Header("Initial Decision Type")]
        public AgentDecisionType initialDecisionType = AgentDecisionType.Wanderer;

        public AgentDecisionModuleBase currentDecisionModule;
        private AgentDecisionModuleBase[] allDecisionModules;

        public IAgentHandle iAgentHandle;

        protected override void Awake()
        {
            base.Awake();
            if (agentName.IsNullOrEmpty()) agentName=gameObject.name;

            //if (dir==null) dir=FindFirstObjectByType<Directory>();

            // Find all decision modules attached to this agent and initialize them.
            allDecisionModules = GetComponents<AgentDecisionModuleBase>();
            foreach (var module in allDecisionModules)
            {
                module.Initialize(this);
                if (module.DecisionType==initialDecisionType)
                    module.enabled = true;
                else
                    module.enabled = false;
                //Debug.Log($"[AgentModule {agentName}] initialized decision module: {module.GetType().Name} ({module.DecisionType})", this);
            }

            // Pick the initial module
            SwitchDecisionModule(initialDecisionType);
        }

        protected override void Update()
        {
            base.Update();
            //Debug.Log($"AgentModule.Update {agentName}: currentDecisionModule={currentDecisionModule}");
        }

        private int debugDoubleTick = -1;
        // Tick is called by WorldObject, pass it along to the current DecisionModule
        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

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
            Debug.Log($"AgentModule.SwitchDecisionModule {agentName}: decisionType = {decisionType}", this);

            // Disable the current one if any
            if (currentDecisionModule != null)
            {
                currentDecisionModule.EndDecisionModule();  // notify the old module it is losing control.
                currentDecisionModule.enabled = false;
            }

            // Find a module with matching DecisionType
            var nextModule = allDecisionModules.FirstOrDefault(m => m.DecisionType == decisionType);

            // failed to find a matching module.  Add them all and try again.
            if (nextModule == null)
            {
                worldObject.CreateModulesIfNeeded(ModuleFlagsTemplates.DecisionModules);
                allDecisionModules = GetComponents<AgentDecisionModuleBase>();
                nextModule = allDecisionModules.FirstOrDefault(m => m.DecisionType == decisionType);
            }
            if (nextModule.DecisionType != decisionType) Debug.LogError($"ERROR A: nextModule.DecisionType={nextModule.DecisionType},decisionType={decisionType},currentDecisionModule.DecisionType={currentDecisionModule.DecisionType}");
            currentDecisionModule = nextModule;

            if (currentDecisionModule != null)
            {
                currentDecisionModule.enabled = true;
                currentDecisionModule.BeginDecisionModule();  // notify the new module it is gaining control.
                //Debug.Log($"[AgentModule {agentName}] Switched to module {currentDecisionModule.GetType().Name}", this);
                if(currentDecisionModule.DecisionType == AgentDecisionType.Follower)
                {
                    // cast the decision module
                    FollowerDecisionModule followerDecisionModule = (FollowerDecisionModule) currentDecisionModule;
                    // find the leader
                    WorldObject leader = worldObject.packMemberModule.currentPack.packLeader;
                    // distance is set by number of pack members ahead of me.
                    float distance = worldObject.packMemberModule.currentPack.packAgentList.Count * 1.5f;
                    // set who to follow and how far.
                    followerDecisionModule.SetFollowTarget(leader, distance);
                }

                if(currentDecisionModule.DecisionType == AgentDecisionType.Wanderer)
                {
                    // cast the decision module
                    WandererDecisionModule wandererDecisionModule = (WandererDecisionModule) currentDecisionModule;
                }

                if(currentDecisionModule.DecisionType == AgentDecisionType.Player)
                {
                    // cast the decision module
                    PlayerDecisionModule playerDecisionModule = (PlayerDecisionModule) currentDecisionModule;
                }

                if(currentDecisionModule.DecisionType == AgentDecisionType.Immobile)
                {
                    // cast the decision module
                    ImmobileDecisionModule immobileDecisionModule = (ImmobileDecisionModule) currentDecisionModule;
                }

                if(currentDecisionModule.DecisionType == AgentDecisionType.LLM)
                {
                    // cast the decision module
                    ImmobileDecisionModule LLMDecisionModule = (ImmobileDecisionModule) currentDecisionModule;
                }
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
            currentDecisionModule.EndDecisionModule();  // notify the old module it is losing control.
            currentDecisionModule = decisionModule;
            currentDecisionModule.Initialize(this);
            currentDecisionModule.BeginDecisionModule();  // notify the new module it is gaining control.
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

        public void BecomeLLM()
        {
            SwitchDecisionModule(AgentDecisionType.LLM);
        }

        public void BecomeTaskFollower()
        {
            SwitchDecisionModule(AgentDecisionType.TaskFollower);
        }
    }

    public enum AgentDecisionType
    {
        Undefined = 0,
        Player,         // player controlled
        Follower,       // simple follower
        Wanderer,       // simple wanderer
        Immobile,       // just sits there
        LLM,            // driven by LLM (obsolete)
        TaskFollower,   // drives based on Task list

        // Add more: Predator, Boss, Civilian, Summoned, etc.
    }
}