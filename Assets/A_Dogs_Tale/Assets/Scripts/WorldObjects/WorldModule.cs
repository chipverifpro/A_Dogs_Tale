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