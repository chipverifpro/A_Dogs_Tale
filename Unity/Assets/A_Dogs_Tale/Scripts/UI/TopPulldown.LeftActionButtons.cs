using DogGame.Modules;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public partial class TopPulldown
{
    private void BuildLeftActionButtons(Transform parent, Transform searchRoot)
    {
        homeButtonRect = BuildLeftActionButton(
            parent,
            searchRoot,
            "HomeButton",
            spriteName: "Home",
            spriteIndex: 0,
            slotFromRight: HomeButtonTopSlotFromRight,
            HandleHomeButtonPressed,
            "Home",
            out homeButtonImage);

        cameraModeButtonRect = BuildLeftActionButton(
            parent,
            searchRoot,
            "CameraModeButton",
            spriteName: "Camera",
            spriteIndex: 2,
            slotFromRight: CameraModeButtonTopSlotFromRight,
            HandleCameraModeButtonPressed,
            "Camera Mode",
            out cameraModeButtonImage);

        questButtonRect = BuildLeftActionButton(
            parent,
            searchRoot,
            "QuestButton",
            spriteName: "Quest",
            spriteIndex: 1,
            slotFromRight: QuestButtonTopSlotFromRight,
            HandleQuestButtonPressed,
            "Quests",
            out questButtonImage);
    }

    private RectTransform BuildLeftActionButton(
        Transform parent,
        Transform searchRoot,
        string buttonName,
        string spriteName,
        int spriteIndex,
        int slotFromRight,
        UnityAction clickHandler,
        string tooltipText,
        out Image buttonImage)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, buttonName);
        GameObject buttonObject;
        if (existingButton == null)
        {
            buttonObject = new GameObject(
                buttonName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        RectTransform buttonRect = GetOrAddComponent<RectTransform>(buttonObject);
        ConfigureTopControlRect(buttonRect, slotFromRight);

        buttonImage = GetOrAddComponent<Image>(buttonObject);
        buttonImage.sprite = GetAndroidButtonSprite(spriteName, spriteIndex);
        buttonImage.preserveAspect = true;
        buttonImage.color = Color.white;
        buttonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = buttonImage;
        button.onClick.RemoveListener(clickHandler);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(clickHandler);

        ConfigureTooltip(buttonObject, () => tooltipText);

        return buttonRect;
    }

    private void HandleHomeButtonPressed()
    {
        CloseTopActionPanels();

        SceneFader sceneFader = EnsureDir() && dir.sceneFader != null
            ? dir.sceneFader
            : FindFirstObjectByType<SceneFader>();

        if (sceneFader == null)
        {
            Debug.LogWarning("TopPulldown: title menu fader is not available for Home button.", this);
            BottomBanner.Show("Home is not ready yet.");
            return;
        }

        sceneFader.ReturnToTitleMenu();
    }

    private void HandleCameraModeButtonPressed()
    {
        CloseTopActionPanels();

        CameraModeSwitcher cameraModeSwitcher = EnsureDir() && dir.cameraModeSwitcher != null
            ? dir.cameraModeSwitcher
            : FindFirstObjectByType<CameraModeSwitcher>();

        if (cameraModeSwitcher == null)
        {
            Debug.LogWarning("TopPulldown: camera mode switcher is not available.", this);
            BottomBanner.Show("Camera mode is not ready yet.");
            return;
        }

        cameraModeSwitcher.SelectNextView();
    }

    private void HandleQuestButtonPressed()
    {
        CloseTopActionPanels();

        QuestJournalUI questJournal = FindFirstObjectByType<QuestJournalUI>();
        if (questJournal == null)
        {
            _ = QuestManager.Instance;
            GameObject journalObject = new("QuestJournalUI");
            questJournal = journalObject.AddComponent<QuestJournalUI>();
        }

        questJournal.Toggle();
    }

    private void CloseTopActionPanels()
    {
        CloseDropdown();
        CloseModePanel();
        CloseSpeedPanel();
        CloseEmoteDropdown();
        HideTooltip();
    }

    private Sprite GetAndroidButtonSprite(string spriteName, int index)
    {
        return SpriteServer.SpriteLookup(spriteName)
            ?? SpriteServer.SpriteSheetLookup(androidButtonSpriteResourcePath, index);
    }
}

public partial class TopPulldown
{
    [Header("Corner Controls")]
    [SerializeField] private bool useCornerControls = true;
    [SerializeField] private float cornerControlMargin = 24f;
    [SerializeField] private float interactionPanelButtonScale = 0.58f;
    [SerializeField] private string interactionSideButtonsSpriteResourcePath = "Sprites/Frames/Interaction_Side_Buttons_A";

