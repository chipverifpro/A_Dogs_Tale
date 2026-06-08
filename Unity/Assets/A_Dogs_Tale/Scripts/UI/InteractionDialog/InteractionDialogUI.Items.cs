using System.Collections.Generic;
using DogGame;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public sealed partial class InteractionDialogUI : MonoBehaviour
{
    #region Fields

    private GameObject tradeArrowsObject;

    private Button giveHotspotButton;

    private Button exchangeHotspotButton;

    private Button takeHotspotButton;

    private GameObject actionPanelObject;

    private GameObject packHeldItemListObject;

    private RectTransform packHeldItemListContentRect;

    private ScrollRect packHeldItemListScrollRect;

    private TextMeshProUGUI packHeldItemListEmptyLabel;

    [SerializeField, Min(0f)] private float throwForwardImpulse = 7f;

    [SerializeField, Min(0f)] private float throwUpwardImpulse = 2f;

    [SerializeField, Min(0f)] private float throwReleaseHeight = 0.5f;

    private readonly List<WorldObject> playerItemOptions = new();

    private readonly List<WorldObject> targetAgentOptions = new();

    private readonly List<WorldObject> targetItemOptions = new();

    private readonly List<PackHeldItemOption> packHeldItemOptions = new();

    private readonly List<Image> packHeldItemListBackgrounds = new();

    #endregion

    #region Nested Types

    private sealed class PackHeldItemOption
    {
        public PackHeldItemOption(WorldObject agent, WorldObject item)
        {
            Agent = agent;
            Item = item;
        }

        public WorldObject Agent { get; }
        public WorldObject Item { get; }
    }

    private enum InventoryAction
    {
        Use = 0,
        Eat = 1,
        Drop = 4,
        PickUp = 5
    }

    #endregion

    #region UI Construction

    private void BuildTradeArrows(Transform parent)
    {
        tradeArrowsObject = CreateUIObject("TradeArrows", parent);
        RectTransform arrowsRect = tradeArrowsObject.GetComponent<RectTransform>();
        arrowsRect.anchorMin = new Vector2(0.5f, 1f);
        arrowsRect.anchorMax = new Vector2(0.5f, 1f);
        arrowsRect.pivot = new Vector2(0.5f, 0.5f);
        arrowsRect.anchoredPosition = new Vector2(0f, -292f);
        arrowsRect.sizeDelta = new Vector2(86f, 186f);

        Image arrowsImage = tradeArrowsObject.AddComponent<Image>();
        arrowsImage.sprite = tradeArrowsSprite;
        arrowsImage.preserveAspect = true;
        arrowsImage.color = Color.white;
        arrowsImage.raycastTarget = false;

        giveHotspotButton = CreateTradeHotspot(parent, "GiveHotspot", new Vector2(0f, -244f), OnGiveClicked);
        exchangeHotspotButton = CreateTradeHotspot(parent, "ExchangeHotspot", new Vector2(0f, -292f), OnTradeClicked);
        takeHotspotButton = CreateTradeHotspot(parent, "TakeHotspot", new Vector2(0f, -340f), OnTakeItemClicked);
    }

    private Button CreateTradeHotspot(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        UnityEngine.Events.UnityAction clickHandler)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(94f, 44f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(clickHandler);
        ConfigureTooltip(buttonObject, GetInteractionButtonTooltipText(objectName));
        return button;
    }

    private void BuildActionButtons(Transform parent)
    {
        actionPanelObject = CreateUIObject("ActionPanel", parent);
        RectTransform actionPanelRect = actionPanelObject.GetComponent<RectTransform>();
        actionPanelRect.anchorMin = new Vector2(0.5f, 1f);
        actionPanelRect.anchorMax = new Vector2(0.5f, 1f);
        actionPanelRect.pivot = new Vector2(0.5f, 0.5f);
        actionPanelRect.anchoredPosition = new Vector2(270f, -690f);
        actionPanelRect.sizeDelta = new Vector2(690f, 300f);

        VerticalLayoutGroup layout = actionPanelObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 8f;
        layout.padding = new RectOffset(0, 0, 0, 0);

        Transform topRow = CreateActionButtonRow("HeldItemActionRowTop", actionPanelObject.transform, actionButtonHeight);
        Transform bottomRow = CreateActionButtonRow("HeldItemActionRowBottom", actionPanelObject.transform, actionButtonHeight * 0.86f);

        CreateActionButton(topRow, InventoryAction.Use, OnUseClicked);
        CreateActionButton(topRow, InventoryAction.Eat, OnEatClicked);
        CreateActionButton(bottomRow, InventoryAction.Drop, OnDropClicked, 0.86f);
        CreateThrowActionButton(bottomRow, 0.86f);
        CreateActionButton(bottomRow, InventoryAction.PickUp, OnPickUpClicked, 0.86f);
    }

    private void BuildPackHeldItemList(Transform parent)
    {
        packHeldItemListObject = CreateUIObject("PackHeldItemList", parent);
        RectTransform listRect = packHeldItemListObject.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.5f, 1f);
        listRect.anchorMax = new Vector2(0.5f, 1f);
        listRect.pivot = new Vector2(0.5f, 0.5f);
        listRect.anchoredPosition = new Vector2(-425f, -690f);
        listRect.sizeDelta = new Vector2(470f, 300f);

        Image listBackground = packHeldItemListObject.AddComponent<Image>();
        listBackground.color = new Color(0.09f, 0.065f, 0.035f, 0.58f);
        listBackground.raycastTarget = true;

        packHeldItemListScrollRect = packHeldItemListObject.AddComponent<ScrollRect>();
        packHeldItemListScrollRect.horizontal = false;
        packHeldItemListScrollRect.vertical = true;
        packHeldItemListScrollRect.movementType = ScrollRect.MovementType.Clamped;
        packHeldItemListScrollRect.scrollSensitivity = 24f;

        GameObject viewportObject = CreateUIObject("Viewport", packHeldItemListObject.transform);
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
        packHeldItemListContentRect = contentObject.GetComponent<RectTransform>();
        packHeldItemListContentRect.anchorMin = new Vector2(0f, 1f);
        packHeldItemListContentRect.anchorMax = new Vector2(1f, 1f);
        packHeldItemListContentRect.pivot = new Vector2(0.5f, 1f);
        packHeldItemListContentRect.anchoredPosition = Vector2.zero;
        packHeldItemListContentRect.sizeDelta = Vector2.zero;

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

        packHeldItemListScrollRect.viewport = viewportRect;
        packHeldItemListScrollRect.content = packHeldItemListContentRect;

        GameObject emptyObject = CreateUIObject("EmptyLabel", packHeldItemListObject.transform);
        RectTransform emptyRect = emptyObject.GetComponent<RectTransform>();
        emptyRect.anchorMin = Vector2.zero;
        emptyRect.anchorMax = Vector2.one;
        emptyRect.offsetMin = new Vector2(24f, 24f);
        emptyRect.offsetMax = new Vector2(-24f, -24f);

        packHeldItemListEmptyLabel = emptyObject.AddComponent<TextMeshProUGUI>();
        packHeldItemListEmptyLabel.text = "No pack held items";
        packHeldItemListEmptyLabel.fontSize = 24f;
        packHeldItemListEmptyLabel.color = new Color(0.86f, 0.8f, 0.66f, 0.78f);
        packHeldItemListEmptyLabel.alignment = TextAlignmentOptions.Center;
        packHeldItemListEmptyLabel.raycastTarget = false;

        packHeldItemListObject.SetActive(false);
    }

    private Transform CreateActionButtonRow(string rowName, Transform parent, float rowHeight)
    {
        GameObject rowObject = CreateUIObject(rowName, parent);

        HorizontalLayoutGroup rowLayout = rowObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = false;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.spacing = 8f;

        LayoutElement layoutElement = rowObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = rowHeight;
        layoutElement.minHeight = rowHeight;

        return rowObject.transform;
    }

    private void CreateActionButton(Transform parent, InventoryAction action, UnityEngine.Events.UnityAction clickHandler, float heightScale = 1f)
    {
        Sprite sprite = GetInventoryActionSprite(action);
        string fallbackText = GetActionFallbackText(action);
        Button button = CreateSpriteButton($"{action}Button", parent, sprite, fallbackText, clickHandler);
        ConfigureActionButtonSize(button, sprite, actionButtonHeight * Mathf.Max(0.01f, heightScale));
    }

    private void CreateThrowActionButton(Transform parent, float heightScale = 1f)
    {
        Sprite sprite = SpriteServer.SpriteLookup("Throw_Item")
        ?? SpriteServer.SpriteSheetLookup("Sprites/DogActions_B", 0);
        Button button = CreateSpriteButton("ThrowButton", parent, sprite, "THROW", OnThrowClicked);
        ConfigureActionButtonSize(button, sprite, actionButtonHeight * Mathf.Max(0.01f, heightScale));
    }

    private static void ConfigureActionButtonSize(Button button, Sprite sprite, float buttonHeight)
    {
        float width = buttonHeight;
        if (sprite != null && sprite.rect.height > 0f)
            width = buttonHeight * (sprite.rect.width / sprite.rect.height);

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, buttonHeight);

        LayoutElement layoutElement = button.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = buttonHeight;
        layoutElement.minWidth = width;
        layoutElement.minHeight = buttonHeight;
    }

    private List<PackHeldItemOption> BuildPackHeldItemOptions()
    {
        List<PackHeldItemOption> options = new();
        Pack playerPack = Dir.Instance != null ? Dir.Instance.playerPack : null;
        if (playerPack == null || playerPack.packAgentList == null)
            return options;

        for (int i = 0; i < playerPack.packAgentList.Count; i++)
            AddPackHeldItemOptionsForAgent(options, playerPack.packAgentList[i]);

        return options;
    }

    private void CreatePackHeldItemListRow(PackHeldItemOption option, int index)
    {
        GameObject rowObject = CreateUIObject($"PackHeldItemRow_{index}", packHeldItemListContentRect);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, PackMemberListRowHeight);

        LayoutElement layoutElement = rowObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = PackMemberListRowHeight;
        layoutElement.minHeight = PackMemberListRowHeight;

        Image background = rowObject.AddComponent<Image>();
        background.color = GetPackHeldItemListRowColor(false);
        background.raycastTarget = true;

        Button button = rowObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(() => SelectPackHeldItem(option));

        GameObject labelObject = CreateUIObject("Label", rowObject.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 2f);
        labelRect.offsetMax = new Vector2(-12f, -2f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = FormatPackHeldItemListLabel(option);
        label.fontSize = 22f;
        label.color = new Color(1f, 0.88f, 0.58f, 1f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        packHeldItemListBackgrounds.Add(background);
        ConfigureTooltip(rowObject, $"Select {FormatPackHeldItemListLabel(option)}");
    }

    #endregion

    #region UI Refresh

    private void RefreshInteractionView(bool forcePreviewRefresh = false)
    {
        RefreshTabHighlights();
        if (sharedState.CurrentTab == InteractionTab.Pack)
        {
            RefreshPackView(forcePreviewRefresh);
            return;
        }

        if (sharedState.CurrentTab == InteractionTab.Social)
        {
            RefreshSocialView(forcePreviewRefresh);
            return;
        }

        if (sharedState.CurrentTab == InteractionTab.Quests)
        {
            RefreshQuestsView(forcePreviewRefresh);
            return;
        }

        if (sharedState.CurrentTab == InteractionTab.Scent)
        {
            RefreshScentView(forcePreviewRefresh);
            return;
        }

        SetPackControlsActive(false);
        SetQuestControlsActive(false);
        SetScentControlsActive(false);
        SetSocialControlsActive(false);
        SetPackIndicatorButtonsActive(false);
        SetItemsControlsActive(true);
        SetPreviewSlotActive(playerItemPreviewSlot, true);
        SetPreviewSlotActive(targetItemPreviewSlot, true);
        SetItemSelectionTypeLabelsActive(true);

        BuildPlayerAgentOptions();
        ApplyPendingSelection(playerAgentOptions, sharedState.PendingLeftAgentSelection, ref sharedState.SelectedPlayerAgentIndex);
        WorldObject player = GetSelectedFromList(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex);
        WorldObject previousPlayerItem = GetSelectedFromList(playerItemOptions, ref itemsState.SelectedPlayerItemIndex);
        BuildItemOptions(player, playerItemOptions);
        KeepSelectedObject(playerItemOptions, previousPlayerItem, ref itemsState.SelectedPlayerItemIndex);
        WorldObject playerItem = GetSelectedFromList(playerItemOptions, ref itemsState.SelectedPlayerItemIndex);
        RefreshPackHeldItemList(player, playerItem);

        BuildTargetAgentOptions(player);
        ApplyPendingSelection(targetAgentOptions, sharedState.PendingRightAgentSelection, ref itemsState.SelectedTargetAgentIndex);
        WorldObject target = GetSelectedFromList(targetAgentOptions, ref itemsState.SelectedTargetAgentIndex);
        WorldObject previousTargetItem = GetSelectedFromList(targetItemOptions, ref itemsState.SelectedTargetItemIndex);
        BuildItemOptions(target, targetItemOptions);
        KeepSelectedObject(targetItemOptions, previousTargetItem, ref itemsState.SelectedTargetItemIndex);
        WorldObject targetItem = GetSelectedFromList(targetItemOptions, ref itemsState.SelectedTargetItemIndex);

        RefreshCircleAndHotspot(playerPreviewSlot, playerAgentOptions.Count, previousPlayerAgentButton, nextPlayerAgentButton);
        RefreshCircleAndHotspot(playerItemPreviewSlot, playerItemOptions.Count, previousPlayerItemButton, nextPlayerItemButton);
        RefreshCircleAndHotspot(targetPreviewSlot, targetAgentOptions.Count, previousTargetAgentButton, nextTargetAgentButton);
        RefreshCircleAndHotspot(targetItemPreviewSlot, targetItemOptions.Count, previousTargetItemButton, nextTargetItemButton);

        SetLabelText(playerNameLabel, player != null ? player.DisplayName : string.Empty);
        SetLabelText(playerHeldItemLabel, playerItem != null ? playerItem.DisplayName : string.Empty);
        SetLabelText(targetNameLabel, target != null ? target.DisplayName : string.Empty);
        SetLabelText(targetHeldItemLabel, targetItem != null ? targetItem.DisplayName : string.Empty);

        if (forcePreviewRefresh || player != sharedState.DisplayedPlayer)
            BuildPreviewClone(playerPreviewSlot, player, "Player");
        if (forcePreviewRefresh || playerItem != itemsState.DisplayedPlayerItem)
            BuildPreviewClone(playerItemPreviewSlot, playerItem, "PlayerItem");
        if (forcePreviewRefresh || target != itemsState.DisplayedTarget)
            BuildPreviewClone(targetPreviewSlot, target, "Target");
        if (forcePreviewRefresh || targetItem != itemsState.DisplayedTargetItem)
            BuildPreviewClone(targetItemPreviewSlot, targetItem, "TargetItem");

        sharedState.DisplayedPlayer = player;
        itemsState.DisplayedPlayerItem = playerItem;
        itemsState.DisplayedTarget = target;
        itemsState.DisplayedTargetItem = targetItem;
        ClearPendingSelections();
    }

    private void SetItemsControlsActive(bool active)
    {
        if (tradeArrowsObject != null)
            tradeArrowsObject.SetActive(active);
        if (giveHotspotButton != null)
            giveHotspotButton.gameObject.SetActive(active);
        if (exchangeHotspotButton != null)
            exchangeHotspotButton.gameObject.SetActive(active);
        if (takeHotspotButton != null)
            takeHotspotButton.gameObject.SetActive(active);
        if (actionPanelObject != null)
            actionPanelObject.SetActive(active);
        if (packHeldItemListObject != null)
        {
            packHeldItemListObject.SetActive(active);
            if (active)
                packHeldItemListObject.transform.SetAsLastSibling();
        }
    }

    private void RefreshPackHeldItemList(WorldObject selectedAgent, WorldObject selectedItem)
    {
        if (packHeldItemListContentRect == null)
            return;

        List<PackHeldItemOption> currentOptions = BuildPackHeldItemOptions();
        bool optionsChanged = itemsState.PackHeldItemListDirty || HasPackHeldItemOptionsChanged(currentOptions);
        bool selectionChanged = itemsState.DisplayedPackHeldItemSelectedAgent != selectedAgent ||
        itemsState.DisplayedPackHeldItemSelectedItem != selectedItem;

        if (optionsChanged)
        {
            packHeldItemOptions.Clear();
            packHeldItemOptions.AddRange(currentOptions);
            RebuildPackHeldItemListRows();
            itemsState.PackHeldItemListDirty = false;
        }

        if (packHeldItemListEmptyLabel != null)
            packHeldItemListEmptyLabel.gameObject.SetActive(packHeldItemOptions.Count <= 0);

        RefreshPackHeldItemListHighlights(selectedAgent, selectedItem);
        if (optionsChanged || selectionChanged)
            ScrollPackHeldItemListToSelection(selectedAgent, selectedItem);

        itemsState.DisplayedPackHeldItemSelectedAgent = selectedAgent;
        itemsState.DisplayedPackHeldItemSelectedItem = selectedItem;
    }

    private bool HasPackHeldItemOptionsChanged(List<PackHeldItemOption> currentOptions)
    {
        if (currentOptions.Count != packHeldItemOptions.Count)
            return true;

        for (int i = 0; i < currentOptions.Count; i++)
        {
            if (currentOptions[i].Agent != packHeldItemOptions[i].Agent ||
                currentOptions[i].Item != packHeldItemOptions[i].Item)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearPackHeldItemListRows()
    {
        packHeldItemListBackgrounds.Clear();
        ClearListContent(packHeldItemListContentRect);
    }

    private void RefreshPackHeldItemListHighlights(WorldObject selectedAgent, WorldObject selectedItem)
    {
        for (int i = 0; i < packHeldItemListBackgrounds.Count; i++)
        {
            bool selected = i < packHeldItemOptions.Count &&
            packHeldItemOptions[i].Agent == selectedAgent &&
            packHeldItemOptions[i].Item == selectedItem;
            packHeldItemListBackgrounds[i].color = GetPackHeldItemListRowColor(selected);
        }
    }

    private void ScrollPackHeldItemListToSelection(WorldObject selectedAgent, WorldObject selectedItem)
    {
        if (packHeldItemOptions.Count <= 0)
            return;

        int selectedIndex = FindPackHeldItemOptionIndex(selectedAgent, selectedItem);
        if (selectedIndex < 0)
            return;

        int visibleRows = Mathf.Max(1, Mathf.FloorToInt(300f / (PackMemberListRowHeight + PackMemberListRowSpacing)));
        ScrollListToSelectionWithVisibleRows(
            packHeldItemListScrollRect,
            selectedIndex,
            packHeldItemOptions.Count,
            visibleRows);
    }

    #endregion

    #region Selection State

    private static void BuildItemOptions(WorldObject carrier, List<WorldObject> options)
    {
        options.Clear();

        ContainerModule container = GetOrCreateContainer(carrier);
        if (container == null || container.HeldItemCount <= 0)
            return;

        for (int i = 0; i < container.HeldItemCount; i++)
        {
            WorldObject item = container.HeldItems[i];
            if (item != null)
                options.Add(item);
        }
    }

    private void BuildTargetAgentOptions(WorldObject player)
    {
        WorldObject previousSelection = GetSelectedFromList(targetAgentOptions, ref itemsState.SelectedTargetAgentIndex);
        targetAgentOptions.Clear();

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (player == null || registry == null)
        {
            itemsState.SelectedTargetAgentIndex = 0;
            return;
        }

        float radiusSqr = tradePartnerSearchRadiusTiles * tradePartnerSearchRadiusTiles;
        Vector3 playerPosition = player.pos3d_map;

        foreach (WorldObject candidate in registry.GetAllObjects())
        {
            if (candidate == null || candidate == player || !candidate.gameObject.activeInHierarchy)
                continue;

            if (!CanUseAsTradeTarget(candidate))
                continue;

            Vector3 delta = candidate.pos3d_map - playerPosition;
            delta.y = 0f;
            float distanceSqr = delta.sqrMagnitude;
            if (distanceSqr > radiusSqr)
                continue;

            targetAgentOptions.Add(candidate);
        }

        targetAgentOptions.Sort((a, b) =>
        {
                float aDistanceSqr = GetPlanarDistanceSqr(playerPosition, a.pos3d_map);
                float bDistanceSqr = GetPlanarDistanceSqr(playerPosition, b.pos3d_map);
                int distanceComparison = aDistanceSqr.CompareTo(bDistanceSqr);
                if (distanceComparison != 0)
                return distanceComparison;

                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
        });

        KeepSelectedObject(targetAgentOptions, previousSelection, ref itemsState.SelectedTargetAgentIndex);
    }

    private static Sprite GetInventoryActionSprite(InventoryAction action)
    {
        string spriteName = action switch
        {
            InventoryAction.Use => "UseItem",
            InventoryAction.Eat => "EatItem",
            InventoryAction.Drop => "DropItem",
            InventoryAction.PickUp => "PickUpItem",
            _ => string.Empty
        };

        return SpriteServer.SpriteLookup(spriteName);
    }

    private static Color GetPackHeldItemListRowColor(bool selected)
    {
        return selected
        ? new Color(0.28f, 0.2f, 0.07f, 0.96f)
        : new Color(0.12f, 0.095f, 0.055f, 0.86f);
    }

    private static string GetActionFallbackText(InventoryAction action)
    {
        return action switch
        {
            InventoryAction.Use => "USE",
            InventoryAction.Eat => "EAT",
            InventoryAction.Drop => "DROP",
            InventoryAction.PickUp => "PICK UP",
            _ => action.ToString()
        };
    }

    private int FindPackHeldItemOptionIndex(WorldObject agent, WorldObject item)
    {
        for (int i = 0; i < packHeldItemOptions.Count; i++)
        {
            if (packHeldItemOptions[i].Agent == agent && packHeldItemOptions[i].Item == item)
                return i;
        }

        return -1;
    }

    private static string FormatPackHeldItemListLabel(PackHeldItemOption option)
    {
        string itemName = option != null && option.Item != null ? option.Item.DisplayName : "Item";
        string agentName = option != null && option.Agent != null ? option.Agent.DisplayName : "Agent";
        return $"{itemName} - {agentName}";
    }

    private static bool CanUseAsTradeTarget(WorldObject candidate)
    {
        if (candidate == null)
            return false;

        return candidate.containerModule != null &&
        candidate.containerModule.itemCapacity > 0;
    }

    private static Vector3 GetDropPositionNearCarrier(WorldObject carrier, WorldObject item)
    {
        Vector3 dropDirection = carrier.transform.forward;
        dropDirection.y = 0f;
        if (dropDirection.sqrMagnitude < 0.001f)
            dropDirection = Vector3.forward;
        dropDirection.Normalize();

        float itemRadius = item != null ? item.sizeRadius : 0f;
        float dropDistance = Mathf.Max(0.65f, carrier.sizeRadius + itemRadius + 0.2f);
        Vector3 dropPosition = carrier.transform.position + dropDirection * dropDistance;
        dropPosition.y = carrier.transform.position.y;
        return dropPosition;
    }

    private static Vector3 GetFacingDirection(WorldObject carrier)
    {
        Vector3 direction = carrier.transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.forward;

        return direction.normalized;
    }

    private Vector3 GetThrowReleasePosition(WorldObject carrier, WorldObject item, Vector3 direction)
    {
        float itemRadius = item != null ? item.sizeRadius : 0f;
        float releaseDistance = Mathf.Max(0.65f, carrier.sizeRadius + itemRadius + 0.2f);
        Vector3 releasePosition = carrier.transform.position + direction * releaseDistance;
        releasePosition.y = carrier.transform.position.y + throwReleaseHeight;
        return releasePosition;
    }

    private static ContainerModule GetOrCreateContainer(WorldObject owner)
    {
        if (owner == null)
            return null;

        if (owner.containerModule == null)
            owner.CreateModulesIfNeeded(ModuleFlags.containerModule);

        return owner.containerModule;
    }

    #endregion

    #region Input And Actions

    private void SelectPackHeldItem(PackHeldItemOption option)
    {
        if (option == null || option.Agent == null || option.Item == null)
            return;

        BuildPlayerAgentOptions();
        int agentIndex = playerAgentOptions.IndexOf(option.Agent);
        if (agentIndex < 0)
            return;

        sharedState.SelectedPlayerAgentIndex = agentIndex;
        BuildItemOptions(option.Agent, playerItemOptions);
        int itemIndex = playerItemOptions.IndexOf(option.Item);
        if (itemIndex < 0)
            return;

        itemsState.SelectedPlayerItemIndex = itemIndex;
        itemsState.PackHeldItemListDirty = true;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnPreviousPlayerItemClicked()
    {
        CycleSelection(playerItemOptions, ref itemsState.SelectedPlayerItemIndex, -1);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnNextPlayerItemClicked()
    {
        CycleSelection(playerItemOptions, ref itemsState.SelectedPlayerItemIndex, 1);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnPreviousTargetItemClicked()
    {
        CycleSelection(targetItemOptions, ref itemsState.SelectedTargetItemIndex, -1);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnNextTargetItemClicked()
    {
        CycleSelection(targetItemOptions, ref itemsState.SelectedTargetItemIndex, 1);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnUseClicked()
    {
        WorldObject user = sharedState.DisplayedPlayer;
        WorldObject item = itemsState.DisplayedPlayerItem;
        ContainerModule container = GetOrCreateContainer(user);
        if (user == null)
            return;

        if (item == null)
        {
            ShowInteractionMessage($"{user.DisplayName} has no item to use");
            return;
        }

        if (item.activatorModule == null)
        {
            ShowInteractionMessage($"{item.DisplayName} cannot be used");
            return;
        }

        ActivatorModule activator = item.activatorModule;
        string itemName = item.DisplayName;
        bool success = activator.TryUseItem(user, itemsState.DisplayedTarget);
        if (success && activator.parameterDestruct)
        {
            if (container != null && !container.ReleaseItem(item, out string reason))
            {
                ShowInteractionMessage(reason);
                Debug.LogWarning($"InteractionDialogUI: failed to destroy used item {itemName}: {reason}", this);
                RefreshInteractionView(forcePreviewRefresh: true);
                return;
            }

            Destroy(item.gameObject);
        }

        ShowInteractionMessage(success
            ? $"{user.DisplayName} used {itemName}"
            : $"{user.DisplayName} could not use {itemName}");

        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnEatClicked()
    {
        WorldObject eater = sharedState.DisplayedPlayer;
        ContainerModule container = GetOrCreateContainer(eater);
        if (eater == null || container == null)
            return;

        WorldObject item = itemsState.DisplayedPlayerItem;
        if (item == null)
        {
            ShowInteractionMessage($"{eater.DisplayName} has no item to eat");
            return;
        }

        string itemName = item.DisplayName;
        if (!container.ReleaseItem(item, out string reason))
        {
            ShowInteractionMessage(reason);
            Debug.LogWarning($"InteractionDialogUI: failed to eat {itemName}: {reason}", this);
            return;
        }

        Destroy(item.gameObject);
        ShowInteractionMessage($"{eater.DisplayName} ate {itemName}");
        itemsState.SelectedPlayerItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnDropClicked()
    {
        WorldObject carrier = sharedState.DisplayedPlayer;
        WorldObject item = itemsState.DisplayedPlayerItem;
        ContainerModule container = GetOrCreateContainer(carrier);
        if (carrier == null || item == null || container == null)
            return;

        if (!TryDropItemNearCarrier(container, carrier, item, out string reason))
        {
            ShowInteractionMessage(reason);
            Debug.LogWarning($"InteractionDialogUI: failed to drop {item.DisplayName}: {reason}", this);
            return;
        }

        ShowInteractionMessage($"{carrier.DisplayName} dropped {item.DisplayName}");
        itemsState.SelectedPlayerItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnThrowClicked()
    {
        WorldObject carrier = sharedState.DisplayedPlayer;
        WorldObject item = itemsState.DisplayedPlayerItem;
        ContainerModule container = GetOrCreateContainer(carrier);
        if (carrier == null || item == null || container == null)
            return;

        if (!TryThrowItemFromCarrier(container, carrier, item, out string reason))
        {
            ShowInteractionMessage(reason);
            Debug.LogWarning($"InteractionDialogUI: failed to throw {item.DisplayName}: {reason}", this);
            return;
        }

        ShowInteractionMessage($"{carrier.DisplayName} threw {item.DisplayName}");
        itemsState.SelectedPlayerItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnPickUpClicked()
    {
        WorldObject carrier = sharedState.DisplayedPlayer;
        ContainerModule container = GetOrCreateContainer(carrier);
        if (carrier == null || container == null)
            return;

        if (!container.TryPickupNearestItem(out WorldObject pickedUpItem, out string reason))
        {
            ShowInteractionMessage(reason);
            return;
        }

        ShowInteractionMessage($"{carrier.DisplayName} picked up {pickedUpItem.DisplayName}");
        itemsState.SelectedPlayerItemIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void OnGiveClicked()
    {
        WorldObject giver = sharedState.DisplayedPlayer;
        WorldObject item = itemsState.DisplayedPlayerItem;
        WorldObject recipient = itemsState.DisplayedTarget;
        ContainerModule giverContainer = GetOrCreateContainer(giver);
        ContainerModule recipientContainer = GetOrCreateContainer(recipient);

        if (giver == null || giverContainer == null)
            return;

        if (item == null)
        {
            ShowInteractionMessage($"{giver.DisplayName} has no item to give");
            return;
        }

        if (recipient == null || recipientContainer == null)
        {
            ShowInteractionMessage("No one nearby to give an item to");
            return;
        }

        if (TransferItem(giverContainer, recipientContainer, item, out string reason))
        {
            ShowInteractionMessage($"{giver.DisplayName} gave {item.DisplayName} to {recipient.DisplayName}");
            itemsState.SelectedPlayerItemIndex = 0;
            itemsState.SelectedTargetItemIndex = 0;
            RefreshInteractionView(forcePreviewRefresh: true);
            return;
        }

        ShowInteractionMessage(reason);
        Debug.LogWarning($"InteractionDialogUI: failed to give {item.DisplayName}: {reason}", this);
    }

    private void OnTakeItemClicked()
    {
        WorldObject taker = sharedState.DisplayedPlayer;
        WorldObject giver = itemsState.DisplayedTarget;
        WorldObject item = itemsState.DisplayedTargetItem;
        ContainerModule takerContainer = GetOrCreateContainer(taker);
        ContainerModule giverContainer = GetOrCreateContainer(giver);

        if (taker == null || takerContainer == null)
            return;

        if (giver == null || giverContainer == null)
        {
            ShowInteractionMessage("No one nearby to take an item from");
            return;
        }

        if (item == null)
        {
            ShowInteractionMessage($"{giver.DisplayName} has no selected item to take");
            return;
        }

        if (TransferItem(giverContainer, takerContainer, item, out string reason))
        {
            ShowInteractionMessage($"{taker.DisplayName} took {item.DisplayName} from {giver.DisplayName}");
            itemsState.SelectedTargetItemIndex = 0;
            itemsState.SelectedPlayerItemIndex = 0;
            RefreshInteractionView(forcePreviewRefresh: true);
            return;
        }

        ShowInteractionMessage(reason);
        Debug.LogWarning($"InteractionDialogUI: failed to take {item.DisplayName}: {reason}", this);
    }

    private void OnTradeClicked()
    {
        WorldObject trader = sharedState.DisplayedPlayer;
        WorldObject partner = itemsState.DisplayedTarget;
        WorldObject traderItem = itemsState.DisplayedPlayerItem;
        WorldObject partnerItem = itemsState.DisplayedTargetItem;
        ContainerModule traderContainer = GetOrCreateContainer(trader);
        ContainerModule partnerContainer = GetOrCreateContainer(partner);

        if (trader == null || traderContainer == null)
            return;

        if (partner == null || partnerContainer == null)
        {
            ShowInteractionMessage("No one nearby to trade with");
            return;
        }

        if (traderItem == null)
        {
            if (partnerItem != null)
            {
                OnTakeItemClicked();
                return;
            }

            ShowInteractionMessage($"{trader.DisplayName} has no item to trade");
            return;
        }

        if (partnerItem == null)
        {
            OnGiveClicked();
            return;
        }

        if (SwapItems(traderContainer, partnerContainer, traderItem, partnerItem, out string reason))
        {
            ShowInteractionMessage($"{trader.DisplayName} traded {traderItem.DisplayName} to {partner.DisplayName} for {partnerItem.DisplayName}");
            RefreshInteractionView(forcePreviewRefresh: true);
            return;
        }

        ShowInteractionMessage(reason);
        Debug.LogWarning($"InteractionDialogUI: failed to trade {traderItem.DisplayName} for {partnerItem.DisplayName}: {reason}", this);
    }

    private static bool TryDropItemNearCarrier(ContainerModule source, WorldObject carrier, WorldObject item, out string reason)
    {
        if (source == null)
        {
            reason = "Source inventory is unavailable.";
            return false;
        }

        if (carrier == null)
        {
            reason = "No carrier selected.";
            return false;
        }

        if (item == null)
        {
            reason = "No item selected.";
            return false;
        }

        return source.DropItemOnGround(item, GetDropPositionNearCarrier(carrier, item), out reason);
    }

    private bool TryThrowItemFromCarrier(ContainerModule source, WorldObject carrier, WorldObject item, out string reason)
    {
        if (source == null)
        {
            reason = "Source inventory is unavailable.";
            return false;
        }

        if (carrier == null)
        {
            reason = "No carrier selected.";
            return false;
        }

        if (item == null)
        {
            reason = "No item selected.";
            return false;
        }

        Vector3 direction = GetFacingDirection(carrier);
        KineticModule kinetic = EnsureKineticModule(item);
        if (kinetic == null)
        {
            reason = $"{item.DisplayName} could not add a KineticModule.";
            return false;
        }

        Vector3 releasePosition = GetThrowReleasePosition(carrier, item, direction);
        if (!source.DropItemOnGround(item, releasePosition, out reason))
            return false;

        kinetic.Stop();
        kinetic.ApplyImpulse((direction * throwForwardImpulse) + (Vector3.up * throwUpwardImpulse));
        NotifyFetchQuestModulesObjectThrown(item, carrier);
        reason = string.Empty;
        return true;
    }

    private static void NotifyFetchQuestModulesObjectThrown(WorldObject thrownItem, WorldObject thrower)
    {
        if (thrownItem == null)
            return;

        if (thrower != null && thrower.fetchQuestModule is FetchQuestModule throwerFetchQuest)
            throwerFetchQuest.ObserveObjectThrown(thrownItem);

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (registry == null)
            return;

        foreach (WorldObject candidate in registry.GetAllObjects())
        {
            if (candidate == null || candidate == thrower)
                continue;

            if (candidate.fetchQuestModule is FetchQuestModule fetchQuest && fetchQuest.IsRunning)
                fetchQuest.ObserveObjectThrown(thrownItem);
        }
    }

    private static bool TransferItem(ContainerModule source, ContainerModule destination, WorldObject item, out string reason)
    {
        if (source == null)
        {
            reason = "Source inventory is unavailable.";
            return false;
        }

        if (destination == null)
        {
            reason = "Destination inventory is unavailable.";
            return false;
        }

        if (source == destination)
        {
            reason = "Cannot transfer an item to the same inventory.";
            return false;
        }

        if (item == null)
        {
            reason = "No item selected.";
            return false;
        }

        if (!source.ReleaseItem(item, out reason))
            return false;

        if (destination.ReceiveItem(item, false, out reason))
            return true;

        string receiveFailure = reason;
        if (!source.ReceiveItem(item, false, out string rollbackReason))
            reason = $"{receiveFailure} {item.DisplayName} could not be returned: {rollbackReason}";
        else
        reason = receiveFailure;

        return false;
    }

    private static bool SwapItems(ContainerModule firstContainer, ContainerModule secondContainer, WorldObject firstItem, WorldObject secondItem, out string reason)
    {
        if (firstContainer == null || secondContainer == null)
        {
            reason = "One of the inventories is unavailable.";
            return false;
        }

        if (firstContainer == secondContainer)
        {
            reason = "Cannot trade within the same inventory.";
            return false;
        }

        if (firstItem == null || secondItem == null)
        {
            reason = "Both sides need an item selected to trade.";
            return false;
        }

        if (!firstContainer.ReleaseItem(firstItem, out reason))
            return false;

        if (!secondContainer.ReleaseItem(secondItem, out string secondReleaseReason))
        {
            RestoreItem(firstContainer, firstItem);
            reason = secondReleaseReason;
            return false;
        }

        bool firstReceivedSecond = firstContainer.ReceiveItem(secondItem, false, out string firstReceiveReason);
        bool secondReceivedFirst = secondContainer.ReceiveItem(firstItem, false, out string secondReceiveReason);

        if (firstReceivedSecond && secondReceivedFirst)
        {
            reason = string.Empty;
            return true;
        }

        if (firstReceivedSecond)
            firstContainer.ReleaseItem(secondItem, out _);

        if (secondReceivedFirst)
            secondContainer.ReleaseItem(firstItem, out _);

        RestoreItem(firstContainer, firstItem);
        RestoreItem(secondContainer, secondItem);

        reason = !firstReceivedSecond ? firstReceiveReason : secondReceiveReason;
        return false;
    }

    private static void RestoreItem(ContainerModule container, WorldObject item)
    {
        if (container != null && item != null)
            container.ReceiveItem(item, false, out _);
    }

    #endregion

    #region Helpers

    private static void AddPackHeldItemOptionsForAgent(List<PackHeldItemOption> options, WorldObject agent)
    {
        if (agent == null || !agent.gameObject.activeInHierarchy)
            return;

        ContainerModule container = GetOrCreateContainer(agent);
        if (container == null || container.HeldItemCount <= 0)
            return;

        for (int i = 0; i < container.HeldItemCount; i++)
        {
            WorldObject item = container.HeldItems[i];
            if (item != null)
                options.Add(new PackHeldItemOption(agent, item));
        }
    }

    private void RebuildPackHeldItemListRows()
    {
        ClearPackHeldItemListRows();

        for (int i = 0; i < packHeldItemOptions.Count; i++)
            CreatePackHeldItemListRow(packHeldItemOptions[i], i);

        LayoutRebuilder.ForceRebuildLayoutImmediate(packHeldItemListContentRect);
    }

    private static KineticModule EnsureKineticModule(WorldObject item)
    {
        if (item == null)
            return null;

        KineticModule kinetic = item.kineticModule != null
        ? item.kineticModule
        : item.GetComponent<KineticModule>();

        if (kinetic != null)
            return kinetic;

        item.CreateModulesIfNeeded(ModuleFlags.kineticModule);
        return item.kineticModule != null
        ? item.kineticModule
        : item.GetComponent<KineticModule>();
    }

    #endregion
}
