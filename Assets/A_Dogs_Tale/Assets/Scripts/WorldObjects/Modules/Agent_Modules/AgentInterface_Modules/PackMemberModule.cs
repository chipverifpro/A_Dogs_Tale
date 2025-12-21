using System;
using UnityEngine;

namespace DogGame.Modules
{
    public class PackMemberModule : WorldModule
    {
        public Pack currentPack;

        public bool IsLeader
        {
            get
            {
                return ((currentPack != null) && (currentPack.packLeader == worldObject));
            }
        }

        protected override void Awake()
        {
            base.Awake();
        }

        public override void Tick(float deltaTime)
        {
            //Debug.Log($"PackMemberModule {worldObject.DisplayName}: Tick {deltaTime}");
        }

        public void JoinPack(Pack packToJoin, bool setAsLeader = false)
        {
            Debug.LogWarning($"PackMemberModule JoinPack ({packToJoin}, {setAsLeader}) this");
            if (packToJoin == null) return;

            if (currentPack != null && currentPack != packToJoin)
            {
                LeaveCurrentPack();
            }

            currentPack = packToJoin;
            currentPack.AddMember(worldObject, setAsLeader);
        }

        public void LeaveCurrentPack()
        {
            if (currentPack == null) return;

            currentPack.RemoveMember(worldObject);
            currentPack = null;
            // clear leader if set???
        }

        public void RequestBecomeControlledAgent(int agentIndex)
        {
            
        }

        public void RequestBecomeLeader()
        {
            // remove existing pack leader
            // set this as pack leader
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

        public ActivateResult HandleRequestToJoinPack(in ActivateContext context)
        {
            // context.target = this.worldObject;
            // context.instigator = null if user requested it.
            Pack targetPack = null;
            bool setAsLeader;

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

            int packNum = 0;
            //if (context.instigator == null)       // TEMPORARY DISABLE
            //{
                // determine a random pack to join.
                int packCount = dir.packManager.packs.Count;
                // Random: packnum is 0 to packCount+1-1.  if = packCount, create a new pack.
                packNum = UnityEngine.Random.Range(0,packCount+1);  // ok to create another pack.
                //Debug.LogWarning($"packNum to join = {packNum}");
                if (packNum==packCount)
                {
                    // create a pack
                    dir.packManager.CreateNewPack($"{worldObject.DisplayName}'s Pack");
                    setAsLeader = true;
                }
                else
                {
                    // no need to create a pack, will join an existing one.
                    setAsLeader = false;
                }
                targetPack = dir.packManager.packs[packNum];
            //} 
            //else
            //{
            //    targetPack = context.instigator.packMemberModule.currentPack;
            //}
            //int numAgentsInPack = targetPack.packAgentList.Count;
            //bool setAsLeader = numAgentsInPack==0;  // nobody in pack
            targetPack.AddMember(worldObject, setAsLeader: setAsLeader);
            if (setAsLeader)
            {
                return ActivateResult.Accepted($"{worldObject.DisplayName} joined pack {dir.packManager.packs[packNum].packName} as leader {dir.packManager.packs[packNum].packLeader.DisplayName}");
            }
            else
            {
                return ActivateResult.Accepted($"{worldObject.DisplayName} joined pack {dir.packManager.packs[packNum].packName} as follower {dir.packManager.packs[packNum].packAgentList.Count-1} of leader {dir.packManager.packs[packNum].packLeader.DisplayName}.");
            }
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