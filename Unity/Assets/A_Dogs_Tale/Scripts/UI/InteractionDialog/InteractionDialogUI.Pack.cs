using System.Collections.Generic;
using DogGame;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public sealed partial class InteractionDialogUI : MonoBehaviour
{
    #region Fields

    private Button setLeaderButton;

    private Button joinPackButton;

    private Button leavePackButton;

    private GameObject packActionPanelObject;

    private GameObject packMemberListObject;

    private RectTransform packMemberListViewportRect;

    private RectTransform packMemberListContentRect;

    private ScrollRect packMemberScrollRect;

    private float packMemberListDragStartLocalY;

    private float packMemberListDragStartContentY;

    private int selectedPackLeftIndex;

    private int selectedPackRightIndex = 1;

    private WorldObject displayedPackLeft;

    private WorldObject displayedPackRight;

    private readonly List<WorldObject> packMemberOptions = new();

    private readonly List<WorldObject> packRightOptions = new();

    private readonly List<Image> packMemberListBackgrounds = new();

    #endregion

    #region Nested Types

    private enum PackButtonKind
    {
        Behavior,
        Membership,
        Formation
    }

    #endregion

    #region UI Construction

    private void BuildPackActionButtons(Transform parent)
    {
        packActionPanelObject = CreateUIObject("PackActionPanel", parent);
        RectTransform actionPanelRect = packActionPanelObject.GetComponent<RectTransform>();
        actionPanelRect.anchorMin = new Vector2(0.5f, 1f);
        actionPanelRect.anchorMax = new Vector2(0.5f, 1f);
        actionPanelRect.pivot = new Vector2(0.5f, 0.5f);
        actionPanelRect.anchoredPosition = new Vector2(270f, -690f);
        actionPanelRect.sizeDelta = new Vector2(690f, 300f);

        VerticalLayoutGroup layout = packActionPanelObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 8f;
        layout.padding = new RectOffset(0, 0, 0, 0);

        Transform behaviorRow = CreateActionButtonRow("PackBehaviorActionRow", packActionPanelObject.transform, 86f);
        Transform membershipRow = CreateActionButtonRow("PackMembershipActionRow", packActionPanelObject.transform, 86f);
        Transform formationRow = CreateActionButtonRow("PackFormationActionRow", packActionPanelObject.transform, 86f);

        CreatePackActionButton(behaviorRow, "TakeControlButton", PackButtonKind.Behavior, 0, "TAKE CONTROL", () => OnPackBehaviorClicked(AgentDecisionType.Player));
        CreatePackActionButton(behaviorRow, "RegroupButton", PackButtonKind.Behavior, 1, "REGROUP", () => OnPackBehaviorClicked(AgentDecisionType.Follower));
        CreatePackActionButton(behaviorRow, "WaitHereButton", PackButtonKind.Behavior, 3, "WAIT HERE", () => OnPackBehaviorClicked(AgentDecisionType.Immobile));
        CreatePackActionButton(behaviorRow, "PatrolRoomButton", PackButtonKind.Behavior, 4, "PATROL ROOM", () => OnPackBehaviorClicked(AgentDecisionType.Wanderer));
        CreatePackActionButton(behaviorRow, "ExploreButton", PackButtonKind.Behavior, 2, "EXPLORE", () => OnPackBehaviorClicked(AgentDecisionType.Explorer));
        CreatePackActionButton(behaviorRow, "AIButton", PackButtonKind.Behavior, 5, "AI", () => OnPackBehaviorClicked(AgentDecisionType.TaskFollower));

        setLeaderButton = CreatePackActionButton(membershipRow, "SetLeaderButton", PackButtonKind.Membership, 4, "SET LEADER", OnSetPackLeaderClicked);
        joinPackButton = CreatePackActionButton(membershipRow, "JoinPackButton", PackButtonKind.Membership, 0, "JOIN", OnJoinPackClicked, false);
        leavePackButton = CreatePackActionButton(membershipRow, "LeavePackButton", PackButtonKind.Membership, 2, "LEAVE", OnLeavePackClicked);

        CreatePackActionButton(formationRow, "AbreastFormationButton", PackButtonKind.Formation, 6, "ABREAST", () => OnPackFormationClicked(FormationsEnum.LineAbreast));
        CreatePackActionButton(formationRow, "TwoColumnsFormationButton", PackButtonKind.Formation, 10, "TWO COLUMNS", () => OnPackFormationClicked(FormationsEnum.TwoColums));
        CreatePackActionButton(formationRow, "WedgeFormationButton", PackButtonKind.Formation, 12, "WEDGE", () => OnPackFormationClicked(FormationsEnum.Wedge));
        CreatePackActionButton(formationRow, "CircleFormationButton", PackButtonKind.Formation, 14, "CIRCLE", () => OnPackFormationClicked(FormationsEnum.Circle));
        CreatePackActionButton(formationRow, "FollowFormationButton", PackButtonKind.Formation, 16, "FOLLOW", () => OnPackFormationClicked(FormationsEnum.SingleFile));
        CreatePackActionButton(formationRow, "ClusterFormationButton", PackButtonKind.Formation, -1, "CLUSTER", null, false);

        packActionPanelObject.SetActive(false);
    }

    private void BuildPackMemberList(Transform parent)
    {
        packMemberListObject = CreateUIObject("PackMemberList", parent);
        RectTransform listRect = packMemberListObject.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.5f, 1f);
        listRect.anchorMax = new Vector2(0.5f, 1f);
        listRect.pivot = new Vector2(0.5f, 0.5f);
        listRect.anchoredPosition = new Vector2(-425f, -690f);
        listRect.sizeDelta = new Vector2(470f, 300f);

        Image listBackground = packMemberListObject.AddComponent<Image>();
        listBackground.color = new Color(0.09f, 0.065f, 0.035f, 0.58f);
        listBackground.raycastTarget = true;

        GameObject viewportObject = CreateUIObject("Viewport", packMemberListObject.transform);
        packMemberListViewportRect = viewportObject.GetComponent<RectTransform>();
        packMemberListViewportRect.anchorMin = Vector2.zero;
        packMemberListViewportRect.anchorMax = Vector2.one;
        packMemberListViewportRect.offsetMin = new Vector2(10f, 10f);
        packMemberListViewportRect.offsetMax = new Vector2(-10f, -10f);

        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUIObject("Content", viewportObject.transform);
        packMemberListContentRect = contentObject.GetComponent<RectTransform>();
        packMemberListContentRect.anchorMin = new Vector2(0f, 1f);
        packMemberListContentRect.anchorMax = new Vector2(1f, 1f);
        packMemberListContentRect.pivot = new Vector2(0.5f, 1f);
        packMemberListContentRect.anchoredPosition = Vector2.zero;
        packMemberListContentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = PackMemberListRowSpacing;
        int padding = Mathf.RoundToInt(PackMemberListPadding);
        layout.padding = new RectOffset(padding, padding, padding, padding);

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        packMemberScrollRect = packMemberListObject.AddComponent<ScrollRect>();
        packMemberScrollRect.content = packMemberListContentRect;
        packMemberScrollRect.viewport = packMemberListViewportRect;
        packMemberScrollRect.horizontal = false;
        packMemberScrollRect.vertical = true;
        packMemberScrollRect.movementType = ScrollRect.MovementType.Clamped;
        packMemberScrollRect.scrollSensitivity = 24f;

        GameObject hitAreaObject = CreateUIObject("HitArea", packMemberListObject.transform);
        RectTransform hitAreaRect = hitAreaObject.GetComponent<RectTransform>();
        hitAreaRect.anchorMin = Vector2.zero;
        hitAreaRect.anchorMax = Vector2.one;
        hitAreaRect.offsetMin = Vector2.zero;
        hitAreaRect.offsetMax = Vector2.zero;

        Image hitAreaImage = hitAreaObject.AddComponent<Image>();
        hitAreaImage.color = new Color(1f, 1f, 1f, 0.001f);
        hitAreaImage.raycastTarget = true;

        InteractionDialogPackMemberListHitArea hitArea = hitAreaObject.AddComponent<InteractionDialogPackMemberListHitArea>();
        hitArea.Initialize(this);

        packMemberListObject.SetActive(false);
    }

    private Button CreatePackActionButton(
        Transform parent,
        string objectName,
        PackButtonKind kind,
        int spriteIndex,
        string fallbackText,
        UnityEngine.Events.UnityAction clickHandler,
        bool implemented = true)
    {
        Sprite sprite = GetPackActionSprite(kind, spriteIndex);
        Button button = CreateSpriteButton(objectName, parent, sprite, fallbackText, clickHandler ?? OnUnimplementedPackActionClicked);
        ConfigureActionButtonSize(button, sprite, 78f);
        button.interactable = implemented;

        Image image = button.targetGraphic as Image;
        if (image != null && !implemented)
            image.color = new Color(image.color.r, image.color.g, image.color.b, 0.36f);

        return button;
    }

    private void BuildPackIndicatorButtons(Transform parent)
    {
        playerPackIndicatorButton = CreatePackIndicatorButton(parent, "PlayerPackIndicatorButton", new Vector2(-334f, -320f), OnPlayerPackIndicatorClicked);
        targetPackIndicatorButton = CreatePackIndicatorButton(parent, "TargetPackIndicatorButton", new Vector2(320f, -320f), OnTargetPackIndicatorClicked);
        SetPackIndicatorButtonsActive(false);
    }

    private Button CreatePackIndicatorButton(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        UnityEngine.Events.UnityAction clickHandler)
    {
        Button button = CreateInvisibleButton(objectName, parent, clickHandler);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(84f, 84f);
        return button;
    }

    private void CreatePackMemberListRow(WorldObject member, int index)
    {
        GameObject rowObject = CreateUIObject($"PackMemberRow_{index}", packMemberListContentRect);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, PackMemberListRowHeight);

        LayoutElement layoutElement = rowObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = PackMemberListRowHeight;
        layoutElement.minHeight = PackMemberListRowHeight;

        Image background = rowObject.AddComponent<Image>();
        background.color = GetPackMemberListRowColor(index == selectedPackLeftIndex);
        background.raycastTarget = true;

        int capturedIndex = index;
        InteractionDialogPackMemberRowClickTrigger clickTrigger = rowObject.AddComponent<InteractionDialogPackMemberRowClickTrigger>();
        clickTrigger.Initialize(this, capturedIndex);

        GameObject labelObject = CreateUIObject("Label", rowObject.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 2f);
        labelRect.offsetMax = new Vector2(-12f, -2f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = GetPackMemberListLabelText(member);
        label.fontSize = 22f;
        label.color = new Color(1f, 0.88f, 0.58f, 1f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        packMemberListBackgrounds.Add(background);
        ConfigureTooltip(rowObject, $"Select {member.DisplayName}");
    }

    private void CreatePackMemberListPlaceholder(string text)
    {
        GameObject rowObject = CreateUIObject("PackMemberListPlaceholder", packMemberListContentRect);
        LayoutElement layoutElement = rowObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = PackMemberListRowHeight;
        layoutElement.minHeight = PackMemberListRowHeight;

        TextMeshProUGUI label = rowObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 22f;
        label.color = new Color(1f, 0.88f, 0.58f, 0.68f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
    }

    #endregion

    #region UI Refresh

    private void RefreshPackView(bool forcePreviewRefresh = false)
    {
        SetItemsControlsActive(false);
        SetQuestControlsActive(false);
        SetScentControlsActive(false);
        SetSocialControlsActive(false);
        SetPackControlsActive(true);
        SetPackIndicatorButtonsActive(true);
        SetItemSelectionTypeLabelsActive(false);

        BuildPackMemberOptions();
        ApplyPendingSelection(packMemberOptions, pendingLeftAgentSelection, ref selectedPackLeftIndex);
        WorldObject leftMember = GetSelectedFromList(packMemberOptions, ref selectedPackLeftIndex);
        RefreshPackMemberList();
        BuildPackRightOptions(leftMember);
        ApplyPendingSelection(packRightOptions, pendingRightAgentSelection, ref selectedPackRightIndex);
        WorldObject rightMember = GetSelectedFromList(packRightOptions, ref selectedPackRightIndex);

        RefreshCircleAndHotspot(playerPreviewSlot, packMemberOptions.Count, previousPlayerAgentButton, nextPlayerAgentButton);
        RefreshCircleAndHotspot(targetPreviewSlot, packRightOptions.Count, previousTargetAgentButton, nextTargetAgentButton);
        RefreshCircleAndHotspot(playerItemPreviewSlot, 0, previousPlayerItemButton, nextPlayerItemButton);
        RefreshCircleAndHotspot(targetItemPreviewSlot, 0, previousTargetItemButton, nextTargetItemButton);
        RefreshPackIndicatorSlot(playerItemPreviewSlot, leftMember);
        RefreshPackIndicatorSlot(targetItemPreviewSlot, rightMember);
        RefreshPackIndicatorButton(playerPackIndicatorButton, leftMember);
        RefreshPackIndicatorButton(targetPackIndicatorButton, rightMember);
        RefreshPackMembershipButtons(rightMember);

        SetLabelText(playerNameLabel, leftMember != null ? leftMember.DisplayName : string.Empty);
        SetLabelText(playerHeldItemLabel, string.Empty);
        SetLabelText(targetNameLabel, rightMember != null ? rightMember.DisplayName : string.Empty);
        SetLabelText(targetHeldItemLabel, string.Empty);

        if (forcePreviewRefresh || leftMember != displayedPackLeft)
            BuildPreviewClone(playerPreviewSlot, leftMember, "PackLeft");
        if (forcePreviewRefresh || rightMember != displayedPackRight)
            BuildPreviewClone(targetPreviewSlot, rightMember, "PackRight");

        BuildPreviewClone(playerItemPreviewSlot, null, "PackLeftItem");
        BuildPreviewClone(targetItemPreviewSlot, null, "PackRightItem");

        displayedPackLeft = leftMember;
        displayedPackRight = rightMember;
        displayedPlayer = leftMember;
        displayedPlayerItem = null;
        displayedTarget = rightMember;
        displayedTargetItem = null;
        ClearPendingSelections();
    }

    private void SetPackControlsActive(bool active)
    {
        if (packActionPanelObject != null)
            packActionPanelObject.SetActive(active);
        if (packMemberListObject != null)
        {
            packMemberListObject.SetActive(active);
            if (active)
                packMemberListObject.transform.SetAsLastSibling();
        }
    }

    private void RefreshPackMemberList()
    {
        if (packMemberListContentRect == null)
            return;

        ClearPackMemberListRows();

        if (packMemberOptions.Count <= 0)
        {
            CreatePackMemberListPlaceholder("No pack members");
            return;
        }

        for (int i = 0; i < packMemberOptions.Count; i++)
            CreatePackMemberListRow(packMemberOptions[i], i);

        RefreshPackMemberListHighlights();
        LayoutRebuilder.ForceRebuildLayoutImmediate(packMemberListContentRect);
        ScrollPackMemberListToSelection();
    }

    private void ClearPackMemberListRows()
    {
        packMemberListBackgrounds.Clear();
        ClearListContent(packMemberListContentRect);
    }

    private void RefreshPackMemberListHighlights()
    {
        for (int i = 0; i < packMemberListBackgrounds.Count; i++)
            packMemberListBackgrounds[i].color = GetPackMemberListRowColor(i == selectedPackLeftIndex);
    }

    private void ScrollPackMemberListToSelection()
    {
        ScrollListToSelectionNormalized(packMemberScrollRect, selectedPackLeftIndex, packMemberOptions.Count);
    }

    internal void ScrollPackMemberList(Vector2 scrollDelta)
    {
        if (packMemberListContentRect == null || packMemberScrollRect == null)
            return;

        float currentOffset = packMemberListContentRect.anchoredPosition.y;
        SetPackMemberListScrollOffset(currentOffset - scrollDelta.y * packMemberScrollRect.scrollSensitivity);
    }

    private void SetPackMemberListScrollOffset(float offsetY)
    {
        SetFixedRowListScrollOffset(
            packMemberScrollRect,
            packMemberListContentRect,
            packMemberListViewportRect,
            packMemberOptions.Count,
            offsetY);
    }

    private void SetPackIndicatorButtonsActive(bool active)
    {
        if (playerPackIndicatorButton != null)
            playerPackIndicatorButton.gameObject.SetActive(active);
        if (targetPackIndicatorButton != null)
            targetPackIndicatorButton.gameObject.SetActive(active);
    }

    private void RefreshPackIndicatorSlot(InteractionDialogPreviewSlot slot, WorldObject member)
    {
        if (slot == null)
            return;

        if (slot.Image != null)
            slot.Image.gameObject.SetActive(false);

        if (slot.CircleImage == null)
            return;

        bool hasMember = member != null;
        slot.CircleImage.gameObject.SetActive(hasMember);
        if (!hasMember)
            return;

        slot.CircleImage.sprite = GetPackIndicatorSprite(member);
        slot.CircleImage.preserveAspect = true;
        slot.CircleImage.color = Color.white;
        slot.CircleImage.rectTransform.sizeDelta = slot.CircleSize;
    }

    private static void RefreshPackIndicatorButton(Button button, WorldObject member)
    {
        if (button != null)
            button.interactable = member != null && !IsPlayerPackLeader(member);
    }

    private void RefreshPackMembershipButtons(WorldObject member)
    {
        bool inPlayerPack = IsInPlayerPack(member);
        bool isLeader = IsPlayerPackLeader(member);

        SetPackActionButtonInteractable(setLeaderButton, inPlayerPack && !isLeader);
        SetPackActionButtonInteractable(joinPackButton, member != null && !inPlayerPack);
        SetPackActionButtonInteractable(leavePackButton, inPlayerPack);
    }

    private static void SetPackActionButtonInteractable(Button button, bool interactable)
    {
        if (button == null)
            return;

        button.interactable = interactable;

        Image image = button.targetGraphic as Image;
        if (image != null)
            image.color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.36f);
    }

    #endregion

    #region Selection State

    private void BuildPackMemberOptions()
    {
        WorldObject previousLeft = GetSelectedFromList(packMemberOptions, ref selectedPackLeftIndex);
        packMemberOptions.Clear();

        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        if (playerPack != null && playerPack.packAgentList != null)
        {
            for (int i = 0; i < playerPack.packAgentList.Count; i++)
            {
                WorldObject agent = playerPack.packAgentList[i];
                if (agent != null && agent.gameObject.activeInHierarchy)
                    packMemberOptions.Add(agent);
            }
        }

        Dir dir = Dir.Instance;
        WorldObject packLeader = dir != null && dir.playerPack != null ? dir.playerPack.packLeader : null;
        if (packMemberOptions.Count == 0 && packLeader != null)
            packMemberOptions.Add(packLeader);

        KeepSelectedObject(packMemberOptions, previousLeft, ref selectedPackLeftIndex);
    }

    private void BuildPackRightOptions(WorldObject leftMember)
    {
        WorldObject previousRight = GetSelectedFromList(packRightOptions, ref selectedPackRightIndex);
        packRightOptions.Clear();

        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        if (playerPack != null && playerPack.packAgentList != null)
        {
            for (int i = 0; i < playerPack.packAgentList.Count; i++)
            {
                WorldObject agent = playerPack.packAgentList[i];
                if (agent != null && agent != leftMember && agent.gameObject.activeInHierarchy)
                    packRightOptions.Add(agent);
            }
        }

        AddNearbyPackRightAgents(leftMember);
        KeepSelectedObject(packRightOptions, previousRight, ref selectedPackRightIndex);
    }

    private static Sprite GetPackActionSprite(PackButtonKind kind, int spriteIndex)
    {
        string spriteSheet = kind == PackButtonKind.Behavior
        ? "Sprites/MoveModes_B"
        : "Sprites/PackFormationsSprites_C";

        return SpriteServer.SpriteSheetLookup(spriteSheet, spriteIndex);
    }

    private static Color GetPackMemberListRowColor(bool selected)
    {
        return selected
        ? new Color(0.95f, 0.54f, 0.12f, 0.86f)
        : new Color(0.20f, 0.13f, 0.065f, 0.78f);
    }

    private static Sprite GetPackIndicatorSprite(WorldObject member)
    {
        int spriteIndex;
        if (IsPlayerPackLeader(member))
            spriteIndex = 18;
        else if (IsInPlayerPack(member))
        spriteIndex = 19;
        else
        spriteIndex = 20;

        return SpriteServer.SpriteSheetLookup("Sprites/PackFormationsSprites_C", spriteIndex);
    }

    private static string GetPackMemberListLabelText(WorldObject member)
    {
        if (member == null)
            return string.Empty;

        return IsPlayerPackLeader(member)
        ? $"{member.DisplayName}  Leader"
        : member.DisplayName;
    }

    private int GetPackMemberListRowIndexAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        return GetFixedRowListRowIndexAtScreenPosition(
            packMemberOptions.Count,
            packMemberListViewportRect,
            packMemberListContentRect,
            screenPosition,
            eventCamera);
    }

    private bool TryGetPackMemberListLocalPoint(Vector2 screenPosition, Camera eventCamera, out Vector2 localPoint)
    {
        return TryGetListLocalPoint(packMemberListViewportRect, screenPosition, eventCamera, out localPoint);
    }

    private float GetPackMemberListMaxScrollOffset()
    {
        return GetFixedRowListMaxScrollOffset(packMemberListViewportRect, packMemberOptions.Count);
    }

    private void AddNearbyPackRightAgents(WorldObject leftMember)
    {
        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (leftMember == null || registry == null)
            return;

        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        float radius = tradePartnerSearchRadiusTiles * socialNearbyRadiusMultiplier;
        float radiusSqr = radius * radius;
        Vector3 leftPosition = leftMember.pos3d_map;

        foreach (WorldObject candidate in registry.GetAllObjects())
        {
            if (candidate == null || candidate == leftMember || !candidate.gameObject.activeInHierarchy)
                continue;

            if (!CanUseAsSocialTarget(candidate))
                continue;

            if (candidate.packMemberModule != null && candidate.packMemberModule.currentPack == playerPack)
                continue;

            Vector3 delta = candidate.pos3d_map - leftPosition;
            delta.y = 0f;
            if (delta.sqrMagnitude > radiusSqr)
                continue;

            if (!packRightOptions.Contains(candidate))
                packRightOptions.Add(candidate);
        }

        packRightOptions.Sort((a, b) =>
        {
                bool aInPlayerPack = IsInPlayerPack(a);
                bool bInPlayerPack = IsInPlayerPack(b);
                if (aInPlayerPack != bInPlayerPack)
                return aInPlayerPack ? -1 : 1;

                float aDistanceSqr = GetPlanarDistanceSqr(leftPosition, a.pos3d_map);
                float bDistanceSqr = GetPlanarDistanceSqr(leftPosition, b.pos3d_map);
                int distanceComparison = aDistanceSqr.CompareTo(bDistanceSqr);
                if (distanceComparison != 0)
                return distanceComparison;

                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
        });
    }

    private void EnsurePackRightSelection(WorldObject leftMember)
    {
        if (packMemberOptions.Count <= 1)
        {
            selectedPackRightIndex = 0;
            return;
        }

        selectedPackLeftIndex = Mathf.Clamp(selectedPackLeftIndex, 0, packMemberOptions.Count - 1);
        selectedPackRightIndex = Mathf.Clamp(selectedPackRightIndex, 0, packMemberOptions.Count - 1);
        if (GetSelectedFromList(packMemberOptions, ref selectedPackRightIndex) != leftMember)
            return;

        selectedPackRightIndex = FindNextPackMemberIndex(selectedPackRightIndex, 1, selectedPackLeftIndex);
    }

    private int FindNextPackMemberIndex(int currentIndex, int direction, int skipIndex)
    {
        if (packMemberOptions.Count <= 0)
            return 0;

        if (packMemberOptions.Count == 1)
            return 0;

        int nextIndex = currentIndex;
        for (int i = 0; i < packMemberOptions.Count; i++)
        {
            nextIndex = (nextIndex + direction + packMemberOptions.Count) % packMemberOptions.Count;
            if (nextIndex != skipIndex)
                return nextIndex;
        }

        return currentIndex;
    }

    private static bool IsInPlayerPack(WorldObject candidate)
    {
        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        return candidate != null &&
        candidate.packMemberModule != null &&
        candidate.packMemberModule.currentPack == playerPack;
    }

    private static bool IsPlayerPackLeader(WorldObject candidate)
    {
        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        return candidate != null && playerPack != null && playerPack.packLeader == candidate;
    }

    private WorldObject GetSelectedPackLeftMember()
    {
        if (displayedPackLeft != null)
            return displayedPackLeft;

        BuildPackMemberOptions();
        return GetSelectedFromList(packMemberOptions, ref selectedPackLeftIndex);
    }

    private WorldObject GetSelectedPackRightMember()
    {
        if (displayedPackRight != null)
            return displayedPackRight;

        BuildPackMemberOptions();
        WorldObject leftMember = GetSelectedFromList(packMemberOptions, ref selectedPackLeftIndex);
        BuildPackRightOptions(leftMember);
        return GetSelectedFromList(packRightOptions, ref selectedPackRightIndex);
    }

    private static string GetPackBehaviorDisplayName(AgentDecisionType decisionType)
    {
        return decisionType switch
        {
            AgentDecisionType.Player => "Take Control",
            AgentDecisionType.Follower => "Regroup",
            AgentDecisionType.Immobile => "Wait Here",
            AgentDecisionType.Wanderer => "Patrol Room",
            AgentDecisionType.Explorer => "Explore",
            AgentDecisionType.TaskFollower => "AI",
            _ => decisionType.ToString()
        };
    }

    private static string GetPackFormationDisplayName(FormationsEnum formation)
    {
        return formation switch
        {
            FormationsEnum.LineAbreast => "Abreast",
            FormationsEnum.TwoColums => "Two Columns",
            FormationsEnum.Wedge => "Wedge",
            FormationsEnum.Circle => "Circle",
            FormationsEnum.SingleFile => "Follow",
            _ => formation.ToString()
        };
    }

    #endregion

    #region Input And Actions

    internal void OnPackMemberListRowClicked(int index)
    {
        if (index < 0 || index >= packMemberOptions.Count)
            return;

        AudioPlayer.PlayUiButtonClick();
        pendingLeftAgentSelection = packMemberOptions[index];
        selectedPackLeftIndex = index;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    internal void SelectPackMemberListRowAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        int rowIndex = GetPackMemberListRowIndexAtScreenPosition(screenPosition, eventCamera);
        if (rowIndex >= 0)
            OnPackMemberListRowClicked(rowIndex);
    }

    internal void BeginPackMemberListDrag(Vector2 screenPosition, Camera eventCamera)
    {
        if (!TryGetPackMemberListLocalPoint(screenPosition, eventCamera, out Vector2 localPoint))
            return;

        packMemberListDragStartLocalY = localPoint.y;
        packMemberListDragStartContentY = packMemberListContentRect != null
        ? packMemberListContentRect.anchoredPosition.y
        : 0f;
    }

    internal void DragPackMemberList(Vector2 screenPosition, Camera eventCamera)
    {
        if (!TryGetPackMemberListLocalPoint(screenPosition, eventCamera, out Vector2 localPoint))
            return;

        float dragDeltaY = localPoint.y - packMemberListDragStartLocalY;
        SetPackMemberListScrollOffset(packMemberListDragStartContentY + dragDeltaY);
    }

    private void CyclePackLeftSelection(int direction)
    {
        BuildPackMemberOptions();
        if (packMemberOptions.Count <= 1)
            return;

        int previousLeftIndex = Mathf.Clamp(selectedPackLeftIndex, 0, packMemberOptions.Count - 1);
        selectedPackLeftIndex = FindNextPackMemberIndex(selectedPackLeftIndex, direction, -1);
        selectedPackRightIndex = previousLeftIndex;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void CyclePackRightSelection(int direction)
    {
        BuildPackMemberOptions();
        WorldObject leftMember = GetSelectedFromList(packMemberOptions, ref selectedPackLeftIndex);
        BuildPackRightOptions(leftMember);
        if (packRightOptions.Count <= 1)
            return;

        CycleSelection(packRightOptions, ref selectedPackRightIndex, direction);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnPackBehaviorClicked(AgentDecisionType decisionType)
    {
        WorldObject member = GetSelectedPackRightMember();
        if (member == null)
        {
            ShowInteractionMessage("No agent selected");
            return;
        }

        if (decisionType == AgentDecisionType.Player && !TrySelectPackMemberForPlayerControl(member))
        {
            ShowInteractionMessage($"{member.DisplayName} could not be controlled");
            return;
        }

        if (member.agentModule == null)
            member.CreateModulesIfNeeded(ModuleFlags.agentModule);

        if (member.agentModule == null)
        {
            ShowInteractionMessage($"{member.DisplayName} cannot change behavior");
            Debug.LogWarning($"InteractionDialogUI: {member.DisplayName} has no AgentModule for pack behavior {decisionType}.", member);
            return;
        }

        member.agentModule.SwitchDecisionModule(decisionType);
        ShowInteractionMessage($"{member.DisplayName} behavior set to {GetPackBehaviorDisplayName(decisionType)}");
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private bool TrySelectPackMemberForPlayerControl(WorldObject member)
    {
        GameInputRouter router = Dir.Instance != null ? Dir.Instance.gameInputRouter : null;
        if (router == null)
            router = GameInputRouter.Instance;

        if (router == null)
            return false;

        return router.TrySelectControlledAgent(member);
    }

    private void OnSetPackLeaderClicked()
    {
        WorldObject member = GetSelectedPackRightMember();
        PromotePackIndicatorSelection(member);
    }

    private void OnPlayerPackIndicatorClicked()
    {
        PromotePackIndicatorSelection(GetSelectedPackLeftMember());
    }

    private void OnTargetPackIndicatorClicked()
    {
        WorldObject member = GetSelectedPackRightMember();
        if (member != null && !IsInPlayerPack(member))
        {
            JoinPackFromIndicatorSelection(member);
            return;
        }

        PromotePackIndicatorSelection(member);
    }

    private void OnJoinPackClicked()
    {
        JoinPackFromIndicatorSelection(GetSelectedPackRightMember());
    }

    private void JoinPackFromIndicatorSelection(WorldObject member)
    {
        if (member == null)
        {
            ShowInteractionMessage("No agent selected");
            return;
        }

        if (!TryJoinPlayerPackTail(member, out string reason))
        {
            ShowInteractionMessage(reason);
            Debug.LogWarning($"InteractionDialogUI: failed to add {member.DisplayName} to player pack: {reason}", member);
            return;
        }

        ShowInteractionMessage($"{member.DisplayName} joined the pack");
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void PromotePackIndicatorSelection(WorldObject member)
    {
        if (member == null)
        {
            ShowInteractionMessage("No agent selected");
            return;
        }

        if (IsPlayerPackLeader(member))
        {
            ShowInteractionMessage($"{member.DisplayName} is already pack leader");
            return;
        }

        if (!TryPromoteToPlayerPackLeader(member, out string reason))
        {
            ShowInteractionMessage(reason);
            Debug.LogWarning($"InteractionDialogUI: failed to promote {member.DisplayName} to player pack leader: {reason}", member);
            return;
        }

        selectedPackLeftIndex = 0;
        selectedPackRightIndex = 0;
        ShowInteractionMessage($"{member.DisplayName} is pack leader");
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private static bool TryPromoteToPlayerPackLeader(WorldObject member, out string reason)
    {
        reason = string.Empty;
        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        if (member == null)
        {
            reason = "No agent selected.";
            return false;
        }

        if (playerPack == null)
        {
            reason = "No player pack available.";
            return false;
        }

        if (member.agentModule == null || member.packMemberModule == null)
            member.CreateModulesIfNeeded(ModuleFlagsTemplates.FullAgent);

        if (member.packMemberModule == null)
        {
            reason = $"{member.DisplayName} cannot join a pack.";
            return false;
        }

        Pack currentPack = member.packMemberModule.currentPack;
        if (currentPack != null && currentPack != playerPack && !member.packMemberModule.LeaveCurrentPack())
        {
            reason = $"{member.DisplayName} could not leave {currentPack.packName}.";
            return false;
        }

        bool changed = playerPack.AddMember(member, setAsLeader: true);
        if (!changed && playerPack.packLeader != member)
        {
            reason = $"{member.DisplayName} could not become leader.";
            return false;
        }

        if (playerPack.packLeader == member)
            playerPack.SetPackFollowChain();

        return playerPack.packLeader == member;
    }

    private static bool TryJoinPlayerPackTail(WorldObject member, out string reason)
    {
        reason = string.Empty;
        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        if (member == null)
        {
            reason = "No agent selected.";
            return false;
        }

        if (playerPack == null)
        {
            reason = "No player pack available.";
            return false;
        }

        if (member.agentModule == null || member.packMemberModule == null)
            member.CreateModulesIfNeeded(ModuleFlagsTemplates.FullAgent);

        if (member.packMemberModule == null)
        {
            reason = $"{member.DisplayName} cannot join a pack.";
            return false;
        }

        Pack currentPack = member.packMemberModule.currentPack;
        if (currentPack == playerPack)
            return true;

        if (currentPack != null && !member.packMemberModule.LeaveCurrentPack())
        {
            reason = $"{member.DisplayName} could not leave {currentPack.packName}.";
            return false;
        }

        bool changed = playerPack.AddMember(member, setAsLeader: false);
        if (!changed && !IsInPlayerPack(member))
        {
            reason = $"{member.DisplayName} could not join the pack.";
            return false;
        }

        playerPack.SetPackFollowChain();
        return IsInPlayerPack(member);
    }

    private void OnLeavePackClicked()
    {
        WorldObject member = GetSelectedPackRightMember();
        PackMemberModule packMember = member != null ? member.packMemberModule : null;
        if (member == null || packMember == null)
        {
            ShowInteractionMessage("No pack member selected");
            return;
        }

        if (!IsInPlayerPack(member))
        {
            ShowInteractionMessage($"{member.DisplayName} is not in the pack");
            return;
        }

        if (!packMember.LeaveCurrentPack())
        {
            ShowInteractionMessage($"{member.DisplayName} cannot leave the pack");
            return;
        }

        ShowInteractionMessage($"{member.DisplayName} left the pack");
        selectedPackLeftIndex = 0;
        selectedPackRightIndex = 1;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnPackFormationClicked(FormationsEnum formation)
    {
        Pack pack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        if (pack == null)
        {
            ShowInteractionMessage("No player pack available");
            return;
        }

        pack.SetFormation(formation);
        ShowInteractionMessage($"Pack formation set to {GetPackFormationDisplayName(formation)}");
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnUnimplementedPackActionClicked()
    {
        ShowInteractionMessage("That pack command is not implemented yet");
    }

    #endregion
}
