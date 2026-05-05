using UnityEngine;
using InspectorTools;

// ----- ABSTRACT BASE CLASS -----

namespace DogGame.Modules
{
    // This enum applies to multiple DecisionModules, so has been moved to this common base class.
    public enum NavigationSource
        {
            None,               // nowhere to go right now (maybe stay, maybe decide to wander a bit?)
            PlayerDirection,    // keyboard or joystick controls
            ClickToMove,        // clicked on destination, have not set up pathfinding route yet, so head direct.
            Pathfinding,        // following pathfinding crumbs.
            Scripted,           // following a script (don't interrupt/override)
            AI                  // Dog decided what to do itself (via motivations likely)
        }
    
    [InspectorNote("AgentDecision_Modules/Agent Decision Module Base", "BASE MODULE, DO NOT INSTANTIATE.", UnityEditor.MessageType.Error)]
    public abstract class AgentDecisionModuleBase : WorldModule
    {

        public abstract AgentDecisionType DecisionType { get; }
        protected AgentModule agentModule;
 
        // Initialize called from WorldObject.Awake phase
        public virtual void Initialize(AgentModule agentModuleOwner)
        {
            agentModule = agentModuleOwner;
        }

        public override void Tick(float deltaTime)
        {
            Debug.Log($"AgentDecisionModuleBase {worldObject.DisplayName}: Tick {deltaTime}");
        }

        protected void StopMovementIntent()
        {
            worldObject?.agentMovementModule?.ClearDesiredMovement();
        }

        protected void UseAutonomousFaceMovement()
        {
            MotionModule motion = worldObject?.motionModule;
            if (motion == null)
                return;

            motion.motionControlMode = MotionControlMode.Autopilot;
            motion.facingMode = FacingMode.FaceMovementDirection;
            motion.facingTarget = null;
            motion.isBackpedaling = false;
        }

        // These functions should be called when decisionModule changes.
        // Clears / resumes actions in progress.
        public abstract void BeginDecisionModule(bool resume=false);
        public abstract void EndDecisionModule();
    }
}
