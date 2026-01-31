namespace DogGame.Noise
{
    public static class NoiseIdUtil
    {
        public const int UnknownId = -1;

        public static bool IsValidWorldObjectId(int objectId) => objectId > 0;

        public static int GetWorldObjectIdOrUnknown(WorldObject worldObject)
        {
            if (worldObject == null) return UnknownId;

            int id = worldObject.ObjectId;
            return IsValidWorldObjectId(id) ? id : UnknownId;
        }

        public static string GetWorldObjectNameOrUnknown(WorldObject worldObject)
        {
            if (worldObject == null) return "unknown";
            return string.IsNullOrWhiteSpace(worldObject.DisplayName) ? worldObject.name : worldObject.DisplayName;
        }
    }
}