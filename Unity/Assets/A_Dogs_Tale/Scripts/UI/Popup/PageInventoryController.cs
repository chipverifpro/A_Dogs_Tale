using UnityEngine;
using UnityEngine.UIElements;

public class PageInventoryController : MonoBehaviour
{
    public void Bind(VisualElement root)
    {
        var grid = root.Q<VisualElement>("InventoryGrid");
        if (grid != null)
        {
            for (int i = 0; i < 16; i++)
            {
                var card = new VisualElement();
                card.AddToClassList("inventory-item");
                card.style.width = 120;
                card.style.height = 120;
                card.Add(new Label("Item " + (i+1)));
                grid.Add(card);
            }
        }
    }
}