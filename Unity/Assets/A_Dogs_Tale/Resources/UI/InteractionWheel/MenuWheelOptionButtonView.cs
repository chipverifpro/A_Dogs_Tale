#nullable enable
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DogGame.UI.InteractionWheel
{
    [RequireComponent(typeof(Button))]
    public sealed class MenuWheelOptionButtonView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [Header("References")]
        [SerializeField] private Image backgroundImage = null!;
        [SerializeField] private TMP_Text labelText = null!;
        [SerializeField] private Image? iconImage;

        [Header("Styling")]
        [SerializeField] private Color enabledBackground = Color.white;
        [SerializeField] private Color disabledBackground = new Color(0.75f, 0.75f, 0.75f, 1f);
        [SerializeField] private Color enabledText = Color.black;
        [SerializeField] private Color disabledText = new Color(0.25f, 0.25f, 0.25f, 1f);

        [Tooltip("Highlight is a subtle scale bump (keeps white background requirement).")]
        [SerializeField] private float highlightedScale = 1.07f;

        private Button button = null!;
        private RectTransform rectTransform = null!;
        private HorizontalLayoutGroup? horizontalLayoutGroup;
        private ContentSizeFitter? contentSizeFitter;
        private LayoutElement? rootLayoutElement;
        private LayoutElement? labelLayoutElement;
        public WheelOption? BoundOption { get; private set; }

        // Controller hooks
        public Action<bool, WheelOption, RectTransform>? onHoverChanged;
        public Action? onClicked;

        private Vector3 baseScale;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void Bind(WheelOption option)
        {
            EnsureInitialized();

            BoundOption = option;

            labelText.text = option.label ?? "";
            bool enabled = option.isEnabled;
            button.interactable = enabled;

            backgroundImage.color = enabled ? enabledBackground : disabledBackground;
            labelText.color = enabled ? enabledText : disabledText;

            if (iconImage != null)
            {
                if (option.icon != null)
                {
                    iconImage.enabled = true;
                    iconImage.sprite = option.icon;
                }
                else
                {
                    iconImage.enabled = false;
                }
            }

            SetHighlighted(false);
        }

        public RectTransform RectTransform => rectTransform;

        public void ApplyManualLayout(Vector2 size, Vector4 labelInsets)
        {
            EnsureInitialized();
            DisableAutomaticLayout();

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;

            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = new Vector2(labelInsets.x, labelInsets.y);
            labelRect.offsetMax = new Vector2(-labelInsets.z, -labelInsets.w);
        }

        public void SetHighlighted(bool highlighted)
        {
            EnsureInitialized();
            transform.localScale = highlighted ? (baseScale * highlightedScale) : baseScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (BoundOption == null) return;
            onHoverChanged?.Invoke(true, BoundOption, (RectTransform)transform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (BoundOption == null) return;
            onHoverChanged?.Invoke(false, BoundOption, (RectTransform)transform);
        }

        private void EnsureInitialized()
        {
            if (rectTransform == null)
                rectTransform = (RectTransform)transform;

            if (button == null)
            {
                button = GetComponent<Button>();
                button.onClick.AddListener(HandleClicked);
            }

            if (horizontalLayoutGroup == null)
                horizontalLayoutGroup = GetComponent<HorizontalLayoutGroup>();

            if (contentSizeFitter == null)
                contentSizeFitter = GetComponent<ContentSizeFitter>();

            if (rootLayoutElement == null)
                rootLayoutElement = GetComponent<LayoutElement>();

            if (labelLayoutElement == null && labelText != null)
                labelLayoutElement = labelText.GetComponent<LayoutElement>();

            if (baseScale == default)
                baseScale = transform.localScale;

            DisableAutomaticLayout();
        }

        private void HandleClicked()
        {
            onClicked?.Invoke();
        }

        private void DisableAutomaticLayout()
        {
            if (horizontalLayoutGroup != null)
                horizontalLayoutGroup.enabled = false;

            if (contentSizeFitter != null)
                contentSizeFitter.enabled = false;

            if (rootLayoutElement != null)
                rootLayoutElement.ignoreLayout = true;

            if (labelLayoutElement != null)
                labelLayoutElement.ignoreLayout = true;
        }
    }
}
