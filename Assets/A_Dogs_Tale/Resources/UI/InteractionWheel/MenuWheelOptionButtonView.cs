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
        public WheelOption? BoundOption { get; private set; }

        // Controller hooks
        public Action<bool, WheelOption, RectTransform>? onHoverChanged;
        public Action? onClicked;

        private Vector3 baseScale;

        private void Awake()
        {
            button = GetComponent<Button>();
            baseScale = transform.localScale;

            button.onClick.AddListener(() =>
            {
                onClicked?.Invoke();
            });
        }

        public void Bind(WheelOption option)
        {
            BoundOption = option;

            labelText.text = option.label ?? "";
            Debug.Log($"Bind Button: {labelText.text}");
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

        public void SetHighlighted(bool highlighted)
        {
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
    }
}