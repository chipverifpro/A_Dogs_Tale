#nullable enable
using System;
using Newtonsoft.Json.Linq;

namespace DogGame.LLM
{
    public static class LLMResponseSanitizer
    {
        /// <summary>
        /// Attempts to extract a single JSON object string from messy model output:
        /// - strips ```json fences
        /// - finds first {...} block
        /// - if the whole thing is a wrapper JSON, tries to extract output_text.text
        /// </summary>
        public static bool TryExtractJsonObject(string? rawText, out string jsonObject, out string error)
        {
            jsonObject = "";
            error = "";

            if (string.IsNullOrWhiteSpace(rawText))
            {
                error = "Empty response text.";
                return false;
            }

            string text = rawText.Trim();

            // 1) If it starts with a markdown fence, strip it.
            // Handles ```json ... ``` or ``` ... ```
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                // Remove leading fence line
                int firstNewline = text.IndexOf('\n');
                if (firstNewline >= 0)
                    text = text.Substring(firstNewline + 1);

                // Remove trailing fence
                int lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (lastFence >= 0)
                    text = text.Substring(0, lastFence);

                text = text.Trim();
            }

            // 2) Sometimes models prepend "json" or "JSON:" lines — harmless, we just locate the first '{'
            int firstBrace = text.IndexOf('{');
            int lastBrace = text.LastIndexOf('}');

            // 3) If it doesn't look like a JSON object at all, it might be a wrapper JSON.
            // Try parsing and pulling out output[0].content[0].text (OpenAI Responses style),
            // or candidates[0].content.parts[0].text (Gemini style).
            if (firstBrace < 0 || lastBrace <= firstBrace)
            {
                if (TryExtractEmbeddedTextField(text, out string embedded))
                {
                    text = embedded.Trim();

                    // strip fences again just in case
                    if (text.StartsWith("```", StringComparison.Ordinal))
                    {
                        int nl = text.IndexOf('\n');
                        if (nl >= 0) text = text.Substring(nl + 1);
                        int lf = text.LastIndexOf("```", StringComparison.Ordinal);
                        if (lf >= 0) text = text.Substring(0, lf);
                        text = text.Trim();
                    }

                    firstBrace = text.IndexOf('{');
                    lastBrace = text.LastIndexOf('}');
                }
            }

            if (firstBrace < 0 || lastBrace <= firstBrace)
            {
                error = $"Could not locate JSON object braces in response. First80='{TrimForLog(text, 80)}'";
                return false;
            }

            jsonObject = text.Substring(firstBrace, (lastBrace - firstBrace) + 1).Trim();
            return true;
        }

        private static bool TryExtractEmbeddedTextField(string text, out string embeddedText)
        {
            embeddedText = "";

            try
            {
                var root = JObject.Parse(text);

                // OpenAI Responses-like: output[].content[].text where type==output_text
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

                            string? t = c?["text"]?.Value<string>();
                            if (!string.IsNullOrWhiteSpace(t))
                            {
                                embeddedText = t!;
                                return true;
                            }
                        }
                    }
                }

                // Gemini generateContent: candidates[0].content.parts[0].text
                var candidates = root["candidates"] as JArray;
                if (candidates != null && candidates.Count > 0)
                {
                    var parts = candidates[0]?["content"]?["parts"] as JArray;
                    if (parts != null && parts.Count > 0)
                    {
                        string? t = parts[0]?["text"]?.Value<string>();
                        if (!string.IsNullOrWhiteSpace(t))
                        {
                            embeddedText = t!;
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // not a wrapper JSON; ignore
            }

            return false;
        }

        private static string TrimForLog(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}