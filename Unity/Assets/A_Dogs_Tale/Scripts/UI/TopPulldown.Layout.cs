using UnityEngine;

public partial class TopPulldown
{
    private Vector2 GetTopControlPosition(int slotFromRight)
    {
        float scale = GetTopControlsFitScale();
        if (UseTwoRowTopControls())
        {
            int indexFromLeft = Mathf.Clamp(TopControlButtonCount - 1 - slotFromRight, 0, TopControlButtonCount - 1);
            int row = indexFromLeft / TopControlColumnsWhenTwoRows;
            int column = indexFromLeft % TopControlColumnsWhenTwoRows;
            float frameWidth = GetPulldownFrameSizeForCurrentButtonSize().x * scale;
            float leftEdge = (pulldownFrameOffset.x * scale) - (frameWidth * 0.5f);
            float x = leftEdge +
                      (topControlsInset.x * scale) +
                      ((topControlButtonSize + modeButtonSpacing) * column * scale) +
                      (topControlButtonSize * scale);
            float y = -(((topControlsInset.y + ((topControlButtonSize + modeButtonSpacing) * row)) * scale) + GetTopSafeAreaInset());
            return new Vector2(x, y);
        }

        return new Vector2(
            GetTopControlsFrameRightEdge(scale) - GetTopControlRightInset(slotFromRight, scale),
            -((topControlsInset.y * scale) + GetTopSafeAreaInset()));
    }

    private Vector2 GetTopPanelPosition(int slotFromRight)
    {
        float scale = GetTopControlsFitScale();
        Vector2 buttonPosition = GetTopControlPosition(slotFromRight);
        int row = UseTwoRowTopControls()
            ? Mathf.Clamp(TopControlButtonCount - 1 - slotFromRight, 0, TopControlButtonCount - 1) / TopControlColumnsWhenTwoRows
            : 0;
        float y = -(((topControlsInset.y +
                      (topControlButtonSize * (row + 1)) +
                      (modeButtonSpacing * row)) * scale) + GetTopSafeAreaInset() + 12f);
        return new Vector2(buttonPosition.x, y);
    }

    private float GetTopControlsFrameRightEdge(float scale)
    {
        return (pulldownFrameOffset.x * scale) + ((GetPulldownFrameSizeForCurrentButtonSize().x * scale) * 0.5f);
    }

    private float GetTopControlRightInset(int slotFromRight, float scale)
    {
        float scaledButtonSize = topControlButtonSize * scale;
        float scaledButtonSpacing = modeButtonSpacing * scale;
        return (topControlsInset.x * scale) + ((scaledButtonSize + scaledButtonSpacing) * slotFromRight);
    }

    private Vector2 GetPulldownFrameShownPosition()
    {
        return new Vector2(pulldownFrameOffset.x * GetTopControlsFitScale(), GetPulldownFrameShownY());
    }

    private float GetPulldownFrameShownY()
    {
        return (pulldownFrameOffset.y * GetTopControlsFitScale()) - GetTopSafeAreaInset();
    }

    private Vector2 GetPulldownTabPosition()
    {
        float scale = GetTopControlsFitScale();
        return new Vector2(pulldownTabOffset.x * scale, (pulldownTabOffset.y * scale) - GetTopSafeAreaInset());
    }

    private float GetTopSafeAreaInset()
    {
        if (!respectTopSafeArea || overlayCanvas == null || Screen.height <= 0)
            return 0f;

        float topInsetPixels = Mathf.Max(0f, Screen.height - Screen.safeArea.yMax);
        if (topInsetPixels <= 0f)
            return 0f;

        RectTransform canvasRect = overlayCanvas.transform as RectTransform;
        float canvasHeight = canvasRect != null && canvasRect.rect.height > 0f
            ? canvasRect.rect.height
            : Screen.height;

        return topInsetPixels * (canvasHeight / Screen.height);
    }

