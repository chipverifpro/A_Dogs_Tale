using UnityEngine;

public class PlayerInputStateDebugger : MonoBehaviour
{
    //[SerializeField] private NewInputAdapter inputAdapter;
    public bool enableDebugLogging = false;
    //[SerializeField] private PlayerInputState inputState;   // a pointer, not a local copy
    private GameInputRouter gameInputRouter;
    
    private void Start()
    {
        if (gameInputRouter == null)
        {
            gameInputRouter = GameInputRouter.Instance;
            if (gameInputRouter == null)
            {
                Debug.LogError("[PlayerDecisionModule] No GameInputRouter in scene.", this);
                enabled = false;
                return;
            }
        }

        if (gameInputRouter.InputState == null)
        {
            Debug.LogError($"[PlayerInputStateDebugger] gameInputRouter.InputState is null.", this);
        }
    }

    private void Update()
    {
        if (!enableDebugLogging)
            return;

        if (Time.frameCount % 500 == 0)
            Debug.Log($"[PlayerInputStateDebugger] monitor running... {Time.frameCount}");

        if (gameInputRouter.InputState == null)
        {
            Debug.LogWarning("[PlayerInputStateDebugger] InputState is null on NewInputAdapter.", this);
            return;
        }

        if (gameInputRouter.InputState.moveAxis != new Vector2(0f, 0f))
            Debug.Log($"Move={gameInputRouter.InputState.moveAxis}");
        if (gameInputRouter.InputState.strafeAxis != 0f)
            Debug.Log($"Strafe={gameInputRouter.InputState.strafeAxis}");
        if (gameInputRouter.InputState.zoomDelta != 0f)
            Debug.Log($"Zoom={gameInputRouter.InputState.zoomDelta:F2}");
        if (gameInputRouter.InputState.cameraViewSelect != CameraModes.Unchanged)
            Debug.Log($"Camera={gameInputRouter.InputState.cameraViewSelect}");
        if (gameInputRouter.InputState.barkPressed)
            Debug.Log($"Bark");
        if (gameInputRouter.InputState.markTerritoryPressed)
            Debug.Log($"MarkTerritory");
        if (gameInputRouter.InputState.digPressed)
            Debug.Log($"Dig");
        if (gameInputRouter.InputState.changeFormationPressed)
            Debug.Log($"Formation Change");
        if (gameInputRouter.InputState.interactPressed)
            Debug.Log($"Interract");
        if (gameInputRouter.InputState.selectObjectPressed)
            Debug.Log($"SelectObject location={gameInputRouter.InputState.clickTargetLocationWorld}");
        if (gameInputRouter.InputState.hasClickTargetWorldObject)
            Debug.Log($"SelectObject name={gameInputRouter.InputState.clickTargetWorldObject.DisplayName}");

        if (gameInputRouter.InputState.anyKeyOrButtonDown)
            Debug.Log($"AnyKey");
        
        if (gameInputRouter.InputState.requestedPlayerAgentDelta != 0)
        {
            Debug.Log($"PlayerAgentDelta={gameInputRouter.InputState.requestedPlayerAgentDelta}");
            // Note: Index below is just an example of potential usage.  Only use Delta.
            Debug.Log($"PlayerAgentIndex={gameInputRouter.InputState.requestedPlayerAgentIndex}");
        }
    }
}
