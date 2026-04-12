using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class FreeCameraController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float fastMoveMultiplier = 1.5f;
    [SerializeField] private float moveAcceleration = 14f;
    [SerializeField] private float moveDeceleration = 28f;
    [SerializeField] private float panSpeed = 0.0075f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 0.0375f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private CameraModeSwitcher switcher;
    private bool isActive;
    private float yaw;
    private float pitch;
    private Vector3 currentVelocity;
    private CursorLockMode previousCursorLockMode = CursorLockMode.None;
    private bool previousCursorVisible = true;

    private void OnEnable()
    {
        SnapToCurrentTransform();
    }

    public void SetSwitcher(CameraModeSwitcher owner)
    {
        switcher = owner;
    }

    public void SetActive(bool active)
    {
        isActive = active;
        enabled = active;
        currentVelocity = Vector3.zero;

        if (active)
        {
            SnapToCurrentTransform();
            CaptureCursor();
        }
        else
        {
            ReleaseCursor();
        }
    }

    public void SnapToCurrentTransform()
    {
        Vector3 euler = transform.rotation.eulerAngles;
        yaw = euler.y;
        pitch = NormalizePitch(euler.x);
    }

    public void NotifyZoomChanged()
    {
        // Intentionally empty; lens updates happen on the vcam.
    }

    private void Update()
    {
        if (!isActive || switcher == null || !switcher.freeCameraActive)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        HandleKeyboardMove(deltaTime);
        HandleMouseRotate();
        HandleMousePan(deltaTime);
    }

    private void HandleKeyboardMove(float deltaTime)
    {
        if (Keyboard.current == null)
            return;

        Vector3 input = Vector3.zero;

        if (Keyboard.current.wKey.isPressed) input += Vector3.forward;
        if (Keyboard.current.sKey.isPressed) input += Vector3.back;
        if (Keyboard.current.dKey.isPressed) input += Vector3.right;
        if (Keyboard.current.aKey.isPressed) input += Vector3.left;
        if (Keyboard.current.eKey.isPressed) input += Vector3.up;
        if (Keyboard.current.qKey.isPressed) input += Vector3.down;

        float speed = moveSpeed;
        if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)
            speed *= fastMoveMultiplier;

        Vector3 desiredVelocity = Vector3.zero;
        if (input != Vector3.zero)
        {
            Vector3 worldMove =
                (transform.forward * input.z) +
                (transform.right * input.x) +
                (Vector3.up * input.y);

            desiredVelocity = worldMove.normalized * speed;
        }

        float rate = input == Vector3.zero ? moveDeceleration : moveAcceleration;
        currentVelocity = Vector3.MoveTowards(currentVelocity, desiredVelocity, rate * deltaTime);

        if (currentVelocity.sqrMagnitude <= 0.000001f)
            currentVelocity = Vector3.zero;

        transform.position += currentVelocity * deltaTime;
    }

    private void HandleMouseRotate()
    {
        if (Mouse.current == null || !CursorIsCaptured() || IsPanModifierPressed())
            return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        if (delta == Vector2.zero)
            return;

        yaw += delta.x * rotationSpeed;
        pitch = Mathf.Clamp(pitch - delta.y * rotationSpeed, minPitch, maxPitch);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleMousePan(float deltaTime)
    {
        if (Mouse.current == null || !CursorIsCaptured() || !IsPanModifierPressed())
            return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        if (delta == Vector2.zero)
            return;

        Vector3 pan =
            (-transform.right * delta.x * panSpeed) +
            (-transform.up * delta.y * panSpeed);

        transform.position += pan * Mathf.Max(1f, moveSpeed * 0.1f) * deltaTime * 60f;
    }

    public void FocusLeaderNow()
    {
        if (switcher == null || switcher.dir == null || switcher.dir.playerPack == null)
            return;

        WorldObject leader = switcher.dir.playerPack.packLeader;
        if (leader == null)
            return;

        Vector3 anchor = leader.transform.position;
        if (leader.appearanceModule != null && leader.appearanceModule.head != null)
            anchor = leader.appearanceModule.head.transform.position;

        Vector3 toLeader = anchor - transform.position;
        if (toLeader.sqrMagnitude <= 0.0001f)
            return;

        Quaternion lookRotation = Quaternion.LookRotation(toLeader.normalized, Vector3.up);
        Vector3 lookEuler = lookRotation.eulerAngles;
        yaw = lookEuler.y;
        pitch = NormalizePitch(lookEuler.x);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private static bool IsPanModifierPressed()
    {
        return Keyboard.current != null &&
               (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
    }

    private static float NormalizePitch(float x)
    {
        if (x > 180f)
            x -= 360f;
        return x;
    }

    private void CaptureCursor()
    {
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ReleaseCursor()
    {
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;
    }

    private static bool CursorIsCaptured()
    {
        return Cursor.lockState == CursorLockMode.Locked;
    }
}
