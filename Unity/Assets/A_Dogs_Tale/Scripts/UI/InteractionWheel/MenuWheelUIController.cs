#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DogGame.UI.InteractionWheel
{
    public sealed class MenuWheelUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform wheelRoot = null!;
        [SerializeField] private RectTransform optionsContainer = null!;
        [SerializeField] private MenuWheelOptionButtonView optionButtonPrefab = null!;
        [SerializeField] private MenuWheelOptionButtonView cancelButton = null!;
        [SerializeField] private MenuWheelInputBlocker inputBlocker = null!;
        [SerializeField] private Canvas rootCanvas = null!;
        [SerializeField] private Camera? worldCamera;
        [SerializeField] private MenuWheelCenterPreviewView? centerPreviewView;

        [Header("Layout")]
        [SerializeField] private MenuWheelLayoutSettings layoutSettings = new();

        [Header("Behavior")]
        [Tooltip("Default timescale while menu is open. 0 = pause, 1 = normal, 0.25 = slow.")]
        [SerializeField] private float openTimeScale = 0f;

        [Tooltip("If true, releasing with no highlighted option triggers Cancel.")]
        [SerializeField] private bool releaseWithNoSelectionCancels = true;

        [Tooltip("If true, right-click closes the wheel.")]
        [SerializeField] private bool rightClickCloses = true;

        private RectTransform canvasRect = null!;
        private CanvasScaler? canvasScaler;

        private bool isOpen;
        private float previousTimeScale;
        private WheelMenuModel? currentMenu;
        private int currentPageIndex;

        private readonly List<MenuWheelOptionButtonView> spawnedButtons = new();
        private int highlightedIndex = -1;
        private MenuWheelResolvedLayout currentLayout;
        private bool hasCurrentLayout;
        private bool ignoreOpeningPointerUntilRelease;

        public bool IsOpen => isOpen;

        [Header("Color Ring Transforms")]
        [SerializeField] private RectTransform colorRingOuter = null!;
        [SerializeField] private RectTransform colorRingInner = null!;
        [Range(0.50f, 2.00f)]
        [SerializeField] private float colorRingRadiusScale = 1.15f;

        public int GetPrimaryOptionCapacity(int fallbackMaxPrimaryOptions)
        {
            return layoutSettings != null
                ? layoutSettings.GetPrimaryOptionCapacity(fallbackMaxPrimaryOptions)
                : Mathf.Max(3, fallbackMaxPrimaryOptions);
        }

        public MenuWheelPageCapacity GetPageCapacity(int fallbackMaxPrimaryOptions)
        {
            return layoutSettings != null
                ? layoutSettings.GetPageCapacity(fallbackMaxPrimaryOptions)
                : new MenuWheelPageCapacity(Mathf.Max(3, fallbackMaxPrimaryOptions), 0);
        }

        private void Awake()
        {
            if (rootCanvas == null)
                rootCanvas = GetComponentInParent<Canvas>();

            canvasRect = rootCanvas.GetComponent<RectTransform>();
            canvasScaler = rootCanvas.GetComponent<CanvasScaler>();

            if (worldCamera == null)
                worldCamera = Camera.main;

            if (layoutSettings == null)
                layoutSettings = new MenuWheelLayoutSettings();

            DisableAutomaticScaling();
            NormalizeCanvasHierarchy();
            EnsureInputBlockerBehindWheel();
            EnsureCenterPreviewView();

            var rootGraphic = GetComponent<Graphic>();
            if (rootGraphic != null)
                rootGraphic.raycastTarget = false;

            SetVisible(false);

            if (inputBlocker != null)
                inputBlocker.onPressedOutside = HandlePressedOutside;
        }

        private void OnDestroy()
        {
            if (isOpen)
                RestoreTimeScale();
        }

        public void OpenMenuWheel(WheelMenuModel menuModel, float? overrideTimeScale = null)
        {
            if (menuModel == null)
                throw new ArgumentNullException(nameof(menuModel));

            gameObject.SetActive(true);

            currentMenu = menuModel;
            currentPageIndex = 0;

            ApplyTimeScale(overrideTimeScale ?? openTimeScale);

            SetVisible(true);
            isOpen = true;

            ConfigureCancelButton();
            BuildPage(pageIndex: 0);
            ApplyManualLayout();
            WorldObject previewTarget = menuModel.context.target != null
                ? menuModel.context.target
                : menuModel.context.actor;
            centerPreviewView?.Show(previewTarget);
            SetHighlightedIndex(-1);
            ignoreOpeningPointerUntilRelease = IsPrimaryPointerCurrentlyDown();
        }

        public void CloseMenuWheel()
        {
            ClearSpawnedButtons();

            centerPreviewView?.Hide();
            SetVisible(false);

            if (isOpen)
                RestoreTimeScale();

            hasCurrentLayout = false;
            highlightedIndex = -1;
            ignoreOpeningPointerUntilRelease = false;
            isOpen = false;
            currentMenu = null;
            currentPageIndex = 0;

            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!isOpen || currentMenu == null)
                return;

            ApplyManualLayout();

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

            Vector2 pointerScreenPos;
            bool pointerIsDown;
            bool pointerReleasedThisFrame;

            if (!TryGetPointer(out pointerScreenPos, out pointerIsDown, out pointerReleasedThisFrame))
            {
                SetHighlightedIndex(-1);
                return;
            }

            UpdateHighlight(pointerScreenPos);

            if (ignoreOpeningPointerUntilRelease)
            {
                if (pointerReleasedThisFrame || !pointerIsDown)
                    ignoreOpeningPointerUntilRelease = false;

                return;
            }

            if (pointerReleasedThisFrame)
            {
                Debug.Log($"[MenuWheelUI] Pointer released. screenPos={pointerScreenPos}, highlightedIndex={highlightedIndex}");
                HandlePointerReleased();
            }
        }

        private void HandlePointerReleased()
        {
            Debug.Log($"[MenuWheelUI] HandlePointerReleased() highlightedIndex={highlightedIndex}, spawnedButtons={spawnedButtons.Count}");

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
            Debug.Log($"[MenuWheelUI] Button pressed: id='{selectedOption.id}', label='{selectedOption.label}'", selectedButton);

            if (!selectedOption.isEnabled)
                return;

            if (selectedOption.navigationPageIndex >= 0)
            {
                SwitchToPage(selectedOption.navigationPageIndex);
                return;
            }

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
            ApplyManualLayout();
            SetHighlightedIndex(-1);
        }

        private void BuildPage(int pageIndex)
        {
            if (currentMenu == null)
                return;

            ClearSpawnedButtons();

            List<WheelOption> pageOptions = currentMenu.pages[pageIndex];

            for (int optionIndex = 0; optionIndex < pageOptions.Count; optionIndex++)
            {
                WheelOption option = pageOptions[optionIndex];

                MenuWheelOptionButtonView buttonView =
                    Instantiate(optionButtonPrefab, optionsContainer);

                buttonView.Bind(option);

                spawnedButtons.Add(buttonView);
            }
        }

        private void ApplyManualLayout()
        {
            NormalizeCanvasHierarchy();
            EnsureInputBlockerBehindWheel();

            currentLayout = layoutSettings.Resolve(new Vector2(Screen.width, Screen.height));
            hasCurrentLayout = true;

            Vector2 wheelCenterScreen = ClampCenterToScreen(ResolveWheelCenterScreen(currentLayout), currentLayout);
            Vector2 wheelCenterOffset = wheelCenterScreen - (currentLayout.ScreenSize * 0.5f);
            //($"ApplyManualLayout() wheelCenterScreen={wheelCenterScreen}, wheelCenterOffset={wheelCenterOffset}");
            
            optionsContainer.anchorMin = new Vector2(0.5f, 0.5f);
            optionsContainer.anchorMax = new Vector2(0.5f, 0.5f);
            optionsContainer.pivot = new Vector2(0.5f, 0.5f);
            optionsContainer.sizeDelta = currentLayout.ScreenSize;
            optionsContainer.anchoredPosition = wheelCenterOffset;
            UpdateRingSizes(wheelCenterOffset);

            LayoutButtons(spawnedButtons, currentLayout);

            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                spawnedButtons[i].ApplyManualLayout(currentLayout.OptionButtonSize, currentLayout.LabelInsets);
                spawnedButtons[i].ApplyVisualScale(currentLayout.ButtonScale);
            }

            cancelButton.ApplyManualLayout(currentLayout.CancelButtonSize, currentLayout.LabelInsets);
            cancelButton.ApplyVisualScale(currentLayout.ButtonScale);
            cancelButton.RectTransform.anchoredPosition = wheelCenterOffset + (Vector2.down * currentLayout.CancelOffset);
            centerPreviewView?.ApplyLayout(wheelCenterOffset, currentLayout);
        }

        private static void LayoutButtons(List<MenuWheelOptionButtonView> buttons, MenuWheelResolvedLayout layout)
        {
            if (buttons.Count == 0)
                return;

            var innerButtons = new List<MenuWheelOptionButtonView>(buttons.Count);
            var outerButtons = new List<MenuWheelOptionButtonView>(buttons.Count);

            for (int i = 0; i < buttons.Count; i++)
            {
                MenuWheelOptionButtonView button = buttons[i];
                WheelOptionRingPlacement ringPlacement = button.BoundOption != null
                    ? (button.BoundOption.resolvedRingPlacement != WheelOptionRingPlacement.Auto
                        ? button.BoundOption.resolvedRingPlacement
                        : button.BoundOption.ringPlacement)
                    : WheelOptionRingPlacement.Auto;

                if (layout.UseOuterRing && ringPlacement == WheelOptionRingPlacement.Outer)
                    outerButtons.Add(button);
                else
                    innerButtons.Add(button);
            }

            LayoutButtonsInCircle(
                innerButtons,
                radius: layout.InnerRingRadius,
                startAngleDeg: layout.InnerRingStartAngleDegrees);

            LayoutButtonsInCircle(
                outerButtons,
                radius: layout.OuterRingRadius,
                startAngleDeg: layout.OuterRingStartAngleDegrees);
        }

        private static void LayoutButtonsInCircle(List<MenuWheelOptionButtonView> buttons, float radius, float startAngleDeg)
        {
            if (buttons.Count == 0)
                return;

            float angleStep = 360f / buttons.Count;

            for (int i = 0; i < buttons.Count; i++)
            {
                float angleDeg = startAngleDeg - (angleStep * i);
                float angleRad = angleDeg * Mathf.Deg2Rad;

                float x = Mathf.Cos(angleRad) * radius;
                float y = Mathf.Sin(angleRad) * radius;

                buttons[i].RectTransform.anchoredPosition = new Vector2(x, y);
            }
        }

        private Vector2 ResolveWheelCenterScreen(MenuWheelResolvedLayout layout)
        {
            Vector2 screenCenter = layout.ScreenSize * 0.5f;

            if (layoutSettings.CenterMode == MenuWheelCenterMode.CenterOnScreen)
                return screenCenter;

            if (currentMenu == null || currentMenu.context.actor == null)
                return screenCenter;

            Camera? cameraToUse = worldCamera != null ? worldCamera : Camera.main;
            if (cameraToUse == null)
                return screenCenter;

            Vector3 worldAnchor = GetActorAnchor(currentMenu.context.actor);
            Vector3 projected = cameraToUse.WorldToScreenPoint(worldAnchor);

            if (projected.z <= 0f)
                return screenCenter;

            return new Vector2(projected.x, projected.y);
        }

        private static Vector3 GetActorAnchor(WorldObject actor)
        {
            if (actor.appearanceModule != null && actor.appearanceModule.mainRenderer != null)
                return actor.appearanceModule.mainRenderer.bounds.center;

            if (actor.TryGetComponent<Collider>(out Collider collider))
                return collider.bounds.center;

            return actor.transform.position + (Vector3.up * Mathf.Max(0.5f, actor.sizeRadius));
        }

        private static Vector2 ClampCenterToScreen(Vector2 desiredCenterScreen, MenuWheelResolvedLayout layout)
        {
            //Debug.Log($"ClampCenterToScreen({desiredCenterScreen})");

            Vector2 effectiveButtonSize = layout.EffectiveOptionButtonSize;
            Vector2 effectiveCancelSize = layout.EffectiveCancelButtonSize;
            float maxButtonRadius = layout.MaxButtonRadius;

            float horizontalExtent = maxButtonRadius + (effectiveButtonSize.x * 0.5f) + layout.EdgePadding;
            float topExtent = maxButtonRadius + (effectiveButtonSize.y * 0.5f) + layout.EdgePadding;
            float bottomExtent = Mathf.Max(
                maxButtonRadius + (effectiveButtonSize.y * 0.5f),
                layout.CancelOffset + (effectiveCancelSize.y * 0.5f)) + layout.EdgePadding;

            return new Vector2(
                Mathf.Clamp(desiredCenterScreen.x, horizontalExtent, layout.ScreenSize.x - horizontalExtent),
                Mathf.Clamp(desiredCenterScreen.y, bottomExtent, layout.ScreenSize.y - topExtent));
        }

        private void ConfigureCancelButton()
        {
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

            cancelButton.onClicked = CloseMenuWheel;
            cancelButton.onHoverChanged = null;
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
            if (!hasCurrentLayout)
                return;

            Camera? uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    optionsContainer, pointerScreenPos, uiCamera, out Vector2 localPos))
            {
                SetHighlightedIndex(-1);
                return;
            }

            centerPreviewView?.SetFacingDirection(localPos);

            if (localPos.magnitude < currentLayout.DeadzoneRadius)
            {
                SetHighlightedIndex(-1);
                return;
            }

            int bestIndex = FindClosestButton(localPos);
            //Debug.Log($"[MenuWheelUI] UpdateHighlight() localPos={localPos}, bestIndex={bestIndex}, deadzone={currentLayout.DeadzoneRadius}");
            SetHighlightedIndex(bestIndex);
        }

        private int FindClosestButton(Vector2 localDirectionFromCenter)
        {
            if (spawnedButtons.Count == 0)
                return -1;

            int bestIndex = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                Vector2 buttonPos = spawnedButtons[i].RectTransform.anchoredPosition;
                float distance = (localDirectionFromCenter - buttonPos).sqrMagnitude;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void SetHighlightedIndex(int newIndex)
        {
            if (highlightedIndex == newIndex)
                return;

            if (highlightedIndex >= 0 && highlightedIndex < spawnedButtons.Count)
                spawnedButtons[highlightedIndex].SetHighlighted(false);

            highlightedIndex = newIndex;

            if (highlightedIndex >= 0 && highlightedIndex < spawnedButtons.Count)
                spawnedButtons[highlightedIndex].SetHighlighted(true);
        }

        private void HandlePressedOutside()
        {
            if (ignoreOpeningPointerUntilRelease)
                return;

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
            if (wheelRoot != null)
                wheelRoot.gameObject.SetActive(visible);

            if (inputBlocker != null)
                inputBlocker.gameObject.SetActive(visible);
        }

        private void DisableAutomaticScaling()
        {
            if (canvasScaler != null)
                canvasScaler.enabled = false;
        }

        private void NormalizeCanvasHierarchy()
        {
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.anchoredPosition = new Vector2(0.5f, 0.5f);;
            canvasRect.sizeDelta = Vector2.zero;
            canvasRect.localScale = Vector3.one;

            wheelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            wheelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            wheelRoot.pivot = new Vector2(0.5f, 0.5f);
            wheelRoot.anchoredPosition = new Vector2(0.5f, 0.5f);
            wheelRoot.sizeDelta = Vector2.zero;
            wheelRoot.localScale = Vector3.one;
        }

        private void EnsureInputBlockerBehindWheel()
        {
            if (inputBlocker == null || wheelRoot == null)
                return;

            inputBlocker.transform.SetAsFirstSibling();
            wheelRoot.SetAsLastSibling();
        }

        private void EnsureCenterPreviewView()
        {
            if (centerPreviewView != null)
                return;

            Transform? existing = wheelRoot.Find("CenterPreview");
            GameObject previewObject;
            if (existing != null)
            {
                previewObject = existing.gameObject;
            }
            else
            {
                previewObject = new GameObject("CenterPreview", typeof(RectTransform));
                previewObject.transform.SetParent(wheelRoot, false);
            }

            centerPreviewView = previewObject.GetComponent<MenuWheelCenterPreviewView>();
            if (centerPreviewView == null)
                centerPreviewView = previewObject.AddComponent<MenuWheelCenterPreviewView>();
        }

        private static bool TryGetPointer(out Vector2 pointerScreenPos, out bool pointerIsDown, out bool pointerReleasedThisFrame)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.touches.Count > 0)
            {
                foreach (var touchControl in touchscreen.touches)
                {
                    if (touchControl == null)
                        continue;

                    var press = touchControl.press;
                    if (press == null)
                        continue;

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

            var mouse = Mouse.current;
            if (mouse != null)
            {
                pointerScreenPos = mouse.position.ReadValue();
                pointerIsDown = mouse.leftButton.isPressed;
                pointerReleasedThisFrame = mouse.leftButton.wasReleasedThisFrame;
                return true;
            }

            pointerScreenPos = default;
            pointerIsDown = false;
            pointerReleasedThisFrame = false;
            return false;
        }

        private static bool IsPrimaryPointerCurrentlyDown()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.touches.Count > 0)
            {
                foreach (var touchControl in touchscreen.touches)
                {
                    if (touchControl?.press != null && touchControl.press.isPressed)
                        return true;
                }
            }

            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }

        private void UpdateRingSizes(Vector2 wheelCenterOffset)
        {
            if (!hasCurrentLayout)
                return;

            UpdateRingRect(colorRingInner, wheelCenterOffset, currentLayout.InnerRingRadius, colorRingRadiusScale, visible: true);
            UpdateRingRect(colorRingOuter, wheelCenterOffset, currentLayout.OuterRingRadius, colorRingRadiusScale, visible: currentLayout.UseOuterRing);
        }

        private static void UpdateRingRect(RectTransform rect, Vector2 wheelCenterOffset, float radius, float radiusScale, bool visible)
        {
            if (rect == null)
                return;

            rect.gameObject.SetActive(visible);
            if (!visible)
                return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = wheelCenterOffset;

            float diameter = radius * 2f * radiusScale;

            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, diameter);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, diameter);
        }
    }
}
