using System.Collections.Generic;
using DogGame;
using DogGame.Modules;
using UnityEngine;
using UnityEngine.UI;
public sealed partial class InteractionDialogUI : MonoBehaviour
{
    #region Fields

    private GameObject socialEmoteGridObject;

    private RectTransform socialEmoteGridContentRect;

    private ScrollRect socialEmoteGridScrollRect;

    private const int HumanEmojiCount = 32;

    private const int SocialEmoteGridColumns = 5;

    private const float SocialEmoteTileSize = 72f;

    private int selectedSocialTargetIndex;

    private WorldObject displayedSocialLeft;

    private WorldObject displayedSocialRight;

    private bool displayedSocialEmoteGridUsesHuman;

    private bool displayedSocialEmoteGridInitialized;

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
        gridRect.anchoredPosition = new Vector2(-425f, -690f);
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

        GridLayoutGroup layout = contentObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(SocialEmoteTileSize, SocialEmoteTileSize);
        layout.spacing = new Vector2(8f, 8f);
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = SocialEmoteGridColumns;

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
        GameObject tileObject = CreateSocialEmoteTile($"SocialHumanEmote_{spriteIndex}", sprite, $"Human Emote {spriteIndex + 1}");
        Button button = tileObject.GetComponent<Button>();
        int capturedIndex = spriteIndex;
        button.onClick.AddListener(() => HandleSocialHumanEmoteClicked(capturedIndex));
    }

    private GameObject CreateSocialEmoteTile(string objectName, Sprite sprite, string tooltipText)
    {
        GameObject tileObject = CreateUIObject(objectName, socialEmoteGridContentRect);
        LayoutElement layout = tileObject.AddComponent<LayoutElement>();
        layout.preferredWidth = SocialEmoteTileSize;
        layout.preferredHeight = SocialEmoteTileSize;
        layout.minWidth = SocialEmoteTileSize;
        layout.minHeight = SocialEmoteTileSize;

        Image background = tileObject.AddComponent<Image>();
        background.color = new Color(0.2f, 0.15f, 0.08f, 0.9f);
        background.raycastTarget = true;

        Button button = tileObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);

        GameObject iconObject = CreateUIObject("Icon", tileObject.transform);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = Vector2.one * (SocialEmoteTileSize - 16f);

        Image iconImage = iconObject.AddComponent<Image>();
        iconImage.sprite = sprite;
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;
        iconImage.raycastTarget = false;

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
        ApplyPendingSelection(playerAgentOptions, pendingLeftAgentSelection, ref selectedPlayerAgentIndex);
        WorldObject leftMember = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
        RefreshSocialEmoteGrid(leftMember);
        BuildSocialTargetOptions(leftMember);
        ApplyPendingSelection(socialTargetOptions, pendingRightAgentSelection, ref selectedSocialTargetIndex);
        WorldObject rightMember = GetSelectedFromList(socialTargetOptions, ref selectedSocialTargetIndex);

        RefreshCircleAndHotspot(playerPreviewSlot, playerAgentOptions.Count, previousPlayerAgentButton, nextPlayerAgentButton);
        RefreshCircleAndHotspot(targetPreviewSlot, socialTargetOptions.Count, previousTargetAgentButton, nextTargetAgentButton);
        RefreshCircleAndHotspot(playerItemPreviewSlot, 0, previousPlayerItemButton, nextPlayerItemButton);
        RefreshCircleAndHotspot(targetItemPreviewSlot, 0, previousTargetItemButton, nextTargetItemButton);

        SetLabelText(playerNameLabel, leftMember != null ? leftMember.DisplayName : string.Empty);
        SetLabelText(playerHeldItemLabel, string.Empty);
        SetLabelText(targetNameLabel, rightMember != null ? rightMember.DisplayName : string.Empty);
        SetLabelText(targetHeldItemLabel, string.Empty);

        if (forcePreviewRefresh || leftMember != displayedSocialLeft)
            BuildPreviewClone(playerPreviewSlot, leftMember, "SocialLeft");
        if (forcePreviewRefresh || rightMember != displayedSocialRight)
            BuildPreviewClone(targetPreviewSlot, rightMember, "SocialRight");

        BuildPreviewClone(playerItemPreviewSlot, null, "SocialLeftItem");
        BuildPreviewClone(targetItemPreviewSlot, null, "SocialRightItem");

        displayedSocialLeft = leftMember;
        displayedSocialRight = rightMember;
        displayedPlayer = leftMember;
        displayedPlayerItem = null;
        displayedTarget = rightMember;
        displayedTargetItem = null;
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
        if (displayedSocialEmoteGridInitialized && displayedSocialEmoteGridUsesHuman == useHumanSet)
            return;

        ClearSocialEmoteGridTiles();
        displayedSocialEmoteGridUsesHuman = useHumanSet;
        displayedSocialEmoteGridInitialized = true;

        if (useHumanSet)
        {
            for (int i = 0; i < HumanEmojiCount; i++)
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
        WorldObject previousSelection = GetSelectedFromList(socialTargetOptions, ref selectedSocialTargetIndex);
        socialTargetOptions.Clear();

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (player == null || registry == null)
        {
            selectedSocialTargetIndex = 0;
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

        KeepSelectedObject(socialTargetOptions, previousSelection, ref selectedSocialTargetIndex);
    }

    private static Sprite GetDogEmoteSprite(DogEmojiEntry entry)
    {
        return SpriteServer.SpriteLookup(entry.EntryId)
        ?? SpriteServer.SpriteSheetLookup($"DogEmojiSheet{entry.SheetId}", entry.SpriteIndex);
    }

    private static Sprite GetHumanEmoteSprite(int spriteIndex)
    {
        return SpriteServer.SpriteSheetLookup("Sprites/Emotes/Human_Emoji_A", spriteIndex)
        ?? SpriteServer.SpriteSheetLookup("Human_Emoji_A", spriteIndex);
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
        WorldObject actor = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
        if (actor == null)
            return;

        BottomBanner.LogEmote(actor, entry.EntryId);
    }

    private void HandleSocialHumanEmoteClicked(int spriteIndex)
    {
        WorldObject actor = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
        if (actor == null)
            return;

        Sprite sprite = GetHumanEmoteSprite(spriteIndex);
        if (sprite != null)
            EmoteIconVisualFactory.Show(actor, sprite);
    }

    private void CycleSocialLeftSelection(int direction)
    {
        BuildPlayerAgentOptions();
        if (playerAgentOptions.Count <= 1)
            return;

        CycleSelection(playerAgentOptions, ref selectedPlayerAgentIndex, direction);
        selectedSocialTargetIndex = 0;
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    private void CycleSocialRightSelection(int direction)
    {
        BuildPlayerAgentOptions();
        WorldObject player = GetSelectedFromList(playerAgentOptions, ref selectedPlayerAgentIndex);
        BuildSocialTargetOptions(player);
        if (socialTargetOptions.Count <= 1)
            return;

        CycleSelection(socialTargetOptions, ref selectedSocialTargetIndex, direction);
        RefreshInteractionView(forcePreviewRefresh: true);
    }

    #endregion
}
