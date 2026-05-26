using UnityEngine;
using UnityEngine.UI;

public partial class TopPulldown
{
    private void BuildInventoryButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "InventoryButton");
        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "InventoryButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        inventoryButtonRect = buttonObject.GetComponent<RectTransform>();
        if (createdButton)
        {
            inventoryButtonRect.anchorMin = new Vector2(1f, 1f);
            inventoryButtonRect.anchorMax = new Vector2(1f, 1f);
            inventoryButtonRect.pivot = new Vector2(1f, 1f);
            inventoryButtonRect.anchoredPosition = new Vector2(
                -(topControlButtonMargin + ((topControlButtonSize + modeButtonSpacing) * 5f)),
                -topControlButtonMargin);
            inventoryButtonRect.sizeDelta = new Vector2(topControlButtonSize, topControlButtonSize);
        }
        ConfigureTopControlRect(inventoryButtonRect, 5);

        inventoryButtonImage = GetOrAddComponent<Image>(buttonObject);
        inventoryButtonImage.color = topControlButtonColor;
        inventoryButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = inventoryButtonImage;
        button.onClick.RemoveListener(ToggleInventoryDialog);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(ToggleInventoryDialog);

        Transform existingIcon = buttonObject.transform.Find("Icon");
        GameObject iconObject;
        bool createdIcon = existingIcon == null;
        if (createdIcon)
        {
            iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
        }
        else
        {
            iconObject = existingIcon.gameObject;
        }

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        if (createdIcon)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = Vector2.one * (topControlButtonSize * 0.72f);
            iconRect.anchoredPosition = Vector2.zero;
        }
        ConfigureTopControlIconRect(iconRect, 0.72f);

        inventoryIconImage = GetOrAddComponent<Image>(iconObject);
        inventoryIconImage.sprite = GetInventoryButtonSprite();
        inventoryIconImage.preserveAspect = true;
        inventoryIconImage.color = Color.white;

        ConfigureTooltip(buttonObject, () => "Inventory");
    }

    private void ToggleInventoryDialog()
    {
        CloseDropdown();
        CloseModePanel();
        CloseSpeedPanel();
        CloseEmoteDropdown();

        InventoryDialogUI inventoryDialog = FindFirstObjectByType<InventoryDialogUI>();
        if (inventoryDialog == null)
        {
            GameObject inventoryDialogObject = new GameObject("InventoryDialogUI");
            inventoryDialog = inventoryDialogObject.AddComponent<InventoryDialogUI>();
        }

        inventoryDialog.Toggle();
    }

    private Sprite GetInventoryButtonSprite()
    {
        return SpriteServer.SpriteSheetLookup(inventoryActionSpriteResourcePath, 2);
    }
}