    private float GetTopControlsFitScale()
    {
        if (!scaleTopControlsToFitWidth)
            return 1f;

        float canvasWidth = GetCanvasWidth();

        float frameWidth = GetPulldownFrameSizeForCurrentButtonSize().x;
        if (canvasWidth <= 0f || frameWidth <= 0f)
            return 1f;

        return Mathf.Min(1f, canvasWidth / frameWidth);
    }

    private Vector2 GetPulldownFrameSizeForCurrentButtonSize()
    {
        return UseTwoRowTopControls()
            ? GetPulldownFrameSize(columns: TopControlColumnsWhenTwoRows, rows: 2)
            : GetPulldownFrameSize(columns: TopControlButtonCount, rows: 1);
    }

    private Vector2 GetSingleRowPulldownFrameSizeForCurrentButtonSize()
    {
        return GetPulldownFrameSize(columns: TopControlButtonCount, rows: 1);
    }

    private Vector2 GetPulldownFrameSize(int columns, int rows)
    {
        float buttonSize = Mathf.Max(1f, topControlButtonSize);
        float spacing = Mathf.Max(0f, modeButtonSpacing);
        int clampedColumns = Mathf.Max(1, columns);
        int clampedRows = Mathf.Max(1, rows);
        float width = (topControlsInset.x * 2f) +
                      (buttonSize * clampedColumns) +
                      (spacing * Mathf.Max(0, clampedColumns - 1));
        float bottomPadding = Mathf.Max(0f, pulldownFrameSize.y - topControlsInset.y - PersistentGameSettings.DefaultButtonSize);
        float height = topControlsInset.y +
                       (buttonSize * clampedRows) +
                       (spacing * Mathf.Max(0, clampedRows - 1)) +
                       bottomPadding;
        return new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
    }

    private bool UseTwoRowTopControls()
    {
        if (!scaleTopControlsToFitWidth)
            return false;

        float canvasWidth = GetCanvasWidth();
        if (canvasWidth <= 0f)
            return false;

        return GetSingleRowPulldownFrameSizeForCurrentButtonSize().x > canvasWidth;
    }

    private float GetCanvasWidth()
    {
        RectTransform canvasRect = overlayCanvas != null ? overlayCanvas.transform as RectTransform : null;
        return canvasRect != null && canvasRect.rect.width > 0f
            ? canvasRect.rect.width
            : Screen.width;
    }

