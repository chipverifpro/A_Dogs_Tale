using UnityEngine;

namespace DogGame.Modules
{
    [DisallowMultipleComponent]
    public class ActivatorModule : WorldModule
    {
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