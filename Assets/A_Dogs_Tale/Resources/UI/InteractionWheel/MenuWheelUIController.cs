#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DogGame.UI.InteractionWheel
{
    public sealed class MenuWheelUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform wheelRoot = null!;
        [SerializeField] private RectTransform optionsContainer = null!;
        [SerializeField] private MenuWheelOptionButtonView optionButtonPrefab = null!;
        [SerializeField] private MenuWheelOptionButtonView cancelButton = null!;
        [SerializeField] private MenuWheelTooltipView tooltipView = null!;
        [SerializeField] private MenuWheelInputBlocker inputBlocker = null!;
        [SerializeField] private Canvas rootCanvas = null!;
        private RectTransform canvasRect = null!;
        [Header("Layout")]
        [Tooltip("Wheel radius as fraction of min(screenWidth, screenHeight).")]
        [Range(0.10f, 0.60f)]
        [SerializeField] private float wheelRadiusFactor = 0.38f;

        [Tooltip("Deadzone radius as fraction of min(screenWidth, screenHeight). Inside = no option highlighted.")]
        [Range(0.02f, 0.25f)]
        [SerializeField] private float deadzoneFactor = 0.10f;

        [Tooltip("Angle offset in degrees. 90 means first option starts at top.")]
        [SerializeField] private float startAngleDegrees = 90f;

        [Header("Behavior")]
        [Tooltip("Default timescale while menu is open. 0 = pause, 1 = normal, 0.25 = slow.")]
        [SerializeField] private float openTimeScale = 0f;

        [Tooltip("If true, releasing with no highlighted option triggers Cancel.")]
        [SerializeField] private bool releaseWithNoSelectionCancels = true;

        [Tooltip("If true, right-click closes the wheel.")]
        [SerializeField] private bool rightClickCloses = true;

        // Runtime state
        private bool isOpen;
        private float previousTimeScale;
        private WheelMenuModel? currentMenu;
        private int currentPageIndex;

        private readonly List<MenuWheelOptionButtonView> spawnedButtons = new();
        private int highlightedIndex = -1;

        // Needed so the blocker can close us without direct scene references.
        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (rootCanvas == null)
                rootCanvas = GetComponentInParent<Canvas>();

            canvasRect = rootCanvas.GetComponent<RectTransform>();

            SetVisible(true);   // DEBUG: should be false

            if (inputBlocker != null)
                inputBlocker.onPressedOutside = HandlePressedOutside;
        }

        private void OnDestroy()
        {
            // Restore timescale if the object is destroyed while open.
            if (isOpen)
                RestoreTimeScale();
        }

        /// <summary>
        /// Open wheel UI with the already-resolved menu model (Step 2 output).
        /// Starts at page 0.
        /// </summary>
        public void OpenMenuWheel(WheelMenuModel menuModel, float? overrideTimeScale = null)
        {
            if (menuModel == null) throw new ArgumentNullException(nameof(menuModel));

            gameObject.SetActive(true); // <- re-enable before building

            currentMenu = menuModel;
            currentPageIndex = 0;

            ApplyTimeScale(overrideTimeScale ?? openTimeScale);

            SetVisible(true);
            isOpen = true;

            // Cancel button is always present and acts as a close.
            ConfigureCancelButton();

            BuildPage(pageIndex: 0);

            // Start with nothing highlighted until pointer moves out of deadzone.
            SetHighlightedIndex(-1);
        }

        public void CloseMenuWheel()
        {
            if (!isOpen)
                return;

            ClearSpawnedButtons();
            tooltipView.HideTooltip();

            SetVisible(false);
            isOpen = false;

            currentMenu = null;
            currentPageIndex = 0;

            RestoreTimeScale();

            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!isOpen || currentMenu == null)
                return;

            // Close hotkeys (Input System)
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseMenuWheel();
                return;
            }

            var mouse = Mouse.current;
            if (rightClickCloses && mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                CloseMenuWheel();
                return;
            }

            // Update highlight from pointer position (mouse or touch)
            Vector2 pointerScreenPos;
            bool pointerIsDown;
            bool pointerReleasedThisFrame;

            if (!TryGetPointer(out pointerScreenPos, out pointerIsDown, out pointerReleasedThisFrame))
            {
                SetHighlightedIndex(-1);
                tooltipView.HideTooltip();
                return;
            }

            UpdateHighlight(pointerScreenPos);

            // Only select on an actual release *this frame*
            if (pointerReleasedThisFrame)
            {
                HandlePointerReleased();
            }
        }

        private void HandlePointerReleased()
        {
            // Choose selection
            if (highlightedIndex < 0)
            {
                if (releaseWithNoSelectionCancels)
                    CloseMenuWheel();
                else
                    CloseMenuWheel();

                return;
            }

            MenuWheelOptionButtonView selectedButton = spawnedButtons[highlightedIndex];
            WheelOption selectedOption = selectedButton.BoundOption!;

            if (!selectedOption.isEnabled)
            {
                // Disabled: just close or keep open? You asked for hints explaining disabled.
                // I'd keep open so the user can choose something else.
                // But release is the "confirm" action, so we should NOT close on disabled selection.
                return;
            }

            // Navigation options are handled by UI
            if (selectedOption.id == WheelMenuResolver.MoreOptionId)
            {
                SwitchToPage(1);
                return;
            }

            if (selectedOption.id == WheelMenuResolver.BackOptionId)
            {
                SwitchToPage(0);
                return;
            }

            // Normal option: execute callback if present.
            WheelContext context = currentMenu!.context;
            try
            {
                selectedOption.callback?.Invoke(context);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MenuWheelUI] Exception while executing option '{selectedOption.id}': {exception}");
            }

            CloseMenuWheel();
        }

        private void SwitchToPage(int pageIndex)
        {
            if (currentMenu == null)
                return;

            if (pageIndex < 0 || pageIndex >= currentMenu.pages.Count)
                return;

            currentPageIndex = pageIndex;
            BuildPage(pageIndex);
            SetHighlightedIndex(-1);
        }

        private void BuildPage(int pageIndex)
        {
            if (currentMenu == null) return;

            ClearSpawnedButtons();

            List<WheelOption> pageOptions = currentMenu.pages[pageIndex];

            // Instantiate
            for (int optionIndex = 0; optionIndex < pageOptions.Count; optionIndex++)
            {
                WheelOption option = pageOptions[optionIndex];

                MenuWheelOptionButtonView buttonView =
                    Instantiate(optionButtonPrefab, optionsContainer);

                buttonView.Bind(option);
                buttonView.onHoverChanged = HandleButtonHoverChanged;

                spawnedButtons.Add(buttonView);
            }

            // Let TMP + ContentSizeFitter compute final sizes before we place in a circle.
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                var rt = spawnedButtons[i].GetComponent<RectTransform>();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
            Canvas.ForceUpdateCanvases();

            LayoutButtonsInCircle(spawnedButtons, wheelRadiusFactor, startAngleDegrees);
        }

        private void LayoutButtonsInCircle(List<MenuWheelOptionButtonView> buttons, float radiusFactor, float startAngleDeg)
        {
            if (canvasRect == null) return;

            // Canvas units (after CanvasScaler) — stable.
            float canvasMin = Mathf.Min(canvasRect.rect.width, canvasRect.rect.height);
            float radius = canvasMin * radiusFactor;

            int count = buttons.Count;
            if (count == 0) return;

            float angleStep = 360f / count;

            for (int i = 0; i < count; i++)
            {
                float angleDeg = startAngleDeg - (angleStep * i);
                float angleRad = angleDeg * Mathf.Deg2Rad;

                float x = Mathf.Cos(angleRad) * radius;
                float y = Mathf.Sin(angleRad) * radius;

                RectTransform rt = buttons[i].GetComponent<RectTransform>();

                // Because OptionsContainer is anchored/pivoted at center,
                // anchoredPosition is in its local canvas units.
                rt.anchoredPosition = new Vector2(x, y);
            }
        }
        private void ConfigureCancelButton()
        {
            // Cancel button is special: always enabled, always visible, closes on click.
            cancelButton.Bind(new WheelOption
            {
                id = "cancel",
                label = "Cancel",
                hint = "Close the menu",
                disabledHint = "",
                isVisible = true,
                isEnabled = true,
                sortPriority = int.MinValue,
                callback = null
            });

            cancelButton.onClicked = () => CloseMenuWheel();
            cancelButton.onHoverChanged = (isHovering, option, rectTransform) =>
            {
                if (!isHovering)
                {
                    tooltipView.HideTooltip();
                    return;
                }

                tooltipView.ShowTooltip(option.hint, rectTransform);
            };
        }

        private void ClearSpawnedButtons()
        {
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                if (spawnedButtons[i] != null)
                    Destroy(spawnedButtons[i].gameObject);
            }
            spawnedButtons.Clear();
        }

        private void UpdateHighlight(Vector2 pointerScreenPos)
        {
            if (canvasRect == null) return;

            Camera? uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

            // Convert screen pointer to local coordinates in the OptionsContainer space (center is 0,0).
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    optionsContainer, pointerScreenPos, uiCamera, out Vector2 localPos))
            {
                SetHighlightedIndex(-1);
                tooltipView.HideTooltip();
                return;
            }

            float canvasMin = Mathf.Min(canvasRect.rect.width, canvasRect.rect.height);
            float deadzone = canvasMin * deadzoneFactor;

            if (localPos.magnitude < deadzone)
            {
                SetHighlightedIndex(-1);
                tooltipView.HideTooltip();
                return;
            }

            int bestIndex = FindClosestButtonByAngle(localPos);
            SetHighlightedIndex(bestIndex);

            if (bestIndex >= 0)
            {
                var button = spawnedButtons[bestIndex];
                var option = button.BoundOption!;
                string hintText = option.isEnabled ? option.hint : option.disabledHint;
                tooltipView.ShowTooltip(hintText, button.GetComponent<RectTransform>());
            }
        }

        private int FindClosestButtonByAngle(Vector2 localDirectionFromCenter)
        {
            if (spawnedButtons.Count == 0)
                return -1;

            // Determine pointer angle in degrees
            float pointerAngleDeg = Mathf.Atan2(localDirectionFromCenter.y, localDirectionFromCenter.x) * Mathf.Rad2Deg;

            // Buttons are placed at known angles based on their anchored positions.
            int bestIndex = -1;
            float bestDelta = float.MaxValue;

            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                RectTransform buttonTransform = spawnedButtons[i].GetComponent<RectTransform>();
                Vector2 buttonPos = buttonTransform.anchoredPosition;

                float buttonAngleDeg = Mathf.Atan2(buttonPos.y, buttonPos.x) * Mathf.Rad2Deg;
                float angleDelta = Mathf.Abs(Mathf.DeltaAngle(pointerAngleDeg, buttonAngleDeg));

                if (angleDelta < bestDelta)
                {
                    bestDelta = angleDelta;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void SetHighlightedIndex(int newIndex)
        {
            if (highlightedIndex == newIndex)
                return;

            // Clear old
            if (highlightedIndex >= 0 && highlightedIndex < spawnedButtons.Count)
                spawnedButtons[highlightedIndex].SetHighlighted(false);

            highlightedIndex = newIndex;

            // Set new
            if (highlightedIndex >= 0 && highlightedIndex < spawnedButtons.Count)
                spawnedButtons[highlightedIndex].SetHighlighted(true);
        }

        private void HandleButtonHoverChanged(bool isHovering, WheelOption option, RectTransform sourceRect)
        {
            // Mouse hover support (drag highlight also shows tooltip; both can coexist)
            if (!isOpen)
                return;

            if (!isHovering)
            {
                tooltipView.HideTooltip();
                return;
            }

            string text = option.isEnabled ? option.hint : option.disabledHint;
            tooltipView.ShowTooltip(text, sourceRect);
        }

        private void HandlePressedOutside()
        {
            // Click/tap outside wheel -> close
            CloseMenuWheel();
        }

        private void ApplyTimeScale(float newTimeScale)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = newTimeScale;
        }

        private void RestoreTimeScale()
        {
            Time.timeScale = previousTimeScale;
        }

        private void SetVisible(bool visible)
        {
            if (wheelRoot != null) wheelRoot.gameObject.SetActive(visible);
            if (inputBlocker != null) inputBlocker.gameObject.SetActive(visible);
        }

        private static bool TryGetPointer(out Vector2 pointerScreenPos, out bool pointerIsDown, out bool pointerReleasedThisFrame)
        {
            // Touch priority
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.touches.Count > 0)
            {
                // Pick the first active touch
                foreach (var touchControl in touchscreen.touches)
                {
                    if (touchControl == null) continue;

                    var press = touchControl.press;
                    if (press == null) continue;

                    // Consider it active if currently pressed OR just released this frame
                    bool isPressed = press.isPressed;
                    bool releasedThisFrame = press.wasReleasedThisFrame;

                    if (!isPressed && !releasedThisFrame)
                        continue;

                    pointerScreenPos = touchControl.position.ReadValue();
                    pointerIsDown = isPressed;
                    pointerReleasedThisFrame = releasedThisFrame;
                    return true;
                }
            }

            // Mouse
            var mouse = Mouse.current;
            if (mouse != null)
            {
                pointerScreenPos = mouse.position.ReadValue();
                pointerIsDown = mouse.leftButton.isPressed;
                pointerReleasedThisFrame = mouse.leftButton.wasReleasedThisFrame;
                return true;
            }

            // No pointer device
            pointerScreenPos = default;
            pointerIsDown = false;
            pointerReleasedThisFrame = false;
            return false;
        }
    }
}