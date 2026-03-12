#nullable enable
using DogGame.Modules;

namespace DogGame.Lua
{
    /// <summary>
    /// Lua-friendly flattened view of a PerceptionEvent.
    /// </summary>
    public sealed class PerceptionEventState
    {
        public string sense = "";
        public string type = "";

        public float strength01;
        public float novelty01;
        public float interest01;

        public float worldX;
        public float worldY;
        public float worldZ;

        public bool hasTarget;
        public int targetObjectId = -1;
        public string targetName = "";

        public bool hasScent;
        public string scentKey = "";
        public string scentCategory = "";
        public int scentCategoryId = -1;
        public string scentName = "";

        public bool hasVision;
        public float visionDistanceMeters;
        public float visionSpeedMps;
        public float visionAngleDeg;
        public string visionKind = "";
        public int visionKindId = -1;
        public string visionRelation = "";
        public int visionRelationId = -1;

        public bool hasSound;
        public float soundLoudness01;
        public float soundDistanceMeters;
        public string soundCategory = "";
        public int soundCategoryId = -1;
        public string soundSubtype = "";
        public int soundSubtypeId = -1;
        public bool soundAddressedToMe;
        public float soundAddressedConfidence01;

        public static PerceptionEventState FromPerceptionEvent(in PerceptionEvent e)
        {
            var state = new PerceptionEventState
            {
                sense = e.Sense.ToString(),
                type = e.Type.ToString(),
                strength01 = e.Strength01,
                novelty01 = e.Novelty01,
                interest01 = e.Interest01,
                worldX = e.WorldPos.x,
                worldY = e.WorldPos.y,
                worldZ = e.WorldPos.z,
                hasTarget = e.Target != null,
                targetObjectId = e.Target != null ? e.Target.ObjectId : -1,
                targetName = e.Target != null ? e.Target.DisplayName : ""
            };

            if (e.Scent.HasValue)
            {
                var scent = e.Scent.Value;
                state.hasScent = true;
                state.scentKey = scent.ScentKey;
                state.scentCategory = scent.Category.ToString();
                state.scentCategoryId = (int)scent.Category;
                state.scentName = scent.ScentName;
            }

            if (e.Vision.HasValue)
            {
                var vision = e.Vision.Value;
                state.hasVision = true;
                state.visionDistanceMeters = vision.DistanceMeters;
                state.visionSpeedMps = vision.SpeedMps;
                state.visionAngleDeg = vision.AngleDeg;
                state.visionKind = vision.Kind.ToString();
                state.visionKindId = (int)vision.Kind;
                state.visionRelation = vision.Relation.ToString();
                state.visionRelationId = (int)vision.Relation;
            }

            if (e.Sound.HasValue)
            {
                var sound = e.Sound.Value;
                state.hasSound = true;
                state.soundLoudness01 = sound.Loudness01;
                state.soundDistanceMeters = sound.DistanceMeters;
                state.soundCategory = sound.Category.ToString();
                state.soundCategoryId = (int)sound.Category;
                state.soundSubtype = sound.Subtype.ToString();
                state.soundSubtypeId = (int)sound.Subtype;
                state.soundAddressedToMe = sound.AddressedToMe;
                state.soundAddressedConfidence01 = sound.AddressedConfidence01;
            }

            return state;
        }
    }
}
