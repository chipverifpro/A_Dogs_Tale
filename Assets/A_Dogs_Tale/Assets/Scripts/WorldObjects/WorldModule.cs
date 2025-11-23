using UnityEngine;

// WorldModules are components that can be attached to WorldObjects to give them specific functionalities.
// Based on WorldModule, are a variety of specialized modules like LocationModule, MotionModule, VisualModule, etc.
public abstract class WorldModule : MonoBehaviour
{
    protected ObjectDirectory dir;

    protected virtual void Awake()
    {
        dir = ObjectDirectory.Instance;
        if (dir == null)
            Debug.LogError("WorldModule: ObjectDirectory.Instance is null!", this);
    }

    // future hook: OnWorldObjectAttached(), OnWorldObjectDetached(), etc.
}


// Placeholders for other WorldModule types:
public class MotionModule : WorldModule
{
    public Vector3? Destination;
    public float MoveSpeed = 3f;
}

public class NPCModule : WorldModule
{
    public enum State { Idle, Patrol, Attack }
    public State CurrentState = State.Idle;
}

public class ScentEmitter : WorldModule
{
    public ScentSource scentSource;
}

public class ActivatorModule : WorldModule
{
    public UnityEngine.Events.UnityEvent OnActivate;
}