    private void ConfigureTopControlRect(RectTransform rectTransform, int slotFromRight)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        rectTransform.localScale = Vector3.one * GetTopControlsFitScale();
        rectTransform.anchoredPosition = GetTopControlPosition(slotFromRight);
        rectTransform.sizeDelta = new Vector2(topControlButtonSize, topControlButtonSize);
    }

    private void ConfigureTopPanelRect(RectTransform rectTransform, int slotFromRight)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        rectTransform.anchoredPosition = GetTopPanelPositionForRect(rectTransform, slotFromRight);
    }

    private void ConfigureTopControlIconRect(RectTransform iconRect, float sizeScale)
    {
        if (iconRect == null)
            return;

        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.localScale = Vector3.one;
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = Vector2.one * (topControlButtonSize * sizeScale);
    }

    private void UpdateTopControlsAutoHide()
    {
        if (!uiBuilt)
            return;

        if (useCornerControls)
        {
            topControlsVisibility = 1f;
            topControlsSlideVelocity = 0f;
            ApplyCornerControlsLayout();
            if (IsAnyTopPanelOpen())
                BottomBanner.Collapse();
            return;
        }

        bool topPulldownExplicitlyOpen = pulldownOpenedByTab || IsAnyTopPanelOpen();
        if (topPulldownExplicitlyOpen)
            BottomBanner.Collapse();

        bool targetControlsVisible = !autoHideTopControls || topPulldownExplicitlyOpen;
        float targetVisibility = targetControlsVisible ? 1f : 0f;

        if (!autoHideTopControls)
        {
            topControlsVisibility = targetVisibility;
            topControlsSlideVelocity = 0f;
        }
        else
        {
            topControlsVisibility = Mathf.SmoothDamp(
                topControlsVisibility,
                targetVisibility,
                ref topControlsSlideVelocity,
                Mathf.Max(0.01f, topControlsSlideDuration),
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            if (Mathf.Abs(topControlsVisibility - targetVisibility) < 0.001f)
            {
                topControlsVisibility = targetVisibility;
                topControlsSlideVelocity = 0f;
            }
        }

        ApplyTopControlsSlidePosition();
        UpdatePulldownTabVisibility(targetControlsVisible);

        if (targetVisibility <= 0f && topControlsVisibility < 0.05f)
            HideTooltip();
    }

    private bool IsAnyTopPanelOpen()
    {
        return (dropdownRect != null && dropdownRect.gameObject.activeSelf) ||
               (modePanelRect != null && modePanelRect.gameObject.activeSelf) ||
               (speedPanelRect != null && speedPanelRect.gameObject.activeSelf) ||
               (emoteDropdownRect != null && emoteDropdownRect.gameObject.activeSelf);
    }

    private void ApplyTopControlsSlidePosition()
    {
        if (useCornerControls)
        {
            ApplyCornerControlsLayout();
            ApplyTopPanelPositions();
            return;
        }

        float scale = GetTopControlsFitScale();
        float shownY = -((topControlsInset.y * scale) + GetTopSafeAreaInset());
        int rowCount = UseTwoRowTopControls() ? 2 : 1;
        float hiddenY = (((topControlButtonSize * rowCount) + (modeButtonSpacing * Mathf.Max(0, rowCount - 1))) * scale) + topControlsHiddenTopPadding;
        float y = Mathf.Lerp(hiddenY, shownY, topControlsVisibility);
        float frameY = Mathf.Lerp(GetPulldownFrameHiddenY(), GetPulldownFrameShownY(), topControlsVisibility);

        ApplyTopControlsFitScale();
        ApplyPulldownFramePosition(frameY);
        ApplyPulldownTabPosition();
        ApplyTopControlPosition(targetButtonRect, 0, y);
        ApplyTopControlPosition(modeButtonRect, 1, y);
        ApplyTopControlPosition(speedButtonRect, 2, y);
        ApplyTopControlPosition(simulationButtonRect, 3, y);
        ApplyTopControlPosition(emoteButtonRect, 4, y);
        ApplyTopControlPosition(inventoryButtonRect, 5, y);
        ApplyTopControlPosition(digButtonRect, 6, y);
        ApplyTopControlPosition(questButtonRect, QuestButtonTopSlotFromRight, y);
        ApplyTopControlPosition(cameraModeButtonRect, CameraModeButtonTopSlotFromRight, y);
        ApplyTopControlPosition(homeButtonRect, HomeButtonTopSlotFromRight, y);
        ApplyTopPanelPositions();
    }

    private void ApplyTopControlsFitScale()
    {
        float scale = GetTopControlsFitScale();
        ApplyTopControlSizesForCurrentButtonSize();
        ApplyTopControlScale(pulldownFrameRect, scale);
        ApplyTopControlScale(pulldownTabRect, scale);
        ApplyTopControlScale(targetButtonRect, scale);
        ApplyTopControlScale(modeButtonRect, scale);
        ApplyTopControlScale(speedButtonRect, scale);
        ApplyTopControlScale(simulationButtonRect, scale);
        ApplyTopControlScale(emoteButtonRect, scale);
        ApplyTopControlScale(inventoryButtonRect, scale);
        ApplyTopControlScale(digButtonRect, scale);
        ApplyTopControlScale(questButtonRect, scale);
        ApplyTopControlScale(cameraModeButtonRect, scale);
        ApplyTopControlScale(homeButtonRect, scale);
    }

    private void RefreshPersistentButtonSizePreference(bool force = false)
    {
        if (!force && Time.unscaledTime < nextPersistentButtonSizeRefreshTime)
            return;

        nextPersistentButtonSizeRefreshTime = Time.unscaledTime + 0.25f;
        float savedButtonSize = PersistentGameSettings.SnapButtonSize(PersistentGameSettings.GetCurrentOrSaved().buttonSize);
        if (!force && Mathf.Approximately(savedButtonSize, appliedPersistentButtonSize))
            return;

        appliedPersistentButtonSize = savedButtonSize;
        topControlButtonSize = savedButtonSize;

        if (pulldownFrameRect == null)
            return;

        ApplyTopControlSizesForCurrentButtonSize();
        ApplyTopControlsSlidePosition();
    }

    private void ApplyTopControlSizesForCurrentButtonSize()
    {
        if (pulldownFrameRect != null)
            pulldownFrameRect.sizeDelta = GetPulldownFrameSizeForCurrentButtonSize();

        if (pulldownFrameImage != null)
            pulldownFrameImage.sprite = GetPulldownFrameSprite();

        ApplyPulldownEndRetractButtonRects();

        ApplyTopControlButtonSize(targetButtonRect);
        ApplyTopControlButtonSize(modeButtonRect);
        ApplyTopControlButtonSize(speedButtonRect);
        ApplyTopControlButtonSize(simulationButtonRect);
        ApplyTopControlButtonSize(emoteButtonRect);
        ApplyTopControlButtonSize(inventoryButtonRect);
        ApplyTopControlButtonSize(digButtonRect);
        ApplyTopControlButtonSize(homeButtonRect);
        ApplyTopControlButtonSize(cameraModeButtonRect);
        ApplyTopControlButtonSize(questButtonRect);

        ConfigureTopControlIconRect(targetButtonIconImage != null ? targetButtonIconImage.rectTransform : null, 0.68f);
        ConfigureTopControlIconRect(targetPreviewImage != null ? targetPreviewImage.rectTransform : null, 0.82f);
        ConfigureTopControlIconRect(modeIconImage != null ? modeIconImage.rectTransform : null, 0.72f);
        ConfigureTopControlIconRect(speedIconImage != null ? speedIconImage.rectTransform : null, 0.72f);
        ConfigureTopControlIconRect(simulationIconImage != null ? simulationIconImage.rectTransform : null, 0.72f);
        ConfigureTopControlIconRect(emoteIconImage != null ? emoteIconImage.rectTransform : null, 0.72f);
        ConfigureTopControlIconRect(inventoryIconImage != null ? inventoryIconImage.rectTransform : null, 0.72f);
        SetDigIconSize(digIconImage != null ? digIconImage.rectTransform : null, digIconImage != null ? digIconImage.sprite : null);
        ApplyInteractionPanelLayout();
    }

    private void ApplyTopControlButtonSize(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.sizeDelta = new Vector2(topControlButtonSize, topControlButtonSize);
    }

    private static void ApplyTopControlScale(RectTransform rectTransform, float scale)
    {
        if (rectTransform == null)
            return;

        rectTransform.localScale = Vector3.one * scale;
    }

    private void ExpandTopControlsFromTab()
    {
        pulldownOpenedByTab = true;
        HideTooltip();
        BottomBanner.Collapse();

        if (pulldownTabRect != null)
            pulldownTabRect.gameObject.SetActive(false);
    }

    private void CollapseTopControlsToTab()
    {
        pulldownOpenedByTab = false;
        CloseDropdown();
        CloseModePanel();
        CloseSpeedPanel();
        CloseEmoteDropdown();
        HideTooltip();
    }

    public static void CollapseOpenControls()
    {
        TopPulldown pulldown = FindFirstObjectByType<TopPulldown>();
        if (pulldown == null)
            return;

        pulldown.CollapseTopControlsToTab();
    }

    private void UpdatePulldownTabVisibility()
    {
        bool targetControlsVisible = !autoHideTopControls || pulldownOpenedByTab || IsAnyTopPanelOpen();
        UpdatePulldownTabVisibility(targetControlsVisible);
    }

    private void UpdatePulldownTabVisibility(bool targetControlsVisible)
    {
        if (pulldownTabRect == null)
            return;

        bool shouldShowTab = autoHideTopControls &&
                             !pulldownOpenedByTab &&
                             !targetControlsVisible &&
                             topControlsVisibility <= 0.05f;

        if (pulldownTabRect.gameObject.activeSelf != shouldShowTab)
            pulldownTabRect.gameObject.SetActive(shouldShowTab);
    }

    private void ApplyPulldownFramePosition(float y)
    {
        if (pulldownFrameRect == null)
            return;

        pulldownFrameRect.anchoredPosition = new Vector2(pulldownFrameOffset.x * GetTopControlsFitScale(), y);
    }

    private void ApplyTopControlPosition(RectTransform rectTransform, int slotFromRight, float y)
    {
        if (rectTransform == null)
            return;

        Vector2 anchoredPosition = GetTopControlPosition(slotFromRight);
        if (UseTwoRowTopControls())
        {
            float scale = GetTopControlsFitScale();
            float topRowShownY = -((topControlsInset.y * scale) + GetTopSafeAreaInset());
            anchoredPosition.y = y + (anchoredPosition.y - topRowShownY);
        }
        else
        {
            anchoredPosition.y = y;
        }

        rectTransform.anchoredPosition = anchoredPosition;
    }

    private void ApplyPulldownTabPosition()
    {
        if (pulldownTabRect == null)
            return;

        pulldownTabRect.anchoredPosition = GetPulldownTabPosition();
    }

    private void ApplyTopPanelPositions()
    {
        ApplyTopPanelPosition(dropdownRect, 0);
        ApplyTopPanelPosition(modePanelRect, 1);
        ApplyTopPanelPosition(speedPanelRect, 2);
        ApplyTopPanelPosition(emoteDropdownRect, 4);
    }

    private void ApplyTopPanelPosition(RectTransform rectTransform, int slotFromRight)
    {
        if (rectTransform == null)
            return;

        if (rectTransform == modePanelRect)
        {
            ApplyCenteredModePanelPosition();
            return;
        }

        if (rectTransform == speedPanelRect)
        {
            ApplyCenteredSpeedPanelPosition();
            return;
        }

        if (rectTransform == emoteDropdownRect)
        {
            ApplyCenteredEmoteDropdownPosition();
            return;
        }

        rectTransform.anchoredPosition = GetTopPanelPositionForRect(rectTransform, slotFromRight);
    }

    private Vector2 GetTopPanelPositionForRect(RectTransform rectTransform, int slotFromRight)
    {
        Vector2 position = GetTopPanelPosition(slotFromRight);
        if (rectTransform == speedPanelRect)
            position.y -= 48f;

        return position;
    }

    private void ApplyCenteredModePanelPosition()
    {
        if (modePanelRect == null)
            return;

        modePanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        modePanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        modePanelRect.pivot = new Vector2(0.5f, 0.5f);
        modePanelRect.anchoredPosition = Vector2.zero;
    }

    private void ApplyCenteredSpeedPanelPosition()
    {
        if (speedPanelRect == null)
            return;

        speedPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        speedPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        speedPanelRect.pivot = new Vector2(0.5f, 0.5f);
        speedPanelRect.anchoredPosition = Vector2.zero;
        PositionSpeedPanelCloseOverlay();
    }

    private void ApplyCenteredEmoteDropdownPosition()
    {
        if (emoteDropdownRect == null)
            return;

        emoteDropdownRect.anchorMin = new Vector2(0.5f, 0.5f);
        emoteDropdownRect.anchorMax = new Vector2(0.5f, 0.5f);
        emoteDropdownRect.pivot = new Vector2(0.5f, 0.5f);
        emoteDropdownRect.anchoredPosition = Vector2.zero;
        ClampEmoteDropdownToCanvas();
    }

    private Transform GetTopLevelOverlayParent(Transform fallbackParent)
    {
        return overlayCanvas != null ? overlayCanvas.transform : fallbackParent;
    }

    private float GetPulldownFrameHiddenY()
    {
        if (pulldownFrameRect == null)
            return GetPulldownFrameShownY();

        float frameHeight = pulldownFrameRect.rect.height > 0f
            ? pulldownFrameRect.rect.height
            : GetPulldownFrameSizeForCurrentButtonSize().y;
        return GetPulldownFrameShownY() + (frameHeight * GetTopControlsFitScale()) + topControlsHiddenTopPadding;
    }
}
