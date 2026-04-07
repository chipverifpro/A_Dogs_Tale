using UnityEngine;

public class DoorSimpleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform hingeTransform;

    [Header("State")]
    [SerializeField] private bool startsOpen = false;
    [SerializeField] private bool isOpen = false;

    [Header("Motion")]
    [SerializeField] private float closedAngle = 0f;
    [SerializeField] private float openAngle = -90f;
    [SerializeField] private float openCloseSpeedDegreesPerSecond = 180f;

    private float currentAngle;
    private float targetAngle;

    private void Awake()
    {
        if (hingeTransform == null)
        {
            Debug.LogError($"DoorSimpleController on {name} is missing hingeTransform.");
            enabled = false;
            return;
        }

        isOpen = startsOpen;
        currentAngle = isOpen ? openAngle : closedAngle;
        targetAngle = currentAngle;

        ApplyAngleImmediately(currentAngle);
    }

    private void Update()
    {
        if (Mathf.Approximately(currentAngle, targetAngle))
            return;

        currentAngle = Mathf.MoveTowards(
            currentAngle,
            targetAngle,
            openCloseSpeedDegreesPerSecond * Time.deltaTime);

        ApplyAngleImmediately(currentAngle);
    }

    public void OpenDoor()
    {
        isOpen = true;
        targetAngle = openAngle;
    }

    public void CloseDoor()
    {
        isOpen = false;
        targetAngle = closedAngle;
    }

    public void ToggleDoor()
    {
        if (isOpen)
            CloseDoor();
        else
            OpenDoor();
    }

    public bool IsOpen()
    {
        return isOpen;
    }

    private void ApplyAngleImmediately(float angleDegrees)
    {
        Vector3 localEulerAngles = hingeTransform.localEulerAngles;
        hingeTransform.localRotation = Quaternion.Euler(localEulerAngles.x, angleDegrees, localEulerAngles.z);
    }
}