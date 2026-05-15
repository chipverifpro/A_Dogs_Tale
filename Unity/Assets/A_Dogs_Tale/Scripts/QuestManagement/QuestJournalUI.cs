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
    [SerializeField] private Vector2 dialogSize = new(720f, 640f);

    private readonly HashSet<QuestModuleBase> expandedQuestModules = new();

    private Canvas overlayCanvas;
    private GameObject dialogRoot;
    private RectTransform contentRect;
    private TextMeshProUGUI emptyLabel;
    private bool isOpen;

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

        if (isOpen)
            RefreshQuestList();
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
        RefreshQuestList();
    }

    public void Hide()
    {
        isOpen = false;

        if (dialogRoot != null)
            dialogRoot.SetActive(false);
    }

    private void OnActiveQuestsChanged()
    {
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
        dialogRect.anchorMin = new Vector2(1f, 0.5f);
        dialogRect.anchorMax = new Vector2(1f, 0.5f);
        dialogRect.pivot = new Vector2(1f, 0.5f);
        dialogRect.anchoredPosition = new Vector2(-72f, 0f);
        dialogRect.sizeDelta = dialogSize;

        Image dialogImage = dialogRoot.AddComponent<Image>();
        dialogImage.color = new Color(0.055f, 0.05f, 0.043f, 0.96f);

        BuildHeader(dialogRoot.transform);
        BuildBody(dialogRoot.transform);
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

        Button closeButton = CreateButton("CloseButton", parent, "X", Hide);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-20f, -18f);
        closeRect.sizeDelta = new Vector2(54f, 54f);
    }

    private void BuildBody(Transform parent)
    {
        GameObject bodyObject = CreateUIObject("Body", parent);
        RectTransform bodyRect = bodyObject.GetComponent<RectTransform>();
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(28f, 28f);
        bodyRect.offsetMax = new Vector2(-28f, -86f);

        Image bodyImage = bodyObject.AddComponent<Image>();
        bodyImage.color = new Color(0.02f, 0.018f, 0.014f, 0.32f);

        GameObject scrollObject = CreateUIObject("ScrollView", bodyObject.transform);
        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(14f, 14f);
        scrollRect.offsetMax = new Vector2(-14f, -14f);

        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        GameObject viewportObject = CreateUIObject("Viewport", scrollObject.transform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = Color.clear;

        Mask viewportMask = viewportObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

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

        scroll.viewport = viewportRect;
        scroll.content = contentRect;

        GameObject emptyObject = CreateUIObject("EmptyLabel", bodyObject.transform);
        RectTransform emptyRect = emptyObject.GetComponent<RectTransform>();
        emptyRect.anchorMin = Vector2.zero;
        emptyRect.anchorMax = Vector2.one;
        emptyRect.offsetMin = new Vector2(32f, 32f);
        emptyRect.offsetMax = new Vector2(-32f, -32f);

        emptyLabel = emptyObject.AddComponent<TextMeshProUGUI>();
        emptyLabel.text = "No active quests";
        emptyLabel.fontSize = 26f;
        emptyLabel.color = new Color(0.86f, 0.8f, 0.66f, 0.78f);
        emptyLabel.alignment = TextAlignmentOptions.Center;
    }

    private void RefreshQuestList()
    {
        for (int i = contentRect.childCount - 1; i >= 0; i--)
            Destroy(contentRect.GetChild(i).gameObject);

        IReadOnlyList<QuestModuleBase> activeQuests = QuestManager.ActiveQuestModules;
        int renderedQuestCount = 0;

        foreach (QuestModuleBase quest in activeQuests)
        {
            if (quest == null || !quest.IsRunning)
                continue;

            BuildQuestRow(quest, contentRect);
            renderedQuestCount++;
        }

        emptyLabel.gameObject.SetActive(renderedQuestCount == 0);
    }

    private void BuildQuestRow(QuestModuleBase quest, Transform parent)
    {
        GameObject rowObject = CreateUIObject($"{quest.QuestTitle}Row", parent);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, expandedQuestModules.Contains(quest) ? 320f : 86f);

        Image rowImage = rowObject.AddComponent<Image>();
        rowImage.color = new Color(0.12f, 0.105f, 0.08f, 0.92f);

        VerticalLayoutGroup rowLayout = rowObject.AddComponent<VerticalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.UpperLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.padding = new RectOffset(14, 14, 10, 10);
        rowLayout.spacing = 8f;

        BuildQuestHeader(quest, rowObject.transform);

        if (expandedQuestModules.Contains(quest))
            BuildObjectiveList(quest, rowObject.transform);
    }

    private void BuildQuestHeader(QuestModuleBase quest, Transform parent)
    {
        Button headerButton = CreateButton("QuestHeader", parent, "", () => ToggleExpanded(quest));
        RectTransform headerRect = headerButton.GetComponent<RectTransform>();
        headerRect.sizeDelta = new Vector2(0f, 62f);

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
        timerLabel.text = quest.HasCountdown ? FormatCountdown(quest) : "";
        timerLabel.rectTransform.sizeDelta = new Vector2(180f, 0f);
    }

    private void BuildObjectiveList(QuestModuleBase quest, Transform parent)
    {
        if (!string.IsNullOrWhiteSpace(quest.QuestSummary))
        {
            TextMeshProUGUI summaryLabel = CreateLabel("QuestSummary", parent, 18f, new Color(0.9f, 0.85f, 0.72f, 0.86f), TextAlignmentOptions.Left);
            summaryLabel.text = quest.QuestSummary;
            summaryLabel.rectTransform.sizeDelta = new Vector2(0f, 30f);
        }

        foreach (QuestObjectiveSnapshot objective in quest.ObjectiveSnapshots)
        {
            TextMeshProUGUI objectiveLabel = CreateLabel("Objective", parent, 20f, new Color(0.94f, 0.91f, 0.82f, 1f), TextAlignmentOptions.Left);
            string marker = objective.IsCompleted ? "[x]" : "[ ]";
            string prefix = objective.IsCurrent && !objective.IsCompleted ? "> " : "  ";
            objectiveLabel.text = $"{prefix}{marker} {objective.Description}";
            objectiveLabel.rectTransform.sizeDelta = new Vector2(0f, 26f);
        }
    }

    private void ToggleExpanded(QuestModuleBase quest)
    {
        if (expandedQuestModules.Contains(quest))
            expandedQuestModules.Remove(quest);
        else
            expandedQuestModules.Add(quest);

        RefreshQuestList();
    }

    private static string FormatCountdown(QuestModuleBase quest)
    {
        int totalSeconds = Mathf.CeilToInt(quest.CountdownRemainingSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        string label = string.IsNullOrWhiteSpace(quest.CountdownLabel) ? "Time" : quest.CountdownLabel;
        return $"{label} {minutes:00}:{seconds:00}";
    }

    private static Button CreateButton(string objectName, Transform parent, string text, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        Button button = buttonObject.AddComponent<Button>();
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.15f, 0.105f, 0.88f);
        button.targetGraphic = image;
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
