using System.Collections.Generic;
using DogGame;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

public partial class TopPulldown
{
    private void BuildEmoteDropdown(Transform parent, Transform searchRoot)
    {
        Transform existingDropdown = FindExistingUiElement(parent, searchRoot, "EmoteDropdown");
        if (existingDropdown != null)
        {
            BindExistingEmoteDropdown(existingDropdown.gameObject);
            return;
        }

        GameObject dropdownObject = new GameObject(
            "EmoteDropdown",
            typeof(RectTransform),
            typeof(Image));
        dropdownObject.transform.SetParent(GetTopLevelOverlayParent(parent), false);

        emoteDropdownRect = dropdownObject.GetComponent<RectTransform>();
        ConfigureTopPanelRect(emoteDropdownRect, 4);
        emoteDropdownRect.sizeDelta = new Vector2(GetVisibleEmoteDropdownWidth(), GetVisibleEmoteDropdownHeight());
        ApplyCenteredEmoteDropdownPosition();

        Image dropdownImage = dropdownObject.GetComponent<Image>();
        bool hasFrame = ApplyPanelFrame(dropdownImage, GetEmoteFrameSprite());
        if (!hasFrame)
            dropdownImage.color = dropdownBackgroundColor;

        GameObject titleObject = CreateTMPLabel(
            parent: dropdownObject.transform,
            name: "Title",
            text: "Emotes",
            fontSize: 26f,
            alignment: TextAlignmentOptions.Left);

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(14f, -42f);
        titleRect.offsetMax = new Vector2(-14f, -10f);
        titleObject.SetActive(!hasFrame);

        GameObject scrollObject = new GameObject(
            "ScrollView",
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect));
        scrollObject.transform.SetParent(dropdownObject.transform, false);

        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = hasFrame ? new Vector2(58f, 54f) : new Vector2(12f, 12f);
        scrollRectTransform.offsetMax = hasFrame ? new Vector2(-58f, -132f) : new Vector2(-16f, -48f);

        Image scrollBackground = scrollObject.GetComponent<Image>();
        scrollBackground.color = new Color(1f, 1f, 1f, 0.08f);

        emoteDropdownScrollRect = scrollObject.GetComponent<ScrollRect>();
        emoteDropdownScrollRect.horizontal = false;
        emoteDropdownScrollRect.movementType = ScrollRect.MovementType.Clamped;
        emoteDropdownScrollRect.scrollSensitivity = 28f;

