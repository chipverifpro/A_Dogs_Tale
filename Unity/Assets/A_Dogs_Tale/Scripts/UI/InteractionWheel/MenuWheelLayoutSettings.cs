#nullable enable
using System;
using UnityEngine;

namespace DogGame.UI.InteractionWheel
{
    public enum MenuWheelCenterMode
    {
        CenterOnScreen = 0,
        CenterOnAgent = 1,
    }

    [Serializable]
    public sealed class MenuWheelLayoutSettings
    {
        [Header("Placement")]
        [SerializeField] private MenuWheelCenterMode centerMode = MenuWheelCenterMode.CenterOnScreen;

        [Tooltip("Padding from the screen edge as a fraction of the smaller screen dimension.")]
        [Range(0.00f, 0.25f)]
        [SerializeField] private float edgePaddingPercent = 0.06f;

        [Header("Wheel")]
        [Tooltip("Wheel radius as a fraction of the smaller screen dimension.")]
        [Range(0.08f, 0.45f)]
        [SerializeField] private float wheelRadiusPercent = 0.22f;

        [Tooltip("Deadzone radius as a fraction of the smaller screen dimension.")]
        [Range(0.02f, 0.25f)]
        [SerializeField] private float deadzonePercent = 0.10f;

        [Tooltip("Angle offset in degrees. 90 means the first option starts at the top.")]
        [SerializeField] private float startAngleDegrees = 90f;

        [Header("Option Buttons")]
        [Tooltip("Option button width as a fraction of the smaller screen dimension.")]
        [Range(0.05f, 0.30f)]
        [SerializeField] private float optionWidthPercent = 0.16f;

        [Tooltip("Option button height as a fraction of the smaller screen dimension.")]
        [Range(0.03f, 0.16f)]
        [SerializeField] private float optionHeightPercent = 0.06f;

        [Header("Cancel Button")]
        [Tooltip("Cancel button width as a fraction of the smaller screen dimension.")]
        [Range(0.05f, 0.30f)]
        [SerializeField] private float cancelWidthPercent = 0.14f;

        [Tooltip("Cancel button height as a fraction of the smaller screen dimension.")]
        [Range(0.03f, 0.16f)]
        [SerializeField] private float cancelHeightPercent = 0.055f;

        [Tooltip("Distance from wheel center to cancel button center as a fraction of the smaller screen dimension.")]
        [Range(0.00f, 0.25f)]
        [SerializeField] private float cancelOffsetPercent = 0.07f;

        [Header("Center Preview")]
        [Tooltip("Preview size as a fraction of the smaller screen dimension.")]
        [Range(0.05f, 0.30f)]
        [SerializeField] private float previewSizePercent = 0.16f;

        [Header("Label Insets")]
        [Tooltip("Horizontal inset inside each button as a fraction of the smaller screen dimension.")]
        [Range(0.00f, 0.06f)]
        [SerializeField] private float horizontalLabelInsetPercent = 0.012f;

        [Tooltip("Vertical inset inside each button as a fraction of the smaller screen dimension.")]
        [Range(0.00f, 0.06f)]
        [SerializeField] private float verticalLabelInsetPercent = 0.008f;

        public MenuWheelCenterMode CenterMode => centerMode;
        public float StartAngleDegrees => startAngleDegrees;

        public MenuWheelResolvedLayout Resolve(Vector2 screenSize)
        {
            float screenMin = Mathf.Min(screenSize.x, screenSize.y);

            return new MenuWheelResolvedLayout(
                screenSize: screenSize,
                screenMin: screenMin,
                wheelRadius: screenMin * wheelRadiusPercent,
                deadzoneRadius: screenMin * deadzonePercent,
                optionButtonSize: new Vector2(
                    screenMin * optionWidthPercent,
                    screenMin * optionHeightPercent),
                cancelButtonSize: new Vector2(
                    screenMin * cancelWidthPercent,
                    screenMin * cancelHeightPercent),
                cancelOffset: screenMin * cancelOffsetPercent,
                previewSize: screenMin * previewSizePercent,
                edgePadding: screenMin * edgePaddingPercent,
                labelInsets: new Vector4(
                    screenMin * horizontalLabelInsetPercent,
                    screenMin * verticalLabelInsetPercent,
                    screenMin * horizontalLabelInsetPercent,
                    screenMin * verticalLabelInsetPercent));
        }
    }

    public readonly struct MenuWheelResolvedLayout
    {
        public MenuWheelResolvedLayout(
            Vector2 screenSize,
            float screenMin,
            float wheelRadius,
            float deadzoneRadius,
            Vector2 optionButtonSize,
            Vector2 cancelButtonSize,
            float cancelOffset,
            float previewSize,
            float edgePadding,
            Vector4 labelInsets)
        {
            ScreenSize = screenSize;
            ScreenMin = screenMin;
            WheelRadius = wheelRadius;
            DeadzoneRadius = deadzoneRadius;
            OptionButtonSize = optionButtonSize;
            CancelButtonSize = cancelButtonSize;
            CancelOffset = cancelOffset;
            PreviewSize = previewSize;
            EdgePadding = edgePadding;
            LabelInsets = labelInsets;
        }

        public Vector2 ScreenSize { get; }
        public float ScreenMin { get; }
        public float WheelRadius { get; }
        public float DeadzoneRadius { get; }
        public Vector2 OptionButtonSize { get; }
        public Vector2 CancelButtonSize { get; }
        public float CancelOffset { get; }
        public float PreviewSize { get; }
        public float EdgePadding { get; }
        public Vector4 LabelInsets { get; }
    }
}
