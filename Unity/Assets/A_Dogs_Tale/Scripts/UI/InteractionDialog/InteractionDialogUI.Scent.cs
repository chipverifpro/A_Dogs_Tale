using System.Collections.Generic;
using DogGame;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
public sealed partial class InteractionDialogUI : MonoBehaviour
{
    #region Fields

    private GameObject scentSourceListObject;

    private RectTransform scentSourceListRect;

    private RectTransform scentSourceListContentRect;

    private ScrollRect scentSourceListScrollRect;

    private TextMeshProUGUI scentSourceListEmptyLabel;

    private float scentSourceListDragStartLocalY;

    private float scentSourceListDragStartContentY;

    private bool scentSourceListPointerDown;

    private bool scentSourceListPointerDragged;

    private Vector2 scentSourceListPointerDownPosition;

    private readonly List<Image> scentSourceListBackgrounds = new();

    private readonly List<WorldObject> scentTargetOptions = new();

    #endregion

    #region UI Construction

    private void BuildScentSourceList(Transform parent)
    {
        scentSourceListObject = CreateUIObject("ScentSourceList", parent);
        scentSourceListRect = scentSourceListObject.GetComponent<RectTransform>();
        scentSourceListRect.anchorMin = new Vector2(0.5f, 1f);
        scentSourceListRect.anchorMax = new Vector2(0.5f, 1f);
        scentSourceListRect.pivot = new Vector2(0.5f, 0.5f);
        scentSourceListRect.anchoredPosition = new Vector2(425f, -690f);
        scentSourceListRect.sizeDelta = new Vector2(470f, 300f);

        Image listBackground = scentSourceListObject.AddComponent<Image>();
        listBackground.color = new Color(0.09f, 0.065f, 0.035f, 0.58f);
        listBackground.raycastTarget = true;

        scentSourceListScrollRect = scentSourceListObject.AddComponent<ScrollRect>();
        scentSourceListScrollRect.horizontal = false;
        scentSourceListScrollRect.vertical = true;
        scentSourceListScrollRect.movementType = ScrollRect.MovementType.Clamped;
        scentSourceListScrollRect.scrollSensitivity = 24f;

        GameObject viewportObject = CreateUIObject("Viewport", scentSourceListObject.transform);
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
        scentSourceListContentRect = contentObject.GetComponent<RectTransform>();
        scentSourceListContentRect.anchorMin = new Vector2(0f, 1f);
        scentSourceListContentRect.anchorMax = new Vector2(1f, 1f);
        scentSourceListContentRect.pivot = new Vector2(0.5f, 1f);
        scentSourceListContentRect.anchoredPosition = Vector2.zero;
        scentSourceListContentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 5f;
        layout.padding = new RectOffset(4, 4, 4, 4);

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scentSourceListScrollRect.viewport = viewportRect;
        scentSourceListScrollRect.content = scentSourceListContentRect;

        GameObject emptyObject = CreateUIObject("EmptyLabel", scentSourceListObject.transform);
        RectTransform emptyRect = emptyObject.GetComponent<RectTransform>();
        emptyRect.anchorMin = Vector2.zero;
        emptyRect.anchorMax = Vector2.one;
        emptyRect.offsetMin = new Vector2(24f, 24f);
        emptyRect.offsetMax = new Vector2(-24f, -24f);

        scentSourceListEmptyLabel = emptyObject.AddComponent<TextMeshProUGUI>();
        scentSourceListEmptyLabel.text = "No scent sources";
        scentSourceListEmptyLabel.fontSize = 24f;
        scentSourceListEmptyLabel.color = new Color(0.86f, 0.8f, 0.66f, 0.78f);
        scentSourceListEmptyLabel.alignment = TextAlignmentOptions.Center;
        scentSourceListEmptyLabel.raycastTarget = false;

        GameObject hitAreaObject = CreateUIObject("HitArea", scentSourceListObject.transform);
        RectTransform hitAreaRect = hitAreaObject.GetComponent<RectTransform>();
        hitAreaRect.anchorMin = Vector2.zero;
        hitAreaRect.anchorMax = Vector2.one;
        hitAreaRect.offsetMin = Vector2.zero;
        hitAreaRect.offsetMax = Vector2.zero;

        Image hitAreaImage = hitAreaObject.AddComponent<Image>();
        hitAreaImage.color = new Color(1f, 1f, 1f, 0.001f);
        hitAreaImage.raycastTarget = true;

        InteractionDialogScentSourceListHitArea hitArea = hitAreaObject.AddComponent<InteractionDialogScentSourceListHitArea>();
        hitArea.Initialize(this);

        scentSourceListObject.SetActive(false);
    }

