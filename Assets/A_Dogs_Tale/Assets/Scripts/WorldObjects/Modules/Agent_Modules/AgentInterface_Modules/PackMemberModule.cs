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
            Debug.Log($"JoinPack ({packToJoin}, {setAsLeader}) this");
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