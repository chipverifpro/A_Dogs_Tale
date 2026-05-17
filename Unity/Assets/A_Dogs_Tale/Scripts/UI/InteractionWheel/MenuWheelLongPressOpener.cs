#nullable enable
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DogGame.UI.InteractionWheel
{
    /// <summary>
    /// Long-press opener for interaction wheel (Option 3 behavior):
    /// - Press and hold can start anywhere (not on UI)
    /// - While holding, pointer can move onto a target
    /// - When hold threshold reached, wheel opens for the acquired target (if any)
    /// - Uses Input System package only
    /// </summary>
    public sealed class MenuWheelLongPressOpener : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera mainCamera = null!;

        [Tooltip("Actor performing the interaction (player dog).")]
        [SerializeField] private WorldObject actorWorldObject = null!;

        [Header("Raycast")]
        [Tooltip("Which layers are considered interactable for long-press.")]
        [SerializeField] private LayerMask interactableMask = ~0;

        [Tooltip("Max raycast distance.")]
        [SerializeField] private float maxRayDistance = 2000f;

        [Header("Long Press")]
        [Tooltip("Hold time to open wheel (seconds).")]
        [SerializeField] private float holdToOpenSeconds = 0.45f;

        [Tooltip("Before a target is acquired, if pointer moves more than this many pixels, cancel tracking.")]
        [SerializeField] private float maxMovePixelsBeforeAcquire = 45f;

        [Header("Target Acquisition (Option 3)")]
        [SerializeField] private bool allowAcquireDuringHold = true;
        [SerializeField] private bool useAimAssist = true;
        [SerializeField] private float aimAssistRadiusPixels = 26f;
        [SerializeField] private int aimAssistSamples = 8;

        [Tooltip("Once a target is acquired, keep it locked (prevents flicker between nearby objects).")]
        [SerializeField] private bool lockTargetOnceAcquired = true;

        [Header("Wheel Build")]
        [SerializeField] private int maxPrimaryOptions = 8;

        [Tooltip("If true, do not open the wheel when there are no options (would show only Cancel).")]
        [SerializeField] private bool preventOpenIfNoOptions = true;

        [Tooltip("Time scale to apply while wheel is open (0 pause, 1 normal, 0.25 slow).")]
        [SerializeField] private float menuOpenTimeScale = 0f;

        // Press tracking
        private bool isPressTracking;
        private double pressStartTime;
        private Vector2 pressStartScreenPos;
        private int pressPointerId; // -1 mouse, touchId for touch

        // Target tracking
        private WorldObject? pressedTarget;
        private Vector3 pressedWorldPoint;

        private void Reset()
        {
            if (mainCamera == null) mainCamera = Camera.main!;
        }

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main!;
        }

        private void Update()
        {
            // Ensure wheel exists and suppress opener while wheel is open.
            MenuWheelUIController wheelUI = MenuWheelUIFactory.GetOrCreate();
            if (wheelUI != null && wheelUI.IsOpen)
            {
                ClearPressTracking();
                return;
            }

            // Get pointer state
            if (!TryGetPrimaryPointer(out PointerState pointer))
            {
                ClearPressTracking();
                return;
            }

            // Start tracking on press-down (Option 3: does NOT require hitting a target)
            if (pointer.pressedThisFrame)
            {
                if (NewInputAdapter.IsPointerReservedForMobileJoystick(pointer.pointerId) ||
                    NewInputAdapter.IsScreenPositionInMobileJoystickZone(pointer.screenPos))
                {
                    ClearPressTracking();
                    return;
                }

                if (IsPointerOverUI(pointer))
                {
                    ClearPressTracking();
                    return;
                }

                StartPressTracking(pointer);
                return;
            }

            // Update tracking
            if (!isPressTracking)
                return;

            if (NewInputAdapter.IsPointerReservedForMobileJoystick(pointer.pointerId))
            {
                ClearPressTracking();
                return;
            }

            // If pointer released/canceled, stop tracking
            if (!pointer.isDown)
            {
                ClearPressTracking();
                return;
            }

            // Before acquiring a target, allow some movement but cancel if huge drift
            if (pressedTarget == null)
            {
                float movedPixels = Vector2.Distance(pointer.screenPos, pressStartScreenPos);
                if (movedPixels > maxMovePixelsBeforeAcquire)
                {
                    ClearPressTracking();
                    return;
                }
            }

            // Acquire or update target during hold (Option 3)
            if (allowAcquireDuringHold)
            {
                if (pressedTarget == null)
                {
                    if (TryAcquireTarget(pointer.screenPos, out var acquiredTarget, out var acquiredWorldPoint))
                    {
                        pressedTarget = acquiredTarget;
                        pressedWorldPoint = acquiredWorldPoint;
                    }
                }
                else if (!lockTargetOnceAcquired)
                {
                    if (TryAcquireTarget(pointer.screenPos, out var acquiredTarget, out var acquiredWorldPoint))
                    {
                        pressedTarget = acquiredTarget;
                        pressedWorldPoint = acquiredWorldPoint;
                    }
                }
            }

            // Hold threshold reached?
            double heldSeconds = Time.unscaledTimeAsDouble - pressStartTime;
            if (heldSeconds >= holdToOpenSeconds)
            {
                // Open only if target was acquired
                if (pressedTarget != null)
                    TryOpenWheelForTarget(pressedTarget, pressedWorldPoint);

                ClearPressTracking();
            }
        }

        private void StartPressTracking(PointerState pointer)
        {
            isPressTracking = true;
            pressStartTime = Time.unscaledTimeAsDouble;
            pressStartScreenPos = pointer.screenPos;
            pressPointerId = pointer.pointerId;

            pressedTarget = null;
            pressedWorldPoint = default;
        }

        private void ClearPressTracking()
        {
            isPressTracking = false;
            pressStartTime = 0;
            pressStartScreenPos = default;
            pressPointerId = 0;

            pressedTarget = null;
            pressedWorldPoint = default;
        }

        private void TryOpenWheelForTarget(WorldObject target, Vector3 worldPoint)
        {
            if (actorWorldObject == null)
                return;

            MenuWheelUIController wheelUI = MenuWheelUIFactory.GetOrCreate();
            if (wheelUI == null)
                return;

            WheelMenuModel model = WheelMenuResolver.CreateWheelMenu(
                actor: actorWorldObject,
                target: target,
                worldPoint: worldPoint,
                pageCapacity: wheelUI.GetPageCapacity(maxPrimaryOptions)
            );

            if (preventOpenIfNoOptions)
            {
                bool hasAny = model.pages.Count > 0 && model.pages[0].Count > 0;
                if (!hasAny)
                    return;
            }

            wheelUI.OpenMenuWheel(model, overrideTimeScale: menuOpenTimeScale);
        }

        // ---------------------------
        // Target acquisition
        // ---------------------------

        private bool TryAcquireTarget(Vector2 screenPos, out WorldObject target, out Vector3 worldPoint)
        {
            // Direct raycast
            if (TryRaycastAt(screenPos, out target, out worldPoint))
                return true;

            if (!useAimAssist)
                return false;

            WorldObject? bestTarget = null;
            Vector3 bestWorldPoint = default;
            float bestScore = float.MaxValue;

            int samples = Mathf.Max(4, aimAssistSamples);
            float radius = Mathf.Max(2f, aimAssistRadiusPixels);

            for (int i = 0; i < samples; i++)
            {
                float angle = (i / (float)samples) * Mathf.PI * 2f;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Vector2 samplePos = screenPos + offset;

                if (TryRaycastAt(samplePos, out var candidate, out var candidateWorldPoint))
                {
                    float score = offset.magnitude; // all same here, but kept extensible
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestTarget = candidate;
                        bestWorldPoint = candidateWorldPoint;
                    }
                }
            }

            if (bestTarget != null)
            {
                target = bestTarget;
                worldPoint = bestWorldPoint;
                return true;
            }

            target = null!;
            worldPoint = default;
            return false;
        }

        private bool TryRaycastAt(Vector2 screenPos, out WorldObject target, out Vector3 worldPoint)
        {
            target = null!;
            worldPoint = default;

            if (mainCamera == null)
                return false;

            Ray ray = mainCamera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, interactableMask, QueryTriggerInteraction.Ignore))
                return false;

            worldPoint = hit.point;

            WorldObject? wo = hit.collider.GetComponentInParent<WorldObject>();
            if (wo == null)
                return false;

            target = wo;
            return true;
        }

        // ---------------------------
        // Input System pointer helper
        // ---------------------------

        private struct PointerState
        {
            public int pointerId; // -1 mouse, touchId for touch
            public Vector2 screenPos;
            public bool isDown;
            public bool pressedThisFrame;
        }

        private static bool TryGetPrimaryPointer(out PointerState pointer)
        {
            // Touch priority
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                for (int i = 0; i < touchscreen.touches.Count; i++)
                {
                    var touch = touchscreen.touches[i];
                    if (touch == null) continue;

                    bool pressedThisFrame = touch.press.wasPressedThisFrame;
                    bool isDown = touch.press.isPressed;

                    // We only care about a touch that is currently down or just began
                    if (!isDown && !pressedThisFrame)
                        continue;

                    int pointerId = touch.touchId.ReadValue();
                    Vector2 screenPos = touch.position.ReadValue();
                    if (NewInputAdapter.IsPointerReservedForMobileJoystick(pointerId) ||
                        NewInputAdapter.IsScreenPositionInMobileJoystickZone(screenPos))
                    {
                        continue;
                    }

                    pointer = new PointerState
                    {
                        pointerId = pointerId,
                        screenPos = screenPos,
                        isDown = isDown,
                        pressedThisFrame = pressedThisFrame
                    };
                    return true;
                }
            }

            // Mouse
            var mouse = Mouse.current;
            if (mouse != null)
            {
                pointer = new PointerState
                {
                    pointerId = -1,
                    screenPos = mouse.position.ReadValue(),
                    isDown = mouse.leftButton.isPressed,
                    pressedThisFrame = mouse.leftButton.wasPressedThisFrame
                };
                return true;
            }

            pointer = default;
            return false;
        }

        private static bool IsPointerOverUI(PointerState pointer)
        {
            if (EventSystem.current == null)
                return false;

            // Mouse
            if (pointer.pointerId < 0)
                return EventSystem.current.IsPointerOverGameObject();

            // Touch: use touchId
            return EventSystem.current.IsPointerOverGameObject(pointer.pointerId);
        }
    }
}