    private RectTransform interactionPanelRect;
    private Image interactionPanelImage;
    private RectTransform sniffCommandButtonRect;
    private Image sniffCommandButtonImage;
    private readonly RectTransform[] interactionButtonRects = new RectTransform[5];

    private void BuildCornerControls(Transform parent, Transform searchRoot)
    {
        if (!useCornerControls)
            return;

        BuildSniffCommandButton(parent, searchRoot);
        BuildInteractionPanel(parent, searchRoot);
        HidePulldownControlsReplacedByCorners();
        ApplyCornerControlsLayout();
    }

    private void BuildInteractionPanel(Transform parent, Transform searchRoot)
    {
        Transform existingPanel = FindExistingUiElement(parent, searchRoot, "InteractionTabPanel");
        GameObject panelObject;
        if (existingPanel == null)
        {
            panelObject = new GameObject("InteractionTabPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panelObject.transform.SetParent(parent, false);
        }
        else
        {
            panelObject = existingPanel.gameObject;
        }

        interactionPanelRect = GetOrAddComponent<RectTransform>(panelObject);
        interactionPanelImage = GetOrAddComponent<Image>(panelObject);
        interactionPanelImage.sprite = GetInteractionSideButtonsSprite();
        interactionPanelImage.color = Color.white;
        interactionPanelImage.preserveAspect = true;
        interactionPanelImage.raycastTarget = false;

        VerticalLayoutGroup layout = GetOrAddComponent<VerticalLayoutGroup>(panelObject);
        layout.spacing = 0f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        interactionButtonRects[0] = BuildInteractionTabButton(panelObject.transform, "InteractSocialButton", InteractionDialogUI.InteractionTab.Social, "Social");
        interactionButtonRects[1] = BuildInteractionTabButton(panelObject.transform, "InteractPackButton", InteractionDialogUI.InteractionTab.Pack, "Pack");
        interactionButtonRects[2] = BuildInteractionTabButton(panelObject.transform, "InteractItemsButton", InteractionDialogUI.InteractionTab.Items, "Items");
        interactionButtonRects[3] = BuildInteractionTabButton(panelObject.transform, "InteractQuestsButton", InteractionDialogUI.InteractionTab.Quests, "Quests");
        interactionButtonRects[4] = BuildInteractionTabButton(panelObject.transform, "InteractScentButton", InteractionDialogUI.InteractionTab.Scent, "Scent");
    }

    private void BuildSniffCommandButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "SniffCommandButton");
        GameObject buttonObject;
        if (existingButton == null)
        {
            buttonObject = new GameObject(
                "SniffCommandButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        sniffCommandButtonRect = GetOrAddComponent<RectTransform>(buttonObject);
        sniffCommandButtonImage = GetOrAddComponent<Image>(buttonObject);
        sniffCommandButtonImage.sprite = SpriteServer.SpriteLookup("AndroidButtonsAndQuests_3")
            ?? SpriteServer.SpriteSheetLookup("Sprites/AndroidButtonsAndQuests", 3);
        sniffCommandButtonImage.preserveAspect = true;
        sniffCommandButtonImage.color = Color.white;
        sniffCommandButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = sniffCommandButtonImage;
        button.onClick.RemoveListener(HandleSniffCommandButtonPressed);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(HandleSniffCommandButtonPressed);

        ConfigureTooltip(buttonObject, () => "Sniff (N)");
    }

    private void HandleSniffCommandButtonPressed()
    {
        CloseTopActionPanels();
        SniffInput.TryRunPlayerSniff("sniff_button");
    }

    private RectTransform BuildInteractionTabButton(Transform parent, string buttonName, InteractionDialogUI.InteractionTab tab, string tooltipText)
    {
        Transform existingButton = parent.Find(buttonName);
        GameObject buttonObject;
        if (existingButton == null)
        {
            buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        RectTransform rect = GetOrAddComponent<RectTransform>(buttonObject);
        Image buttonImage = GetOrAddComponent<Image>(buttonObject);
        buttonImage.color = Color.clear;
        buttonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = buttonImage;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(() => OpenInteractionTab(tab));

        Transform oldIcon = buttonObject.transform.Find("Icon");
        if (oldIcon != null)
            oldIcon.gameObject.SetActive(false);

        LayoutElement layout = GetOrAddComponent<LayoutElement>(buttonObject);
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;

        ConfigureTooltip(buttonObject, () => tooltipText);
        return rect;
    }

    private void ApplyCornerControlsLayout()
    {
        if (!useCornerControls || !uiBuilt)
            return;

        HidePulldownControlsReplacedByCorners();

        float topInset = GetTopSafeAreaInset();
        float secondRowY = -(cornerControlMargin + topInset + topControlButtonSize + modeButtonSpacing);
        float thirdRowY = secondRowY - topControlButtonSize - modeButtonSpacing;
        ConfigureCornerButton(homeButtonRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cornerControlMargin, -(cornerControlMargin + topInset)));
        ConfigureCornerButton(cameraModeButtonRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-cornerControlMargin, -(cornerControlMargin + topInset)));
        ConfigureCornerButton(speedButtonRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cornerControlMargin, secondRowY));
        ConfigureCornerButton(sniffCommandButtonRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cornerControlMargin, thirdRowY));

        ApplyTopControlButtonSize(homeButtonRect);
        ApplyTopControlButtonSize(cameraModeButtonRect);
        ApplyTopControlButtonSize(speedButtonRect);
        ApplyTopControlButtonSize(sniffCommandButtonRect);
        ConfigureTopControlIconRect(speedIconImage != null ? speedIconImage.rectTransform : null, 0.72f);
        ApplySniffResultsOverlayLayout();

        ApplyInteractionPanelLayout();
    }

    private void ConfigureCornerButton(RectTransform rectTransform, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition)
    {
        if (rectTransform == null)
            return;

        rectTransform.gameObject.SetActive(true);
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = pivot;
        rectTransform.localScale = Vector3.one;
        rectTransform.anchoredPosition = anchoredPosition;
    }

    private void ApplyInteractionPanelLayout()
    {
        if (interactionPanelRect == null)
            return;

        float hotspotHeight = Mathf.Max(48f, topControlButtonSize * interactionPanelButtonScale);
        float panelHeight = hotspotHeight * interactionButtonRects.Length;
        float panelWidth = GetInteractionSideButtonsAspectRatio() * panelHeight;
        interactionPanelRect.gameObject.SetActive(true);
        if (interactionPanelImage != null)
        {
            interactionPanelImage.sprite = GetInteractionSideButtonsSprite();
            interactionPanelImage.color = Color.white;
            interactionPanelImage.preserveAspect = true;
        }

        float topInset = GetTopSafeAreaInset();
        float secondRowY = -(cornerControlMargin + topInset + topControlButtonSize + modeButtonSpacing);
        interactionPanelRect.anchorMin = new Vector2(1f, 1f);
        interactionPanelRect.anchorMax = new Vector2(1f, 1f);
        interactionPanelRect.pivot = new Vector2(1f, 1f);
        interactionPanelRect.localScale = Vector3.one;
        interactionPanelRect.anchoredPosition = new Vector2(-cornerControlMargin, secondRowY);
        interactionPanelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        for (int i = 0; i < interactionButtonRects.Length; i++)
        {
            RectTransform rect = interactionButtonRects[i];
            if (rect == null)
                continue;

            rect.sizeDelta = new Vector2(panelWidth, hotspotHeight);
            LayoutElement layout = GetOrAddComponent<LayoutElement>(rect.gameObject);
            layout.minWidth = panelWidth;
            layout.preferredWidth = panelWidth;
            layout.minHeight = hotspotHeight;
            layout.preferredHeight = hotspotHeight;
        }
    }

    private void HidePulldownControlsReplacedByCorners()
    {
        SetRectActive(pulldownFrameRect, false);
        SetRectActive(pulldownTabRect, false);
        SetRectActive(targetButtonRect, false);
        SetRectActive(modeButtonRect, false);
        SetRectActive(simulationButtonRect, false);
        SetRectActive(emoteButtonRect, false);
        SetRectActive(inventoryButtonRect, false);
        SetRectActive(digButtonRect, false);
        SetRectActive(questButtonRect, false);
    }

    private static void SetRectActive(RectTransform rectTransform, bool active)
    {
        if (rectTransform != null && rectTransform.gameObject.activeSelf != active)
            rectTransform.gameObject.SetActive(active);
    }

    private void OpenInteractionTab(InteractionDialogUI.InteractionTab tab)
    {
        CloseTopActionPanels();

        InteractionDialogUI dialog = FindFirstObjectByType<InteractionDialogUI>(FindObjectsInactive.Include);
        if (dialog == null)
        {
            GameObject dialogObject = new("InteractionDialogUI");
            dialog = dialogObject.AddComponent<InteractionDialogUI>();
        }

        dialog.Show(tab);
    }

    private Sprite GetInteractionSideButtonsSprite()
    {
        return SpriteServer.SpriteLookup("Interaction_Side_Buttons_A")
            ?? SpriteServer.SpriteResourceLookup(interactionSideButtonsSpriteResourcePath);
    }

    private float GetInteractionSideButtonsAspectRatio()
    {
        Sprite sprite = GetInteractionSideButtonsSprite();
        if (sprite == null || sprite.rect.height <= 0f)
            return 171f / 560f;

        return sprite.rect.width / sprite.rect.height;
    }
}
