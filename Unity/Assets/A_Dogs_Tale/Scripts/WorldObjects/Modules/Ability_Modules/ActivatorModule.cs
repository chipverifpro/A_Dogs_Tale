using UnityEngine;
using InspectorTools;
using UnityEngine.UIElements;

namespace DogGame.Modules
{
    // Each item that contain
    public enum HowToUse
    {
        DoNothing = 0,      // for objects that do nothing
        CreateLeash,        // for rope or chain objects
        EatFood,            // for food objects
    }

    [DisallowMultipleComponent]
    [InspectorNote("Ability_Modules/Activator Module", "What the agent or object does when clicked on.  To be replaced by Interaction Wheel?", UnityEditor.MessageType.Warning)]
    public class ActivatorModule : WorldModule
    {
        public HowToUse howToUse      = HowToUse.DoNothing;
        [Header("Optional Use Parameters")]
        public float  parameterFloat  = -1f;    // eg: rope length
        public int    parameterInt    = -1;     // eg: calories when eaten
        public string parameterString = "";     // eg: adjective when eaten: "Yummy"

        private int calories = 0;   // just a dumb little accumulator of everything eaten.

        public bool TryUseItem (WorldObject agent, WorldObject otherAgent)
        {
            bool success = false;
            switch(howToUse)
            {
                case HowToUse.DoNothing:
                    Debug.Log($"Item {this.worldObject.DisplayName} cannot be used.");
                    success = false;
                    break;

                case HowToUse.CreateLeash:
                    success = UseToCreateLeash(rope: this.worldObject, walkerWorldObject:agent, dogWorldObject:otherAgent, maxLength: parameterFloat);
                    break;

                case HowToUse.EatFood:
                    if (parameterString=="" || parameterInt<0)
                    {
                        Debug.Log($"Item {this.worldObject.DisplayName} cannot be eaten unless parameters String (adjective) and Int (calories) are set");
                        success = false;
                    }
                    else
                    {
                        calories += parameterInt;
                        Debug.Log($"{agent.DisplayName} ate the {parameterString} {this.worldObject.DisplayName} gaining {parameterInt} calories.");
                        success = true;
                    };
                    break;
            }
            return success;
        }

        public bool UseToCreateLeash (WorldObject rope, WorldObject walkerWorldObject, WorldObject dogWorldObject, float maxLength)
        {    
            LeashLink leash;    // created leash isn't really needed here.

            bool created = Dir.Instance.leashSystem.TryCreateLeash(
                a: walkerWorldObject,
                roleA: LeashEndRole.Handle,
                b: dogWorldObject,
                roleB: LeashEndRole.Clip,
                maxLength: maxLength,
                out leash);
            if (created) 
                Debug.Log(leash.LeashToString());
            else
                Debug.LogWarning($"Failed to create leash made from {this.worldObject.DisplayName} from {walkerWorldObject} to {dogWorldObject} of length {maxLength}");
            
            return created;
    }

        public ActivateResult HandleActivate(in ActivateContext context, in ActivateRequest request)
        {
            //Debug.Log($"ActivatorModule.HandleActivate");

            // Route by request kind
            switch (request.kind)
            {
                case ActivateKind.RequestToJoinPack:
                    if (worldObject.packMemberModule == null && context.promoteTarget)
                        worldObject.CreateModulesIfNeeded(ModuleFlags.packMemberModule);
                    if (worldObject.packMemberModule != null)
                        return worldObject.packMemberModule.HandleRequestToJoinPack(context);
                    else
                        return ActivateResult.Ignored("Target did not have a packMemberModule.");
                default:
                    return ActivateResult.Ignored("Unhandled interaction.");
            }
        }
    }
}