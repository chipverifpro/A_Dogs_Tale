using UnityEngine;

public class ScentGUI : MonoBehaviour
{
    [Header("External object references")]
    Directory dir;
    public SniffModeVisuals sniffVisuals;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            sniffVisuals.SetSniffMode(true);
            if (dir == null)
            {
                dir = Directory.Instance;
            }
            if (dir.scentRegistry == null)
            {
                Debug.LogError("ScentGUI: scentRegistry is null!");
                return;
            }
            dir.scentRegistry.ActivateScentOverlay();
            // trigger your sniff UI & scent detection
        }
        else if (Input.GetKeyUp(KeyCode.F))
        {
            sniffVisuals.SetSniffMode(false);
            // hide sniff UI
        }
    }
    // In some UI controller:
    public void OnSniff(Cell currentCell)
    {
        var detections = dir.scentRegistry.CollectScentsAtCell(currentCell, dir.scents);

        // Bind to UI list
        //scentListUI.SetItems(detections);
    }

    // Called when the player clicks a scent in the sniff list UI:
    public void OnScentClicked(ScentDetection detection)
    {
        dir.scentRegistry.ActivateScentOverlay(detection.scentSource);
    }
}