    private void CreateScentSourceListRow(WorldObject source, int index)
    {
        GameObject rowObject = CreateUIObject($"ScentSourceRow_{index}", scentSourceListContentRect);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, PackMemberListRowHeight);

        LayoutElement layoutElement = rowObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = PackMemberListRowHeight;
        layoutElement.minHeight = PackMemberListRowHeight;

        Image background = rowObject.AddComponent<Image>();
        background.color = GetScentSourceListRowColor(index == scentState.SelectedTargetIndex);
        background.raycastTarget = true;

        GameObject labelObject = CreateUIObject("Label", rowObject.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 2f);
        labelRect.offsetMax = new Vector2(-12f, -2f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = source != null ? source.DisplayName : string.Empty;
        label.fontSize = 22f;
        label.color = new Color(1f, 0.88f, 0.58f, 1f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        scentSourceListBackgrounds.Add(background);
        if (source != null)
            ConfigureTooltip(rowObject, $"Select {source.DisplayName}");
    }

    #endregion

    #region UI Refresh

    private void RefreshScentView(bool forcePreviewRefresh = false)
    {
        SetPackControlsActive(false);
        SetQuestControlsActive(false);
        SetScentControlsActive(true);
        SetSocialControlsActive(false);
        SetPackIndicatorButtonsActive(false);
        SetItemsControlsActive(false);
        SetPreviewSlotActive(playerItemPreviewSlot, false);
        SetPreviewSlotActive(targetItemPreviewSlot, false);
        SetItemSelectionTypeLabelsActive(false);

        BuildPlayerAgentOptions();
        ApplyPendingSelection(playerAgentOptions, sharedState.PendingLeftAgentSelection, ref sharedState.SelectedPlayerAgentIndex);
        WorldObject leftMember = GetSelectedFromList(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex);
        BuildScentTargetOptions(leftMember);
        ApplyPendingSelection(scentTargetOptions, sharedState.PendingRightAgentSelection, ref scentState.SelectedTargetIndex);
        WorldObject rightMember = GetSelectedFromList(scentTargetOptions, ref scentState.SelectedTargetIndex);
        RefreshScentSourceList();

        RefreshCircleAndHotspot(playerPreviewSlot, playerAgentOptions.Count, previousPlayerAgentButton, nextPlayerAgentButton);
        RefreshCircleAndHotspot(targetPreviewSlot, scentTargetOptions.Count, previousTargetAgentButton, nextTargetAgentButton);
        RefreshCircleAndHotspot(playerItemPreviewSlot, 0, previousPlayerItemButton, nextPlayerItemButton);
        RefreshCircleAndHotspot(targetItemPreviewSlot, 0, previousTargetItemButton, nextTargetItemButton);

        SetLabelText(playerNameLabel, leftMember != null ? leftMember.DisplayName : string.Empty);
        SetLabelText(playerHeldItemLabel, string.Empty);
        SetLabelText(targetNameLabel, rightMember != null ? rightMember.DisplayName : string.Empty);
        SetLabelText(targetHeldItemLabel, string.Empty);

        if (forcePreviewRefresh || leftMember != scentState.DisplayedLeft)
            BuildPreviewClone(playerPreviewSlot, leftMember, "ScentLeft");
        if (forcePreviewRefresh || rightMember != scentState.DisplayedRight)
            BuildPreviewClone(targetPreviewSlot, rightMember, "ScentRight");

        BuildPreviewClone(playerItemPreviewSlot, null, "ScentLeftItem");
        BuildPreviewClone(targetItemPreviewSlot, null, "ScentRightItem");

        scentState.DisplayedLeft = leftMember;
        scentState.DisplayedRight = rightMember;
        sharedState.DisplayedPlayer = leftMember;
        itemsState.DisplayedPlayerItem = null;
        itemsState.DisplayedTarget = rightMember;
        itemsState.DisplayedTargetItem = null;
        ClearPendingSelections();
    }

    private void SetScentControlsActive(bool active)
    {
        if (scentSourceListObject != null)
        {
            scentSourceListObject.SetActive(active);
            if (active)
                scentSourceListObject.transform.SetAsLastSibling();
        }
    }

    private void RefreshScentSourceList()
    {
        if (scentSourceListContentRect == null)
            return;

        bool listChanged = HasScentSourceListChanged();
        bool selectionChanged = scentState.DisplayedSourceListSelectedIndex != scentState.SelectedTargetIndex;
        if (!listChanged)
        {
            RefreshScentSourceListHighlights();
            if (selectionChanged)
                ScrollScentSourceListToSelection();
            scentState.DisplayedSourceListSelectedIndex = scentState.SelectedTargetIndex;
            return;
        }

        ClearScentSourceListRows();

        if (scentSourceListEmptyLabel != null)
            scentSourceListEmptyLabel.gameObject.SetActive(scentTargetOptions.Count <= 0);

        if (scentTargetOptions.Count <= 0)
        {
            RememberDisplayedScentSourceListState();
            return;
        }

        for (int i = 0; i < scentTargetOptions.Count; i++)
            CreateScentSourceListRow(scentTargetOptions[i], i);

        RefreshScentSourceListHighlights();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scentSourceListContentRect);
        ScrollScentSourceListToSelection();
        RememberDisplayedScentSourceListState();
    }

