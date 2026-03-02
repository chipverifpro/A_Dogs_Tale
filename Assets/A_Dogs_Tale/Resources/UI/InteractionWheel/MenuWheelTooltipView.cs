#nullable enable
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DogGame.UI.InteractionWheel
{
    public sealed class MenuWheelTooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform root = null!;
        [SerializeField] private TMP_Text textField = null!;
        [SerializeField] private LayoutElement? layoutElement;
        [SerializeField] private float maxWidthPixels = 420f;
        [SerializeField] private Vector2 screenPadding = new Vector2(18f, 18f);

        private void Awake()
        {
            HideTooltip();
        }

        public void ShowTooltip(string text, RectTransform sourceRect)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                HideTooltip();
                return;
            }

            root.gameObject.SetActive(true);
            textField.text = text;

            if (layoutElement != null)
                layoutElement.preferredWidth = maxWidthPixels;

            // Position: slightly above the hovered/highlighted button.
            Vector3[] corners = new Vector3[4];
            sourceRect.GetWorldCorners(corners);

            // Choose top center of button
            Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;
            Vector3 tooltipWorldPos = topCenter + new Vector3(0f, 18f, 0f);

            root.position = tooltipWorldPos;

            ClampToScreen();
        }

        public void HideTooltip()
        {
            root.gameObject.SetActive(false);
        }

        private void ClampToScreen()
        {
            // Keep tooltip inside screen bounds (simple clamp).
            Vector3[] corners = new Vector3[4];
            root.GetWorldCorners(corners);

            float minX = corners[0].x;
            float maxX = corners[2].x;
            float minY = corners[0].y;
            float maxY = corners[2].y;

            Vector3 pos = root.position;

            if (minX < screenPadding.x) pos.x += (screenPadding.x - minX);
            if (maxX > Screen.width - screenPadding.x) pos.x -= (maxX - (Screen.width - screenPadding.x));
            if (minY < screenPadding.y) pos.y += (screenPadding.y - minY);
            if (maxY > Screen.height - screenPadding.y) pos.y -= (maxY - (Screen.height - screenPadding.y));

            root.position = pos;
        }
    }
}