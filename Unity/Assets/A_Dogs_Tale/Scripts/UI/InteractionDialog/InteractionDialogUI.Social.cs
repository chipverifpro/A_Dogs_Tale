using System.Collections.Generic;
using DogGame;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public sealed partial class InteractionDialogUI : MonoBehaviour
{
    #region Fields

    private GameObject socialEmoteGridObject;

    private RectTransform socialEmoteGridContentRect;

    private ScrollRect socialEmoteGridScrollRect;

    private const float SocialEmoteRowHeight = 128f;

    private const float SocialEmoteIconSize = 112f;

    [SerializeField, Min(0f)] private float socialNearbyRadiusMultiplier = 2f;

    private readonly List<GameObject> socialEmoteGridTiles = new();

    private readonly List<WorldObject> socialTargetOptions = new();

    #endregion

    #region UI Construction

    private void BuildSocialEmoteGrid(Transform parent)
    {
        socialEmoteGridObject = CreateUIObject("SocialEmoteGrid", parent);
        RectTransform gridRect = socialEmoteGridObject.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 1f);
        gridRect.anchorMax = new Vector2(0.5f, 1f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.anchoredPosition = new Vector2(-305f, -690f);
        gridRect.sizeDelta = new Vector2(470f, 300f);

        Image gridBackground = socialEmoteGridObject.AddComponent<Image>();
        gridBackground.color = new Color(0.09f, 0.065f, 0.035f, 0.58f);
        gridBackground.raycastTarget = true;

        socialEmoteGridScrollRect = socialEmoteGridObject.AddComponent<ScrollRect>();
        socialEmoteGridScrollRect.horizontal = false;
        socialEmoteGridScrollRect.vertical = true;
        socialEmoteGridScrollRect.movementType = ScrollRect.MovementType.Clamped;
        socialEmoteGridScrollRect.scrollSensitivity = 24f;

        GameObject viewportObject = CreateUIObject("Viewport", socialEmoteGridObject.transform);
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
        socialEmoteGridContentRect = contentObject.GetComponent<RectTransform>();
        socialEmoteGridContentRect.anchorMin = new Vector2(0f, 1f);
        socialEmoteGridContentRect.anchorMax = new Vector2(1f, 1f);
        socialEmoteGridContentRect.pivot = new Vector2(0.5f, 1f);
        socialEmoteGridContentRect.anchoredPosition = Vector2.zero;
        socialEmoteGridContentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 6f;
        layout.padding = new RectOffset(6, 6, 6, 6);

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        socialEmoteGridScrollRect.viewport = viewportRect;
        socialEmoteGridScrollRect.content = socialEmoteGridContentRect;
        socialEmoteGridObject.SetActive(false);
    }

    private void CreateSocialDogEmoteTile(DogEmojiEntry entry, Sprite sprite)
    {
        GameObject tileObject = CreateSocialEmoteTile($"SocialDogEmote_{entry.EntryId}", sprite, entry.Name);
        Button button = tileObject.GetComponent<Button>();
        button.onClick.AddListener(() => HandleSocialDogEmoteClicked(entry));
    }

    private void CreateSocialHumanEmoteTile(int spriteIndex, Sprite sprite)
    {
        GameObject tileObject = CreateSocialEmoteTile($"SocialHumanEmote_{spriteIndex}", sprite, SpriteServer.GetHumanEmojiDisplayName(spriteIndex));
        Button button = tileObject.GetComponent<Button>();
        int capturedIndex = spriteIndex;
        button.onClick.AddListener(() => HandleSocialHumanEmoteClicked(capturedIndex));
    }

    private GameObject CreateSocialEmoteTile(string objectName, Sprite sprite, string tooltipText)
    {
        GameObject tileObject = CreateUIObject(objectName, socialEmoteGridContentRect);
        LayoutElement layout = tileObject.AddComponent<LayoutElement>();
        layout.preferredHeight = SocialEmoteRowHeight;
        layout.minHeight = SocialEmoteRowHeight;

        Image background = tileObject.AddComponent<Image>();
        background.color = new Color(0.2f, 0.15f, 0.08f, 0.9f);
        background.raycastTarget = true;

        Button button = tileObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);

        GameObject iconObject = CreateUIObject("Icon", tileObject.transform);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(66f, 0f);
        iconRect.sizeDelta = Vector2.one * SocialEmoteIconSize;

        Image iconImage = iconObject.AddComponent<Image>();
        iconImage.sprite = sprite;
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;
        iconImage.raycastTarget = false;

        GameObject labelObject = CreateUIObject("Label", tileObject.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(136f, 12f);
        labelRect.offsetMax = new Vector2(-14f, -12f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = tooltipText;
        label.fontSize = 26f;
        label.color = new Color(1f, 0.88f, 0.58f, 1f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        ConfigureTooltip(tileObject, tooltipText);
        socialEmoteGridTiles.Add(tileObject);
        return tileObject;
    }

    #endregion

    #region UI Refresh

    private void RefreshSocialView(bool forcePreviewRefresh = false)
    {
        SetPackControlsActive(false);
        SetQuestControlsActive(false);
        SetScentControlsActive(false);
        SetSocialControlsActive(true);
        SetPackIndicatorButtonsActive(false);
        SetItemsControlsActive(false);
        SetPreviewSlotActive(playerItemPreviewSlot, false);
        SetPreviewSlotActive(targetItemPreviewSlot, false);
        SetItemSelectionTypeLabelsActive(false);

        BuildPlayerAgentOptions();
        ApplyPendingSelection(playerAgentOptions, sharedState.PendingLeftAgentSelection, ref sharedState.SelectedPlayerAgentIndex);
        WorldObject leftMember = GetSelectedFromList(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex);
        RefreshSocialEmoteGrid(leftMember);
        BuildSocialTargetOptions(leftMember);
        ApplyPendingSelection(socialTargetOptions, sharedState.PendingRightAgentSelection, ref socialState.SelectedTargetIndex);
        WorldObject rightMember = GetSelectedFromList(socialTargetOptions, ref socialState.SelectedTargetIndex);

        RefreshCircleAndHotspot(playerPreviewSlot, playerAgentOptions.Count, previousPlayerAgentButton, nextPlayerAgentButton);
        RefreshCircleAndHotspot(targetPreviewSlot, socialTargetOptions.Count, previousTargetAgentButton, nextTargetAgentButton);
        RefreshCircleAndHotspot(playerItemPreviewSlot, 0, previousPlayerItemButton, nextPlayerItemButton);
        RefreshCircleAndHotspot(targetItemPreviewSlot, 0, previousTargetItemButton, nextTargetItemButton);

        SetLabelText(playerNameLabel, leftMember != null ? leftMember.DisplayName : string.Empty);
        SetLabelText(playerHeldItemLabel, string.Empty);
        SetLabelText(targetNameLabel, rightMember != null ? rightMember.DisplayName : string.Empty);
        SetLabelText(targetHeldItemLabel, string.Empty);
        SetBottomTargetAgentLabel(leftMember);

        if (forcePreviewRefresh || leftMember != socialState.DisplayedLeft)
            BuildPreviewClone(playerPreviewSlot, leftMember, "SocialLeft");
        if (forcePreviewRefresh || rightMember != socialState.DisplayedRight)
            BuildPreviewClone(targetPreviewSlot, rightMember, "SocialRight");

        BuildPreviewClone(playerItemPreviewSlot, null, "SocialLeftItem");
        BuildPreviewClone(targetItemPreviewSlot, null, "SocialRightItem");

        socialState.DisplayedLeft = leftMember;
        socialState.DisplayedRight = rightMember;
        sharedState.DisplayedPlayer = leftMember;
        itemsState.DisplayedPlayerItem = null;
        itemsState.DisplayedTarget = rightMember;
        itemsState.DisplayedTargetItem = null;
        ClearPendingSelections();
    }

    private void SetSocialControlsActive(bool active)
    {
        if (socialEmoteGridObject != null)
        {
            socialEmoteGridObject.SetActive(active);
            if (active)
                socialEmoteGridObject.transform.SetAsLastSibling();
        }
    }

    private void RefreshSocialEmoteGrid(WorldObject leftMember)
    {
        if (socialEmoteGridContentRect == null)
            return;

        bool useHumanSet = leftMember != null && leftMember.species == Species.Human;
        if (socialState.DisplayedEmoteGridInitialized && socialState.DisplayedEmoteGridUsesHuman == useHumanSet)
            return;

        ClearSocialEmoteGridTiles();
        socialState.DisplayedEmoteGridUsesHuman = useHumanSet;
        socialState.DisplayedEmoteGridInitialized = true;

        if (useHumanSet)
        {
            for (int i = 0; i < SpriteServer.HumanEmojiCount; i++)
            {
                Sprite sprite = GetHumanEmoteSprite(i);
                if (sprite != null)
                    CreateSocialHumanEmoteTile(i, sprite);
            }
        }
        else
        {
            for (int i = 0; i < DogEmojiCatalog.Entries.Length; i++)
            {
                DogEmojiEntry entry = DogEmojiCatalog.Entries[i];
                Sprite sprite = GetDogEmoteSprite(entry);
                if (sprite != null)
                    CreateSocialDogEmoteTile(entry, sprite);
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(socialEmoteGridContentRect);
        if (socialEmoteGridScrollRect != null)
            socialEmoteGridScrollRect.verticalNormalizedPosition = 1f;
    }

    private void ClearSocialEmoteGridTiles()
    {
        socialEmoteGridTiles.Clear();

        if (socialEmoteGridContentRect == null)
            return;

        ClearListContent(socialEmoteGridContentRect);
    }

    #endregion

    #region Selection State

    private void BuildSocialTargetOptions(WorldObject player)
    {
        WorldObject previousSelection = GetSelectedFromList(socialTargetOptions, ref socialState.SelectedTargetIndex);
        socialTargetOptions.Clear();

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (player == null || registry == null)
        {
            socialState.SelectedTargetIndex = 0;
            return;
        }

        float radius = tradePartnerSearchRadiusTiles * socialNearbyRadiusMultiplier;
        float radiusSqr = radius * radius;
        Vector3 playerPosition = player.pos3d_map;

        foreach (WorldObject candidate in registry.GetAllObjects())
        {
            if (candidate == null || candidate == player || !candidate.gameObject.activeInHierarchy)
                continue;

            if (!CanUseAsSocialTarget(candidate))
                continue;

            Vector3 delta = candidate.pos3d_map - playerPosition;
            delta.y = 0f;
            float distanceSqr = delta.sqrMagnitude;
            if (distanceSqr > radiusSqr)
                continue;

            socialTargetOptions.Add(candidate);
        }

        socialTargetOptions.Sort((a, b) =>
        {
                float aDistanceSqr = GetPlanarDistanceSqr(playerPosition, a.pos3d_map);
                float bDistanceSqr = GetPlanarDistanceSqr(playerPosition, b.pos3d_map);
                int distanceComparison = aDistanceSqr.CompareTo(bDistanceSqr);
                if (distanceComparison != 0)
                return distanceComparison;

                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
        });

        KeepSelectedObject(socialTargetOptions, previousSelection, ref socialState.SelectedTargetIndex);
    }

    private static Sprite GetDogEmoteSprite(DogEmojiEntry entry)
    {
        return SpriteServer.SpriteLookup(entry.EntryId)
        ?? SpriteServer.SpriteSheetLookup($"DogEmojiSheet{entry.SheetId}", entry.SpriteIndex);
    }

    private static Sprite GetHumanEmoteSprite(int spriteIndex)
    {
        return SpriteServer.GetHumanEmojiSprite(spriteIndex);
    }

    private static bool CanUseAsSocialTarget(WorldObject candidate)
    {
        if (candidate == null)
            return false;

        return candidate.Kind == WorldObjectKind.Agent ||
        candidate.agentModule != null ||
        candidate.GetComponent<AgentModule>() != null;
    }

    #endregion

    #region Input And Actions

    private void HandleSocialDogEmoteClicked(DogEmojiEntry entry)
    {
        WorldObject actor = GetSelectedFromList(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex);
        if (actor == null)
            return;

        BottomBanner.LogEmote(actor, entry.EntryId);
    }

    private void HandleSocialHumanEmoteClicked(int spriteIndex)
    {
        WorldObject actor = GetSelectedFromList(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex);
        if (actor == null)
            return;

        BottomBanner.LogHumanEmote(actor, spriteIndex);
    }

    private void CycleSocialLeftSelection(int direction)
    {
        BuildPlayerAgentOptions();
        if (playerAgentOptions.Count <= 1)
            return;

        CycleSelection(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex, direction);
        socialState.SelectedTargetIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void CycleSocialRightSelection(int direction)
    {
        BuildPlayerAgentOptions();
        WorldObject player = GetSelectedFromList(playerAgentOptions, ref sharedState.SelectedPlayerAgentIndex);
        BuildSocialTargetOptions(player);
        if (socialTargetOptions.Count <= 1)
            return;

        CycleSelection(socialTargetOptions, ref socialState.SelectedTargetIndex, direction);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    #endregion
}
