using UnityEngine;
using UnityEngine.UI;

public partial class TopPulldown
{
    private void BuildEmoteButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "EmoteButton");
        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "EmoteButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        emoteButtonRect = buttonObject.GetComponent<RectTransform>();
        if (createdButton)
        {
            emoteButtonRect.anchorMin = new Vector2(1f, 1f);
            emoteButtonRect.anchorMax = new Vector2(1f, 1f);
            emoteButtonRect.pivot = new Vector2(1f, 1f);
            emoteButtonRect.anchoredPosition = new Vector2(
                -(topControlButtonMargin + ((topControlButtonSize + modeButtonSpacing) * 4f)),
                -topControlButtonMargin);
            emoteButtonRect.sizeDelta = new Vector2(topControlButtonSize, topControlButtonSize);
        }
        ConfigureTopControlRect(emoteButtonRect, 4);

        emoteButtonImage = GetOrAddComponent<Image>(buttonObject);
        emoteButtonImage.color = topControlButtonColor;
        emoteButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = emoteButtonImage;
        button.onClick.RemoveListener(ToggleEmoteDropdown);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(ToggleEmoteDropdown);

        Transform existingIcon = buttonObject.transform.Find("Icon");
        GameObject iconObject;
        bool createdIcon = existingIcon == null;
        if (createdIcon)
        {
            iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
        }
        else
        {
            iconObject = existingIcon.gameObject;
        }

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        if (createdIcon)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = Vector2.one * (topControlButtonSize * 0.72f);
            iconRect.anchoredPosition = Vector2.zero;
        }
        ConfigureTopControlIconRect(iconRect, 0.72f);

        emoteIconImage = GetOrAddComponent<Image>(iconObject);
        emoteIconImage.preserveAspect = true;
        emoteIconImage.color = Color.white;
        RefreshEmoteButtonState(force: true);

        ConfigureTooltip(buttonObject, GetEmoteButtonTooltipText);
    }

    private void HandleEmoteSelected(DogEmojiEntry entry)
    {
        SetSelectedEmote(entry);
        RefreshEmoteButtonState(force: true);
        BottomBanner.LogEmote(GetCurrentControlledWorldObject(), entry.EntryId);
        CloseEmoteDropdown();
    }

    private void RefreshEmoteButtonState(bool force = false)
    {
        if (emoteIconImage == null || emoteButtonImage == null)
            return;

        EnsureDefaultEmoteSelection();

        Sprite selectedSprite = selectedEmoteEntry.HasValue
            ? GetEmoteSprite(selectedEmoteEntry.Value)
            : null;

        if (!force && emoteIconImage.sprite == selectedSprite)
            return;

        emoteIconImage.sprite = selectedSprite;
        emoteButtonImage.color = selectedEmoteEntry.HasValue
            ? dropdownSelectedColor
            : topControlButtonColor;

        RefreshActiveTooltipText();
    }

    private void EnsureDefaultEmoteSelection()
    {
        if (selectedEmoteEntry.HasValue)
            return;

        for (int i = 0; i < DogEmojiCatalog.Entries.Length; i++)
        {
            DogEmojiEntry entry = DogEmojiCatalog.Entries[i];
            if (entry.Name == "Happy" && GetEmoteSprite(entry) != null)
            {
                SetSelectedEmote(entry);
                return;
            }
        }

        for (int i = 0; i < DogEmojiCatalog.Entries.Length; i++)
        {
            DogEmojiEntry entry = DogEmojiCatalog.Entries[i];
            if (GetEmoteSprite(entry) != null)
            {
                SetSelectedEmote(entry);
                return;
            }
        }
    }

    private void SetSelectedEmote(DogEmojiEntry entry)
    {
        if (GetEmoteSprite(entry) == null)
            return;

        selectedEmoteEntry = entry;
    }

    private string GetSelectedEmoteId()
    {
        return selectedEmoteEntry.HasValue ? selectedEmoteEntry.Value.EntryId : string.Empty;
    }

    private Sprite GetEmoteSprite(DogEmojiEntry entry)
    {
        return SpriteServer.SpriteSheetLookup($"DogEmojiSheet{entry.SheetId}", entry.SpriteIndex);
    }

    private string GetEmoteButtonTooltipText()
    {
        return selectedEmoteEntry.HasValue
            ? $"Emote: {selectedEmoteEntry.Value.Name}"
            : "Emote Catalog";
    }
}
