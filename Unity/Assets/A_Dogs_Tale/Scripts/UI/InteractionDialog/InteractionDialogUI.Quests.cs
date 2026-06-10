using System.Collections.Generic;
using DogGame;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public sealed partial class InteractionDialogUI : MonoBehaviour
{
    #region Fields

    private GameObject questListObject;

    private RectTransform questListContentRect;

    private ScrollRect questListScrollRect;

    private TextMeshProUGUI questListEmptyLabel;

    private readonly List<WorldObject> questTargetOptions = new();

    private readonly List<QuestModuleBase> interactionQuestModules = new();

    private readonly Dictionary<QuestModuleBase, TextMeshProUGUI> interactionQuestStatusLabels = new();

    private readonly HashSet<QuestModuleBase> expandedInteractionQuestModules = new();

    #endregion

    #region UI Construction

    private void BuildInteractionQuestList(Transform parent)
    {
        questListObject = CreateUIObject("InteractionQuestList", parent);
        RectTransform listRect = questListObject.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.5f, 1f);
        listRect.anchorMax = new Vector2(0.5f, 1f);
        listRect.pivot = new Vector2(0.5f, 0.5f);
        listRect.anchoredPosition = new Vector2(-305f, -690f);
        listRect.sizeDelta = new Vector2(470f, 300f);

        Image listBackground = questListObject.AddComponent<Image>();
        listBackground.color = new Color(0.09f, 0.065f, 0.035f, 0.58f);
        listBackground.raycastTarget = true;

        questListScrollRect = questListObject.AddComponent<ScrollRect>();
        questListScrollRect.horizontal = false;
        questListScrollRect.vertical = true;
        questListScrollRect.movementType = ScrollRect.MovementType.Clamped;
        questListScrollRect.scrollSensitivity = 24f;

        GameObject viewportObject = CreateUIObject("Viewport", questListObject.transform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(10f, 10f);
        viewportRect.offsetMax = new Vector2(-10f, -10f);

        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(0.055f, 0.047f, 0.036f, 0.45f);
        viewportImage.raycastTarget = true;
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUIObject("Content", viewportObject.transform);
        questListContentRect = contentObject.GetComponent<RectTransform>();
        questListContentRect.anchorMin = new Vector2(0f, 1f);
        questListContentRect.anchorMax = new Vector2(1f, 1f);
        questListContentRect.pivot = new Vector2(0.5f, 1f);
        questListContentRect.anchoredPosition = Vector2.zero;
        questListContentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 8f;
        layout.padding = new RectOffset(4, 4, 4, 4);

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        questListScrollRect.viewport = viewportRect;
        questListScrollRect.content = questListContentRect;

        GameObject emptyObject = CreateUIObject("EmptyLabel", questListObject.transform);
        RectTransform emptyRect = emptyObject.GetComponent<RectTransform>();
        emptyRect.anchorMin = Vector2.zero;
        emptyRect.anchorMax = Vector2.one;
        emptyRect.offsetMin = new Vector2(24f, 24f);
        emptyRect.offsetMax = new Vector2(-24f, -24f);

        questListEmptyLabel = emptyObject.AddComponent<TextMeshProUGUI>();
        questListEmptyLabel.text = "No quests";
        questListEmptyLabel.fontSize = 24f;
        questListEmptyLabel.color = new Color(0.86f, 0.8f, 0.66f, 0.78f);
        questListEmptyLabel.alignment = TextAlignmentOptions.Center;
        questListEmptyLabel.raycastTarget = false;

        questListObject.SetActive(false);
    }

    private void BuildInteractionQuestRow(QuestModuleBase quest, Transform parent)
    {
        if (quest == null)
            return;

        GameObject rowObject = CreateUIObject($"{quest.QuestTitle}Row", parent);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        bool expanded = expandedInteractionQuestModules.Contains(quest);
        float rowHeight = expanded ? 270f : 70f;
        if (expanded && CanShowInteractionQuestAcceptButton(quest))
            rowHeight += 42f;
        rowRect.sizeDelta = new Vector2(0f, rowHeight);

        LayoutElement rowLayoutElement = rowObject.AddComponent<LayoutElement>();
        rowLayoutElement.preferredHeight = rowHeight;
        rowLayoutElement.minHeight = rowHeight;
        rowLayoutElement.flexibleHeight = 0f;

        Image rowImage = rowObject.AddComponent<Image>();
        rowImage.color = GetInteractionQuestRowColor(quest.Status);
        rowImage.raycastTarget = true;

        VerticalLayoutGroup rowLayout = rowObject.AddComponent<VerticalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.UpperLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.padding = new RectOffset(10, 10, 8, 8);
        rowLayout.spacing = 6f;

        BuildInteractionQuestHeader(quest, rowObject.transform);

        if (expanded)
            BuildInteractionQuestObjectiveList(quest, rowObject.transform);
    }

    private void BuildInteractionQuestHeader(QuestModuleBase quest, Transform parent)
    {
        Button headerButton = CreateInteractionQuestButton("QuestHeader", parent, string.Empty, () => ToggleInteractionQuestExpanded(quest));
        RectTransform headerRect = headerButton.GetComponent<RectTransform>();
        headerRect.sizeDelta = new Vector2(0f, 52f);
        SetInteractionQuestPreferredHeight(headerButton.gameObject, 52f);

        HorizontalLayoutGroup headerLayout = headerButton.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = false;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;
        headerLayout.spacing = 8f;

        TextMeshProUGUI expandLabel = CreateInteractionQuestLabel("ExpandIcon", headerButton.transform, 24f, new Color(0.98f, 0.93f, 0.78f, 1f), TextAlignmentOptions.Center);
        expandLabel.text = expandedInteractionQuestModules.Contains(quest) ? "v" : ">";
        expandLabel.rectTransform.sizeDelta = new Vector2(26f, 0f);

        TextMeshProUGUI titleLabel = CreateInteractionQuestLabel("QuestTitle", headerButton.transform, 19f, new Color(0.98f, 0.93f, 0.78f, 1f), TextAlignmentOptions.MidlineLeft);
        titleLabel.text = quest.QuestTitle;
        titleLabel.rectTransform.sizeDelta = new Vector2(255f, 0f);
        titleLabel.overflowMode = TextOverflowModes.Ellipsis;

        TextMeshProUGUI timerLabel = CreateInteractionQuestLabel("QuestTimer", headerButton.transform, 17f, new Color(0.84f, 0.95f, 1f, 1f), TextAlignmentOptions.MidlineRight);
        timerLabel.text = quest.HasCountdown ? FormatInteractionQuestCountdown(quest) : FormatInteractionQuestStatus(quest.Status);
        timerLabel.rectTransform.sizeDelta = new Vector2(110f, 0f);
        interactionQuestStatusLabels[quest] = timerLabel;
    }

    private void BuildInteractionQuestObjectiveList(QuestModuleBase quest, Transform parent)
    {
        if (!string.IsNullOrWhiteSpace(quest.QuestSummary))
        {
            TextMeshProUGUI summaryLabel = CreateInteractionQuestLabel("QuestSummary", parent, 16f, new Color(0.9f, 0.85f, 0.72f, 0.86f), TextAlignmentOptions.Left);
            summaryLabel.text = quest.QuestSummary;
            summaryLabel.textWrappingMode = TextWrappingModes.Normal;
            summaryLabel.rectTransform.sizeDelta = new Vector2(0f, 42f);
            SetInteractionQuestPreferredHeight(summaryLabel.gameObject, 42f);
        }

        foreach (QuestObjectiveSnapshot objective in quest.ObjectiveSnapshots)
        {
            TextMeshProUGUI objectiveLabel = CreateInteractionQuestLabel("Objective", parent, 16f, new Color(0.94f, 0.91f, 0.82f, 1f), TextAlignmentOptions.Left);
            string marker = objective.IsCompleted ? "[x]" : "[ ]";
            string prefix = objective.IsCurrent && !objective.IsCompleted ? "> " : "  ";
            objectiveLabel.text = $"{prefix}{marker} {objective.Description}";
            objectiveLabel.textWrappingMode = TextWrappingModes.NoWrap;
            objectiveLabel.overflowMode = TextOverflowModes.Ellipsis;
            objectiveLabel.rectTransform.sizeDelta = new Vector2(0f, 24f);
            SetInteractionQuestPreferredHeight(objectiveLabel.gameObject, 24f);
        }

        if (CanShowInteractionQuestAcceptButton(quest))
            BuildInteractionQuestAcceptButton(quest, parent);
    }

    private void BuildInteractionQuestAcceptButton(QuestModuleBase quest, Transform parent)
    {
        Button acceptButton = CreateInteractionQuestButton("AcceptQuestButton", parent, "Accept Quest", () => AcceptInteractionQuestFromDialog(quest));
        RectTransform acceptRect = acceptButton.GetComponent<RectTransform>();
        acceptRect.sizeDelta = new Vector2(0f, 36f);
        SetInteractionQuestPreferredHeight(acceptButton.gameObject, 36f);
    }

    private Button CreateInteractionQuestButton(string objectName, Transform parent, string text, UnityEngine.Events.UnityAction onClick)
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
            TextMeshProUGUI label = CreateInteractionQuestLabel("Label", buttonObject.transform, 20f, new Color(0.98f, 0.93f, 0.78f, 1f), TextAlignmentOptions.Center);
            label.text = text;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
        }

        ConfigureTooltip(buttonObject, FormatTooltipText(string.IsNullOrEmpty(text) ? objectName : text));
        return button;
    }

    private static TextMeshProUGUI CreateInteractionQuestLabel(string objectName, Transform parent, float fontSize, Color color, TextAlignmentOptions alignment)
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

    private static void SetInteractionQuestPreferredHeight(GameObject uiObject, float height)
    {
        LayoutElement layoutElement = uiObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = uiObject.AddComponent<LayoutElement>();

        layoutElement.preferredHeight = height;
        layoutElement.minHeight = height;
        layoutElement.flexibleHeight = 0f;
    }

    #endregion

    #region UI Refresh

    private void RefreshQuestsView(bool forcePreviewRefresh = false)
    {
        SetPackControlsActive(false);
        SetQuestControlsActive(true);
        SetScentControlsActive(false);
        SetSocialControlsActive(false);
        SetPackIndicatorButtonsActive(false);
        SetItemsControlsActive(false);
        SetPreviewSlotActive(playerItemPreviewSlot, false);
        SetPreviewSlotActive(targetItemPreviewSlot, false);
        SetItemSelectionTypeLabelsActive(false);

        BuildPlayerAgentOptions();
        ApplyPendingSelection(playerAgentOptions, sharedState.PendingLeftAgentSelection, ref sharedState.SelectedPlayerAgentIndex);
        WorldObject leftMember = GetSelectedFromList(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex);
        RefreshInteractionQuestList();
        BuildQuestTargetOptions(leftMember);
        ApplyPendingSelection(questTargetOptions, sharedState.PendingRightAgentSelection, ref questsState.SelectedTargetIndex);
        WorldObject rightMember = GetSelectedFromList(questTargetOptions, ref questsState.SelectedTargetIndex);

        RefreshCircleAndHotspot(playerPreviewSlot, playerAgentOptions.Count, previousPlayerAgentButton, nextPlayerAgentButton);
        RefreshCircleAndHotspot(targetPreviewSlot, questTargetOptions.Count, previousTargetAgentButton, nextTargetAgentButton);
        RefreshCircleAndHotspot(playerItemPreviewSlot, 0, previousPlayerItemButton, nextPlayerItemButton);
        RefreshCircleAndHotspot(targetItemPreviewSlot, 0, previousTargetItemButton, nextTargetItemButton);

        SetLabelText(playerNameLabel, leftMember != null ? leftMember.DisplayName : string.Empty);
        SetLabelText(playerHeldItemLabel, string.Empty);
        SetLabelText(targetNameLabel, rightMember != null ? rightMember.DisplayName : string.Empty);
        SetLabelText(targetHeldItemLabel, string.Empty);
        SetBottomTargetAgentLabel(rightMember);

        if (forcePreviewRefresh || leftMember != questsState.DisplayedLeft)
            BuildPreviewClone(playerPreviewSlot, leftMember, "QuestLeft");
        if (forcePreviewRefresh || rightMember != questsState.DisplayedRight)
            BuildPreviewClone(targetPreviewSlot, rightMember, "QuestRight");

        BuildPreviewClone(playerItemPreviewSlot, null, "QuestLeftItem");
        BuildPreviewClone(targetItemPreviewSlot, null, "QuestRightItem");

        questsState.DisplayedLeft = leftMember;
        questsState.DisplayedRight = rightMember;
        sharedState.DisplayedPlayer = leftMember;
        itemsState.DisplayedPlayerItem = null;
        itemsState.DisplayedTarget = rightMember;
        itemsState.DisplayedTargetItem = null;
        ClearPendingSelections();
    }

    private void SetQuestControlsActive(bool active)
    {
        if (questListObject != null)
        {
            questListObject.SetActive(active);
            if (active)
                questListObject.transform.SetAsLastSibling();
        }
    }

    private void RefreshInteractionQuestList()
    {
        if (questListContentRect == null)
            return;

        if (!questsState.InteractionQuestListDirty)
        {
            UpdateInteractionQuestHeaderLabels();
            return;
        }

        QuestManager.RefreshActiveQuestModules();
        interactionQuestStatusLabels.Clear();

        ClearListContent(questListContentRect);

        CollectInteractionQuestModules();
        int renderedQuestCount = 0;
        foreach (QuestModuleBase quest in interactionQuestModules)
        {
            BuildInteractionQuestRow(quest, questListContentRect);
            renderedQuestCount++;
        }

        if (questListEmptyLabel != null)
            questListEmptyLabel.gameObject.SetActive(renderedQuestCount == 0);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(questListContentRect);

        if (questListScrollRect != null)
            questListScrollRect.verticalNormalizedPosition = 1f;

        questsState.InteractionQuestListDirty = false;
    }

    private void UpdateInteractionQuestHeaderLabels()
    {
        foreach (KeyValuePair<QuestModuleBase, TextMeshProUGUI> row in interactionQuestStatusLabels)
        {
            if (row.Key == null || row.Value == null)
                continue;

            row.Value.text = row.Key.HasCountdown ? FormatInteractionQuestCountdown(row.Key) : FormatInteractionQuestStatus(row.Key.Status);
        }
    }

    #endregion

    #region Selection State

    private void BuildQuestTargetOptions(WorldObject player)
    {
        WorldObject previousSelection = GetSelectedFromList(questTargetOptions, ref questsState.SelectedTargetIndex);
        questTargetOptions.Clear();

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (player == null || registry == null)
        {
            questsState.SelectedTargetIndex = 0;
            return;
        }

        float radiusSqr = tradePartnerSearchRadiusTiles * tradePartnerSearchRadiusTiles;
        Vector3 playerPosition = player.pos3d_map;

        foreach (WorldObject candidate in registry.GetAllObjects())
        {
            if (candidate == null || candidate == player || !candidate.gameObject.activeInHierarchy)
                continue;

            if (!CanUseAsQuestTarget(candidate))
                continue;

            Vector3 delta = candidate.pos3d_map - playerPosition;
            delta.y = 0f;
            float distanceSqr = delta.sqrMagnitude;
            if (distanceSqr > radiusSqr)
                continue;

            questTargetOptions.Add(candidate);
        }

        questTargetOptions.Sort((a, b) =>
        {
                float aDistanceSqr = GetPlanarDistanceSqr(playerPosition, a.pos3d_map);
                float bDistanceSqr = GetPlanarDistanceSqr(playerPosition, b.pos3d_map);
                int distanceComparison = aDistanceSqr.CompareTo(bDistanceSqr);
                if (distanceComparison != 0)
                return distanceComparison;

                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
        });

        KeepSelectedObject(questTargetOptions, previousSelection, ref questsState.SelectedTargetIndex);
    }

    private static Color GetInteractionQuestRowColor(QuestRunStatus status)
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

    private void CollectInteractionQuestModules()
    {
        interactionQuestModules.Clear();

        AddInteractionQuestModules(QuestModuleBase.KnownQuestModules);
        AddInteractionQuestModules(QuestManager.ActiveQuestModules);

        QuestModuleBase[] sceneQuestModules = FindObjectsByType<QuestModuleBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        AddInteractionQuestModules(sceneQuestModules);

        FetchQuestModule[] fetchQuestModules = FindObjectsByType<FetchQuestModule>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        AddInteractionQuestModules(fetchQuestModules);

        WorldObject[] worldObjects = FindObjectsByType<WorldObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (WorldObject worldObject in worldObjects)
        {
            if (worldObject != null)
                AddInteractionQuestModule(worldObject.fetchQuestModule);
        }
    }

    private bool CanShowInteractionQuestAcceptButton(QuestModuleBase quest)
    {
        if (quest == null || !quest.CanStartFromQuestDialog)
            return false;

        return TryGetInteractionQuestActorAndTarget(quest, out WorldObject actor, out WorldObject target) &&
        IsInteractionQuestGiverNearby(actor, target);
    }

    private bool TryGetInteractionQuestActorAndTarget(QuestModuleBase quest, out WorldObject actor, out WorldObject target)
    {
        actor = GetSelectedFromList(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex) ?? GetCurrentControlledWorldObjectForInteractionQuest();
        target = quest != null ? quest.QuestInteractionTarget : null;
        return actor != null && target != null;
    }

    private bool IsInteractionQuestGiverNearby(WorldObject actor, WorldObject target)
    {
        if (actor == null || target == null)
            return false;

        Vector3 delta = actor.transform.position - target.transform.position;
        return delta.sqrMagnitude <= tradePartnerSearchRadiusTiles * tradePartnerSearchRadiusTiles;
    }

    private static WorldObject GetCurrentControlledWorldObjectForInteractionQuest()
    {
        GameInputRouter router = GameInputRouter.Instance;
        if (router != null && router.currentControlledWorldObject != null)
            return router.currentControlledWorldObject;

        Dir dir = Dir.Instance;
        return dir != null && dir.playerPack != null ? dir.playerPack.packLeader : null;
    }

    private static string FormatInteractionQuestCountdown(QuestModuleBase quest)
    {
        int totalSeconds = Mathf.CeilToInt(quest.CountdownRemainingSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        string label = string.IsNullOrWhiteSpace(quest.CountdownLabel) ? "Time" : quest.CountdownLabel;
        return $"{label} {minutes:00}:{seconds:00}";
    }

    private static string FormatInteractionQuestStatus(QuestRunStatus status)
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

    private static bool CanUseAsQuestTarget(WorldObject candidate)
    {
        if (candidate == null || !CanUseAsSocialTarget(candidate))
            return false;

        return candidate.hasAnyQuestModule();
    }

    #endregion

    #region Input And Actions

    private void ToggleInteractionQuestExpanded(QuestModuleBase quest)
    {
        if (expandedInteractionQuestModules.Contains(quest))
            expandedInteractionQuestModules.Remove(quest);
        else
        expandedInteractionQuestModules.Add(quest);

        questsState.InteractionQuestListDirty = true;
        RefreshInteractionQuestList();
    }

    private void AcceptInteractionQuestFromDialog(QuestModuleBase quest)
    {
        if (quest == null)
            return;

        if (!TryGetInteractionQuestActorAndTarget(quest, out WorldObject actor, out WorldObject target))
            return;

        if (!IsInteractionQuestGiverNearby(actor, target))
        {
            BottomBanner.Show($"{target.DisplayName} is too far away.");
            questsState.InteractionQuestListDirty = true;
            RefreshInteractionQuestList();
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

        questsState.InteractionQuestListDirty = true;
        RefreshInteractionQuestList();
    }

    private void CycleQuestLeftSelection(int direction)
    {
        BuildPlayerAgentOptions();
        if (playerAgentOptions.Count <= 1)
            return;

        CycleSelection(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex, direction);
        questsState.SelectedTargetIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void CycleQuestRightSelection(int direction)
    {
        BuildPlayerAgentOptions();
        WorldObject player = GetSelectedFromList(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex);
        BuildQuestTargetOptions(player);
        if (questTargetOptions.Count <= 1)
            return;

        CycleSelection(questTargetOptions, ref questsState.SelectedTargetIndex, direction);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    #endregion

    #region Helpers

    private void AddInteractionQuestModules(IEnumerable<QuestModuleBase> questModules)
    {
        if (questModules == null)
            return;

        foreach (QuestModuleBase questModule in questModules)
            AddInteractionQuestModule(questModule);
    }

    private void AddInteractionQuestModule(QuestModuleBase questModule)
    {
        if (questModule == null || interactionQuestModules.Contains(questModule))
            return;

        interactionQuestModules.Add(questModule);
    }

    #endregion
}
