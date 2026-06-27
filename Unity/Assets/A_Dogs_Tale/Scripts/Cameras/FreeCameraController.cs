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
        HandleMove(deltaTime);
        HandleMouseRotate();
    }

    private void HandleMove(float deltaTime)
    {
        PlayerInputState inputState = GetInputState();
        if (inputState == null)
            return;

        Vector3 input = new(inputState.moveAxis.x, inputState.strafeAxis, inputState.moveAxis.y);

        float speed = moveSpeed;
        if (inputState.sprintHeld)
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
        if (Mouse.current == null || !CursorIsCaptured())
            return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        if (delta == Vector2.zero)
            return;

        yaw += delta.x * rotationSpeed;
        pitch = Mathf.Clamp(pitch - delta.y * rotationSpeed, minPitch, maxPitch);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
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

    private PlayerInputState GetInputState()
    {
        if (switcher != null && switcher.dir != null && switcher.dir.gameInputRouter != null)
            return switcher.dir.gameInputRouter.InputState;

        GameInputRouter router = GameInputRouter.Instance;
        return router != null ? router.InputState : null;
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
