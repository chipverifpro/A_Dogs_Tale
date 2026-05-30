using DogGame.Modules;
using UnityEngine;
using UnityEngine.UI;

public partial class TopPulldown
{
    private void BuildModeButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "DecisionModeButton");
        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "DecisionModeButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        modeButtonRect = buttonObject.GetComponent<RectTransform>();
        if (createdButton)
        {
            modeButtonRect.anchorMin = new Vector2(1f, 1f);
            modeButtonRect.anchorMax = new Vector2(1f, 1f);
            modeButtonRect.pivot = new Vector2(1f, 1f);
            modeButtonRect.anchoredPosition = new Vector2(
                -(topControlButtonMargin + topControlButtonSize + modeButtonSpacing),
                -topControlButtonMargin);
            modeButtonRect.sizeDelta = new Vector2(topControlButtonSize, topControlButtonSize);
        }
        ConfigureTopControlRect(modeButtonRect, 1);

        modeButtonImage = GetOrAddComponent<Image>(buttonObject);
        modeButtonImage.color = topControlButtonColor;
        modeButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = modeButtonImage;
        button.onClick.RemoveListener(ToggleModePanel);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(ToggleModePanel);

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

        modeIconImage = GetOrAddComponent<Image>(iconObject);
        modeIconImage.preserveAspect = true;
        modeIconImage.color = Color.white;
        RefreshModeButtonState(force: true);

        ConfigureTooltip(buttonObject, () => "Behavior");
    }

    private void BuildModePanel(Transform parent, Transform searchRoot)
    {
        Transform existingPanel = FindExistingUiElement(parent, searchRoot, "DecisionModePanel");
        if (existingPanel != null)
        {
            BindExistingModePanel(existingPanel.gameObject);
            return;
        }

        GameObject panelObject = new GameObject(
            "DecisionModePanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(GridLayoutGroup));
        panelObject.transform.SetParent(parent, false);

        modePanelRect = panelObject.GetComponent<RectTransform>();
        ConfigureTopPanelRect(modePanelRect, 1);

        Image panelImage = panelObject.GetComponent<Image>();
        bool hasFrame = ApplyPanelFrame(panelImage, GetBehaviorFrameSprite());
        if (!hasFrame)
            panelImage.color = dropdownBackgroundColor;

        ConfigureModePanelLayout(panelObject, hasFrame);

        for (int i = 0; i < selectableDecisionModes.Length; i++)
            CreateModePanelButton(selectableDecisionModes[i]);

        EnsureInvisibleFrameCloseButton(panelObject.transform, CloseModePanel, new Vector2(-82f, -48f), new Vector2(36f, 36f));
        panelObject.SetActive(false);
    }

    private void ConfigureModePanelLayout(GameObject panelObject, bool hasFrame)
    {
        if (modePanelRect != null)
        {
            float fallbackPadding = 12f;
            float spacing = 8f;
            modePanelRect.sizeDelta = hasFrame
                ? new Vector2(400f, 266.7f)
                : new Vector2(
                    fallbackPadding * 2f + modePanelIconSize * 3f + spacing * 2f,
                    fallbackPadding * 2f + modePanelIconSize * 2f + spacing);
        }

        GridLayoutGroup grid = panelObject.GetComponent<GridLayoutGroup>();
        if (grid == null)
            return;

        grid.padding = hasFrame
            ? new RectOffset(67, 67, 75, 16)
            : new RectOffset(12, 12, 12, 12);
        float cellSize = hasFrame ? 85f : modePanelIconSize;
        float gridSpacing = hasFrame ? 5f : 8f;
        grid.cellSize = new Vector2(cellSize, cellSize);
        grid.spacing = new Vector2(gridSpacing, gridSpacing);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        if (hasFrame)
            ApplyCenteredModePanelPosition();
    }

    private void BindExistingModePanel(GameObject panelObject)
    {
        modePanelRect = panelObject.GetComponent<RectTransform>();
        if (modePanelRect == null)
            return;

        ConfigureTopPanelRect(modePanelRect, 1);

        Image panelImage = panelObject.GetComponent<Image>();
        bool hasFrame = ApplyPanelFrame(panelImage, GetBehaviorFrameSprite());
        if (panelImage != null && !hasFrame)
            panelImage.color = dropdownBackgroundColor;

        ConfigureModePanelLayout(panelObject, hasFrame);
        EnsureInvisibleFrameCloseButton(panelObject.transform, CloseModePanel, new Vector2(-82f, -48f), new Vector2(36f, 36f));

        modeButtonBackgrounds.Clear();
        for (int i = 0; i < selectableDecisionModes.Length; i++)
        {
            AgentDecisionType decisionType = selectableDecisionModes[i];
            Transform buttonTransform = panelObject.transform.Find($"{decisionType}ModeButton");
            if (buttonTransform == null && i < panelObject.transform.childCount)
                buttonTransform = panelObject.transform.GetChild(i);

            if (buttonTransform == null)
                continue;

            BindExistingModePanelButton(buttonTransform.gameObject, decisionType);
        }

        panelObject.SetActive(false);
    }

    private void BindExistingModePanelButton(GameObject buttonObject, AgentDecisionType decisionType)
    {
        Image background = GetOrAddComponent<Image>(buttonObject);
        background.color = dropdownRowColor;
        modeButtonBackgrounds.Add(background);

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = background;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(() => HandleDecisionModeSelected(decisionType));

        Transform iconTransform = buttonObject.transform.Find("Icon");
        if (iconTransform == null)
            return;

        Image iconImage = GetOrAddComponent<Image>(iconTransform.gameObject);
        iconImage.sprite = GetDecisionModeSprite(decisionType);
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;

        ConfigureTooltip(buttonObject, () => GetDecisionModeTooltipText(decisionType));
    }

    private void CreateModePanelButton(AgentDecisionType decisionType)
    {
        GameObject buttonObject = new GameObject(
            $"{decisionType}ModeButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(modePanelRect, false);

        Image background = buttonObject.GetComponent<Image>();
        background.color = dropdownRowColor;
        modeButtonBackgrounds.Add(background);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(() => HandleDecisionModeSelected(decisionType));

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(7f, 7f);
        iconRect.offsetMax = new Vector2(-7f, -7f);

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = GetDecisionModeSprite(decisionType);
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;

        ConfigureTooltip(buttonObject, () => GetDecisionModeTooltipText(decisionType));
    }

    private void ToggleModePanel()
    {
        if (modePanelRect == null)
            return;

        bool shouldOpen = !modePanelRect.gameObject.activeSelf;
        if (shouldOpen)
        {
            CloseSpeedPanel();
            CloseDropdown();
            CloseEmoteDropdown();
            RefreshModeButtonState(force: true);
            RefreshModePanelSelection();
            modePanelRect.gameObject.SetActive(true);
        }
        else
        {
            CloseModePanel();
        }
    }

    private void CloseModePanel()
    {
        if (modePanelRect != null)
            modePanelRect.gameObject.SetActive(false);

        HideTooltip();
    }

    private void HandleDecisionModeSelected(AgentDecisionType decisionType)
    {
        WorldObject controlledObject = GetCurrentControlledWorldObject();
        if (controlledObject == null)
        {
            Debug.LogWarning("TopPulldown: no controlled WorldObject available for decision mode selection.", this);
            return;
        }

        if (controlledObject.agentModule == null)
            controlledObject.CreateModulesIfNeeded(ModuleFlags.agentModule);

        if (controlledObject.agentModule == null)
        {
            Debug.LogWarning($"TopPulldown: {controlledObject.DisplayName} has no AgentModule.", controlledObject);
            return;
        }

        controlledObject.agentModule.SwitchDecisionModule(decisionType);
        RefreshModeButtonState(force: true);
        CloseModePanel();
    }

    private void RefreshModeButtonState(bool force = false)
    {
        if (modeIconImage == null)
            return;

        AgentDecisionType currentDecisionType = GetCurrentDecisionType();
        if (!force && currentDecisionType == displayedDecisionType)
            return;

        displayedDecisionType = currentDecisionType;
        modeIconImage.sprite = GetDecisionModeSprite(currentDecisionType);
        modeButtonImage.color = currentDecisionType == AgentDecisionType.Undefined
            ? topControlButtonColor
            : dropdownSelectedColor;

        RefreshModePanelSelection();
    }

    private void RefreshModePanelSelection()
    {
        AgentDecisionType currentDecisionType = GetCurrentDecisionType();

        for (int i = 0; i < modeButtonBackgrounds.Count && i < selectableDecisionModes.Length; i++)
        {
            Image background = modeButtonBackgrounds[i];
            if (background == null)
                continue;

            background.color = selectableDecisionModes[i] == currentDecisionType
                ? dropdownSelectedColor
                : dropdownRowColor;
        }
    }

    private Sprite GetDecisionModeSprite(AgentDecisionType decisionType)
    {
        return SpriteServer.SpriteLookup(GetDecisionModeSpriteName(decisionType))
            ?? SpriteServer.SpriteLookup("Player")
            ?? SpriteServer.SpriteSheetLookup(modeSpriteResourcePath, GetDecisionModeSpriteIndex(decisionType))
            ?? SpriteServer.SpriteSheetLookup(modeSpriteResourcePath, GetDecisionModeSpriteIndex(AgentDecisionType.Player));
    }

    private string GetDecisionModeSpriteName(AgentDecisionType decisionType)
    {
        switch (decisionType)
        {
            case AgentDecisionType.Player:
                return "Player";
            case AgentDecisionType.Follower:
                return "Follow";
            case AgentDecisionType.Explorer:
                return "Explore";
            case AgentDecisionType.Immobile:
                return "Hold";
            case AgentDecisionType.Wanderer:
                return "Wander";
            case AgentDecisionType.TaskFollower:
                return "LLMControlled";
            default:
                return "Player";
        }
    }

    private int GetDecisionModeSpriteIndex(AgentDecisionType decisionType)
    {
        switch (decisionType)
        {
            case AgentDecisionType.Player:
                return 0;
            case AgentDecisionType.Follower:
                return 1;
            case AgentDecisionType.Explorer:
                return 2;
            case AgentDecisionType.Immobile:
                return 3;
            case AgentDecisionType.Wanderer:
                return 4;
            case AgentDecisionType.TaskFollower:
                return 5;
            default:
                return 0;
        }
    }

    private string GetDecisionModeTooltipText(AgentDecisionType decisionType)
    {
        switch (decisionType)
        {
            case AgentDecisionType.Player:
                return "Player";
            case AgentDecisionType.Follower:
                return "Follow";
            case AgentDecisionType.Explorer:
                return "Explore";
            case AgentDecisionType.Immobile:
                return "Stay";
            case AgentDecisionType.Wanderer:
                return "Wander";
            case AgentDecisionType.TaskFollower:
                return "LLM Controlled";
            default:
                return decisionType.ToString();
        }
    }
}
