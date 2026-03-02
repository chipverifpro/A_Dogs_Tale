#nullable enable
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DogGame.UI.InteractionWheel
{
    /// <summary>
    /// Fullscreen transparent raycast target behind the wheel.
    /// Any press on it counts as "pressed outside" and should close the wheel.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class MenuWheelInputBlocker : MonoBehaviour, IPointerDownHandler
    {
        public Action? onPressedOutside;

        private void Reset()
        {
            // Ensure the image blocks raycasts.
            var img = GetComponent<Image>();
            img.raycastTarget = true;

            // Transparent
            img.color = new Color(0f, 0f, 0f, 0f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            onPressedOutside?.Invoke();
        }
    }
}