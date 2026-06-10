using UnityEngine;
using UnityEngine.InputSystem;

public partial class TopPulldown
{
    public static void ToggleDogSight(string triggerSource = "DogSight")
    {
        TopPulldown pulldown = FindFirstObjectByType<TopPulldown>(FindObjectsInactive.Include);
        if (pulldown == null)
        {
            Debug.LogWarning("TopPulldown: cannot toggle Dog Sight because no TopPulldown instance was found.");
            return;
        }

        pulldown.ToggleSniffMode(triggerSource);
    }

    private void EnsureSniffAction()
    {
        if (sniffAction != null)
            return;

        sniffAction = new InputAction(
            name: "Sniff",
            type: InputActionType.Button,
            binding: "<Keyboard>/f"
        );
    }

    private void OnSniffToggle(InputAction.CallbackContext ctx)
    {
        ToggleSniffMode("InputAction");
    }

    private void ToggleSniffMode(string triggerSource)
    {
        if (lastSniffToggleFrame == Time.frameCount)
            return;

        lastSniffToggleFrame = Time.frameCount;
        isSniffModeActive = !isSniffModeActive;

        EnsureSniffVisuals();

        if (sniffVisuals != null)
        {
            sniffVisuals.SetSniffMode(isSniffModeActive);
        }
        else if (logSniffToggleDiagnostics)
        {
            Debug.LogWarning("TopPulldown: sniffVisuals is not assigned and no SniffModeVisuals component was found.", this);
        }

        if (!EnsureDir() || dir.scentRegistry == null)
        {
            Debug.LogError("TopPulldown: scentRegistry is null!");
            LogSniffDiagnostic(triggerSource, "missing Dir or scentRegistry");
            return;
        }

        if (isSniffModeActive)
        {
            dir.scentRegistry.ActivateScentOverlay(dir.scentRegistry.SelectedTargetScent);
        }
        else
        {
            dir.scentRegistry.DeactivateScentOverlay();
            CloseDropdown();
        }

        LogSniffDiagnostic(triggerSource, isSniffModeActive ? "activated" : "deactivated");
    }

    private void LogSniffDiagnostic(string triggerSource, string result)
    {
        if (dir == null)
        {
            lastSniffDiagnostic = $"Sniff {triggerSource}: {result}; Dir=null";
        }
        else
        {
            ScentAirGround scentSystem = dir.scents != null ? dir.scents : dir.scentAirGround;
            Camera scentCamera = dir.scentCam;
            ScentSource selected = dir.scentRegistry != null ? dir.scentRegistry.SelectedTargetScent : null;

            string selectedDescription = DescribeScentSource(selected);
            string visualDescription = sniffVisuals != null ? $"sniffVisuals={sniffVisuals.name}" : "sniffVisuals=null";
            string cameraDescription = scentCamera != null
                ? $"{scentCamera.name}.enabled={scentCamera.enabled}"
                : "FogCamera=null";
            string scentSystemDescription = scentSystem != null
                ? $"currentAgentId={scentSystem.currentAgentId}, active={scentSystem.IsScentCameraActive}"
                : "ScentAirGround=null";

            lastSniffDiagnostic =
                $"Sniff {triggerSource}: {result}; mode={isSniffModeActive}; selected={selectedDescription}; {visualDescription}; {cameraDescription}; {scentSystemDescription}";
        }

        if (logSniffToggleDiagnostics)
            Debug.Log($"[TopPulldown] {lastSniffDiagnostic}", this);
    }

    private static string DescribeScentSource(ScentSource scentSource)
    {
        if (scentSource == null)
            return "null";

        string scentName = string.IsNullOrWhiteSpace(scentSource.scentName) ? scentSource.category.ToString() : scentSource.scentName;
        return $"{scentName}(agentId={scentSource.agentId})";
    }

    private void EnsureSniffVisuals()
    {
        if (sniffVisuals != null)
            return;

        sniffVisuals = GetComponent<SniffModeVisuals>();
        if (sniffVisuals != null)
            return;

        sniffVisuals = FindFirstObjectByType<SniffModeVisuals>(FindObjectsInactive.Include);
    }

    // Called by other systems (unchanged)
    public void OnSniff(Cell currentCell)
    {
        if (!EnsureDir() || dir.scentRegistry == null || dir.scents == null)
            return;

        var detections = dir.scentRegistry.CollectScentsAtCell(currentCell, dir.scents);
        // bind to UI
    }

    public void OnScentClicked(ScentDetection detection)
    {
        if (!EnsureDir() || dir.scentRegistry == null)
            return;

        ScentSource selectedSource = dir.scentRegistry.SetSelectedTargetScent(detection.scentSource);
        dir.scentRegistry.ActivateScentOverlay(selectedSource);
        RefreshTargetButtonSelectionState();
    }
}
