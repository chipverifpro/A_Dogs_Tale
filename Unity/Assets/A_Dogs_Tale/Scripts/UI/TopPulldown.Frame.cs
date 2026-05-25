using UnityEngine;
using UnityEngine.UI;

public partial class TopPulldown
{
    private void BuildPulldownFrame(Transform canvasTransform)
    {
        Transform existingFrame = canvasTransform.Find("PulldownFrame");
        GameObject frameObject;
        if (existingFrame == null)
        {
            frameObject = new GameObject(
                "PulldownFrame",
                typeof(RectTransform),
                typeof(Image));
            frameObject.transform.SetParent(canvasTransform, false);
        }
        else
        {
            frameObject = existingFrame.gameObject;
        }

        frameObject.transform.SetAsFirstSibling();

        pulldownFrameRect = GetOrAddComponent<RectTransform>(frameObject);
        pulldownFrameRect.anchorMin = new Vector2(0.5f, 1f);
        pulldownFrameRect.anchorMax = new Vector2(0.5f, 1f);
        pulldownFrameRect.pivot = new Vector2(0.5f, 1f);
        pulldownFrameRect.localScale = Vector3.one * GetTopControlsFitScale();
        pulldownFrameRect.anchoredPosition = GetPulldownFrameShownPosition();
        pulldownFrameRect.sizeDelta = GetPulldownFrameSizeForCurrentButtonSize();

        pulldownFrameImage = GetOrAddComponent<Image>(frameObject);
        pulldownFrameImage.sprite = GetPulldownFrameSprite();
        pulldownFrameImage.color = Color.white;
        pulldownFrameImage.preserveAspect = false;
        pulldownFrameImage.raycastTarget = false;

        BuildPulldownRetractButton(frameObject.transform);
        BuildPulldownEndRetractButtons(frameObject.transform);
    }

    private void BuildPulldownRetractButton(Transform frameTransform)
    {
        Transform existingButton = frameTransform.Find("PulldownRetractButton");
        GameObject buttonObject;
        if (existingButton == null)
        {
            buttonObject = new GameObject(
                "PulldownRetractButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(frameTransform, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        pulldownRetractButtonRect = GetOrAddComponent<RectTransform>(buttonObject);
        pulldownRetractButtonRect.anchorMin = new Vector2(0.5f, 0f);
        pulldownRetractButtonRect.anchorMax = new Vector2(0.5f, 0f);
        pulldownRetractButtonRect.pivot = new Vector2(0.5f, 0f);
        pulldownRetractButtonRect.anchoredPosition = pulldownRetractButtonOffset;
        pulldownRetractButtonRect.sizeDelta = pulldownRetractButtonSize;

        Image buttonImage = GetOrAddComponent<Image>(buttonObject);
        buttonImage.color = new Color(1f, 1f, 1f, 0f);
        buttonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = buttonImage;
        button.onClick.RemoveListener(CollapseTopControlsToTab);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(CollapseTopControlsToTab);

        ConfigureTooltip(buttonObject, () => "Hide Controls");
    }

    private void BuildPulldownEndRetractButtons(Transform frameTransform)
    {
        pulldownLeftRetractButtonRect = BuildPulldownEndRetractButton(frameTransform, "PulldownLeftRetractButton", leftSide: true);
        pulldownRightRetractButtonRect = BuildPulldownEndRetractButton(frameTransform, "PulldownRightRetractButton", leftSide: false);
        ApplyPulldownEndRetractButtonRects();
    }

    private RectTransform BuildPulldownEndRetractButton(Transform frameTransform, string objectName, bool leftSide)
    {
        Transform existingButton = frameTransform.Find(objectName);
        GameObject buttonObject;
        if (existingButton == null)
        {
            buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(frameTransform, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        RectTransform rect = GetOrAddComponent<RectTransform>(buttonObject);
        rect.anchorMin = new Vector2(leftSide ? 0f : 1f, 0.5f);
        rect.anchorMax = new Vector2(leftSide ? 0f : 1f, 0.5f);
        rect.pivot = new Vector2(leftSide ? 0f : 1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        Image buttonImage = GetOrAddComponent<Image>(buttonObject);
        buttonImage.color = new Color(1f, 1f, 1f, 0f);
        buttonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = buttonImage;
        button.onClick.RemoveListener(CollapseTopControlsToTab);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(CollapseTopControlsToTab);

        ConfigureTooltip(buttonObject, () => "Hide Controls");
        return rect;
    }

    private void ApplyPulldownEndRetractButtonRects()
    {
        Vector2 frameSize = GetPulldownFrameSizeForCurrentButtonSize();
        Vector2 size = new Vector2(Mathf.Max(1f, pulldownEndRetractButtonWidth), frameSize.y);

        if (pulldownLeftRetractButtonRect != null)
            pulldownLeftRetractButtonRect.sizeDelta = size;

        if (pulldownRightRetractButtonRect != null)
            pulldownRightRetractButtonRect.sizeDelta = size;
    }

    private void BuildPulldownTab(Transform canvasTransform)
    {
        Transform existingTab = canvasTransform.Find("PulldownTab");
        GameObject tabObject;
        if (existingTab == null)
        {
            tabObject = new GameObject(
                "PulldownTab",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            tabObject.transform.SetParent(canvasTransform, false);
        }
        else
        {
            tabObject = existingTab.gameObject;
        }

        tabObject.transform.SetAsLastSibling();

        pulldownTabRect = GetOrAddComponent<RectTransform>(tabObject);
        pulldownTabRect.anchorMin = new Vector2(1f, 1f);
        pulldownTabRect.anchorMax = new Vector2(1f, 1f);
        pulldownTabRect.pivot = new Vector2(1f, 1f);
        pulldownTabRect.localScale = Vector3.one * GetTopControlsFitScale();
        pulldownTabRect.anchoredPosition = GetPulldownTabPosition();
        pulldownTabRect.sizeDelta = pulldownTabSize;

        pulldownTabImage = GetOrAddComponent<Image>(tabObject);
        pulldownTabImage.sprite = GetPulldownTabSprite();
        pulldownTabImage.color = Color.white;
        pulldownTabImage.preserveAspect = true;
        pulldownTabImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(tabObject);
        button.targetGraphic = pulldownTabImage;
        button.onClick.RemoveListener(ExpandTopControlsFromTab);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(ExpandTopControlsFromTab);

        ConfigureTooltip(tabObject, () => "Show Controls");
    }
}
