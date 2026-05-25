using DogGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class TopPulldown
{
    private void BuildTooltip(Transform parent, Transform searchRoot)
    {
        Transform existingTooltip = FindExistingUiElement(parent, searchRoot, "UpperRightTooltip");
        GameObject tooltipObject;
        bool createdTooltip = existingTooltip == null;
        if (createdTooltip)
        {
            tooltipObject = new GameObject(
                "UpperRightTooltip",
                typeof(RectTransform),
                typeof(Image));
            tooltipObject.transform.SetParent(parent, false);
        }
        else
        {
            tooltipObject = existingTooltip.gameObject;
        }

        tooltipRect = GetOrAddComponent<RectTransform>(tooltipObject);
        tooltipRect.anchorMin = new Vector2(0f, 1f);
        tooltipRect.anchorMax = new Vector2(0f, 1f);
        tooltipRect.pivot = new Vector2(0f, 1f);
        tooltipRect.sizeDelta = new Vector2(160f, 52f);

        tooltipBackgroundImage = GetOrAddComponent<Image>(tooltipObject);
        tooltipBackgroundImage.color = tooltipBackgroundColor;
        tooltipBackgroundImage.raycastTarget = false;

        Transform existingLabel = tooltipObject.transform.Find("Label");
        GameObject labelObject;
        if (existingLabel == null)
        {
            labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(tooltipObject.transform, false);
        }
        else
        {
            labelObject = existingLabel.gameObject;
        }

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(tooltipPadding.x, tooltipPadding.y);
        labelRect.offsetMax = new Vector2(-tooltipPadding.x, -tooltipPadding.y);

        tooltipLabel = GetOrAddComponent<TextMeshProUGUI>(labelObject);
        tooltipLabel.fontSize = tooltipFontSize;
        tooltipLabel.color = dropdownTextColor;
        tooltipLabel.alignment = TextAlignmentOptions.Center;
        tooltipLabel.textWrappingMode = TextWrappingModes.NoWrap;
        tooltipLabel.overflowMode = TextOverflowModes.Overflow;
        tooltipLabel.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            tooltipLabel.font = TMP_Settings.defaultFontAsset;

        tooltipObject.SetActive(false);
    }

    private void ConfigureTooltip(GameObject target, System.Func<string> textProvider)
    {
        if (target == null)
            return;

        TopPulldownTooltipTrigger trigger = GetOrAddComponent<TopPulldownTooltipTrigger>(target);
        trigger.Initialize(this, textProvider);
    }

    private string GetSimulationButtonTooltipText()
    {
        return GamePause.IsPaused ? "Play" : "Pause";
    }

    private void RefreshActiveTooltipText()
    {
        if (activeTooltipTrigger == null || tooltipRect == null || !tooltipRect.gameObject.activeSelf)
            return;

        string text = activeTooltipTrigger.GetTooltipText();
        if (string.IsNullOrWhiteSpace(text))
            HideTooltip();
        else
            UpdateTooltipText(text);
    }

    internal void ShowTooltip(TopPulldownTooltipTrigger trigger, Vector2 screenPosition)
    {
        if (trigger == null || tooltipRect == null)
            return;

        string text = trigger.GetTooltipText();
        if (string.IsNullOrWhiteSpace(text))
            return;

        activeTooltipTrigger = trigger;
        tooltipRect.gameObject.SetActive(true);
        UpdateTooltipText(text);
        PositionTooltip(screenPosition);
        tooltipRect.SetAsLastSibling();
    }

    internal void MoveTooltip(TopPulldownTooltipTrigger trigger, Vector2 screenPosition)
    {
        if (trigger == null || trigger != activeTooltipTrigger || tooltipRect == null || !tooltipRect.gameObject.activeSelf)
            return;

        PositionTooltip(screenPosition);
    }

    internal void HideTooltip(TopPulldownTooltipTrigger trigger)
    {
        if (trigger != null && trigger != activeTooltipTrigger)
            return;

        HideTooltip();
    }

    private void HideTooltip()
    {
        activeTooltipTrigger = null;
        if (tooltipRect != null)
            tooltipRect.gameObject.SetActive(false);
    }

    private void UpdateTooltipText(string text)
    {
        if (tooltipLabel == null || tooltipRect == null)
            return;

        tooltipLabel.text = text;
        Vector2 preferred = tooltipLabel.GetPreferredValues(text, tooltipMaxWidth, 0f);
        float width = Mathf.Min(tooltipMaxWidth, preferred.x) + tooltipPadding.x * 2f;
        float height = preferred.y + tooltipPadding.y * 2f;
        tooltipRect.sizeDelta = new Vector2(Mathf.Max(80f, width), Mathf.Max(42f, height));
    }

    private void PositionTooltip(Vector2 screenPosition)
    {
        if (tooltipRect == null || overlayCanvas == null)
            return;

        RectTransform canvasRect = overlayCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Camera eventCamera = overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : overlayCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                eventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        float canvasScale = overlayCanvas.scaleFactor > 0f ? overlayCanvas.scaleFactor : 1f;
        Vector2 scaledOffset = tooltipScreenOffset / canvasScale;

        Vector2 anchoredPosition = new Vector2(
            localPoint.x + (canvasRect.rect.width * 0.5f),
            localPoint.y - (canvasRect.rect.height * 0.5f));
        anchoredPosition += scaledOffset;

        float minX = 12f;
        float maxX = canvasRect.rect.width - tooltipRect.sizeDelta.x - 12f;
        float minY = -(canvasRect.rect.height - tooltipRect.sizeDelta.y - 12f);
        float maxY = -12f;

        anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, Mathf.Max(minX, maxX));
        anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, Mathf.Min(minY, maxY), Mathf.Max(minY, maxY));
        tooltipRect.anchoredPosition = anchoredPosition;
    }
}
