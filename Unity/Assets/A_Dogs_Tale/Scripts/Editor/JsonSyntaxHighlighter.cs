#nullable enable
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public static class JsonSyntaxHighlighter
{
    // Simple tokenizer-style regex. Good enough for inspector preview.
    // Not a full JSON parser, but stable for well-formed JSON output.
    private static readonly Regex TokenRegex = new Regex(
        // groups:
        // 1: key (string followed by :)
        // 2: string
        // 3: number
        // 4: true/false/null
        "(\"(?:\\\\.|[^\"\\\\])*\"\\s*(?=:\\s*))" +                 // key
        "|(\"(?:\\\\.|[^\"\\\\])*?\")" +                           // string
        "|(-?\\b\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?\\b)" +          // number
        "|\\b(true|false|null)\\b",                                // keywords
        RegexOptions.Compiled);

    public static string ToRichText(string json, bool escapeRichText = true)
    {
        if (string.IsNullOrEmpty(json))
            return "<color=#888888>(empty)</color>";

        // Escape < > & so JSON can't break rich text.
        string safe = escapeRichText ? EscapeRich(json) : json;

        var sb = new StringBuilder(safe.Length + 64);

        int last = 0;
        foreach (Match m in TokenRegex.Matches(safe))
        {
            if (m.Index > last)
                sb.Append(safe, last, m.Index - last);

            if (m.Groups[1].Success) // key
            {
                sb.Append(ColorWrap(m.Value, "#7AA2F7")); // blue-ish
            }
            else if (m.Groups[2].Success) // string
            {
                sb.Append(ColorWrap(m.Value, "#9ECE6A")); // green-ish
            }
            else if (m.Groups[3].Success) // number
            {
                sb.Append(ColorWrap(m.Value, "#FF9E64")); // orange-ish
            }
            else if (m.Groups[4].Success) // true/false/null
            {
                sb.Append(ColorWrap(m.Value, "#BB9AF7")); // purple-ish
            }
            else
            {
                sb.Append(m.Value);
            }

            last = m.Index + m.Length;
        }

        if (last < safe.Length)
            sb.Append(safe, last, safe.Length - last);

        return sb.ToString();
    }

    private static string ColorWrap(string text, string hexColor)
        => $"<color={hexColor}>{text}</color>";

    private static string EscapeRich(string text)
    {
        // Order matters
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}