using UnityEngine;

public class PlayerInputStateDebugger : MonoBehaviour
{
    [SerializeField] private NewInputAdapter inputAdapter;
    public bool enableDebugLogging = false;

    private void Awake()
    {
        if (inputAdapter == null)
        {
            inputAdapter = FindFirstObjectByType<NewInputAdapter>();
            if (inputAdapter == null)
            {
                Debug.LogWarning("[PlayerInputStateDebugger] No NewInputAdapter found in scene.", this);
            }
        }
    }

    private void Update()
    {
        if (!enableDebugLogging || inputAdapter == null)
            return;

        if (Time.frameCount % 15 != 0)
            return;

        var state = inputAdapter.InputState;
        if (state == null)
        {
            Debug.LogWarning("[PlayerInputStateDebugger] InputState is null on NewInputAdapter.", this);
            return;
        }

        Debug.Log(
            $"[PlayerInputState] " +
            $"Move={state.moveAxis} " +
            $"Zoom={state.zoomDelta:F2} " +
            $"MarkTerritory={state.markTerritoryPressed} " +
            $"BarkDown={state.barkPressed}",
            this);
    }
}