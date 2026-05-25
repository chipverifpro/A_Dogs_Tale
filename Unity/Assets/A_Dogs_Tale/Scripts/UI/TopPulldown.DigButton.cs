using UnityEngine;
using UnityEngine.UI;

public partial class TopPulldown
{
    private void BuildDigButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "DigButton");
        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "DigButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        digButtonRect = buttonObject.GetComponent<RectTransform>();
        digButtonRect.anchorMin = new Vector2(1f, 1f);
        digButtonRect.anchorMax = new Vector2(1f, 1f);
        digButtonRect.pivot = new Vector2(1f, 1f);
        digButtonRect.anchoredPosition = new Vector2(
            -(topControlButtonMargin + ((topControlButtonSize + modeButtonSpacing) * 6f)),
            -topControlButtonMargin);
        digButtonRect.sizeDelta = new Vector2(topControlButtonSize, topControlButtonSize);
        ConfigureTopControlRect(digButtonRect, 6);

        digButtonImage = GetOrAddComponent<Image>(buttonObject);
        digButtonImage.color = topControlButtonColor;
        digButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = digButtonImage;
        button.onClick.RemoveListener(HandleDigButtonPressed);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(HandleDigButtonPressed);

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
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;

        digIconImage = GetOrAddComponent<Image>(iconObject);
        digIconImage.sprite = GetDigHoleButtonSprite();
        digIconImage.preserveAspect = true;
        digIconImage.color = Color.white;
        SetDigIconSize(iconRect, digIconImage.sprite);

        ConfigureTooltip(buttonObject, () => "Dig");
    }

    private void SetDigIconSize(RectTransform iconRect, Sprite sprite)
    {
        if (iconRect == null)
            return;

        float iconWidth = topControlButtonSize * 0.72f;
        float aspectRatio = sprite != null && sprite.rect.width > 0f
            ? sprite.rect.height / sprite.rect.width
            : 1f;
        float iconHeight = Mathf.Min(iconWidth * aspectRatio, topControlButtonSize * 0.9f);
        iconRect.sizeDelta = new Vector2(iconWidth, iconHeight);
    }

    private void HandleDigButtonPressed()
    {
        CloseDropdown();
        CloseModePanel();
        CloseSpeedPanel();
        CloseEmoteDropdown();

        WorldObject controlledObject = GetCurrentControlledWorldObject();
        if (controlledObject == null)
        {
            Debug.LogWarning("TopPulldown: no controlled WorldObject available for digging.", this);
            BottomBanner.Show("No dog is selected to dig.");
            return;
        }

        TerrainDigService.TryDigAt(controlledObject);
    }

    private Sprite GetDigHoleButtonSprite()
    {
        return SpriteServer.SpriteSheetLookup(digHoleSpriteResourcePath, 0);
    }
}
