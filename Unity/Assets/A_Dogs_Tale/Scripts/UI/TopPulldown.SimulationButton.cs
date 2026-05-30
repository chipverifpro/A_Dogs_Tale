using DogGame;
using UnityEngine;
using UnityEngine.UI;

public partial class TopPulldown
{
    private void BuildSimulationButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "SimulationPauseButton");
        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "SimulationPauseButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        simulationButtonRect = buttonObject.GetComponent<RectTransform>();
        if (createdButton)
        {
            simulationButtonRect.anchorMin = new Vector2(1f, 1f);
            simulationButtonRect.anchorMax = new Vector2(1f, 1f);
            simulationButtonRect.pivot = new Vector2(1f, 1f);
            simulationButtonRect.anchoredPosition = new Vector2(
                -(topControlButtonMargin + ((topControlButtonSize + modeButtonSpacing) * 3f)),
                -topControlButtonMargin);
            simulationButtonRect.sizeDelta = new Vector2(topControlButtonSize, topControlButtonSize);
        }
        ConfigureTopControlRect(simulationButtonRect, 3);

        simulationButtonImage = GetOrAddComponent<Image>(buttonObject);
        simulationButtonImage.color = topControlButtonColor;
        simulationButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = simulationButtonImage;
        button.onClick.RemoveListener(ToggleSimulationPause);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(ToggleSimulationPause);

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

        simulationIconImage = GetOrAddComponent<Image>(iconObject);
        simulationIconImage.preserveAspect = true;
        simulationIconImage.color = Color.white;
        RefreshSimulationButtonState(force: true);

        ConfigureTooltip(buttonObject, GetSimulationButtonTooltipText);
    }

    private void ToggleSimulationPause()
    {
        GamePause.Toggle();
        RefreshSimulationButtonState(force: true);
    }

    private void RefreshSimulationButtonState(bool force = false)
    {
        if (simulationIconImage == null || simulationButtonImage == null)
            return;

        bool isPaused = GamePause.IsPaused;
        if (!force && displayedPausedState.HasValue && displayedPausedState.Value == isPaused)
            return;

        displayedPausedState = isPaused;
        simulationIconImage.sprite = GetSimulationControlSprite(isPaused);
        simulationButtonImage.color = isPaused
            ? dropdownSelectedColor
            : topControlButtonColor;

        RefreshActiveTooltipText();
    }

    private Sprite GetSimulationControlSprite(bool isPaused)
    {
        int desiredIndex = isPaused ? 0 : 1;
        return SpriteServer.SpriteLookup(isPaused ? "Play" : "Pause")
            ?? SpriteServer.SpriteSheetLookup(playPauseSpriteResourcePath, desiredIndex)
            ?? SpriteServer.SpriteSheetLookup(playPauseSpriteResourcePath, 1)
            ?? SpriteServer.SpriteSheetLookup(playPauseSpriteResourcePath, 0);
    }
}
