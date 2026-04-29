using UnityEngine;
using InspectorTools;
using UnityEngine.UIElements;

namespace DogGame.Modules
{
    // Each item that contain
    public enum HowToUse
    {
        DoNothing = 0,      // for objects that do nothing
        CreateLeash,        // for rope or chain items
        EatFood,            // for food items
        Open,               // for key items that open container agents
    }

    [DisallowMultipleComponent]
    [InspectorNote("Ability_Modules/Activator Module", "What the agent or object does when clicked on.  To be replaced by Interaction Wheel?", UnityEditor.MessageType.Warning)]
    public class ActivatorModule : WorldModule
    {
        public HowToUse howToUse      = HowToUse.DoNothing;
        [Header("Optional Use Parameters")]
        public bool   parameterDestruct = false; // true if item is destroyed when used.
        public float  parameterFloat  = -1f;    // eg: rope length
        public int    parameterInt    = -1;     // eg: calories when eaten
        public string parameterString = "";     // eg: adjective when eaten: "Yummy"
        public string toolTip         = "";     // ToolTip for the Use Item button.

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

                case HowToUse.Open:
                    success = UseToOpen(key: this.worldObject, box: otherAgent);
                    break;

                case HowToUse.EatFood:
                    success = UseToEat(food: this.worldObject, agent: agent);
                    break;
            }
            return success;
        }

        public bool UseToEat(WorldObject food, WorldObject agent)
        {
            bool success = false;
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
            return success;
        }
        
        public bool UseToOpen(WorldObject key, WorldObject box)
        {
            bool success = false;
            if (box.containerModule && (box.containerModule.isClosed || box.containerModule.isLocked))
            {
                box.containerModule.isLocked = false;
                box.containerModule.isClosed = false;
                Debug.Log($"Container {box.DisplayName} unlocked and opened using {key.DisplayName}.");
                box.changeDisplayName("Unlocked chest");
                success = true;
            } else
            {
                Debug.Log($"{key.DisplayName} can only open Containers that are closed or locked.");
                success = false;
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