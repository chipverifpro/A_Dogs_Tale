using UnityEngine;

public enum InteractionKind
{
    RequestToJoinPack,
    // later: Talk, PickUp, Open, Attack, Sniff, Etc.
}

public enum InteractionResultKind
{
    Ignored,
    Accepted,
    Rejected,
    Deferred,
    Failed
}

public enum GameMode
{
    WorldBuilding,
    Debug,
    Explore
}

public class InteractionContext
{
    public WorldObject instigator;  // who initiated (player dog / player / etc.)
    public WorldObject target;       // clicked object
    public GameMode gameMode;        // whatever your game uses
    public Vector3 hitPoint;         // where you clicked
    public bool promoteTarget;       // if target doesn't already have necessary capability, give it more

    public InteractionContext(WorldObject instigator, WorldObject target, GameMode gameMode, Vector3 hitPoint, bool promoteTarget = false)
    {
        this.instigator = instigator;
        this.target = target;
        this.gameMode = gameMode;
        this.hitPoint = hitPoint;
        this.promoteTarget = promoteTarget;
    }
}

public class InteractionRequest
{
    public InteractionKind kind;

    // optional payload (for future: itemId, dialog node, etc.)
    public InteractionRequest(InteractionKind kind) => this.kind = kind;
}

public class InteractionResult
{
    public InteractionResultKind kind;
    public string message;

    public InteractionResult(InteractionResultKind kind, string message = null)
    {
        this.kind = kind;
        this.message = message;
    }

    public static InteractionResult Accepted(string msg = null) => new(InteractionResultKind.Accepted, msg);
    public static InteractionResult Rejected(string msg = null) => new(InteractionResultKind.Rejected, msg);
    public static InteractionResult Ignored(string msg = null)  => new(InteractionResultKind.Ignored, msg);
    public static InteractionResult Failed(string msg = null)   => new(InteractionResultKind.Failed, msg);
}


[DefaultExecutionOrder(-150)]
public class GameInputRouter : MonoBehaviour
{
    // Simple singleton pattern; you can make this fancier if you like
    public static GameInputRouter Instance { get; private set; }

    public PlayerInputState InputState { get; private set; } = new PlayerInputState();

    [Tooltip("The WorldObject currently controlled by the player.")]
    public WorldObject currentControlledWorldObject;
    public Directory dir;

    public GameMode currentGameMode = GameMode.Explore;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dir==null) dir=Directory.Instance;

        if (InputState == null)
            InputState = new PlayerInputState();
        
        SetGameMode(GameMode.Explore);
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

    // ======================================================
    // === Scripts in various modules consume InputState: ===
    // === PlayerDecisionModule - player controls         ===
    // === CameraModeSwitcher   - camera controls         ===
    // === GameLoader           - Load / Save / Quit      ===
    // === HeadsUpDisplay       - open menus              ===
    // === GameInputRouter      - activate world objects  ===
    // ======================================================

    public void Update()
    {
        if (InputState.hasClickTargetWorldObject)
            // instigator = null means user input was the source.
            TryClickActivate(InputState.clickTargetWorldObject, InputState.clickTargetWorldObject, InputState.clickTargetLocationWorld);
    }
    
    public void SetGameMode(GameMode value)
    {
        currentGameMode = value;
    }

    public void TryClickActivate(WorldObject instigator, WorldObject target, Vector3 hitPoint)
    {
        if (target == null)
            return;

        // For now, make sure the target can be activated by creating a default activator module if none present.
        //target.CreateModulesIfNeeded(ModuleFlags.activatorModule);
        
        //if (target == null)
        //{
        //    Debug.LogWarning($"Clicked '{target.name}' but it has no ActivatorModule.");
        //    return;
        //}

        var context = new InteractionContext(
            instigator: instigator,         // may be null
            target: target,
            gameMode: currentGameMode,      // global variable
            hitPoint: hitPoint,
            promoteTarget: true);           // allow target to add necessary Modules

        var request = new InteractionRequest(InteractionKind.RequestToJoinPack);

        InteractionResult result = target.HandleInteraction(context, request);

        if (result.kind != InteractionResultKind.Ignored && !string.IsNullOrEmpty(result.message))
            Debug.Log($"Interaction {request.kind} on {target.name}: {result.kind} ({result.message})");
    }
}
