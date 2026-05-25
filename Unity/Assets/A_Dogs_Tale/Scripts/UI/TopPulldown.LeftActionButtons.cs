using DogGame.Modules;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public partial class TopPulldown
{
    private void BuildLeftActionButtons(Transform parent, Transform searchRoot)
    {
        homeButtonRect = BuildLeftActionButton(
            parent,
            searchRoot,
            "HomeButton",
            spriteIndex: 0,
            slotFromRight: HomeButtonTopSlotFromRight,
            HandleHomeButtonPressed,
            "Home",
            out homeButtonImage);

        cameraModeButtonRect = BuildLeftActionButton(
            parent,
            searchRoot,
            "CameraModeButton",
            spriteIndex: 2,
            slotFromRight: CameraModeButtonTopSlotFromRight,
            HandleCameraModeButtonPressed,
            "Camera Mode",
            out cameraModeButtonImage);

        questButtonRect = BuildLeftActionButton(
            parent,
            searchRoot,
            "QuestButton",
            spriteIndex: 1,
            slotFromRight: QuestButtonTopSlotFromRight,
            HandleQuestButtonPressed,
            "Quests",
            out questButtonImage);
    }

    private RectTransform BuildLeftActionButton(
        Transform parent,
        Transform searchRoot,
        string buttonName,
        int spriteIndex,
        int slotFromRight,
        UnityAction clickHandler,
        string tooltipText,
        out Image buttonImage)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, buttonName);
        GameObject buttonObject;
        if (existingButton == null)
        {
            buttonObject = new GameObject(
                buttonName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        RectTransform buttonRect = GetOrAddComponent<RectTransform>(buttonObject);
        ConfigureTopControlRect(buttonRect, slotFromRight);

        buttonImage = GetOrAddComponent<Image>(buttonObject);
        buttonImage.sprite = GetAndroidButtonSprite(spriteIndex);
        buttonImage.preserveAspect = true;
        buttonImage.color = Color.white;
        buttonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = buttonImage;
        button.onClick.RemoveListener(clickHandler);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(clickHandler);

        ConfigureTooltip(buttonObject, () => tooltipText);

        return buttonRect;
    }

    private void HandleHomeButtonPressed()
    {
        CloseTopActionPanels();

        SceneFader sceneFader = EnsureDir() && dir.sceneFader != null
            ? dir.sceneFader
            : FindFirstObjectByType<SceneFader>();

        if (sceneFader == null)
        {
            Debug.LogWarning("TopPulldown: title menu fader is not available for Home button.", this);
            BottomBanner.Show("Home is not ready yet.");
            return;
        }

        sceneFader.ReturnToTitleMenu();
    }

    private void HandleCameraModeButtonPressed()
    {
        CloseTopActionPanels();

        CameraModeSwitcher cameraModeSwitcher = EnsureDir() && dir.cameraModeSwitcher != null
            ? dir.cameraModeSwitcher
            : FindFirstObjectByType<CameraModeSwitcher>();

        if (cameraModeSwitcher == null)
        {
            Debug.LogWarning("TopPulldown: camera mode switcher is not available.", this);
            BottomBanner.Show("Camera mode is not ready yet.");
            return;
        }

        cameraModeSwitcher.SelectNextView();
    }

    private void HandleQuestButtonPressed()
    {
        CloseTopActionPanels();

        QuestJournalUI questJournal = FindFirstObjectByType<QuestJournalUI>();
        if (questJournal == null)
        {
            _ = QuestManager.Instance;
            GameObject journalObject = new("QuestJournalUI");
            questJournal = journalObject.AddComponent<QuestJournalUI>();
        }

        questJournal.Toggle();
    }

    private void CloseTopActionPanels()
    {
        CloseDropdown();
        CloseModePanel();
        CloseSpeedPanel();
        CloseEmoteDropdown();
        HideTooltip();
    }

    private Sprite GetAndroidButtonSprite(int index)
    {
        return SpriteServer.SpriteSheetLookup(androidButtonSpriteResourcePath, index);
    }
}
