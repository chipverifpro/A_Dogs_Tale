using UnityEngine;

public class ScentGUI : MonoBehaviour
{
    [Header("External object references")]
    ObjectDirectory dir;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
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
        dir.scentRegistry.ActivateScentOverlay(detection.source, dir.scents);
    }
}
