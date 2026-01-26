using UnityEngine;
using UnityEngine.InputSystem;

public class ScentGUI : MonoBehaviour
{
    [Header("External object references")]
    private Directory dir;
    public SniffModeVisuals sniffVisuals;

    private InputAction sniffAction;

    private void Awake()
    {
        // Create an input action bound to keyboard F
        sniffAction = new InputAction(
            name: "Sniff",
            type: InputActionType.Button,
            binding: "<Keyboard>/f"
        );
    }

    private void OnEnable()
    {
        sniffAction.Enable();
        sniffAction.started += OnSniffStarted;
        sniffAction.canceled += OnSniffCanceled;
    }

    private void OnDisable()
    {
        sniffAction.started -= OnSniffStarted;
        sniffAction.canceled -= OnSniffCanceled;
        sniffAction.Disable();
    }

    private void OnSniffStarted(InputAction.CallbackContext ctx)
    {
        sniffVisuals.SetSniffMode(true);

        if (dir == null)
            dir = Directory.Instance;

        if (dir.scentRegistry == null)
        {
            Debug.LogError("ScentGUI: scentRegistry is null!");
            return;
        }

        dir.scentRegistry.ActivateScentOverlay();
    }

    private void OnSniffCanceled(InputAction.CallbackContext ctx)
    {
        sniffVisuals.SetSniffMode(false);
        // hide sniff UI / overlay if needed
    }

    // Called by other systems (unchanged)
    public void OnSniff(Cell currentCell)
    {
        var detections = dir.scentRegistry.CollectScentsAtCell(currentCell, dir.scents);
        // bind to UI
    }

    public void OnScentClicked(ScentDetection detection)
    {
        dir.scentRegistry.ActivateScentOverlay(detection.scentSource);
    }
}