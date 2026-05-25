#nullable enable
using System.Collections.Generic;
using DogGame.Modules;
using static DogGame.Modules.VisionPerceptionModule;

namespace DogGame
{
    public static class PackRelations
    {
        public static bool TryGetPackList(WorldObject agent, out List<WorldObject>? packList)
        {
            packList = agent.packMemberModule?.currentPack?.packAgentList;
            return packList != null;
        }

        public static SocialRelation GetRelation(WorldObject self, WorldObject other)
        {
            if (self == other)
                return SocialRelation.Self;

            var pack = self.packMemberModule?.currentPack;
            if (pack == null || pack.packAgentList == null || pack.packAgentList.Count == 0)
                return SocialRelation.NonPack;

            var list = pack.packAgentList;

            // Leader = first entry
            if (list[0] == other)
                return SocialRelation.PackLeader;

            // Packmate = any other entry
            // (Linear search is fine for small packs; pack sizes are typically low.)
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i] == other)
                    return SocialRelation.Packmate;
            }

            return SocialRelation.NonPack;
        }

        public static WorldObject? GetLeader(WorldObject self)
        {
            var list = self.packMemberModule?.currentPack?.packAgentList;
            if (list == null || list.Count == 0) return null;
            return list[0];
        }

        public static bool IsLeaderVisible(WorldObject self, List<VisionDetection> detections)
        {
            var leader = GetLeader(self);
            if (leader == null) return true; // No leader defined => no “missing leader” concept

            for (int i = 0; i < detections.Count; i++)
            {
                if (detections[i].target == leader)
                    return true;
            }
            return false;
        }
    }
}