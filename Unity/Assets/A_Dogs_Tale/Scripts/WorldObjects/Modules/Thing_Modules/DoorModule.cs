#nullable enable
using DogGame.Modules;
using UnityEditor.Tilemaps;
using UnityEngine;
using InspectorTools;

namespace DogGame.World
{
    [InspectorNote("Thing_Modules/Door Module", "Attributes for opening/closing of a door, chest, or hole.")]
    [DisallowMultipleComponent]
    public class DoorModule : WorldModule
    {
        [Header("References")]
        public Transform? hingeTransform;

        [Header("Door State")]
        public bool startsOpen = false;
        public bool isLocked = false;

        [Header("Angles")]
        public float closedAngle = 0f;
        public float openAngle = 90f;

        [Header("Motion")]
        public float openCloseSpeedDegreesPerSecond = 180f;

        private bool isOpen;
        private float currentAngle;
        private float targetAngle;

        public bool IsOpen => isOpen;
        public bool IsClosed => !isOpen;
        public bool IsLocked => isLocked;

        protected override void Awake()
        {
            isOpen = startsOpen;
            currentAngle = isOpen ? openAngle : closedAngle;
            targetAngle = currentAngle;

            ApplyAngleImmediately(currentAngle);
        }

        protected override void Update()
        {
            if (hingeTransform == null)
                return;

            if (Mathf.Approximately(currentAngle, targetAngle))
                return;

            currentAngle = Mathf.MoveTowards(
                currentAngle,
                targetAngle,
                openCloseSpeedDegreesPerSecond * Time.deltaTime);

            ApplyAngleImmediately(currentAngle);
        }

        public bool CanOpen()
        {
            return !isLocked && !isOpen;
        }

        public bool CanClose()
        {
            return isOpen;
        }

        public bool OpenDoor()
        {
            if (!CanOpen())
                return false;

            isOpen = true;
            targetAngle = openAngle;
            return true;
        }

        public bool CloseDoor()
        {
            if (!CanClose())
                return false;

            isOpen = false;
            targetAngle = closedAngle;
            return true;
        }

        public bool ToggleDoor()
        {
            if (isOpen)
                return CloseDoor();

            return OpenDoor();
        }

        public void SetLocked(bool locked)
        {
            isLocked = locked;
        }

        private void ApplyAngleImmediately(float angleDegrees)
        {
            if (hingeTransform == null)
                return;

            Vector3 hingeEulerAngles = hingeTransform.localEulerAngles;
            hingeTransform.localRotation = Quaternion.Euler(
                hingeEulerAngles.x,
                angleDegrees,
                hingeEulerAngles.z);
        }
    }
}