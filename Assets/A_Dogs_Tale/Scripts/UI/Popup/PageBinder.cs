using UnityEngine;
using UnityEngine.UIElements;

public class PageBinder : MonoBehaviour
{
    public PageGameController gameController;
    public PagePackController packController;
    public PageInventoryController inventoryController;
    public PageSettingsController settingsController;

    public void TryBind(string tab, VisualElement pageRoot)
    {
        switch (tab)
        {
            case "Game":      if (gameController != null)      gameController.Bind(pageRoot); break;
            case "Pack":      if (packController != null)      packController.Bind(pageRoot); break;
            case "Inventory": if (inventoryController != null) inventoryController.Bind(pageRoot); break;
            case "Settings":  if (settingsController != null)  settingsController.Bind(pageRoot); break;
        }
    }
}