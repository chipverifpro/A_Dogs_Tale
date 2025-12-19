using UnityEngine;

namespace DogGame.Modules
{
    [DisallowMultipleComponent]
    public class ActivatorModule : WorldModule
    {
        public InteractionResult HandleInteraction(in InteractionContext context, in InteractionRequest request)
        {
            //request.target = this worldObject
            //request.instigator = null when user requested action.

            // Route by request kind
            return request.kind switch
            {
                InteractionKind.RequestToJoinPack => HandleRequestToJoinPack(context),
                _ => InteractionResult.Ignored("Unhandled interaction.")
            };
        }

        // TODO: move this function to PackMemberModule...  If we do, may need to create it first.
        private InteractionResult HandleRequestToJoinPack(in InteractionContext context)
        {
            // context.target = this.worldObject;
            // context.instigator = null if user requested it.
            Pack targetPack = null;

            // Example policy gate: only allow in certain modes
            if ((context.gameMode == GameMode.Debug) || (context.gameMode == GameMode.WorldBuilding))
                return InteractionResult.Rejected($"Not allowed in mode {context.gameMode}.");

            // Target must be promotable (or already an agent)
            if (context.target.packMemberModule == null)
            {
                worldObject.CreateModulesIfNeeded(ModuleFlagsTemplates.FullAgent);
                //worldObject.ApplyFollowerDefaults();
                Debug.Log($"promoted WorldObject {worldObject.DisplayName} to fullAgent.");
            }

            int packNum = 0;
            if (context.instigator == null)
            {
                // determine a random pack to join.
                int packCount = dir.packManager.packs.Count;
                packNum = Random.Range(0,packCount+1);  // ok to create another pack.
                if (packNum==packCount)
                {
                    // create a pack
                    dir.packManager.CreateNewPack($"{worldObject.DisplayName}'s Pack");
                }
                else
                {
                    // no need to create a pack
                }
                targetPack = dir.packManager.packs[packNum];
            } 
            else
            {
                targetPack = context.instigator.packMemberModule.currentPack;
            }
            int numAgentsInPack = targetPack.packAgentList.Count;
            bool setAsLeader = numAgentsInPack==0;  // nobody in pack
            targetPack.AddMember(worldObject, setAsLeader: setAsLeader);
            if (setAsLeader)
            {
                return InteractionResult.Accepted($"{worldObject.DisplayName} joined pack {dir.packManager.packs[packNum].packName} as leader {dir.packManager.packs[packNum].packLeader.DisplayName}");
            }
            else
            {
                return InteractionResult.Accepted($"{worldObject.DisplayName} joined pack {dir.packManager.packs[packNum].packName} as follower {dir.packManager.packs[packNum].packAgentList.Count-1} of leader {dir.packManager.packs[packNum].packLeader.DisplayName}.");
            }
        }
    }
}