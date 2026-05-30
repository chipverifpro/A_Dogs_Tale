using DogGame.Modules;
using UnityEngine;
using UnityEngine.UI;

public partial class TopPulldown
{
    private RectTransform speedPanelCloseButtonRect;

    private void BuildSpeedButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "SpeedModeButton");
        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "SpeedModeButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        speedButtonRect = buttonObject.GetComponent<RectTransform>();
        speedButtonRect.anchorMin = new Vector2(1f, 1f);
        speedButtonRect.anchorMax = new Vector2(1f, 1f);
        speedButtonRect.pivot = new Vector2(1f, 1f);
        speedButtonRect.anchoredPosition = new Vector2(
            -(topControlButtonMargin + ((topControlButtonSize + modeButtonSpacing) * 3f)),
            -topControlButtonMargin);
        speedButtonRect.sizeDelta = new Vector2(topControlButtonSize, topControlButtonSize);
        ConfigureTopControlRect(speedButtonRect, 2);

        speedButtonImage = GetOrAddComponent<Image>(buttonObject);
        speedButtonImage.color = topControlButtonColor;
        speedButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = speedButtonImage;
        button.onClick.RemoveListener(ToggleSpeedPanel);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(ToggleSpeedPanel);

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

        speedIconImage = GetOrAddComponent<Image>(iconObject);
        speedIconImage.preserveAspect = true;
        speedIconImage.color = Color.white;
        RefreshSpeedButtonState(force: true);

        ConfigureTooltip(buttonObject, () => "Gait");
    }

    private void BuildSpeedPanel(Transform parent, Transform searchRoot)
    {
        Transform existingPanel = FindExistingUiElement(parent, searchRoot, "SpeedModePanel");
        if (existingPanel != null)
        {
            BindExistingSpeedPanel(existingPanel.gameObject);
            return;
        }

        GameObject panelObject = new GameObject(
            "SpeedModePanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(GridLayoutGroup));
        panelObject.transform.SetParent(parent, false);

        speedPanelRect = panelObject.GetComponent<RectTransform>();
        ConfigureTopPanelRect(speedPanelRect, 2);

        Image panelImage = panelObject.GetComponent<Image>();
        bool hasFrame = ApplyPanelFrame(panelImage, GetGaitFrameSprite());
        if (!hasFrame)
            panelImage.color = dropdownBackgroundColor;

        ConfigureSpeedPanelLayout(panelObject, hasFrame);

        for (int i = 0; i < selectableSpeedModes.Length; i++)
            CreateSpeedPanelButton(selectableSpeedModes[i]);

        EnsureSpeedPanelCloseOverlay();
        panelObject.SetActive(false);
    }

    private void ConfigureSpeedPanelLayout(GameObject panelObject, bool hasFrame)
    {
        if (speedPanelRect != null)
        {
            float fallbackPadding = 12f;
            float spacing = 8f;
            speedPanelRect.sizeDelta = hasFrame
                ? new Vector2(1120f, 374f)
                : new Vector2(
                    fallbackPadding * 2f + modePanelIconSize * 3f + spacing * 2f,
                    fallbackPadding * 2f + modePanelIconSize);
        }

        GridLayoutGroup grid = panelObject.GetComponent<GridLayoutGroup>();
        if (grid == null)
            return;

        grid.padding = hasFrame
            ? new RectOffset(360, 360, 104, 14)
            : new RectOffset(12, 12, 12, 12);
        grid.cellSize = new Vector2(modePanelIconSize, modePanelIconSize);
        grid.spacing = new Vector2(8f, 8f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        if (hasFrame)
            ApplyCenteredSpeedPanelPosition();
    }

    private void BindExistingSpeedPanel(GameObject panelObject)
    {
        speedPanelRect = panelObject.GetComponent<RectTransform>();
        if (speedPanelRect == null)
            return;

        ConfigureTopPanelRect(speedPanelRect, 2);

        Image panelImage = panelObject.GetComponent<Image>();
        bool hasFrame = ApplyPanelFrame(panelImage, GetGaitFrameSprite());
        if (panelImage != null && !hasFrame)
            panelImage.color = dropdownBackgroundColor;

        ConfigureSpeedPanelLayout(panelObject, hasFrame);
        EnsureSpeedPanelCloseOverlay();

        speedButtonBackgrounds.Clear();
        for (int i = 0; i < selectableSpeedModes.Length; i++)
        {
            WalkMode walkMode = selectableSpeedModes[i];
            Transform buttonTransform = panelObject.transform.Find($"{walkMode}SpeedButton");
            if (buttonTransform == null && i < panelObject.transform.childCount)
                buttonTransform = panelObject.transform.GetChild(i);

            if (buttonTransform == null)
                continue;

            BindExistingSpeedPanelButton(buttonTransform.gameObject, walkMode);
        }

        panelObject.SetActive(false);
    }

    private void BindExistingSpeedPanelButton(GameObject buttonObject, WalkMode walkMode)
    {
        Image background = GetOrAddComponent<Image>(buttonObject);
        background.color = dropdownRowColor;
        speedButtonBackgrounds.Add(background);

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = background;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(() => HandleSpeedModeSelected(walkMode));

        Transform iconTransform = buttonObject.transform.Find("Icon");
        if (iconTransform == null)
            return;

        Image iconImage = GetOrAddComponent<Image>(iconTransform.gameObject);
        iconImage.sprite = GetSpeedModeSprite(walkMode);
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;

        ConfigureTooltip(buttonObject, () => GetSpeedModeTooltipText(walkMode));
    }

    private void CreateSpeedPanelButton(WalkMode walkMode)
    {
        GameObject buttonObject = new GameObject(
            $"{walkMode}SpeedButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(speedPanelRect, false);

        Image background = buttonObject.GetComponent<Image>();
        background.color = dropdownRowColor;
        speedButtonBackgrounds.Add(background);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(() => HandleSpeedModeSelected(walkMode));

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(7f, 7f);
        iconRect.offsetMax = new Vector2(-7f, -7f);

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = GetSpeedModeSprite(walkMode);
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;

        ConfigureTooltip(buttonObject, () => GetSpeedModeTooltipText(walkMode));
    }

    private void ToggleSpeedPanel()
    {
        if (speedPanelRect == null)
            return;

        bool shouldOpen = !speedPanelRect.gameObject.activeSelf;
        if (shouldOpen)
        {
            CloseModePanel();
            CloseDropdown();
            CloseEmoteDropdown();
            RefreshSpeedButtonState(force: true);
            RefreshSpeedPanelSelection();
            speedPanelRect.gameObject.SetActive(true);
            SetSpeedPanelCloseOverlayVisible(true);
        }
        else
        {
            CloseSpeedPanel();
        }
    }

    private void CloseSpeedPanel()
    {
        if (speedPanelRect != null)
            speedPanelRect.gameObject.SetActive(false);

        SetSpeedPanelCloseOverlayVisible(false);
        HideTooltip();
    }

    private void EnsureSpeedPanelCloseOverlay()
    {
        if (overlayCanvas == null)
            return;

        Transform oldGridChild = speedPanelRect != null ? speedPanelRect.Find("FrameCloseButton") : null;
        if (oldGridChild != null)
        {
            if (Application.isPlaying)
                Destroy(oldGridChild.gameObject);
            else
                DestroyImmediate(oldGridChild.gameObject);
        }

        Transform parent = overlayCanvas.transform;
        Transform existingButton = parent.Find("SpeedPanelCloseButton");
        GameObject buttonObject;
        if (existingButton == null)
        {
            buttonObject = new GameObject(
                "SpeedPanelCloseButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        speedPanelCloseButtonRect = GetOrAddComponent<RectTransform>(buttonObject);
        speedPanelCloseButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
        speedPanelCloseButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
        speedPanelCloseButtonRect.pivot = new Vector2(0.5f, 0.5f);
        speedPanelCloseButtonRect.sizeDelta = new Vector2(104f, 95f);
        PositionSpeedPanelCloseOverlay();

        Image image = GetOrAddComponent<Image>(buttonObject);
        image.color = new Color(1f, 1f, 1f, 0.001f);
        image.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = image;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(CloseSpeedPanel);

        ConfigureTooltip(buttonObject, () => "Close");
        buttonObject.SetActive(false);
    }

    private void PositionSpeedPanelCloseOverlay()
    {
        if (speedPanelRect == null || speedPanelCloseButtonRect == null)
            return;

        Vector2 panelSize = speedPanelRect.rect.size;
        if (panelSize.x <= 0f || panelSize.y <= 0f)
            panelSize = speedPanelRect.sizeDelta;

        Vector2 closeSize = speedPanelCloseButtonRect.sizeDelta;
        speedPanelCloseButtonRect.anchoredPosition = speedPanelRect.anchoredPosition +
            new Vector2((panelSize.x * 0.5f) - 310f, (panelSize.y * 0.5f) - 95f);
        speedPanelCloseButtonRect.SetAsLastSibling();
    }

    private void SetSpeedPanelCloseOverlayVisible(bool visible)
    {
        if (speedPanelCloseButtonRect == null)
            EnsureSpeedPanelCloseOverlay();

        if (speedPanelCloseButtonRect == null)
            return;

        if (visible)
            PositionSpeedPanelCloseOverlay();

        speedPanelCloseButtonRect.gameObject.SetActive(visible);
    }

    private void HandleSpeedModeSelected(WalkMode walkMode)
    {
        WorldObject controlledObject = GetCurrentControlledWorldObject();
        if (controlledObject == null)
        {
            Debug.LogWarning("TopPulldown: no controlled WorldObject available for speed selection.", this);
            return;
        }

        Pack targetPack = controlledObject.packMemberModule != null
            ? controlledObject.packMemberModule.currentPack
            : null;

        int changedCount = targetPack != null
            ? targetPack.SetWalkMode(walkMode)
            : SetWalkModeForWorldObject(controlledObject, walkMode);

        if (changedCount <= 0)
        {
            Debug.LogWarning($"TopPulldown: no movement modules available for speed selection from {controlledObject.DisplayName}.", controlledObject);
            return;
        }

        RefreshSpeedButtonState(force: true);
        CloseSpeedPanel();
    }

    private static int SetWalkModeForWorldObject(WorldObject worldObject, WalkMode walkMode)
    {
        if (worldObject == null)
            return 0;

        if (worldObject.agentMovementModule == null || worldObject.motionModule == null)
            worldObject.CreateModulesIfNeeded(ModuleFlags.agentMovementModule | ModuleFlags.motionModule);

        if (worldObject.agentMovementModule != null)
        {
            worldObject.agentMovementModule.SetWalkMode(walkMode);
            return 1;
        }

        if (worldObject.motionModule != null)
        {
            worldObject.motionModule.SetWalkMode(walkMode);
            return 1;
        }

        return 0;
    }

    private void RefreshSpeedButtonState(bool force = false)
    {
        if (speedIconImage == null || speedButtonImage == null)
            return;

        WalkMode currentWalkMode = GetCurrentWalkMode();
        if (!force && currentWalkMode == displayedWalkMode)
            return;

        displayedWalkMode = currentWalkMode;
        speedIconImage.sprite = GetSpeedModeSprite(currentWalkMode);
        speedButtonImage.color = currentWalkMode == WalkMode.None
            ? topControlButtonColor
            : dropdownSelectedColor;

        RefreshSpeedPanelSelection();
        RefreshActiveTooltipText();
    }

    private void RefreshSpeedPanelSelection()
    {
        WalkMode currentWalkMode = GetCurrentWalkMode();

        for (int i = 0; i < speedButtonBackgrounds.Count && i < selectableSpeedModes.Length; i++)
        {
            Image background = speedButtonBackgrounds[i];
            if (background == null)
                continue;

            background.color = selectableSpeedModes[i] == currentWalkMode
                ? dropdownSelectedColor
                : dropdownRowColor;
        }
    }

    private Sprite GetSpeedModeSprite(WalkMode walkMode)
    {
        return SpriteServer.SpriteLookup(GetSpeedModeSpriteName(walkMode))
            ?? SpriteServer.SpriteLookup("Walk")
            ?? SpriteServer.SpriteSheetLookup(speedSpriteResourcePath, GetSpeedModeSpriteIndex(walkMode))
            ?? SpriteServer.SpriteSheetLookup(speedSpriteResourcePath, GetSpeedModeSpriteIndex(WalkMode.Walk));
    }

    private string GetSpeedModeSpriteName(WalkMode walkMode)
    {
        switch (walkMode)
        {
            case WalkMode.Sneak:
                return "Sneak";
            case WalkMode.Walk:
                return "Walk";
            case WalkMode.Run:
                return "Run";
            default:
                return "Walk";
        }
    }

    private int GetSpeedModeSpriteIndex(WalkMode walkMode)
    {
        switch (walkMode)
        {
            case WalkMode.Sneak:
                return 0;
            case WalkMode.Walk:
                return 1;
            case WalkMode.Run:
                return 2;
            default:
                return 1;
        }
    }

    private string GetSpeedModeTooltipText(WalkMode walkMode)
    {
        switch (walkMode)
        {
            case WalkMode.Sneak:
                return "Sneak";
            case WalkMode.Walk:
                return "Walk";
            case WalkMode.Run:
                return "Run";
            default:
                return walkMode.ToString();
        }
    }
}
