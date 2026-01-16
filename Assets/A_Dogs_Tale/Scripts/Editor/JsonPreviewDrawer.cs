#nullable enable
using System;
using System.Collections.Generic;
using DogGame.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(JsonPreviewAttribute))]
public sealed class JsonPreviewDrawer : PropertyDrawer
{
    private GUIStyle? richTextStyle;
    private GUIStyle? headerStyle;

    // Per-field scroll state (keyed by property path)
    private readonly Dictionary<string, Vector2> scrollPositions = new();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var attr = (JsonPreviewAttribute)attribute;

        // Line 1: editable field
        // Line 2: buttons row
        // Box: preview
        return EditorGUIUtility.singleLineHeight + 2
             + EditorGUIUtility.singleLineHeight + 4
             + attr.Height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EnsureStyles();

        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "JsonPreviewAttribute only works on string fields.");
            return;
        }

        var attr = (JsonPreviewAttribute)attribute;

        // --- Row 1: normal editable string field ---
        var row1 = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(row1, property, label);

        // --- Row 2: buttons ---
        var row2 = new Rect(position.x, row1.yMax + 2, position.width, EditorGUIUtility.singleLineHeight);
        DrawButtons(row2, property);

        // --- Preview box ---
        var boxRect = new Rect(position.x, row2.yMax + 4, position.width, attr.Height);
        GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

        // Inner padding
        float pad = 6f;
        var contentRect = new Rect(boxRect.x + pad, boxRect.y + pad, boxRect.width - pad * 2, boxRect.height - pad * 2);

        // Build rich preview text (syntax colored)
        string json = property.stringValue ?? "";
        string rich = JsonSyntaxHighlighter.ToRichText(json);

        // Compute scroll view content height
        float viewWidth = contentRect.width - 16f; // account for vertical scrollbar
        float contentHeight = richTextStyle!.CalcHeight(new GUIContent(rich), viewWidth);
        var viewRect = new Rect(0, 0, viewWidth, Mathf.Max(contentRect.height, contentHeight));

        // Remember scroll per property
        string key = property.propertyPath;
        if (!scrollPositions.TryGetValue(key, out var scroll))
            scroll = Vector2.zero;

        // Draw scroll view
        scroll = GUI.BeginScrollView(contentRect, scroll, viewRect, false, true);

        // Draw selectable rich label inside
        var labelRect = new Rect(0, 0, viewRect.width, viewRect.height);
        EditorGUI.SelectableLabel(labelRect, rich, richTextStyle);

        GUI.EndScrollView();
        scrollPositions[key] = scroll;
    }

    private void DrawButtons(Rect rect, SerializedProperty property)
    {
        float buttonW = 74f;
        float gap = 6f;

        var copyRect = new Rect(rect.x, rect.y, buttonW, rect.height);
        if (GUI.Button(copyRect, "Copy"))
        {
            EditorGUIUtility.systemCopyBuffer = property.stringValue ?? "";
        }

        var prettyRect = new Rect(copyRect.xMax + gap, rect.y, buttonW, rect.height);
        if (GUI.Button(prettyRect, "Pretty"))
        {
            string json = property.stringValue ?? "";
            property.stringValue = TryPrettyPrintAndNormalize(json);
            property.serializedObject.ApplyModifiedProperties();
        }

        var minifyRect = new Rect(prettyRect.xMax + gap, rect.y, buttonW, rect.height);
        if (GUI.Button(minifyRect, "Minify"))
        {
            string json = property.stringValue ?? "";
            property.stringValue = TryMinify(json);
            property.serializedObject.ApplyModifiedProperties();
        }

        // Optional hint on the right
        var hintRect = new Rect(minifyRect.xMax + gap, rect.y, rect.xMax - (minifyRect.xMax + gap), rect.height);
        if (hintRect.width > 40)
            GUI.Label(hintRect, "Rich JSON preview", headerStyle);
    }

    private void EnsureStyles()
    {
        if (richTextStyle == null)
        {
            richTextStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                richText = true,
                fontSize = 11
            };

            // Monospace if available; safe fallback otherwise
            var mono =
                Font.CreateDynamicFontFromOSFont("Menlo", 11) ??
                Font.CreateDynamicFontFromOSFont("Consolas", 11) ??
                Font.CreateDynamicFontFromOSFont("Courier New", 11);

            if (mono != null)
                richTextStyle.font = mono;
        }

        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft
            };
        }
    }

    // -------------------------
    // Formatting helpers
    // -------------------------

    private static string TryPrettyPrintAndNormalize(string json)
    {
        try
        {
            var token = JToken.Parse(json);

            // Make embedded string payloads readable in Inspector:
            NormalizeStringsRecursive(token);

            return token.ToString(Formatting.Indented);
        }
        catch
        {
            return json;
        }
    }

    private static string TryMinify(string json)
    {
        try
        {
            var token = JToken.Parse(json);
            return token.ToString(Formatting.None);
        }
        catch
        {
            return json;
        }
    }

    private static void NormalizeStringsRecursive(JToken token)
    {
        switch (token.Type)
        {
            case JTokenType.Object:
                foreach (var prop in ((JObject)token).Properties())
                    NormalizeStringsRecursive(prop.Value);
                break;

            case JTokenType.Array:
                foreach (var item in (JArray)token)
                    NormalizeStringsRecursive(item);
                break;

            case JTokenType.String:
            {
                string s = token.Value<string>() ?? "";
                s = NormalizeStringForDisplay(s);
                ((JValue)token).Value = s;
                break;
            }
        }
    }

    private static string NormalizeStringForDisplay(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        // Convert literal sequences into real whitespace (common in logged packets)
        s = s.Replace("\\r\\n", "\n");
        s = s.Replace("\\n", "\n");
        s = s.Replace("\\t", "\t");

        // Strip markdown code fences if the model wrapped JSON
        s = StripCodeFencesIfPresent(s);

        // Normalize line endings
        s = s.Replace("\r\n", "\n").Replace("\r", "\n");

        return s;
    }

    private static string StripCodeFencesIfPresent(string s)
    {
        string t = s.Trim();

        if (!t.StartsWith("```", StringComparison.Ordinal))
            return s;

        int firstNewline = t.IndexOf('\n');
        if (firstNewline < 0)
            return s;

        string withoutHeader = t.Substring(firstNewline + 1);

        int lastFence = withoutHeader.LastIndexOf("```", StringComparison.Ordinal);
        if (lastFence >= 0)
            withoutHeader = withoutHeader.Substring(0, lastFence);

        return withoutHeader.Trim();
    }
}