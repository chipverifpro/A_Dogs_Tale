using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

public class NonAgentUserInputHandler : MonoBehaviour
{
    public static GameInputRouter Instance { get; private set; }
    public Directory dir;
    public PlayerInputState InputState { get; private set; } = new PlayerInputState();

    public void Awake()
    {
        if (dir==null) dir=FindFirstObjectByType<Directory>();

    }

    public void Update()
    {
        WorldObject player_agent = dir.playerPack.packLeader;
        RouteClickToTarget();
        HandleCamera(InputState, Time.deltaTime);
        HandleOneShotActions(InputState, player_agent);
        HandleAgentSwitchingAndFormation(InputState, player_agent);
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
            gameMode: dir.gameInputRouter.currentGameMode,      // global variable (Explore, Debug, Build, etc)
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
        dir.gameInputRouter.currentGameMode = value;
    }

    #region Camera

    private void HandleCamera(PlayerInputState state, float deltaTime)
    {
        if (dir.cameraModeSwitcher != null)
        {
            if (Mathf.Abs(state.zoomDelta) > 0.0001f)
            {
                Debug.Log($"ApplyZoomDelta: {state.zoomDelta}");
                dir.cameraModeSwitcher.ApplyZoomDelta(state.zoomDelta);
            }

            if (state.cameraViewSelect != CameraModes.Unchanged)
            {
                Debug.Log($"SelectView: {state.cameraViewSelect}");
                dir.cameraModeSwitcher.SelectView(state.cameraViewSelect);
            }
        }
    }

    #endregion

    #region One-shot actions

    private void HandleOneShotActions(PlayerInputState state, WorldObject worldObject)
    {
        if (state.barkPressed && worldObject.noiseMakerModule != null)
        {
            worldObject.noiseMakerModule.Bark();
        }

        if (state.markTerritoryPressed && worldObject.scentEmitterModule != null)
        {
            worldObject.scentEmitterModule.EmitOnDemandScent(1.0f); // spread over 1 second
        }

        // You can also use state.anyKeyOrButtonPressed to skip cutscenes,
        // advance dialogue, etc. Hook that into your game state manager.
    }

    #endregion

    #region Pack / player agent selection

    private void HandleAgentSwitchingAndFormation(PlayerInputState state, WorldObject worldObject)
    {
        if (worldObject.packMemberModule == null) return;

        if (state.requestedPlayerAgentIndex >= 0)
        {
            worldObject.packMemberModule.RequestBecomeControlledAgent(state.requestedPlayerAgentIndex);
        }

        if (state.changeFormationPressed)
        {
            worldObject.packMemberModule.CycleFormation();
        }
    }

    #endregion
}