using UnityEngine;

public class MotionModule : WorldModule
{
    public Vector3? Destination;
    public float MoveSpeed = 3f;

    public void SetMoveTarget(Vector3 targetLocationWorld)
    {
        Destination = targetLocationWorld;
    }

        public void SetMoveVector(Vector3 targetLocationWorld)
    {
        Destination = targetLocationWorld;
    }
}