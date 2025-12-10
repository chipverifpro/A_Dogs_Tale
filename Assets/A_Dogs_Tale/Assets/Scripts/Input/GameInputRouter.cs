using UnityEngine;

[DefaultExecutionOrder(-150)]
public class GameInputRouter : MonoBehaviour
{
    // Simple singleton pattern; you can make this fancier if you like
    public static GameInputRouter Instance { get; private set; }

    public PlayerInputState InputState { get; private set; } = new PlayerInputState();

    [Tooltip("The WorldObject currently controlled by the player.")]
    public WorldObject currentControlledWorldObject;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (InputState == null)
            InputState = new PlayerInputState();
    }

    public void SetControlledWorldObject(WorldObject wo)
    {
        currentControlledWorldObject = wo;
        // Optional: tell agents they gained/lost control
        // wo.agentModule?.OnBecamePlayerControlled();
    }

    public bool IsControlled(WorldObject wo)
    {
        return wo != null && wo == currentControlledWorldObject;
    }
}
