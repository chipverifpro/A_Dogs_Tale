using System.Collections.Generic;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class QuestJournalUI : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private int uiSortOrder = 5400;
    [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);
    [SerializeField] private Vector2 dialogSize = new(760f, 600f);
    [SerializeField] private float questAcceptRange = 2.5f;
    [SerializeField] private float proximityRefreshInterval = 0.5f;
    [SerializeField] private string questFrameResourcePath = "Sprites/Quest_Frame_A";
    [SerializeField] private Vector2 questFrameCloseButtonAnchoredPosition = new(-160f, -100f);
    [SerializeField] private Vector2 questFrameCloseButtonSize = new(100f, 100f);

    private readonly HashSet<QuestModuleBase> expandedQuestModules = new();
    private readonly List<QuestModuleBase> displayQuestModules = new();
    private readonly Dictionary<QuestModuleBase, TextMeshProUGUI> questStatusLabels = new();

    private Canvas overlayCanvas;
    private GameObject dialogRoot;
    private RectTransform contentRect;
    private RectTransform tooltipRect;
    private ScrollRect questScrollRect;
    private TextMeshProUGUI emptyLabel;
    private TextMeshProUGUI tooltipLabel;
    private Sprite questFrameSprite;
    private bool questFrameApplied;
    private bool isOpen;
    private bool questListDirty = true;
    private float nextProximityRefreshTime;

    private void Awake()
    {
        BuildUI();
        Hide();
    }

    private void OnEnable()
    {
        QuestManager.Instance.ActiveQuestsChanged += OnActiveQuestsChanged;
    }

    private void OnDisable()
    {
        if (QuestManager.Current != null)
            QuestManager.Current.ActiveQuestsChanged -= OnActiveQuestsChanged;
    }

    private void Update()
    {
        if (WasQuestTogglePressedThisFrame())
            Toggle();

        if (!isOpen)
            return;

        if (questListDirty)
            RefreshQuestList();

        if (Time.unscaledTime >= nextProximityRefreshTime)
        {
            nextProximityRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, proximityRefreshInterval);
            questListDirty = true;
        }

        UpdateQuestHeaderLabels();
    }

    public void Toggle()
    {
        if (isOpen)
            Hide();
        else
            Show();
    }

    public void Show()
    {
        EnsureEventSystem();
        isOpen = true;
        dialogRoot.SetActive(true);
        questListDirty = true;
        nextProximityRefreshTime = 0f;
        RefreshQuestList();
    }

    public void Hide()
    {
        isOpen = false;

        if (dialogRoot != null)
            dialogRoot.SetActive(false);

        HideTooltip();
    }

    private void OnActiveQuestsChanged()
    {
        questListDirty = true;

        if (isOpen)
            RefreshQuestList();
    }

    private bool WasQuestTogglePressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.mKey.wasPressedThisFrame)
            return false;

        GameObject selectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selectedObject != null &&
            (selectedObject.GetComponent<TMP_InputField>() != null || selectedObject.GetComponent<InputField>() != null))
        {
            return false;
        }

        return true;
    }

    private void BuildUI()
    {
        EnsureEventSystem();

        GameObject canvasObject = new("QuestJournalCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = uiSortOrder;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 1f;

        dialogRoot = CreateUIObject("QuestJournalDialog", canvasObject.transform);
        RectTransform dialogRect = dialogRoot.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero;
        dialogRect.sizeDelta = dialogSize;

        Image dialogImage = dialogRoot.AddComponent<Image>();
        questFrameApplied = ApplyFrame(dialogImage, GetQuestFrameSprite());
        if (!questFrameApplied)
            dialogImage.color = new Color(0.055f, 0.05f, 0.043f, 0.96f);

        BuildHeader(dialogRoot.transform);
        BuildBody(dialogRoot.transform);
        BuildTooltip(canvasObject.transform);
    }

    private void BuildHeader(Transform parent)
    {
        GameObject titleObject = CreateUIObject("Title", parent);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(28f, -72f);
        titleRect.offsetMax = new Vector2(-86f, -18f);

        TextMeshProUGUI titleLabel = titleObject.AddComponent<TextMeshProUGUI>();
        titleLabel.text = "Quests";
        titleLabel.fontSize = 34f;
        titleLabel.color = new Color(0.98f, 0.93f, 0.78f, 1f);
        titleLabel.alignment = TextAlignmentOptions.MidlineLeft;
        titleObject.SetActive(!questFrameApplied);

        Button closeButton = CreateInvisibleButton("CloseButton", parent, Hide);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = questFrameApplied ? new Vector2(0.5f, 0.5f) : new Vector2(1f, 1f);
        closeRect.anchoredPosition = questFrameApplied ? questFrameCloseButtonAnchoredPosition : new Vector2(-20f, -18f);
        closeRect.sizeDelta = questFrameApplied ? questFrameCloseButtonSize : new Vector2(54f, 54f);
    }

    private void BuildBody(Transform parent)
    {
        GameObject bodyObject = CreateUIObject("Body", parent);
        RectTransform bodyRect = bodyObject.GetComponent<RectTransform>();
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = questFrameApplied ? new Vector2(76f, 64f) : new Vector2(28f, 28f);
        bodyRect.offsetMax = questFrameApplied ? new Vector2(-76f, -146f) : new Vector2(-28f, -86f);

        Image bodyImage = bodyObject.AddComponent<Image>();
        bodyImage.color = questFrameApplied ? Color.clear : new Color(0.02f, 0.018f, 0.014f, 0.32f);

        GameObject scrollObject = CreateUIObject("ScrollView", bodyObject.transform);
        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(14f, 14f);
        scrollRect.offsetMax = new Vector2(-14f, -14f);

        questScrollRect = scrollObject.AddComponent<ScrollRect>();
        questScrollRect.horizontal = false;
        questScrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewportObject = CreateUIObject("Viewport", scrollObject.transform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(0.055f, 0.047f, 0.036f, 0.45f);

        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUIObject("Content", viewportObject.transform);
        contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 10f;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        questScrollRect.viewport = viewportRect;
        questScrollRect.content = contentRect;

        GameObject emptyObject = CreateUIObject("EmptyLabel", bodyObject.transform);
        RectTransform emptyRect = emptyObject.GetComponent<RectTransform>();
        emptyRect.anchorMin = Vector2.zero;
        emptyRect.anchorMax = Vector2.one;
        emptyRect.offsetMin = new Vector2(32f, 32f);
        emptyRect.offsetMax = new Vector2(-32f, -32f);

        emptyLabel = emptyObject.AddComponent<TextMeshProUGUI>();
        emptyLabel.text = "No quests";
        emptyLabel.fontSize = 26f;
        emptyLabel.color = new Color(0.86f, 0.8f, 0.66f, 0.78f);
        emptyLabel.alignment = TextAlignmentOptions.Center;
        emptyLabel.raycastTarget = false;
    }

    private void RefreshQuestList()
    {
        QuestManager.RefreshActiveQuestModules();
        questStatusLabels.Clear();

        for (int i = contentRect.childCount - 1; i >= 0; i--)
            Destroy(contentRect.GetChild(i).gameObject);

        CollectDisplayQuestModules();
        int renderedQuestCount = 0;

        foreach (QuestModuleBase quest in displayQuestModules)
        {
            BuildQuestRow(quest, contentRect);
            renderedQuestCount++;
        }

        emptyLabel.gameObject.SetActive(renderedQuestCount == 0);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        LayoutRebuilder.ForceRebuildLayoutImmediate(dialogRoot.GetComponent<RectTransform>());

        if (questScrollRect != null)
            questScrollRect.verticalNormalizedPosition = 1f;

        questListDirty = false;
    }

    private void CollectDisplayQuestModules()
    {
        displayQuestModules.Clear();

        AddKnownQuestModules(QuestModuleBase.KnownQuestModules);
        AddKnownQuestModules(QuestManager.ActiveQuestModules);

        QuestModuleBase[] sceneQuestModules = FindObjectsByType<QuestModuleBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        AddKnownQuestModules(sceneQuestModules);

        FetchQuestModule[] fetchQuestModules = FindObjectsByType<FetchQuestModule>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        AddKnownQuestModules(fetchQuestModules);

        WorldObject[] worldObjects = FindObjectsByType<WorldObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (WorldObject worldObject in worldObjects)
        {
            if (worldObject == null)
                continue;

            AddKnownQuestModule(worldObject.fetchQuestModule);
        }
    }

    private void AddKnownQuestModules(IEnumerable<QuestModuleBase> questModules)
    {
        if (questModules == null)
            return;

        foreach (QuestModuleBase questModule in questModules)
            AddKnownQuestModule(questModule);
    }

    private void AddKnownQuestModule(QuestModuleBase questModule)
    {
        if (questModule == null || displayQuestModules.Contains(questModule))
            return;

        displayQuestModules.Add(questModule);
    }

    private void BuildQuestRow(QuestModuleBase quest, Transform parent)
    {
        GameObject rowObject = CreateUIObject($"{quest.QuestTitle}Row", parent);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        bool expanded = expandedQuestModules.Contains(quest);
        float rowHeight = expanded ? 320f : 86f;
        if (expanded && CanShowAcceptQuestButton(quest))
            rowHeight += 48f;
        rowRect.sizeDelta = new Vector2(0f, rowHeight);

        LayoutElement rowLayoutElement = rowObject.AddComponent<LayoutElement>();
        rowLayoutElement.preferredHeight = rowHeight;
        rowLayoutElement.minHeight = rowHeight;
        rowLayoutElement.flexibleHeight = 0f;

        Image rowImage = rowObject.AddComponent<Image>();
        rowImage.color = GetQuestRowColor(quest.Status);

        VerticalLayoutGroup rowLayout = rowObject.AddComponent<VerticalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.UpperLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.padding = new RectOffset(14, 14, 10, 10);
        rowLayout.spacing = 8f;

        BuildQuestHeader(quest, rowObject.transform);

        if (expanded)
            BuildObjectiveList(quest, rowObject.transform);
    }

    private void BuildQuestHeader(QuestModuleBase quest, Transform parent)
    {
        Button headerButton = CreateButton("QuestHeader", parent, "", () => ToggleExpanded(quest));
        RectTransform headerRect = headerButton.GetComponent<RectTransform>();
        headerRect.sizeDelta = new Vector2(0f, 62f);

        LayoutElement headerLayoutElement = headerButton.gameObject.AddComponent<LayoutElement>();
        headerLayoutElement.preferredHeight = 62f;
        headerLayoutElement.minHeight = 62f;
        headerLayoutElement.flexibleHeight = 0f;

        HorizontalLayoutGroup headerLayout = headerButton.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = false;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;
        headerLayout.spacing = 10f;

        TextMeshProUGUI expandLabel = CreateLabel("ExpandIcon", headerButton.transform, 28f, new Color(0.98f, 0.93f, 0.78f, 1f), TextAlignmentOptions.Center);
        expandLabel.text = expandedQuestModules.Contains(quest) ? "v" : ">";
        expandLabel.rectTransform.sizeDelta = new Vector2(32f, 0f);

        TextMeshProUGUI titleLabel = CreateLabel("QuestTitle", headerButton.transform, 24f, new Color(0.98f, 0.93f, 0.78f, 1f), TextAlignmentOptions.MidlineLeft);
        titleLabel.text = quest.QuestTitle;
        titleLabel.rectTransform.sizeDelta = new Vector2(330f, 0f);

        TextMeshProUGUI timerLabel = CreateLabel("QuestTimer", headerButton.transform, 22f, new Color(0.84f, 0.95f, 1f, 1f), TextAlignmentOptions.MidlineRight);
        timerLabel.text = quest.HasCountdown ? FormatCountdown(quest) : FormatStatus(quest.Status);
        timerLabel.rectTransform.sizeDelta = new Vector2(180f, 0f);
        questStatusLabels[quest] = timerLabel;
    }

    private void BuildObjectiveList(QuestModuleBase quest, Transform parent)
    {
        if (!string.IsNullOrWhiteSpace(quest.QuestSummary))
        {
            TextMeshProUGUI summaryLabel = CreateLabel("QuestSummary", parent, 18f, new Color(0.9f, 0.85f, 0.72f, 0.86f), TextAlignmentOptions.Left);
            summaryLabel.text = quest.QuestSummary;
            summaryLabel.rectTransform.sizeDelta = new Vector2(0f, 30f);
            SetPreferredHeight(summaryLabel.gameObject, 30f);
        }

        foreach (QuestObjectiveSnapshot objective in quest.ObjectiveSnapshots)
        {
            TextMeshProUGUI objectiveLabel = CreateLabel("Objective", parent, 20f, new Color(0.94f, 0.91f, 0.82f, 1f), TextAlignmentOptions.Left);
            string marker = objective.IsCompleted ? "[x]" : "[ ]";
            string prefix = objective.IsCurrent && !objective.IsCompleted ? "> " : "  ";
            objectiveLabel.text = $"{prefix}{marker} {objective.Description}";
            objectiveLabel.rectTransform.sizeDelta = new Vector2(0f, 26f);
            SetPreferredHeight(objectiveLabel.gameObject, 26f);
        }

        if (CanShowAcceptQuestButton(quest))
            BuildAcceptQuestButton(quest, parent);
    }

    private void BuildAcceptQuestButton(QuestModuleBase quest, Transform parent)
    {
        Button acceptButton = CreateButton("AcceptQuestButton", parent, "Accept Quest", () => AcceptQuestFromDialog(quest));
        RectTransform acceptRect = acceptButton.GetComponent<RectTransform>();
        acceptRect.sizeDelta = new Vector2(0f, 42f);
        SetPreferredHeight(acceptButton.gameObject, 42f);
    }

    private void AcceptQuestFromDialog(QuestModuleBase quest)
    {
        if (quest == null)
            return;

        if (!TryGetQuestActorAndTarget(quest, out WorldObject actor, out WorldObject target))
            return;

        if (!IsQuestGiverNearby(actor, target))
        {
            BottomBanner.Show($"{target.DisplayName} is too far away.");
            questListDirty = true;
            RefreshQuestList();
            return;
        }

        GameInputRouter router = GameInputRouter.Instance;
        GameMode gameMode = router != null ? router.currentGameMode : GameMode.Explore;
        Vector3 hitPoint = target.transform.position;

        var activateContext = new ActivateContext(
            userIsInstigator: true,
            instigator: actor,
            target: target,
            gameMode: gameMode,
            hitPoint: hitPoint,
            promoteTarget: true);

        ActivateResult result = target.Activate(activateContext, new ActivateRequest(ActivateKind.StartQuest));
        if (!string.IsNullOrWhiteSpace(result.message))
        {
            if (result.kind == ActivateResultKind.Accepted)
                BottomBanner.Show(result.message);
            else
                BottomBanner.Show($"Quest not accepted: {result.message}");
        }

        questListDirty = true;
        RefreshQuestList();
    }

    private bool CanShowAcceptQuestButton(QuestModuleBase quest)
    {
        if (quest == null || !quest.CanStartFromQuestDialog)
            return false;

        return TryGetQuestActorAndTarget(quest, out WorldObject actor, out WorldObject target) &&
               IsQuestGiverNearby(actor, target);
    }

    private bool TryGetQuestActorAndTarget(QuestModuleBase quest, out WorldObject actor, out WorldObject target)
    {
        actor = GetCurrentControlledWorldObject();
        target = quest != null ? quest.QuestInteractionTarget : null;
        return actor != null && target != null;
    }

    private bool IsQuestGiverNearby(WorldObject actor, WorldObject target)
    {
        if (actor == null || target == null)
            return false;

        Vector3 delta = actor.transform.position - target.transform.position;
        return delta.sqrMagnitude <= questAcceptRange * questAcceptRange;
    }

    private static WorldObject GetCurrentControlledWorldObject()
    {
        GameInputRouter router = GameInputRouter.Instance;
        if (router != null && router.currentControlledWorldObject != null)
            return router.currentControlledWorldObject;

        Dir dir = Dir.Instance;
        return dir != null && dir.playerPack != null ? dir.playerPack.packLeader : null;
    }

    private void ToggleExpanded(QuestModuleBase quest)
    {
        if (expandedQuestModules.Contains(quest))
            expandedQuestModules.Remove(quest);
        else
            expandedQuestModules.Add(quest);

        questListDirty = true;
        RefreshQuestList();
    }

    private void UpdateQuestHeaderLabels()
    {
        foreach (KeyValuePair<QuestModuleBase, TextMeshProUGUI> row in questStatusLabels)
        {
            if (row.Key == null || row.Value == null)
                continue;

            row.Value.text = row.Key.HasCountdown ? FormatCountdown(row.Key) : FormatStatus(row.Key.Status);
        }
    }

    private static string FormatCountdown(QuestModuleBase quest)
    {
        int totalSeconds = Mathf.CeilToInt(quest.CountdownRemainingSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        string label = string.IsNullOrWhiteSpace(quest.CountdownLabel) ? "Time" : quest.CountdownLabel;
        return $"{label} {minutes:00}:{seconds:00}";
    }

    private static string FormatStatus(QuestRunStatus status)
    {
        return status switch
        {
            QuestRunStatus.Inactive => "Not started",
            QuestRunStatus.Running => "Active",
            QuestRunStatus.Succeeded => "Completed",
            QuestRunStatus.Failed => "Failed",
            QuestRunStatus.Cancelled => "Cancelled",
            _ => status.ToString()
        };
    }

    private static Color GetQuestRowColor(QuestRunStatus status)
    {
        return status switch
        {
            QuestRunStatus.Succeeded => new Color(0.095f, 0.15f, 0.105f, 0.92f),
            QuestRunStatus.Failed => new Color(0.17f, 0.08f, 0.065f, 0.92f),
            QuestRunStatus.Cancelled => new Color(0.12f, 0.105f, 0.105f, 0.92f),
            QuestRunStatus.Inactive => new Color(0.105f, 0.095f, 0.08f, 0.82f),
            _ => new Color(0.12f, 0.105f, 0.08f, 0.92f)
        };
    }

    private Sprite GetQuestFrameSprite()
    {
        if (questFrameSprite == null)
            questFrameSprite = LoadFrameSprite(questFrameResourcePath);

        return questFrameSprite;
    }

    private Sprite LoadFrameSprite(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        Sprite sprite = SpriteServer.SpriteResourceLookup(resourcePath);
        if (sprite != null)
            return sprite;

        Debug.LogWarning($"QuestJournalUI: could not load quest frame sprite at Resources/{resourcePath}.", this);
        return null;
    }

    private static bool ApplyFrame(Image image, Sprite frameSprite)
    {
        if (image == null || frameSprite == null)
            return false;

        image.sprite = frameSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = Color.white;
        return true;
    }

    private static Button CreateButton(string objectName, Transform parent, string text, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        Button button = buttonObject.AddComponent<Button>();
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.15f, 0.105f, 0.88f);
        button.targetGraphic = image;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.28f, 0.24f, 0.17f, 0.95f);
        colors.pressedColor = new Color(0.36f, 0.3f, 0.2f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        if (!string.IsNullOrEmpty(text))
        {
            TextMeshProUGUI label = CreateLabel("Label", buttonObject.transform, 24f, new Color(0.98f, 0.93f, 0.78f, 1f), TextAlignmentOptions.Center);
            label.text = text;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
        }

        return button;
    }

    private Button CreateInvisibleButton(string objectName, Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.001f);
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(onClick);

        QuestJournalTooltipTrigger trigger = buttonObject.AddComponent<QuestJournalTooltipTrigger>();
        trigger.Initialize(this, "Close");

        return button;
    }

    private void BuildTooltip(Transform parent)
    {
        GameObject tooltipObject = CreateUIObject("QuestJournalTooltip", parent);
        tooltipRect = tooltipObject.GetComponent<RectTransform>();
        tooltipRect.anchorMin = new Vector2(0f, 1f);
        tooltipRect.anchorMax = new Vector2(0f, 1f);
        tooltipRect.pivot = new Vector2(0f, 1f);
        tooltipRect.sizeDelta = new Vector2(96f, 42f);

        Image background = tooltipObject.AddComponent<Image>();
        background.color = new Color(0.97f, 0.91f, 0.72f, 0.97f);
        background.raycastTarget = false;

        tooltipLabel = CreateLabel("Label", tooltipObject.transform, 22f, new Color(0.08f, 0.06f, 0.03f, 1f), TextAlignmentOptions.Center);
        tooltipLabel.rectTransform.anchorMin = Vector2.zero;
        tooltipLabel.rectTransform.anchorMax = Vector2.one;
        tooltipLabel.rectTransform.offsetMin = new Vector2(12f, 8f);
        tooltipLabel.rectTransform.offsetMax = new Vector2(-12f, -8f);

        tooltipObject.SetActive(false);
    }

    public void ShowTooltip(string text, Vector2 screenPosition)
    {
        if (tooltipRect == null || tooltipLabel == null || string.IsNullOrWhiteSpace(text))
            return;

        tooltipLabel.text = text;
        tooltipRect.gameObject.SetActive(true);
        PositionTooltip(screenPosition);
        tooltipRect.SetAsLastSibling();
    }

    public void HideTooltip()
    {
        if (tooltipRect != null)
            tooltipRect.gameObject.SetActive(false);
    }

    private void PositionTooltip(Vector2 screenPosition)
    {
        if (tooltipRect == null || overlayCanvas == null)
            return;

        RectTransform canvasRect = overlayCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 localPoint))
            return;

        tooltipRect.anchoredPosition = new Vector2(
            localPoint.x + (canvasRect.rect.width * 0.5f) + 18f,
            localPoint.y - (canvasRect.rect.height * 0.5f) - 18f);
    }

    private static TextMeshProUGUI CreateLabel(string objectName, Transform parent, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject labelObject = CreateUIObject(objectName, parent);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    private static void SetPreferredHeight(GameObject uiObject, float height)
    {
        LayoutElement layoutElement = uiObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = uiObject.AddComponent<LayoutElement>();

        layoutElement.preferredHeight = height;
        layoutElement.minHeight = height;
        layoutElement.flexibleHeight = 0f;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject uiObject = new(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }
}

public sealed class QuestJournalTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    private QuestJournalUI owner;
    private string tooltipText;

    public void Initialize(QuestJournalUI owner, string tooltipText)
    {
        this.owner = owner;
        this.tooltipText = tooltipText;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.ShowTooltip(tooltipText, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        owner?.ShowTooltip(tooltipText, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.HideTooltip();
    }
}
