using UnityEngine;
using UnityEngine.InputSystem;

public class ScentGUI : MonoBehaviour
{
    [Header("External object references")]
    private Dir dir;
    public SniffModeVisuals sniffVisuals;

    private InputAction sniffAction;
    private bool isSniffModeActive;

    private void Awake()
    {
        sniffAction = new InputAction(
            name: "Sniff",
            type: InputActionType.Button,
            binding: "<Keyboard>/f"
        );
    }

    private void OnEnable()
    {
        sniffAction.Enable();
        sniffAction.performed += OnSniffToggle;
    }

    private void OnDisable()
    {
        sniffAction.performed -= OnSniffToggle;
        sniffAction.Disable();
    }

    private void OnSniffToggle(InputAction.CallbackContext ctx)
    {
        isSniffModeActive = !isSniffModeActive;

        sniffVisuals.SetSniffMode(isSniffModeActive);

        if (dir == null)
            dir = Dir.Instance;

        if (dir.scentRegistry == null)
        {
            Debug.LogError("ScentGUI: scentRegistry is null!");
            return;
        }

        if (isSniffModeActive)
        {
            dir.scentRegistry.ActivateScentOverlay();
        }
        else
        {
            dir.scentRegistry.DeactivateScentOverlay(); // ← add this if you don’t already have it
        }
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