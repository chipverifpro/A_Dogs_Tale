namespace DogGame.Lua
{
    public class ScentState
    {
        public string strongestScent = "";
        public bool foodTrail;
        public bool dogTrail;
        public bool humanTrail;
        public bool predatorTrail;

        public float trailAge;
        public float trailDirectionX;
        public float trailDirectionY;
        public float trailDirectionZ;
        public float trailStrength;

        public bool foodNearby;
        public bool freshTrail;
        public bool strangerDogScent;
        public bool packMemberScent;
    }
}
