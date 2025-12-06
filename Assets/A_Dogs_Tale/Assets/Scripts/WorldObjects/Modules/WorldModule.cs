using UnityEngine;

// ----- ABSTRACT BASE CLASS -----

// WorldModules are components that can be attached to WorldObjects to give them specific functionalities.
// Based on WorldModule, are a variety of specialized modules like LocationModule, MotionModule, VisualModule, etc.
public abstract class WorldModule : MonoBehaviour
{
    protected Directory dir;            // auto-set in Awake()
    protected WorldObject worldObject;  // auto-set in Initialize() called by WorldObject

    protected virtual void Awake()
    {
        dir = Directory.Instance;
        if (dir == null)
            Debug.LogError("WorldModule: ObjectDirectory.Instance is null!", this);
    }

    // each WorldModule belongs to a WorldObject
    public virtual void Initialize(WorldObject owner)
    {
        worldObject = owner;
    }

    protected virtual void Update()
    {

    }

    public virtual void Tick(float deltaTime)
    {
        Debug.Log($"WorldModule {worldObject.DisplayName}: Tick {deltaTime}");
    }

    // future hook: OnWorldObjectAttached(), OnWorldObjectDetached(), etc.
}