    private bool HasScentSourceListChanged()
    {
        WorldObject first = scentTargetOptions.Count > 0 ? scentTargetOptions[0] : null;
        WorldObject last = scentTargetOptions.Count > 0 ? scentTargetOptions[^1] : null;
        return scentState.DisplayedSourceListOptionCount != scentTargetOptions.Count ||
        scentState.DisplayedSourceListFirst != first ||
        scentState.DisplayedSourceListLast != last ||
        scentSourceListBackgrounds.Count != scentTargetOptions.Count;
    }

    private void RememberDisplayedScentSourceListState()
    {
        scentState.DisplayedSourceListSelectedIndex = scentState.SelectedTargetIndex;
        scentState.DisplayedSourceListOptionCount = scentTargetOptions.Count;
        scentState.DisplayedSourceListFirst = scentTargetOptions.Count > 0 ? scentTargetOptions[0] : null;
        scentState.DisplayedSourceListLast = scentTargetOptions.Count > 0 ? scentTargetOptions[^1] : null;
    }

    private void ClearScentSourceListRows()
    {
        scentSourceListBackgrounds.Clear();
        ClearListContent(scentSourceListContentRect);
    }

    private void RefreshScentSourceListHighlights()
    {
        for (int i = 0; i < scentSourceListBackgrounds.Count; i++)
            scentSourceListBackgrounds[i].color = GetScentSourceListRowColor(i == scentState.SelectedTargetIndex);
    }

    private void ScrollScentSourceListToSelection()
    {
        ScrollFixedRowListToSelection(
            scentSourceListScrollRect,
            scentSourceListContentRect,
            scentSourceListScrollRect != null ? scentSourceListScrollRect.viewport : null,
            scentState.SelectedTargetIndex,
            scentTargetOptions.Count);
    }

    internal void ScrollScentSourceList(Vector2 scrollDelta)
    {
        if (scentSourceListContentRect == null || scentSourceListScrollRect == null)
            return;

        float currentOffset = scentSourceListContentRect.anchoredPosition.y;
        SetScentSourceListScrollOffset(currentOffset - scrollDelta.y * scentSourceListScrollRect.scrollSensitivity);
    }

    private void SetScentSourceListScrollOffset(float offsetY)
    {
        SetFixedRowListScrollOffset(
            scentSourceListScrollRect,
            scentSourceListContentRect,
            scentSourceListScrollRect != null ? scentSourceListScrollRect.viewport : null,
            scentTargetOptions.Count,
            offsetY);
    }

    #endregion

    #region Selection State

    private void BuildScentTargetOptions(WorldObject player)
    {
        WorldObject previousSelection = GetSelectedFromList(scentTargetOptions, ref scentState.SelectedTargetIndex);
        scentTargetOptions.Clear();

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (player == null || registry == null)
        {
            scentState.SelectedTargetIndex = 0;
            return;
        }

        Vector3 playerPosition = player.pos3d_map;

        foreach (WorldObject candidate in registry.GetAllObjects())
        {
            if (candidate == null || candidate == player || !candidate.gameObject.activeInHierarchy)
                continue;

            if (!CanUseAsScentTarget(candidate))
                continue;

            scentTargetOptions.Add(candidate);
        }

        scentTargetOptions.Sort((a, b) =>
        {
                float aDistanceSqr = GetPlanarDistanceSqr(playerPosition, a.pos3d_map);
                float bDistanceSqr = GetPlanarDistanceSqr(playerPosition, b.pos3d_map);
                int distanceComparison = aDistanceSqr.CompareTo(bDistanceSqr);
                if (distanceComparison != 0)
                return distanceComparison;

                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
        });

        KeepSelectedObject(scentTargetOptions, previousSelection, ref scentState.SelectedTargetIndex);
    }

    private static Color GetScentSourceListRowColor(bool selected)
    {
        return selected
        ? new Color(0.95f, 0.54f, 0.12f, 0.86f)
        : new Color(0.20f, 0.13f, 0.065f, 0.78f);
    }

    private int GetScentSourceListRowIndexAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        RectTransform viewportRect = scentSourceListScrollRect != null ? scentSourceListScrollRect.viewport : null;
        return GetFixedRowListRowIndexAtScreenPosition(
            scentTargetOptions.Count,
            viewportRect,
            scentSourceListContentRect,
            screenPosition,
            eventCamera);
    }

    private bool TryGetScentSourceListLocalPoint(Vector2 screenPosition, Camera eventCamera, out Vector2 localPoint)
    {
        RectTransform viewportRect = scentSourceListScrollRect != null ? scentSourceListScrollRect.viewport : null;
        return TryGetListLocalPoint(viewportRect, screenPosition, eventCamera, out localPoint);
    }

    private float GetScentSourceListMaxScrollOffset()
    {
        RectTransform viewportRect = scentSourceListScrollRect != null ? scentSourceListScrollRect.viewport : null;
        return GetFixedRowListMaxScrollOffset(viewportRect, scentTargetOptions.Count);
    }

    private static bool CanUseAsScentTarget(WorldObject candidate)
    {
        if (candidate == null)
            return false;

        return candidate.scentEmitterModule != null;
    }

    #endregion

    #region Input And Actions

    internal void SelectScentSourceListRowAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        int rowIndex = GetScentSourceListRowIndexAtScreenPosition(screenPosition, eventCamera);
        if (rowIndex >= 0)
            OnScentSourceListRowClicked(rowIndex);
    }

    internal void BeginScentSourceListDrag(Vector2 screenPosition, Camera eventCamera)
    {
        if (!TryGetScentSourceListLocalPoint(screenPosition, eventCamera, out Vector2 localPoint))
            return;

        scentSourceListDragStartLocalY = localPoint.y;
        scentSourceListDragStartContentY = scentSourceListContentRect != null
        ? scentSourceListContentRect.anchoredPosition.y
        : 0f;
    }

    internal void DragScentSourceList(Vector2 screenPosition, Camera eventCamera)
    {
        if (!TryGetScentSourceListLocalPoint(screenPosition, eventCamera, out Vector2 localPoint))
            return;

        float dragDeltaY = localPoint.y - scentSourceListDragStartLocalY;
        SetScentSourceListScrollOffset(scentSourceListDragStartContentY + dragDeltaY);
    }

    internal void OnScentSourceListRowClicked(int index)
    {
        if (index < 0 || index >= scentTargetOptions.Count)
            return;

        AudioPlayer.PlayUiButtonClick();
        sharedState.PendingRightAgentSelection = scentTargetOptions[index];
        scentState.SelectedTargetIndex = index;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void HandleScentSourceListPointerInput()
    {
        if (sharedState.CurrentTab != InteractionTab.Scent ||
            scentSourceListRect == null ||
            scentSourceListContentRect == null ||
            Mouse.current == null)
        {
            scentSourceListPointerDown = false;
            return;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        bool pointerOverList = RectTransformUtility.RectangleContainsScreenPoint(scentSourceListRect, screenPosition, null);
        Vector2 scrollDelta = Mouse.current.scroll.ReadValue();
        if (pointerOverList && Mathf.Abs(scrollDelta.y) > 0.01f)
            ScrollScentSourceList(scrollDelta);

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            scentSourceListPointerDown = pointerOverList;
            scentSourceListPointerDragged = false;
            scentSourceListPointerDownPosition = screenPosition;
            if (scentSourceListPointerDown)
                BeginScentSourceListDrag(screenPosition, null);
        }

        if (scentSourceListPointerDown && Mouse.current.leftButton.isPressed)
        {
            if ((screenPosition - scentSourceListPointerDownPosition).sqrMagnitude > 9f)
                scentSourceListPointerDragged = true;

            if (scentSourceListPointerDragged)
                DragScentSourceList(screenPosition, null);
        }

        if (scentSourceListPointerDown && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (!scentSourceListPointerDragged && pointerOverList)
                SelectScentSourceListRowAtScreenPosition(screenPosition, null);

            scentSourceListPointerDown = false;
            scentSourceListPointerDragged = false;
        }
    }

    private void CycleScentLeftSelection(int direction)
    {
        BuildPlayerAgentOptions();
        if (playerAgentOptions.Count <= 1)
            return;

        CycleSelection(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex, direction);
        scentState.SelectedTargetIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void CycleScentRightSelection(int direction)
    {
        BuildPlayerAgentOptions();
        WorldObject player = GetSelectedFromList(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex);
        BuildScentTargetOptions(player);
        if (scentTargetOptions.Count <= 1)
            return;

        CycleSelection(scentTargetOptions, ref scentState.SelectedTargetIndex, direction);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    #endregion
}
