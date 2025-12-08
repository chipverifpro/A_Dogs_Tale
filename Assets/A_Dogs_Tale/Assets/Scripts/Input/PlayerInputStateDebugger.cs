using UnityEngine;
using DogGame.Modules;

public class PlayerInputStateDebugger : MonoBehaviour
{
    //[SerializeField] private NewInputAdapter inputAdapter;
    public bool enableDebugLogging = false;
    //[SerializeField] private PlayerInputState inputState;   // a pointer, not a local copy
    private GameInputRouter inputRouter;
    
    private void Start()
    {
        if (inputRouter == null)
        {
            inputRouter = GameInputRouter.Instance;
            if (inputRouter == null)
            {
                Debug.LogError("[PlayerDecisionModule] No GameInputRouter in scene.", this);
                enabled = false;
                return;
            }
        }

        if (inputRouter.InputState == null)
        {
            Debug.LogError($"[PlayerInputStateDebugger] inputRouter.InputState is null.", this);
        }
    }

    private void Update()
    {
        if (!enableDebugLogging)
            return;

        if (Time.frameCount % 500 == 0)
            Debug.Log($"[PlayerInputStateDebugger] monitor running... {Time.frameCount}");

        if (inputRouter.InputState == null)
        {
            Debug.LogWarning("[PlayerInputStateDebugger] InputState is null on NewInputAdapter.", this);
            return;
        }

        if (inputRouter.InputState.moveAxis != new Vector2(0f, 0f))
            Debug.Log($"Move={inputRouter.InputState.moveAxis}");
        if (inputRouter.InputState.zoomDelta != 0f)
            Debug.Log($"Zoom={inputRouter.InputState.zoomDelta:F2}");
        if (inputRouter.InputState.cameraViewSelect != CameraModes.Unchanged)
            Debug.Log($"Camera={inputRouter.InputState.cameraViewSelect}");
        if (inputRouter.InputState.barkPressed)
            Debug.Log($"Bark");
        if (inputRouter.InputState.markTerritoryPressed)
            Debug.Log($"MarkTerritory");
        if (inputRouter.InputState.changeFormationPressed)
            Debug.Log($"Formation Change");
        if (inputRouter.InputState.interactPressed)
            Debug.Log($"Interract");
        if (inputRouter.InputState.selectObjectPressed)
            Debug.Log($"SelectObject location={inputRouter.InputState.clickTargetLocationWorld}");
        if (inputRouter.InputState.hasClickTargetWorldObject)
            Debug.Log($"SelectObject name={inputRouter.InputState.clickTargetWorldObject.DisplayName}");

        if (inputRouter.InputState.anyKeyOrButtonDown)
            Debug.Log($"AnyKey");
        
        if (inputRouter.InputState.requestedPlayerAgentDelta != 0)
        {
            Debug.Log($"PlayerAgentDelta={inputRouter.InputState.requestedPlayerAgentDelta}");
            // Note: Index below is just an example of potential usage.  Only use Delta.
            Debug.Log($"PlayerAgentIndex={inputRouter.InputState.requestedPlayerAgentIndex}");
        }
    }
}