        GameObject viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(Mask));
        viewportObject.transform.SetParent(scrollObject.transform, false);

        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = new Vector2(-14f, 0f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(GridLayoutGroup),
            typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);

        emoteDropdownContentRect = contentObject.GetComponent<RectTransform>();
        emoteDropdownContentRect.anchorMin = new Vector2(0f, 1f);
        emoteDropdownContentRect.anchorMax = new Vector2(1f, 1f);
        emoteDropdownContentRect.pivot = new Vector2(0.5f, 1f);
        emoteDropdownContentRect.offsetMin = Vector2.zero;
        emoteDropdownContentRect.offsetMax = Vector2.zero;

        GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(8, 8, 8, 8);
        grid.cellSize = new Vector2(emoteTileSize, emoteTileSize);
        grid.spacing = new Vector2(8f, 8f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, emoteGridColumns);

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = CreateScrollbar(scrollObject.transform);
        emoteDropdownScrollRect.viewport = viewportRect;
        emoteDropdownScrollRect.content = emoteDropdownContentRect;
        emoteDropdownScrollRect.verticalScrollbar = scrollbar;
        emoteDropdownScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        emoteDropdownScrollRect.verticalScrollbarSpacing = 4f;

        dropdownObject.SetActive(false);
    }

    private void BindExistingEmoteDropdown(GameObject dropdownObject)
    {
        emoteDropdownRect = dropdownObject.GetComponent<RectTransform>();
        if (emoteDropdownRect == null)
            return;

        dropdownObject.transform.SetParent(GetTopLevelOverlayParent(dropdownObject.transform.parent), worldPositionStays: false);
        ConfigureTopPanelRect(emoteDropdownRect, 4);
        ApplyCenteredEmoteDropdownPosition();

        Image dropdownImage = dropdownObject.GetComponent<Image>();
        bool hasFrame = ApplyPanelFrame(dropdownImage, GetEmoteFrameSprite());
        if (dropdownImage != null && !hasFrame)
            dropdownImage.color = dropdownBackgroundColor;

        Transform titleTransform = dropdownObject.transform.Find("Title");
        if (titleTransform != null)
            titleTransform.gameObject.SetActive(!hasFrame);

        Transform scrollTransform = dropdownObject.transform.Find("ScrollView");
        emoteDropdownScrollRect = scrollTransform != null ? scrollTransform.GetComponent<ScrollRect>() : null;
        if (scrollTransform != null)
        {
            RectTransform scrollRectTransform = scrollTransform.GetComponent<RectTransform>();
            if (scrollRectTransform != null)
            {
                scrollRectTransform.offsetMin = hasFrame ? new Vector2(58f, 54f) : new Vector2(12f, 12f);
                scrollRectTransform.offsetMax = hasFrame ? new Vector2(-58f, -132f) : new Vector2(-16f, -48f);
            }
        }
        Transform contentTransform = dropdownObject.transform.Find("ScrollView/Viewport/Content");
        emoteDropdownContentRect = contentTransform != null ? contentTransform.GetComponent<RectTransform>() : null;

        dropdownObject.SetActive(false);
    }

    private void ToggleEmoteDropdown()
    {
        if (emoteDropdownRect == null)
            return;

        bool shouldOpen = !emoteDropdownRect.gameObject.activeSelf;
        if (shouldOpen)
            OpenEmoteDropdown();
        else
            CloseEmoteDropdown();
    }

    private void OpenEmoteDropdown()
    {
        if (emoteDropdownRect == null)
            return;

        CloseDropdown();
        CloseModePanel();
        CloseSpeedPanel();
        EnsureDefaultEmoteSelection();
        RefreshEmoteDropdownContents();
        ApplyCenteredEmoteDropdownPosition();
        emoteDropdownRect.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        ClampEmoteDropdownToCanvas();
        if (emoteDropdownScrollRect != null)
            emoteDropdownScrollRect.verticalNormalizedPosition = 1f;
    }

    private void CloseEmoteDropdown()
    {
        if (emoteDropdownRect != null)
            emoteDropdownRect.gameObject.SetActive(false);

        HideTooltip();
    }

    private void RefreshEmoteDropdownContents()
    {
        if (emoteDropdownContentRect != null)
        {
            for (int childIndex = emoteDropdownContentRect.childCount - 1; childIndex >= 0; childIndex--)
                Destroy(emoteDropdownContentRect.GetChild(childIndex).gameObject);
        }
        else
        {
            for (int i = 0; i < emoteDropdownTiles.Count; i++)
            {
                if (emoteDropdownTiles[i] != null)
                    Destroy(emoteDropdownTiles[i]);
            }
        }
        emoteDropdownTiles.Clear();

        if (emoteDropdownContentRect == null)
            return;

        int visibleEntryCount = 0;
        for (int i = 0; i < DogEmojiCatalog.Entries.Length; i++)
        {
            DogEmojiEntry entry = DogEmojiCatalog.Entries[i];
            if (GetEmoteSprite(entry) == null)
                continue;

            emoteDropdownTiles.Add(CreateEmoteTile(entry, entry.EntryId == GetSelectedEmoteId()));
            visibleEntryCount++;
        }

        if (visibleEntryCount == 0)
        {
            emoteDropdownTiles.Add(CreateInfoRowForParent(emoteDropdownContentRect, "No emotes found in the sprite sheets."));
            ResizeEmoteDropdown(1);
            return;
        }

        ResizeEmoteDropdown(visibleEntryCount);
    }

    private void ResizeEmoteDropdown(int entryCount)
    {
        if (emoteDropdownRect == null)
            return;

        int columnCount = Mathf.Max(1, emoteGridColumns);
        int rowCount = Mathf.Max(1, Mathf.CeilToInt(entryCount / (float)columnCount));
        float headerHeight = 56f;
        float chrome = 32f;
        float spacing = 8f;
        float desiredHeight = headerHeight + chrome + (rowCount * emoteTileSize) + (Mathf.Max(0, rowCount - 1) * spacing) + 16f;
        emoteDropdownRect.sizeDelta = new Vector2(GetVisibleEmoteDropdownWidth(), Mathf.Min(GetVisibleEmoteDropdownHeight(), desiredHeight));
        ClampEmoteDropdownToCanvas();
    }

    private float GetVisibleEmoteDropdownWidth()
    {
        const float margin = 12f;
        RectTransform canvasRect = overlayCanvas != null ? overlayCanvas.transform as RectTransform : null;
        if (canvasRect == null || canvasRect.rect.width <= 0f)
            return emoteDropdownWidth;

        return Mathf.Min(emoteDropdownWidth, Mathf.Max(1f, canvasRect.rect.width - (margin * 2f)));
    }

    private float GetVisibleEmoteDropdownHeight()
    {
        const float margin = 12f;
        RectTransform canvasRect = overlayCanvas != null ? overlayCanvas.transform as RectTransform : null;
        if (canvasRect == null || canvasRect.rect.height <= 0f)
            return emoteDropdownMaxHeight;

        return Mathf.Min(emoteDropdownMaxHeight, Mathf.Max(1f, canvasRect.rect.height - (margin * 2f)));
    }

    private void ClampEmoteDropdownToCanvas()
    {
        if (emoteDropdownRect == null || overlayCanvas == null)
            return;

        RectTransform canvasRect = overlayCanvas.transform as RectTransform;
        if (canvasRect == null || canvasRect.rect.width <= 0f || canvasRect.rect.height <= 0f)
            return;

        float margin = 12f;
        Vector2 size = emoteDropdownRect.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            size = emoteDropdownRect.sizeDelta;

        float canvasLeft = canvasRect.rect.xMin + margin;
        float canvasRight = canvasRect.rect.xMax - margin;
        float canvasBottom = canvasRect.rect.yMin + margin;
        float canvasTop = canvasRect.rect.yMax - margin;

        Vector2 pivot = emoteDropdownRect.pivot;
        Vector2 position = emoteDropdownRect.anchoredPosition;
        float left = position.x - (size.x * pivot.x);
        float right = left + size.x;
        float top = position.y + (size.y * (1f - pivot.y));
        float bottom = top - size.y;

        if (left < canvasLeft)
            position.x += canvasLeft - left;
        else if (right > canvasRight)
            position.x -= right - canvasRight;

        if (bottom < canvasBottom)
            position.y += canvasBottom - bottom;
        else if (top > canvasTop)
            position.y -= top - canvasTop;

        emoteDropdownRect.anchoredPosition = position;
    }

    private GameObject CreateInfoRowForParent(Transform parent, string message)
    {
        GameObject rowObject = new GameObject(
            "InfoRow",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        rowObject.transform.SetParent(parent, false);

        LayoutElement layout = rowObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 52f;

        Image background = rowObject.GetComponent<Image>();
        background.color = dropdownRowColor;

        GameObject labelObject = CreateTMPLabel(
            rowObject.transform,
            "Label",
            message,
            20f,
            TextAlignmentOptions.Left);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(14f, 8f);
        labelRect.offsetMax = new Vector2(-14f, -8f);

        return rowObject;
    }

    private GameObject CreateEmoteTile(DogEmojiEntry entry, bool isSelected)
    {
        GameObject tileObject = new GameObject(
            $"Emote_{entry.EntryId}",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        tileObject.transform.SetParent(emoteDropdownContentRect, false);

        LayoutElement layout = tileObject.GetComponent<LayoutElement>();
        layout.preferredWidth = emoteTileSize;
        layout.preferredHeight = emoteTileSize;

        Image background = tileObject.GetComponent<Image>();
        background.color = isSelected ? dropdownSelectedColor : dropdownRowColor;

        Button button = tileObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(() => HandleEmoteSelected(entry));

        GameObject iconObject = new GameObject(
            "Icon",
            typeof(RectTransform),
            typeof(Image));
        iconObject.transform.SetParent(tileObject.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = Vector2.one * (emoteTileSize - 20f);
        iconRect.anchoredPosition = Vector2.zero;

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = GetEmoteSprite(entry);
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;

        ConfigureTooltip(tileObject, () => entry.Name);
        return tileObject;
    }
}
