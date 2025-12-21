using UnityEngine;

public enum ActivateKind
{
    RequestToJoinPack,
    // later: Talk, PickUp, Open, Attack, Sniff, Etc.
    
}

public enum ActivateResultKind
{
    Ignored,
    Accepted,
    Rejected,
    Deferred,
    Failed,
    Errored     // uses Debug.LogError, not Debug.Log
}

public enum GameMode
{
    WorldBuilding,
    Debug,
    Explore
}

public class ActivateContext
{
    public bool userIsInstigator;    // flag identifying user triggered action vs NPC triggered.
    public WorldObject instigator;   // who initiated (player dog / player / etc.)
    public WorldObject target;       // clicked object
    public GameMode gameMode;        // whatever your game uses
    public Vector3 hitPoint;         // where you clicked
    public bool promoteTarget;       // if target doesn't already have necessary capability, give it more

    public ActivateContext(bool userIsInstigator, WorldObject instigator, WorldObject target, GameMode gameMode, Vector3 hitPoint, bool promoteTarget = false)
    {
        this.userIsInstigator = userIsInstigator;
        this.instigator = instigator;
        this.target = target;
        this.gameMode = gameMode;
        this.hitPoint = hitPoint;
        this.promoteTarget = promoteTarget;
    }
}

public class ActivateRequest
{
    public ActivateKind kind;

    // optional payload (for future: itemId, dialog node, etc.)
    public ActivateRequest(ActivateKind kind) => this.kind = kind;
}

public class ActivateResult
{
    public ActivateResultKind kind;
    public string message;

    public ActivateResult(ActivateResultKind kind, string message = null)
    {
        this.kind = kind;
        this.message = message;
    }

    public static ActivateResult Accepted(string msg = null) => new(ActivateResultKind.Accepted, msg);
    public static ActivateResult Rejected(string msg = null) => new(ActivateResultKind.Rejected, msg);
    public static ActivateResult Ignored(string msg = null)  => new(ActivateResultKind.Ignored, msg);
    public static ActivateResult Failed(string msg = null)   => new(ActivateResultKind.Failed, msg);
    public static ActivateResult Errored(string msg = null)   => new(ActivateResultKind.Errored, msg);
}


[DefaultExecutionOrder(-150)]
public class GameInputRouter : MonoBehaviour
{
    // Simple singleton pattern; you can make this fancier if you like
    public static GameInputRouter Instance { get; private set; }

    public PlayerInputState InputState { get; private set; } = new PlayerInputState();

    [Tooltip("The WorldObject currently controlled by the player.")]
    public WorldObject currentControlledWorldObject => dir.playerPack.packLeader;  // pack 0, member 0
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
        RouteClickToTarget();
    }

    // Routes clicked target events to Activate their appropriate WorldObject
    public void RouteClickToTarget()
    {
        if (InputState.hasClickTargetWorldObject)
        {
            WorldObject target;
            Vector3 hitpoint;
            bool userIsInstigator;
            WorldObject instigator;

            target = InputState.clickTargetWorldObject;
            userIsInstigator = true;
            if (dir.playerPack.packLeader)
                instigator = dir.playerPack.packLeader; // for user inputs, use the current leader of the player pack.
            else
                instigator = target;

            // if hitpoint is not valid, use the world location of the target object.
            if(InputState.clickTargetLocationWorld == null)
            {
                if (target.locationModule==null) target.CreateModulesIfNeeded(ModuleFlags.locationModule);
                if (target.locationModule==null) Debug.LogError($"GameInputRouter could not get location of {target.DisplayName} because could not create LocationModule.");
                hitpoint = target.locationModule.pos3d_world;
            }
            else
                hitpoint = InputState.clickTargetLocationWorld;

            //Debug.Log($"GameInputRouter.TryClickActivate(userIsInstigator={userIsInstigator}, instigator={instigator}, target={target}, hitpoint={hitpoint})");
            TryClickActivate(userIsInstigator, instigator, target, hitpoint);
        }
    }

    public void TryClickActivate(bool userIsInstigator, WorldObject instigator, WorldObject target, Vector3 hitPoint)
    {
        if (target == null)
            return;

        // send the Activate command to the target WorldObject where it may
        // ensure the handler exists and forward it, or reject it.

        var context = new ActivateContext(
            userIsInstigator: userIsInstigator,  // identifies it was a user event (click, tap, select, etc.) versus an agent (tryting to pick something up, etc.)
            instigator: instigator,         // who/what is doing the action (for user, it is packLeader of playerPack)
            target: target,                 // who/what is targeted
            gameMode: currentGameMode,      // global variable (Explore, Debug, Build, etc)
            hitPoint: hitPoint,             // pos3d_world of actual contact point
            promoteTarget: true);           // allow target to add necessary Modules, if false then just fail if not available

        var request = new ActivateRequest(ActivateKind.RequestToJoinPack);

        // send request to the WorldObject target.
        ActivateResult result = target.Activate(context, request);

        if (result.kind != ActivateResultKind.Ignored && !string.IsNullOrEmpty(result.message))
            if (result.kind == ActivateResultKind.Errored)
                Debug.LogError($"Interaction {request.kind} on {target.name}: {result.kind} ({result.message})");
            else
                Debug.Log($"Interaction {request.kind} on {target.name}: {result.kind} ({result.message})");
    }
    
    // --- Global status of what is going on ---
    public void SetGameMode(GameMode value)
    {
        currentGameMode = value;
    }

}
