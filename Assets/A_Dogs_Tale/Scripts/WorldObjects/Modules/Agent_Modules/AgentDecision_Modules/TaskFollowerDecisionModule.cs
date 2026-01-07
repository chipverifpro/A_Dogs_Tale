using UnityEngine;


// This is basically a module implementation of TaskControler.cs
// It parses the task list (in TaskListModule) and issues those commands.
//
// when no commands are present, it switches back to another Decision Module
// 
// It can be interrupted by higher priority tasks requested by PlayerDecisionModule or ReactionModule
// in that case, state is preserved for possible resume.
// Hooks present for clearing the queue if resume is not desired.
namespace DogGame.Modules
{
    public class TaskFollowerDecisionModule : AgentDecisionModuleBase
    {
        public override AgentDecisionType DecisionType => AgentDecisionType.TaskFollower;
        #region BeginEndDecisionModule
        // Run this when THIS decision module becomes active
        public override void BeginDecisionModule(bool resume=false)
        {
            if (resume)
            {
            }
            else
            {
                // clear state left over from last time we were active

                // stop any in-progress movement
                worldObject.agentMovementModule.ClearDesiredMove();
            }            
        }

        // Run this when THIS decision module becomes inactive
        public override void EndDecisionModule()
        {
            // retain state (in case requested to resume): currentDestination*
            
            // stop actions in progress: Move
            worldObject.agentMovementModule.ClearDesiredMove();
        }
        #endregion
    }
}