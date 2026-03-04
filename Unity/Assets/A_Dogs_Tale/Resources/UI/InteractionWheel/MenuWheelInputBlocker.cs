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
        [Header("Target Acquisition")]
        [SerializeField] private bool allowAcquireDuringHold = true;
        [SerializeField] private bool useAimAssist = true;
        [SerializeField] private float aimAssistRadiusPixels = 22f;
        [SerializeField] private int aimAssistSamples = 8;

        // When a target is acquired, keep it "sticky" (recommended) so it doesn't flicker to nearby objects.
        [SerializeField] private bool lockTargetOnceAcquired = true;

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