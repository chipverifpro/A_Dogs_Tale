#nullable enable
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DogGame.UI.InteractionWheel
{
    public readonly struct MenuWheelPageCapacity
    {
        public MenuWheelPageCapacity(int innerRingCapacity, int outerRingCapacity)
        {
            InnerRingCapacity = Mathf.Max(1, innerRingCapacity);
            OuterRingCapacity = Mathf.Max(0, outerRingCapacity);
        }

        public int InnerRingCapacity { get; }
        public int OuterRingCapacity { get; }
        public int TotalCapacity => InnerRingCapacity + OuterRingCapacity;
        public bool UsesOuterRing => OuterRingCapacity > 0;
    }

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
        [Tooltip("Enable a second ring of buttons. The outer ring capacity is twice the inner ring capacity.")]
        [SerializeField] private bool enableOuterRing = false;

        [Tooltip("Inner ring radius as a fraction of the smaller screen dimension.")]
        [Range(0.08f, 0.45f)]
        [FormerlySerializedAs("wheelRadiusPercent")]
        [SerializeField] private float innerRadiusPercent = 0.22f;

        [Tooltip("Outer ring radius as a fraction of the smaller screen dimension.")]
        [Range(0.12f, 0.50f)]
        [SerializeField] private float outerRadiusPercent = 0.33f;

        [Tooltip("Deadzone radius as a fraction of the smaller screen dimension.")]
        [Range(0.02f, 0.25f)]
        [SerializeField] private float deadzonePercent = 0.10f;

        [Tooltip("Angle offset in degrees for the inner ring. 90 means the first option starts at the top.")]
        [SerializeField] private float startAngleDegrees = 90f;

        [Tooltip("Angle offset in degrees for the outer ring.")]
        [SerializeField] private float outerRingStartAngleDegrees = 90f;

        [Tooltip("Maximum buttons on the inner ring when the outer ring is enabled. The outer ring can hold twice this amount.")]
        [Range(1, 16)]
        [SerializeField] private int maxButtonsOnInnerRing = 6;

        [Header("Option Buttons")]
        [Tooltip("Option button width as a fraction of the smaller screen dimension.")]
        [Range(0.05f, 0.30f)]
        [SerializeField] private float optionWidthPercent = 0.16f;

        [Tooltip("Option button height as a fraction of the smaller screen dimension.")]
        [Range(0.03f, 0.16f)]
        [SerializeField] private float optionHeightPercent = 0.06f;

        [Tooltip("Uniform scale applied to option and cancel buttons, including their text.")]
        [Range(0.50f, 2.00f)]
        [SerializeField] private float buttonScale = 1.0f;

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
        public float OuterRingStartAngleDegrees => outerRingStartAngleDegrees;
        public bool EnableOuterRing => enableOuterRing;

        public MenuWheelPageCapacity GetPageCapacity(int fallbackMaxPrimaryOptions)
        {
            if (!enableOuterRing)
                return new MenuWheelPageCapacity(Mathf.Max(3, fallbackMaxPrimaryOptions), 0);

            int innerCapacity = Mathf.Max(1, maxButtonsOnInnerRing);
            return new MenuWheelPageCapacity(innerCapacity, innerCapacity * 2);
        }

        public int GetPrimaryOptionCapacity(int fallbackMaxPrimaryOptions)
        {
            return GetPageCapacity(fallbackMaxPrimaryOptions).TotalCapacity;
        }

        public MenuWheelResolvedLayout Resolve(Vector2 screenSize)
        {
            float screenMin = Mathf.Min(screenSize.x, screenSize.y);
            int innerCapacity = Mathf.Max(1, maxButtonsOnInnerRing);
            float innerRadius = screenMin * innerRadiusPercent;
            float outerRadius = enableOuterRing ? screenMin * outerRadiusPercent : innerRadius;
            Vector2 optionButtonSize = new Vector2(
                screenMin * optionWidthPercent,
                screenMin * optionHeightPercent);

            return new MenuWheelResolvedLayout(
                screenSize: screenSize,
                screenMin: screenMin,
                useOuterRing: enableOuterRing,
                innerRingRadius: innerRadius,
                outerRingRadius: outerRadius,
                maxInnerRingButtons: innerCapacity,
                outerRingCapacity: innerCapacity * 2,
                innerRingStartAngleDegrees: startAngleDegrees,
                outerRingStartAngleDegrees: outerRingStartAngleDegrees,
                deadzoneRadius: screenMin * deadzonePercent,
                optionButtonSize: optionButtonSize,
                buttonScale: buttonScale,
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
            bool useOuterRing,
            float innerRingRadius,
            float outerRingRadius,
            int maxInnerRingButtons,
            int outerRingCapacity,
            float innerRingStartAngleDegrees,
            float outerRingStartAngleDegrees,
            float deadzoneRadius,
            Vector2 optionButtonSize,
            float buttonScale,
            Vector2 cancelButtonSize,
            float cancelOffset,
            float previewSize,
            float edgePadding,
            Vector4 labelInsets)
        {
            ScreenSize = screenSize;
            ScreenMin = screenMin;
            UseOuterRing = useOuterRing;
            InnerRingRadius = innerRingRadius;
            OuterRingRadius = outerRingRadius;
            MaxInnerRingButtons = maxInnerRingButtons;
            OuterRingCapacity = outerRingCapacity;
            InnerRingStartAngleDegrees = innerRingStartAngleDegrees;
            OuterRingStartAngleDegrees = outerRingStartAngleDegrees;
            DeadzoneRadius = deadzoneRadius;
            OptionButtonSize = optionButtonSize;
            ButtonScale = buttonScale;
            CancelButtonSize = cancelButtonSize;
            CancelOffset = cancelOffset;
            PreviewSize = previewSize;
            EdgePadding = edgePadding;
            LabelInsets = labelInsets;
        }

        public Vector2 ScreenSize { get; }
        public float ScreenMin { get; }
        public bool UseOuterRing { get; }
        public float InnerRingRadius { get; }
        public float OuterRingRadius { get; }
        public int MaxInnerRingButtons { get; }
        public int OuterRingCapacity { get; }
        public float InnerRingStartAngleDegrees { get; }
        public float OuterRingStartAngleDegrees { get; }
        public float DeadzoneRadius { get; }
        public Vector2 OptionButtonSize { get; }
        public float ButtonScale { get; }
        public Vector2 CancelButtonSize { get; }
        public float CancelOffset { get; }
        public float PreviewSize { get; }
        public float EdgePadding { get; }
        public Vector4 LabelInsets { get; }
        public Vector2 EffectiveOptionButtonSize => OptionButtonSize * ButtonScale;
        public Vector2 EffectiveCancelButtonSize => CancelButtonSize * ButtonScale;
        public float MaxButtonRadius => UseOuterRing ? Mathf.Max(InnerRingRadius, OuterRingRadius) : InnerRingRadius;
    }
}
