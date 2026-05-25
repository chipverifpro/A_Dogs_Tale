using UnityEngine;
using UnityEngine.UI;

public partial class TopPulldown
{
    private void BuildTargetButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "TargetButton");
        if (existingButton == null)
            existingButton = FindExistingUiElement(parent, searchRoot, "ScentTargetButton");

        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "TargetButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        buttonObject.name = "TargetButton";

        targetButtonRect = buttonObject.GetComponent<RectTransform>();
        if (createdButton)
        {
            targetButtonRect.anchorMin = new Vector2(1f, 1f);
            targetButtonRect.anchorMax = new Vector2(1f, 1f);
            targetButtonRect.pivot = new Vector2(1f, 1f);
            targetButtonRect.anchoredPosition = new Vector2(-topControlButtonMargin, -topControlButtonMargin);
            targetButtonRect.sizeDelta = new Vector2(topControlButtonSize, topControlButtonSize);
        }
        ConfigureTopControlRect(targetButtonRect, 0);

        targetButtonImage = GetOrAddComponent<Image>(buttonObject);
        targetButtonImage.color = topControlButtonColor;
        targetButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = targetButtonImage;
        button.onClick.RemoveListener(ToggleDropdown);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(ToggleDropdown);

        Transform existingPreview = buttonObject.transform.Find("AgentPreview");
        GameObject previewObject;
        if (existingPreview == null)
        {
            previewObject = new GameObject("AgentPreview", typeof(RectTransform), typeof(RawImage));
            previewObject.transform.SetParent(buttonObject.transform, false);
        }
        else
        {
            previewObject = existingPreview.gameObject;
        }

        RectTransform previewRect = previewObject.GetComponent<RectTransform>();
        ConfigureTopControlIconRect(previewRect, 0.82f);

        targetPreviewImage = GetOrAddComponent<RawImage>(previewObject);
        targetPreviewImage.color = Color.white;
        targetPreviewImage.raycastTarget = false;
        previewObject.transform.SetAsFirstSibling();

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
            iconRect.sizeDelta = Vector2.one * (topControlButtonSize * 0.68f);
            iconRect.anchoredPosition = Vector2.zero;
        }
        ConfigureTopControlIconRect(iconRect, 0.68f);

        targetButtonIconImage = GetOrAddComponent<Image>(iconObject);
        targetButtonIconImage.sprite = GetTargetCrosshairSprite();
        targetButtonIconImage.preserveAspect = true;
        targetButtonIconImage.color = Color.white;
        targetButtonIconImage.raycastTarget = false;
        targetCrosshairImage = targetButtonIconImage;
        iconObject.transform.SetAsLastSibling();

        RefreshTargetButtonPreview(force: true);
        ConfigureTooltip(buttonObject, GetTargetButtonTooltipText);
    }

    private void RefreshTargetButtonSelectionState()
    {
        if (targetButtonImage == null)
            return;

        ScentSource selectedSource = EnsureDir() && dir.scentRegistry != null
            ? dir.scentRegistry.SelectedTargetScent
            : null;

        if (selectedSource == null)
        {
            targetButtonImage.color = topControlButtonColor;
            return;
        }

        Color accent = GetScentColor(selectedSource);
        accent.a = 0.94f;
        targetButtonImage.color = accent;
    }

    private Sprite GetScentIconSprite()
    {
        return SpriteServer.SpriteLookup("Sense_Smell_None")
            ?? SpriteServer.SpriteLookup("Sense_Smell_Low")
            ?? SpriteServer.SpriteLookup("Sense_Alert_None");
    }

    private Sprite GetTargetCrosshairSprite()
    {
        return SpriteServer.SpriteSheetLookup(targetIconSpriteResourcePath, 0)
            ?? SpriteServer.SpriteLookup("TargetIcon_D_0");
    }

    private string GetTargetButtonTooltipText()
    {
        ScentSource selectedSource = GetSelectedTargetScent();
        if (selectedSource == null)
            return "Target";

        WorldObject targetObject = ResolveScentSourceWorldObject(selectedSource);
        string targetName = targetObject != null && !string.IsNullOrWhiteSpace(targetObject.DisplayName)
            ? targetObject.DisplayName.Trim()
            : GetScentDisplayName(selectedSource);

        return string.IsNullOrWhiteSpace(targetName)
            ? "Target"
            : $"Target: {targetName}";
    }

    private static string GetScentDisplayName(ScentSource scentSource)
    {
        if (scentSource == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(scentSource.scentName)
            ? scentSource.scentName.Trim()
            : scentSource.category.ToString();
    }
}
