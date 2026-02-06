using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.Modules
{
    public class PackMemberModule : WorldModule
    {
        public Pack currentPack;

        [SerializeField] private List<LeashEndpoint> leashEndpoints = new();
        public IReadOnlyList<LeashEndpoint> LeashEndpoints => leashEndpoints;

        public bool isLeader
        {
            get
            {
                return ((currentPack != null) && (currentPack.packLeader == worldObject));
            }
        }

        protected override void Awake()
        {
            if (currentPack!=null)
            {
                //Debug.LogWarning($"[PackMemberModule.Awake {gameObject.name}] setting parent of {name} to {currentPack.name}");
                this.gameObject.transform.SetParent(currentPack.gameObject.transform,false);
            } else
            {
                //Debug.LogWarning($"[PackMemberModule.Awake {gameObject.name}] setting parent of {name} to FreeAgents");
                this.gameObject.transform.SetParent(dir.packManager.FreeAgentsParent.transform);
            }
            base.Awake();
        }

        private int debugDoubleTick = -1;
        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

        }

        // HandleRequestToJoinPack:
        // context.target = this.worldObject (who is changing packs)
        // context.instigatorIsPlayer => true to join PlayerPack (pack 0)
        // context.instigator => if valid, join the requesting agent's pack
        //                    => if null, create a new pack and join it.            
        public ActivateResult HandleRequestToJoinPack(in ActivateContext context)
        {

            // Part A: verify context
            // Example policy gate: only allow in certain modes
            if ((context.gameMode == GameMode.Debug) || (context.gameMode == GameMode.WorldBuilding))
                return ActivateResult.Rejected($"Not allowed in mode {context.gameMode}.");

            // Target must be promotable (or already an agent)
            if (context.target.agentModule == null)
            {
                worldObject.CreateModulesIfNeeded(ModuleFlagsTemplates.FullAgent);
                //worldObject.ApplyFollowerDefaults();
                Debug.Log($"promoted WorldObject {worldObject.DisplayName} to fullAgent.");
            }
            
            int currentPackNum = dir.packManager.GetPackNumber(currentPack);
            int packCount = dir.packManager.packs.Count;
            int targetPackNum;
            Pack targetPack = null;
            
            // Part B: determine target pack
            if (context.userIsInstigator && !dir.packManager.debug_RandomJoin)
            {
                targetPackNum = 0;
                targetPack = dir.playerPack;
            }
            else if (context.instigator != null && !dir.packManager.debug_RandomJoin)     // an agent requested the join, choose their pack.
            {   
                targetPack = context.instigator.packMemberModule.currentPack;
                targetPackNum = dir.packManager.GetPackNumber(targetPack);
            }
            else // ((!context.userIsInstigator || context.instigator == null) || dir.packManager.debug_RandomJoin)    // no requester identified, choose a random pack (including possibly a new one) 
            {
                // determine a random pack to join.
                // Random: targetPackNum is random from 0 to packCount+1-1.  if == packCount, we will create a new pack.
                targetPackNum = UnityEngine.Random.Range(0,packCount+1);  // ok to create another pack.
            }
            //Debug.Log($"packNum to join = {packNum}");
            
            // Part C: leave old pack if in one already
            // ===LEAVE OLD PACK only if we are going to change and we had been in a pack.
            if (currentPackNum != targetPackNum)
            {
                if (!LeaveCurrentPack()) 
                    return ActivateResult.Rejected($"{worldObject.DisplayName} attempt to leave pack {currentPack.packName} was unsuccessful.  No change.");
            }

            // Part D: create new pack if needed
            // ===CREATE A PACK if target is a nonexistant pack number.
            if (targetPackNum>=packCount)
            {
                // create a pack and add myself as leader (aka only member).
                targetPack = dir.packManager.CreateNewPack($"{worldObject.DisplayName}'s Pack", this.worldObject);
                return ActivateResult.Accepted($"{worldObject.DisplayName} created and joined new pack {targetPack.packName} as leader {dir.packManager.packs[targetPackNum].packLeader.DisplayName}");
            }

            // Part E: join new pack (or stay in the same one)
            // ===stay in same pack
            if (targetPackNum==currentPackNum)
            {
                // do nothing (not even any need to try and switch leader, not part of this request)
                return ActivateResult.Accepted($"{worldObject.DisplayName} attempted to join pack {currentPack.packName} but was already in it.  No change.");
            }
            else // ===join a different existing pack
            {
                // join an existing pack.
                targetPack = dir.packManager.packs[targetPackNum];
                targetPack.AddMember(worldObject);
                if (isLeader)  // is leader reflects our current status.
                {
                    return ActivateResult.Accepted($"{worldObject.DisplayName} joined pack {targetPack.packName} as leader {targetPack.packLeader.DisplayName}");
                }
                else
                {
                    return ActivateResult.Accepted($"{worldObject.DisplayName} joined pack {targetPack.packName} as follower {targetPack.packAgentList.Count-1} of leader {targetPack.packLeader.DisplayName}.");
                }
            }
        }

        // unused...so far
        public bool JoinPack(Pack packToJoin, bool setAsLeader = false)
        {
            Debug.LogWarning($"PackMemberModule JoinPack ({packToJoin}, {setAsLeader}) this");
            if (packToJoin == null) return false;

            if (currentPack != null && currentPack != packToJoin)
            {
                if (!LeaveCurrentPack()) return false;
            }

            currentPack = packToJoin;
            currentPack.AddMember(worldObject, setAsLeader);
            return true;
        }

        // called by HandleRequestToJoinPack and JoinPack (both local functions here)
        // returns false if we remain a pack member (only member of PlayerPack)
        public bool LeaveCurrentPack()
        {
            if (currentPack == null) return true;

            if (currentPack.isPlayerPack && currentPack.agentCount==1)
            {
                Debug.Log($"PlayerPack cannot be emptied.  Last member attempted to leave.");
                return false;
            }

            currentPack.RemoveMember(worldObject);
            currentPack = null;
            return true;
        }

        // OBSOLETE, same as RequestBecomeLeader when packNum==0
        public void RequestBecomeControlledAgent(int agentIndex)
        {
            
        }

        public void RequestBecomeLeader()
        {
            // remove existing pack leader
            // set this as pack leader
            currentPack.MoveAgentToLeader(this.worldObject);
        }

        // ---- Formation management functions ----
        public void SetFormation(FormationsEnum new_formation)
        {
            currentPack.SetFormation(new_formation);
        }

        public FormationsEnum GetFormation()
        {
            return currentPack.GetFormation();
        }

        public FormationsEnum CycleFormation()
        {
            FormationsEnum new_formation = GetFormation().Next();   // uses EnumExtensions class defined below.
            SetFormation(new_formation);
            return new_formation;
        }

        public int GetPositionInPack()
        {
            return currentPack.GetPositionInPack(worldObject);
        }

        // return the offset vector for the given formation and position in pack.
        // Assumes position_in_pack is 0 for leader, 1..n for followers.
        // Assumes leader facing north.  Rotation to be applied later.
        // some formations depend on number in pack.
        public Vector2 GetMyFormationOffset()
        {
            Vector2 offset;
            FormationsEnum formation = GetFormation();
            int position_in_pack = GetPositionInPack();
            int number_in_pack = currentPack.packAgentList.Count;
            offset = dir.packFormations.GetOffsetForFormation(formation,position_in_pack,number_in_pack);
            return offset;
        }


    }
}

// ==========================

// Handy helper to allow Next() of an enum.  See CycleFormation() above.
public static class EnumExtensions
{
    public static T Next<T>(this T value) where T : struct, Enum
    {
        T[] values = (T[])Enum.GetValues(typeof(T));
        int index = Array.IndexOf(values, value);
        index = (index + 1) % values.Length;    // wrap around
        return values[index];
    }

    public static T Previous<T>(this T value) where T : struct, Enum
    {
        T[] values = (T[])Enum.GetValues(typeof(T));
        int index = Array.IndexOf(values, value);
        index = (index - 1 + values.Length) % values.Length;
        return values[index];
    }
}