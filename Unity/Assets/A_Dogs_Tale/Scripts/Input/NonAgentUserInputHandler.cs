using UnityEngine;

public class NonAgentUserInputHandler : MonoBehaviour
{
    public static GameInputRouter Instance { get; private set; }
    public Dir dir;
    public PlayerInputState InputState { get; private set; }

    public void Awake()
    {
        EnsureRuntimeReferences(logFailure: true);
    }

    public void Start()
    {
        EnsureRuntimeReferences(logFailure: true);
    }

    private void OnEnable()
    {
        EnsureRuntimeReferences(logFailure: false);
    }

    public void Update()
    {
        if (!EnsureRuntimeReferences(logFailure: false))
            return;

        WorldObject player_agent = dir.playerPack.packLeader;
        HandleCamera(InputState, Time.deltaTime);
        HandleAgentSwitchingAndFormation(InputState, player_agent);
    }

    private bool EnsureRuntimeReferences(bool logFailure)
    {
        if (dir == null)
            dir = Dir.Instance ?? FindFirstObjectByType<Dir>();

        if (dir == null)
        {
            if (logFailure)
                Debug.LogWarning("[NonAgentUserInputHandler] Waiting for Dir after reload.", this);
            return false;
        }

        GameInputRouter router = dir.gameInputRouter != null ? dir.gameInputRouter : GameInputRouter.Instance;
        if (router == null)
        {
            if (logFailure)
                Debug.LogWarning("[NonAgentUserInputHandler] Waiting for GameInputRouter after reload.", this);
            return false;
        }

        if (dir.gameInputRouter == null)
            dir.gameInputRouter = router;

        InputState = router.InputState;
        if (InputState == null)
        {
            if (logFailure)
                Debug.LogWarning("[NonAgentUserInputHandler] Waiting for InputState after reload.", this);
            return false;
        }

        return dir.playerPack != null;
    }

    #region Camera

    private void HandleCamera(PlayerInputState state, float deltaTime)
    {
        if (state == null || dir == null)
            return;

        if (dir.cameraModeSwitcher != null)
        {
            if (Mathf.Abs(state.zoomDelta) > 0.0001f)
            {
                bool pointerOverBottomBanner = BottomBanner.Instance != null && BottomBanner.Instance.IsPointerOverPanel();
                if (!pointerOverBottomBanner)
                {
                    Debug.Log($"ApplyZoomDelta: {state.zoomDelta}");
                    dir.cameraModeSwitcher.ApplyZoomDelta(state.zoomDelta);
                }
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
        if (state == null || worldObject == null)
            return;

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
