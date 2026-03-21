#nullable enable
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DogGame.UI.InteractionWheel
{
    public sealed class MenuWheelRightClickOpener : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;
        [SerializeField] private bool debugLogEveryFrame = false;

        [Header("References")]
        [SerializeField] private Camera mainCamera = null!;
        [SerializeField] private WorldObject actorWorldObject = null!;

        [Header("Raycast")]
        [SerializeField] private LayerMask interactableMask = ~0;
        [SerializeField] private float maxRayDistance = 2000f;

        [Header("Aim Assist")]
        [SerializeField] private bool useAimAssist = true;
        [SerializeField] private float aimAssistRadiusPixels = 18f;
        [SerializeField] private int aimAssistSamples = 8;

        [Header("Wheel Build")]
        [SerializeField] private int maxPrimaryOptions = 8;
        [SerializeField] private bool preventOpenIfNoOptions = true;

        [Header("Wheel Behavior")]
        [SerializeField] private float menuOpenTimeScale = 0f;
        [SerializeField] private bool rightClickTogglesClosedWhenOpen = true;

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
            var mouse = Mouse.current;
            if (mouse == null)
            {
                if (debugLogs && debugLogEveryFrame)
                    Debug.Log("[WheelRC] Mouse.current is null (no mouse device).", this);
                return;
            }

            if (debugLogs && debugLogEveryFrame)
            {
                Vector2 pos = mouse.position.ReadValue();
                Debug.Log($"[WheelRC] frame; mousePos={pos} rightPressedThisFrame={mouse.rightButton.wasPressedThisFrame} rightIsPressed={mouse.rightButton.isPressed}", this);
            }

            // Only act on right-click down
            if (!mouse.rightButton.wasPressedThisFrame)
                return;

            if (debugLogs)
                Debug.Log("[WheelRC] Right-click detected (wasPressedThisFrame).", this);

            // Ensure wheel UI exists
            MenuWheelUIController wheelUI = MenuWheelUIFactory.GetOrCreate();
            if (wheelUI == null)
            {
                if (debugLogs)
                    Debug.LogError("[WheelRC] MenuWheelUIFactory.GetOrCreate() returned null. Check Resources path / prefab.", this);
                return;
            }

            // If wheel already open, optionally toggle closed
            if (wheelUI.IsOpen)
            {
                if (debugLogs)
                    Debug.Log($"[WheelRC] Wheel already open. ToggleClose={rightClickTogglesClosedWhenOpen}", this);

                if (rightClickTogglesClosedWhenOpen)
                    wheelUI.CloseMenuWheel();

                return;
            }

            // Ignore if click started over UI
            Vector2 screenPos = mouse.position.ReadValue();
            if (IsPointerOverBlockingUI(screenPos))
            {
                if (debugLogs)
                    Debug.Log("[WheelRC] Pointer is over BLOCKING UI; ignoring right-click.", this);
                return;
            }

            if (!TryAcquireTarget(screenPos, out var target, out var worldPoint))
            {
                if (debugLogs)
                    Debug.Log($"[WheelRC] No target acquired at screenPos={screenPos}. Check interactableMask/layers/colliders.", this);
                return;
            }

            if (debugLogs)
                Debug.Log($"[WheelRC] Acquired target WorldObject='{target.name}' at worldPoint={worldPoint}", target);

            WheelMenuModel model = WheelMenuResolver.CreateWheelMenu(
                actor: actorWorldObject,
                target: target,
                worldPoint: worldPoint,
                maxPrimaryOptions: maxPrimaryOptions
            );

            int optionCountPage0 = (model.pages.Count > 0) ? model.pages[0].Count : 0;

            if (debugLogs)
                Debug.Log($"[WheelRC] Resolved menu pages={model.pages.Count} page0Options={optionCountPage0}", this);

            if (preventOpenIfNoOptions && optionCountPage0 == 0)
            {
                if (debugLogs)
                    Debug.Log("[WheelRC] preventOpenIfNoOptions=true and page0Options=0. Not opening.", this);
                return;
            }

            if (debugLogs)
                Debug.Log("[WheelRC] Opening wheel.", this);

            wheelUI.OpenMenuWheel(model, overrideTimeScale: menuOpenTimeScale);
        }

        private static bool IsPointerOverBlockingUI(Vector2 screenPos)
        {
            if (EventSystem.current == null)
                return false;

            // If there is no active raycaster/UI system, nothing blocks.
            if (EventSystem.current.currentInputModule == null)
                return false;

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPos
            };

            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            for (int i = 0; i < results.Count; i++)
            {
                GameObject go = results[i].gameObject;
                if (go == null) continue;

                // Ignore wheel UI blocker if it’s accidentally present/inactive state weirdness
                if (go.GetComponentInParent<MenuWheelUIController>(includeInactive: true) != null)
                    continue;

                // Treat UI as blocking only if it's a real interactive control
                if (go.GetComponentInParent<UnityEngine.UI.Selectable>(includeInactive: true) != null)
                    return true;

                // Or if it explicitly wants to block raycasts (common for modal panels)
                var graphic = go.GetComponent<UnityEngine.UI.Graphic>();
                if (graphic != null && graphic.raycastTarget)
                    return true;
            }

            return false;
        }
        private bool TryAcquireTarget(Vector2 screenPos, out WorldObject target, out Vector3 worldPoint)
        {
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
                    float score = offset.magnitude;
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
            {
                if (debugLogs)
                    Debug.LogError("[WheelRC] mainCamera is null.", this);
                return false;
            }

            Ray ray = mainCamera.ScreenPointToRay(screenPos);

            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, interactableMask, QueryTriggerInteraction.Ignore))
            {
                if (debugLogs && debugLogEveryFrame)
                    Debug.Log("[WheelRC] Raycast missed.", this);
                return false;
            }

            if (debugLogs)
                Debug.Log($"[WheelRC] Raycast hit collider='{hit.collider.name}' layer={hit.collider.gameObject.layer} point={hit.point}", hit.collider);

            worldPoint = hit.point;

            WorldObject? wo = hit.collider.GetComponentInParent<WorldObject>();
            if (wo == null)
            {
                if (debugLogs)
                    Debug.Log($"[WheelRC] Hit '{hit.collider.name}' but no WorldObject in parents.", hit.collider);
                return false;
            }

            target = wo;
            return true;
        }

        private static bool IsPointerOverUI(int pointerId)
        {
            if (EventSystem.current == null)
                return false;

            if (pointerId < 0)
                return EventSystem.current.IsPointerOverGameObject();

            return EventSystem.current.IsPointerOverGameObject(pointerId);
        }
    }
}