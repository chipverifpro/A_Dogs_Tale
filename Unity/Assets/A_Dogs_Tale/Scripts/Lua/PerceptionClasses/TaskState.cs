namespace DogGame.Lua
{
    public class TaskState
    {
        public string current = "";
        public string target = "";
        public string destination = "";

        public bool targetVisible;
        public float targetDistance;
        public bool destinationReached;
        public bool pathBlocked;
    }
}
