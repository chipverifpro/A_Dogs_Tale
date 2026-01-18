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

    private readonly Dictionary<string, string> prettyCache = new();
    private readonly Dictionary<string, bool> showPretty = new();
    
    private readonly Dictionary<string, bool> isLocked = new();

    private enum ViewMode { Raw, Pretty, Minify, ExtractedPlan }

    private readonly Dictionary<string, string> viewCache = new();
    private readonly Dictionary<string, ViewMode> viewMode = new();
    
    private readonly Dictionary<string, int> lastRawHash = new();

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

        // --- Row 1: editable string field ---
        var row1 = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(row1, property, label);

        // --- Row 2: buttons ---
        var row2 = new Rect(position.x, row1.yMax + 2, position.width, EditorGUIUtility.singleLineHeight);
        DrawButtons(row2, property);

        // --- Preview box ---
        var boxRect = new Rect(position.x, row2.yMax + 4, position.width, attr.Height);
        GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

        float pad = 6f;
        var contentRect = new Rect(boxRect.x + pad, boxRect.y + pad, boxRect.width - pad * 2, boxRect.height - pad * 2);

        string key = property.propertyPath;
        string raw = property.stringValue ?? "";

        // Ensure dictionaries have entries
        if (!viewMode.ContainsKey(key))
            viewMode[key] = ViewMode.Raw;

        if (!isLocked.ContainsKey(key))
            isLocked[key] = false;

        // Detect raw changes
        int hash = raw.GetHashCode();
        bool rawChanged = !lastRawHash.TryGetValue(key, out int prevHash) || prevHash != hash;

        // Option B: Live-refresh cached view on raw change, BUT NOT when locked
        if (rawChanged && !isLocked[key])
        {
            lastRawHash[key] = hash;

            switch (viewMode[key])
            {
                case ViewMode.Pretty:
                    viewCache[key] = BuildPrettyView(raw);
                    break;

                case ViewMode.Minify:
                    viewCache[key] = TryMinify(raw);
                    break;

                case ViewMode.ExtractedPlan:
                    viewCache[key] = BuildExtractedPlanView(raw);
                    break;

                case ViewMode.Raw:
                default:
                    viewCache.Remove(key);
                    break;
            }

            // Reset scroll on new content (optional)
            scrollPositions[key] = Vector2.zero;
        }
        else if (rawChanged && isLocked[key])
        {
            // Still update the hash so we don't repeatedly detect "changed" every repaint,
            // but do NOT change the displayed cache.
            lastRawHash[key] = hash;
        }

        // Choose what to display:
        // - If not locked: display mode drives view (raw or cached)
        // - If locked: keep showing whatever cache already had; if none, show raw snapshot
        string displayText;
        if (viewMode[key] == ViewMode.Raw)
        {
            displayText = raw;
        }
        else
        {
            if (viewCache.TryGetValue(key, out var cached))
                displayText = cached;
            else
                displayText = isLocked[key] ? raw : raw; // fallback; cache normally exists after pressing Pretty/Extract
        }

        // Syntax highlight
        string rich = JsonSyntaxHighlighter.ToRichText(displayText);

        // Compute scroll content height
        float viewWidth = contentRect.width - 16f; // account for vertical scrollbar
        float contentHeight = richTextStyle!.CalcHeight(new GUIContent(rich), viewWidth);
        var viewRect = new Rect(0, 0, viewWidth, Mathf.Max(contentRect.height, contentHeight));

        if (!scrollPositions.TryGetValue(key, out var scroll))
            scroll = Vector2.zero;

        scroll = GUI.BeginScrollView(contentRect, scroll, viewRect, false, true);

        var labelRect = new Rect(0, 0, viewRect.width, viewRect.height);
        EditorGUI.SelectableLabel(labelRect, rich, richTextStyle);

        GUI.EndScrollView();
        scrollPositions[key] = scroll;
    }

    private void DrawButtons(Rect rect, SerializedProperty property)
    {
        float buttonW = 74f;
        float gap = 6f;

        string key = property.propertyPath;
        string raw = property.stringValue ?? "";

        if (!viewMode.ContainsKey(key))
            viewMode[key] = ViewMode.Raw;

        if (!isLocked.ContainsKey(key))
            isLocked[key] = false;

        // Lock toggle (small)
        float lockW = 56f;
        var lockRect = new Rect(rect.x, rect.y, lockW, rect.height);
        bool newLocked = GUI.Toggle(lockRect, isLocked[key], "Lock", "Button");
        isLocked[key] = newLocked;

        float x = lockRect.xMax + gap;

        // Copy copies what's currently displayed (raw or cached)
        var copyRect = new Rect(x, rect.y, buttonW, rect.height);
        if (GUI.Button(copyRect, "Copy"))
        {
            string toCopy =
                viewMode[key] != ViewMode.Raw && viewCache.TryGetValue(key, out var cached)
                    ? cached
                    : raw;

            EditorGUIUtility.systemCopyBuffer = toCopy;
        }
        x = copyRect.xMax + gap;

        var prettyRect = new Rect(x, rect.y, buttonW, rect.height);
        if (GUI.Button(prettyRect, "Pretty"))
        {
            viewMode[key] = ViewMode.Pretty;
            viewCache[key] = BuildPrettyView(raw);
            scrollPositions[key] = Vector2.zero;
            lastRawHash[key] = raw.GetHashCode();
        }
        x = prettyRect.xMax + gap;

        var minifyRect = new Rect(x, rect.y, buttonW, rect.height);
        if (GUI.Button(minifyRect, "Minify"))
        {
            viewMode[key] = ViewMode.Minify;
            viewCache[key] = TryMinify(raw);
            scrollPositions[key] = Vector2.zero;
            lastRawHash[key] = raw.GetHashCode();
        }
        x = minifyRect.xMax + gap;

        var extractRect = new Rect(x, rect.y, buttonW + 32f, rect.height);
        if (GUI.Button(extractRect, "Extract Plan"))
        {
            viewMode[key] = ViewMode.ExtractedPlan;
            viewCache[key] = BuildExtractedPlanView(raw);
            scrollPositions[key] = Vector2.zero;
            lastRawHash[key] = raw.GetHashCode();
        }
        x = extractRect.xMax + gap;

        var rawRect = new Rect(x, rect.y, buttonW, rect.height);
        if (GUI.Button(rawRect, "Raw"))
        {
            viewMode[key] = ViewMode.Raw;
            viewCache.Remove(key);
            scrollPositions[key] = Vector2.zero;
            lastRawHash[key] = raw.GetHashCode();
        }
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

    private static string BuildPrettyView(string json)
    {
        var json_normalized = NormalizeEscapes(json);
        try
        {
            var token = JToken.Parse(json_normalized);

            NormalizeStringsForDisplay(token);

            return token.ToString(Formatting.Indented);
        }
        catch
        {
            // Fallback: at least unescape common sequences for readability
            return json_normalized;
        }
    }

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
        s = s.Replace(",", ",\n");
        //s = s.Replace(",\"", ",\n\"");
        //s = s.Replace("\",\"", "\",\n\"");
        s = s.Replace("\\\\\\\"", "\"");
        s = s.Replace("\\\"", "\"");
        //s = s.Replace("\"", "\"");
        // Strip markdown code fences if the model wrapped JSON
        s = StripCodeFencesIfPresent(s);

        // Normalize line endings
        s = s.Replace("\r\n", "\n").Replace("\r", "\n");

        return s;
    }

    private static void NormalizeStringsForDisplay(JToken token)
    {
        switch (token.Type)
        {
            case JTokenType.Object:
                foreach (var p in ((JObject)token).Properties())
                    NormalizeStringsForDisplay(p.Value);
                break;

            case JTokenType.Array:
                foreach (var i in (JArray)token)
                    NormalizeStringsForDisplay(i);
                break;

            case JTokenType.String:
            {
                string s = token.Value<string>() ?? "";
                s = NormalizeEscapes(s);
                ((JValue)token).Value = s;
                break;
            }
        }
    }

    private static string NormalizeEscapes(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        // Convert escaped sequences to real characters (display only!)
        //s = s.Replace("\\r\\n", "\n");
        //s = s.Replace("\\n", "\n");
        //s = s.Replace("\\t", "\t");
        //s = s.Replace("\\\"", "\"");
        s = s.Replace("\\\n", "\n");
        s = s.Replace(",", ",\n");
        //s = s.Replace("\",\"", ",\n");
        s = s.Replace("\\r\\n", "\n");
        s = s.Replace("\\n", "\n");
        s = s.Replace("\\t", "\t");
        s = s.Replace("\\\\\\\"", "\"");
        s = s.Replace("\\\"", "\"");

        // Strip markdown fences if present
        s = StripCodeFencesIfPresent(s);

        // Normalize line endings for Unity
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

    private static string BuildExtractedPlanView(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "(empty)";

        if (!TryExtractInnerText(raw, out string innerText, out string reason))
            return $"(extract failed) {reason}";

        innerText = StripCodeFencesIfPresent(innerText).Trim();

        try
        {
            // Parse the extracted inner text as JSON
            var token = JToken.Parse(innerText);

            // Recursively convert JSON-strings into real objects (toolDefinitionsJson, responseSchemaJson, etc.)
            token = ExpandEmbeddedJsonStrings(token, maxDepth: 4);

            // Display niceties: convert \n sequences *inside strings* to real newlines (optional)
            NormalizeStringsForDisplay(token);

            return token.ToString(Formatting.Indented);
        }
        catch (Exception ex)
        {
            return $"(extracted text is not valid JSON: {ex.Message})\n\n{innerText}";
        }
    }

    private static bool TryExtractInnerText(string raw, out string innerText, out string reason)
    {
        innerText = "";
        reason = "";

        try
        {
            var root = JToken.Parse(raw);

            // Case A: OpenAI Responses API: output[].content[].type == "output_text" -> .text
            var output = root["output"] as JArray;
            if (output != null)
            {
                foreach (var item in output)
                {
                    var content = item?["content"] as JArray;
                    if (content == null) continue;

                    foreach (var c in content)
                    {
                        string? type = c?["type"]?.Value<string>();
                        if (!string.Equals(type, "output_text", StringComparison.Ordinal)) continue;

                        string? text = c?["text"]?.Value<string>();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            innerText = text!;
                            return true;
                        }
                    }
                }
            }

            // Case B: Gemini: candidates[0].content.parts[0].text
            var candidates = root["candidates"] as JArray;
            if (candidates != null && candidates.Count > 0)
            {
                var parts = candidates[0]?["content"]?["parts"] as JArray;
                if (parts != null && parts.Count > 0)
                {
                    string? text = parts[0]?["text"]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        innerText = text!;
                        return true;
                    }
                }
            }

            // Case C: “chat style” wrapper: content[] entries with type/output_text and text
            var content2 = root["content"] as JArray;
            if (content2 != null)
            {
                foreach (var c in content2)
                {
                    string? type = c?["type"]?.Value<string>();
                    if (!string.Equals(type, "output_text", StringComparison.Ordinal)) continue;

                    string? text = c?["text"]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        innerText = text!;
                        return true;
                    }
                }
            }

            // Case D: raw itself might already be the plan
            if (root["schema"]?.Value<string>() == "PlanResponseV1")
            {
                innerText = raw;
                return true;
            }

            reason = "No known wrapper shape (OpenAI output_text / Gemini candidates.parts / content.output_text / direct PlanResponseV1).";
            return false;
        }
        catch (Exception ex)
        {
            reason = $"Outer JSON parse failed: {ex.Message}";
            return false;
        }
    }

    private static JToken ExpandEmbeddedJsonStrings(JToken token, int maxDepth, int depth = 0)
    {
        if (depth >= maxDepth)
            return token;

        switch (token.Type)
        {
            case JTokenType.Object:
            {
                var obj = (JObject)token;
                foreach (var prop in obj.Properties())
                {
                    prop.Value = ExpandEmbeddedJsonStrings(prop.Value, maxDepth, depth);

                    // After recursion, also attempt to expand if the value is a JSON string
                    if (prop.Value.Type == JTokenType.String)
                    {
                        var s = prop.Value.Value<string>() ?? "";
                        if (TryParseJsonString(s, out var parsed))
                            prop.Value = ExpandEmbeddedJsonStrings(parsed, maxDepth, depth + 1);
                    }
                }
                return obj;
            }

            case JTokenType.Array:
            {
                var arr = (JArray)token;
                for (int i = 0; i < arr.Count; i++)
                {
                    arr[i] = ExpandEmbeddedJsonStrings(arr[i], maxDepth, depth);

                    if (arr[i].Type == JTokenType.String)
                    {
                        var s = arr[i]!.Value<string>() ?? "";
                        if (TryParseJsonString(s, out var parsed))
                            arr[i] = ExpandEmbeddedJsonStrings(parsed, maxDepth, depth + 1);
                    }
                }
                return arr;
            }

            default:
                return token;
        }
    }

    private static bool TryParseJsonString(string s, out JToken parsed)
    {
        parsed = JValue.CreateNull();

        if (string.IsNullOrWhiteSpace(s))
            return false;

        // Strip fences & normalize line endings ONLY (do NOT unescape quotes here!)
        s = StripCodeFencesIfPresent(s).Trim();
        s = s.Replace("\r\n", "\n").Replace("\r", "\n");

        // Quick heuristic: must start/end like JSON
        char first = s.Length > 0 ? s[0] : '\0';
        char last = s.Length > 0 ? s[^1] : '\0';
        bool looksLikeJson = (first == '{' && last == '}') || (first == '[' && last == ']');
        if (!looksLikeJson)
            return false;

        try
        {
            parsed = JToken.Parse(s);
            return true;
        }
        catch
        {
            return false;
        }
    }
}