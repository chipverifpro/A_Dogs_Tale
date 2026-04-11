using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using InspectorTools;

namespace EditorTools
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true, isFallback = true)]
    public class InspectorNoteEditor : Editor
    {
        private const float OuterPadding = 8f;
        private const float InnerPadding = 8f;
        private const float TitleBottomGap = 3f;
        private const float BoxBottomGap = 6f;
        private const float BorderThickness = 1f;

        public override void OnInspectorGUI()
        {
            DrawInspectorNoteIfPresent();
            DrawDefaultInspector();
        }

        private void DrawInspectorNoteIfPresent()
        {
            if (target == null)
                return;

            Type targetType = target.GetType();

            InspectorNoteAttribute noteAttribute =
                targetType.GetCustomAttribute<InspectorNoteAttribute>(inherit: true);

            if (noteAttribute == null)
                return;

            bool hasTitle = !string.IsNullOrWhiteSpace(noteAttribute.Title);
            bool hasMessage = !string.IsNullOrWhiteSpace(noteAttribute.Message);

            if (!hasTitle && !hasMessage)
                return;

            Color backgroundColor = GetBackgroundColor(noteAttribute.MessageType);
            Color borderColor = GetBorderColor(noteAttribute.MessageType);
            Color titleColor = GetTitleColor(noteAttribute.MessageType);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                wordWrap = true,
                richText = false,
                fontSize = EditorStyles.boldLabel.fontSize
            };

            GUIStyle bodyStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = false
            };

            float viewWidth = EditorGUIUtility.currentViewWidth;
            float contentWidth = Mathf.Max(100f, viewWidth - 40f - (OuterPadding + InnerPadding) * 2f);

            float titleHeight = hasTitle
                ? titleStyle.CalcHeight(new GUIContent(noteAttribute.Title), contentWidth)
                : 0f;

            float bodyHeight = hasMessage
                ? bodyStyle.CalcHeight(new GUIContent(noteAttribute.Message), contentWidth)
                : 0f;

            float totalHeight = OuterPadding * 2f
                                + BorderThickness * 2f
                                + (hasTitle ? titleHeight : 0f)
                                + (hasTitle && hasMessage ? TitleBottomGap : 0f)
                                + (hasMessage ? bodyHeight : 0f)
                                + InnerPadding * 2f;

            Rect outerRect = EditorGUILayout.GetControlRect(false, totalHeight);

            EditorGUI.DrawRect(outerRect, borderColor);

            Rect innerRect = new Rect(
                outerRect.x + BorderThickness,
                outerRect.y + BorderThickness,
                outerRect.width - BorderThickness * 2f,
                outerRect.height - BorderThickness * 2f);

            EditorGUI.DrawRect(innerRect, backgroundColor);

            Rect contentRect = new Rect(
                innerRect.x + InnerPadding,
                innerRect.y + InnerPadding,
                innerRect.width - InnerPadding * 2f,
                innerRect.height - InnerPadding * 2f);

            float currentY = contentRect.y;

            if (hasTitle)
            {
                Color previousContentColor = GUI.contentColor;
                GUI.contentColor = titleColor;

                Rect titleRect = new Rect(contentRect.x, currentY, contentRect.width, titleHeight);
                EditorGUI.LabelField(titleRect, noteAttribute.Title, titleStyle);

                GUI.contentColor = previousContentColor;
                currentY += titleHeight + (hasMessage ? TitleBottomGap : 0f);
            }

            if (hasMessage)
            {
                Rect bodyRect = new Rect(contentRect.x, currentY, contentRect.width, bodyHeight);
                EditorGUI.LabelField(bodyRect, noteAttribute.Message, bodyStyle);
            }

            GUILayout.Space(BoxBottomGap);
        }

        private static Color GetBackgroundColor(MessageType messageType)
        {
            bool isProSkin = EditorGUIUtility.isProSkin;

            return messageType switch
            {
                MessageType.Warning => isProSkin
                    ? new Color(0.33f, 0.27f, 0.12f, 1f)
                    : new Color(1.00f, 0.95f, 0.78f, 1f),

                MessageType.Error => isProSkin
                    ? new Color(0.35f, 0.16f, 0.16f, 1f)
                    : new Color(1.00f, 0.86f, 0.86f, 1f),

                _ => isProSkin
                    ? new Color(0.18f, 0.24f, 0.30f, 1f)
                    : new Color(0.86f, 0.93f, 1.00f, 1f),
            };
        }

        private static Color GetBorderColor(MessageType messageType)
        {
            bool isProSkin = EditorGUIUtility.isProSkin;

            return messageType switch
            {
                MessageType.Warning => isProSkin
                    ? new Color(0.74f, 0.58f, 0.18f, 1f)
                    : new Color(0.76f, 0.58f, 0.10f, 1f),

                MessageType.Error => isProSkin
                    ? new Color(0.80f, 0.30f, 0.30f, 1f)
                    : new Color(0.78f, 0.25f, 0.25f, 1f),

                _ => isProSkin
                    ? new Color(0.38f, 0.62f, 0.88f, 1f)
                    : new Color(0.24f, 0.49f, 0.82f, 1f),
            };
        }

        private static Color GetTitleColor(MessageType messageType)
        {
            bool isProSkin = EditorGUIUtility.isProSkin;

            return messageType switch
            {
                MessageType.Warning => isProSkin
                    ? new Color(1.00f, 0.87f, 0.45f, 1f)
                    : new Color(0.45f, 0.32f, 0.00f, 1f),

                MessageType.Error => isProSkin
                    ? new Color(1.00f, 0.62f, 0.62f, 1f)
                    : new Color(0.55f, 0.10f, 0.10f, 1f),

                _ => isProSkin
                    ? new Color(0.72f, 0.86f, 1.00f, 1f)
                    : new Color(0.10f, 0.31f, 0.58f, 1f),
            };
        }
    }
}