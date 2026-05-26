using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class TopPulldown
{
    private static readonly Vector2 FrameCloseButtonAnchoredPosition = new(-64f, -64f);
    private static readonly Vector2 FrameCloseButtonSize = new(96f, 96f);

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
            return component;

        return target.AddComponent<T>();
    }

    private RectTransform EnsureInvisibleFrameCloseButton(Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        return EnsureInvisibleFrameCloseButton(parent, onClick, FrameCloseButtonAnchoredPosition, FrameCloseButtonSize);
    }

    private RectTransform EnsureInvisibleFrameCloseButton(
        Transform parent,
        UnityEngine.Events.UnityAction onClick,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        if (parent == null || onClick == null)
            return null;

        Transform existingButton = parent.Find("FrameCloseButton");
        GameObject buttonObject;
        if (existingButton == null)
        {
            buttonObject = new GameObject(
                "FrameCloseButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        RectTransform rect = GetOrAddComponent<RectTransform>(buttonObject);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = GetOrAddComponent<Image>(buttonObject);
        image.color = Color.clear;
        image.raycastTarget = true;

        LayoutElement layout = GetOrAddComponent<LayoutElement>(buttonObject);
        layout.ignoreLayout = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = image;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(onClick);

        ConfigureTooltip(buttonObject, () => "Close");
        buttonObject.transform.SetAsLastSibling();
        return rect;
    }

    private Scrollbar CreateScrollbar(Transform parent)
    {
        GameObject scrollbarObject = new GameObject(
            "Scrollbar",
            typeof(RectTransform),
            typeof(Image),
            typeof(Scrollbar));
        scrollbarObject.transform.SetParent(parent, false);

        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 1f);
        scrollbarRect.offsetMin = new Vector2(-12f, 2f);
        scrollbarRect.offsetMax = Vector2.zero;
        scrollbarRect.sizeDelta = new Vector2(12f, 0f);

        Image trackImage = scrollbarObject.GetComponent<Image>();
        trackImage.color = new Color(0.4f, 0.34f, 0.24f, 0.25f);

        GameObject slidingAreaObject = new GameObject("Sliding Area", typeof(RectTransform));
        slidingAreaObject.transform.SetParent(scrollbarObject.transform, false);
        RectTransform slidingAreaRect = slidingAreaObject.GetComponent<RectTransform>();
        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = new Vector2(1f, 2f);
        slidingAreaRect.offsetMax = new Vector2(-1f, -2f);

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(slidingAreaObject.transform, false);

        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 1f);
        handleRect.anchorMax = new Vector2(1f, 1f);
        handleRect.pivot = new Vector2(0.5f, 1f);
        handleRect.sizeDelta = new Vector2(0f, 48f);

        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = new Color(0.63f, 0.52f, 0.31f, 0.85f);

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scrollbar.size = 1f;
        scrollbar.value = 1f;

        return scrollbar;
    }

    private GameObject CreateTMPLabel(
        Transform parent,
        string name,
        string text,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = dropdownTextColor;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        return textObject;
    }
}
