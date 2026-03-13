using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

public class NonAgentUserInputHandler : MonoBehaviour
{
    public static GameInputRouter Instance { get; private set; }
    public Dir dir;
    public PlayerInputState InputState { get; private set; }

    public void Awake()
    {
        if (dir==null) dir=FindFirstObjectByType<Dir>();

    }

    public void Start()
    {
        if (InputState == null)
            InputState = dir.gameInputRouter.InputState;  // keep a reference, not a copy
        
        if (InputState == null)
        {
            Debug.LogError($"[NonAgentUserInputHandle] inputState is null.", this);
        }
    }

    public void Update()
    {
        WorldObject player_agent = dir.playerPack.packLeader;
        HandleCamera(InputState, Time.deltaTime);
        HandleAgentSwitchingAndFormation(InputState, player_agent);
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