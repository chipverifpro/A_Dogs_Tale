using UnityEngine;
using DogGame.Language;
using System.Collections.Generic;
using System.Text;
using System;

namespace DogGame.Comms
{
    /// <summary>
    /// Central service for speech/events. Today: just show translated speech on bottom banner.
    /// Tomorrow: distance/obstructions, multiple listeners, UI bubbles, logs, etc.
    /// </summary>
    public sealed class SpeechService
    {
        public Directory dir;

        private readonly BottomBanner bottomBanner;

        public SpeechService(BottomBanner bottomBanner)
        {
            this.bottomBanner = bottomBanner;
        }

        // Configure colors once
        private static readonly Color PositiveColor = Color.green;
        private static readonly Color NeutralColor  = Color.white;
        private static readonly Color NegativeColor = Color.red;

        // Optional: how to style untranslated stage directions
        private const string UntranslatedOpen  = "<i><color=#A0A0A0>";
        private const string UntranslatedClose = "</color></i>";

        /// <summary>
        /// speaker: who said it (can be null for narration/system)
        /// listener: intended target (can be null). Not necessarily the only one who hears it.
        /// message: human text
        /// </summary>
        public void Speak(WorldObject speaker, WorldObject listener, string message)
        {
            if (bottomBanner == null)
            {
                Debug.LogWarning("SpeechService.Speak called but bottomBanner is null.");
                return;
            }

            List<string> learnedWords;

            string richText = ProcessForDogDisplay(
                originalText: message,
                deleteLearnwordFromSource: true,
                teachWordCallback: (word) => DogSpeechDictionary.Teach(word),
                translateHumanToDog: (word) => DogSpeechDictionary.TranslateHumanToDog(word),
                combineRepeatedSubstitutionsOrNull: (text) => DogSpeechDictionary.CombineRepeatedSubstitutions(text),
                out learnedWords
            );

            bottomBanner.DisplayRich(richText);
            Debug.Log($"Speech {message} -> {richText}");
            if (learnedWords != null && learnedWords.Count > 0)
            {
                // Later: play a sound and show a floating cloud UI.
                // For now, at least log:
                Debug.Log("Learned words: " + string.Join(", ", learnedWords));
            }
        }

        /// <summary>
        /// Processes pseudo-tags and returns TMP-rich text for display.
        /// Also returns learned words that were extracted.
        /// </summary>
        public static string ProcessMarkers(
            string originalText,
            bool deleteFromSource,
            Func<string,bool> teachWordCallback,
            out List<string> learnedWords)
        {
            learnedWords = new List<string>();
            if (string.IsNullOrEmpty(originalText))
                return string.Empty;

            // 1) Extract learnword tags
            string textAfterLearnExtraction;
            var learnList = GrabMarkupContent(
                originalText,
                "learnword",
                deleteFromSource,
                out textAfterLearnExtraction);

            // Teach them (trim/sanitize)
            foreach (string rawWord in learnList)
            {
                string w = (rawWord ?? "").Trim();
                if (w.Length == 0) continue;

                //teachWordCallback?.Invoke(w);

                bool wasNew = (teachWordCallback != null) && teachWordCallback(w);
                if (wasNew)
                    learnedWords.Add(w);
            }

            // 2) Convert pseudo tone tags to TMP color tags
            string tmpText = textAfterLearnExtraction;

            tmpText = ReplaceToneTag(tmpText, "positive", PositiveColor);
            tmpText = ReplaceToneTag(tmpText, "neutral",  NeutralColor);
            tmpText = ReplaceToneTag(tmpText, "negative", NegativeColor);

            // 3) Convert untranslated to TMP styling
            tmpText = ReplaceSimpleTag(tmpText, "untranslated", UntranslatedOpen, UntranslatedClose);

            return tmpText;
        }
        

        /// <summary>
        /// Extract all occurrences of <tagName>...</tagName>.
        /// Returns the inner contents in the order found.
        ///
        /// If deleteFromSource is true, the tags and their contents are removed from the returned cleanedText.
        /// If false, cleanedText == originalText.
        ///
        /// NOTE: This is a simple non-nested tag parser (fine for your pseudo-tags).
        /// </summary>
        public static List<string> GrabMarkupContent(
            string originalText,
            string tagName,
            bool deleteFromSource,
            out string cleanedText)
        {
            cleanedText = originalText ?? string.Empty;

            var results = new List<string>();
            if (string.IsNullOrEmpty(originalText) || string.IsNullOrEmpty(tagName))
                return results;

            string openTag = "<" + tagName + ">";
            string closeTag = "</" + tagName + ">";

            int searchIndex = 0;

            // If we are not deleting, we can avoid building cleanedText.
            StringBuilder cleanedBuilder = deleteFromSource ? new StringBuilder(originalText.Length) : null;
            int lastCopyIndex = 0;

            while (searchIndex < originalText.Length)
            {
                int openIndex = originalText.IndexOf(openTag, searchIndex, System.StringComparison.OrdinalIgnoreCase);
                if (openIndex < 0)
                    break;

                int contentStart = openIndex + openTag.Length;
                int closeIndex = originalText.IndexOf(closeTag, contentStart, System.StringComparison.OrdinalIgnoreCase);
                if (closeIndex < 0)
                    break; // malformed; no close tag

                // Capture content
                string content = originalText.Substring(contentStart, closeIndex - contentStart);
                results.Add(content);

                if (deleteFromSource)
                {
                    // Copy text from lastCopyIndex up to the open tag
                    cleanedBuilder.Append(originalText, lastCopyIndex, openIndex - lastCopyIndex);

                    // Skip the entire tag block
                    int afterClose = closeIndex + closeTag.Length;
                    lastCopyIndex = afterClose;
                    searchIndex = afterClose;
                }
                else
                {
                    searchIndex = closeIndex + closeTag.Length;
                }
            }

            if (deleteFromSource)
            {
                // Copy the tail after the last removed tag
                if (lastCopyIndex < originalText.Length)
                    cleanedBuilder.Append(originalText, lastCopyIndex, originalText.Length - lastCopyIndex);

                cleanedText = cleanedBuilder.ToString();
            }

            return results;
        }


        /// <summary>
        /// End-to-end processing for HUMAN->DOG display:
        /// - Extract learnword tags (teach words)
        /// - Extract untranslated blocks (protect from translation)
        /// - Translate remaining words (unknown => substituteUnknown)
        /// - Reinsert untranslated blocks with styling
        /// - Convert tone tags to TMP colors
        /// </summary>
        public static string ProcessForDogDisplay(
            string originalText,
            bool deleteLearnwordFromSource,
            Func<string, bool> teachWordCallback,
            Func<string, string> translateHumanToDog,
            Func<string, string> combineRepeatedSubstitutionsOrNull = null)
        {
            if (string.IsNullOrEmpty(originalText))
                return string.Empty;

            // 1) Teach learnword(s)
            string afterLearn;
            List<string> learned = GrabMarkupContent(
                originalText,
                "learnword",
                deleteLearnwordFromSource,
                out afterLearn);

            foreach (string raw in learned)
            {
                string w = (raw ?? "").Trim();
                if (w.Length == 0) continue;
                teachWordCallback?.Invoke(w);
            }

            // If you want to *unwrap* learnword (keep the word visible but remove the tag),
            // do this instead of deleting the whole tag block:
            // afterLearn = UnwrapSimpleTag(afterLearn, "learnword");  // (function shown below)

            // 2) Extract untranslated blocks and replace them with placeholders
            var untranslatedBlocks = new List<string>();

            string protectedText = ReplaceTagBlocksWithPlaceholders(
                afterLearn,
                "untranslated",
                untranslatedBlocks);

            // 3) Translate the protected text (placeholders should survive unchanged)
            string translated = translateHumanToDog != null
                ? translateHumanToDog(protectedText)
                : protectedText;

            // Optionally collapse repeats (your "... ... ..." combiner)
            if (combineRepeatedSubstitutionsOrNull != null)
                translated = combineRepeatedSubstitutionsOrNull(translated);

            // 4) Reinsert untranslated blocks (styled, and NOT translated)
            string withUntranslated = ReinsertPlaceholders(
                translated,
                untranslatedBlocks,
                content => $"{UntranslatedOpen}{EscapeTMP(content)}{UntranslatedClose}");

            // 5) Convert tone tags to TMP color tags (do this after translation)
            withUntranslated = ReplaceToneTag(withUntranslated, "positive", PositiveColor);
            withUntranslated = ReplaceToneTag(withUntranslated, "neutral",  NeutralColor);
            withUntranslated = ReplaceToneTag(withUntranslated, "negative", NegativeColor);

            return withUntranslated;
        }

        // -------------------- helpers --------------------

        private static string ReplaceTagBlocksWithPlaceholders(string input, string tagName, List<string> contents)
        {
            if (string.IsNullOrEmpty(input)) return input;

            string openTag = "<" + tagName + ">";
            string closeTag = "</" + tagName + ">";

            int searchIndex = 0;
            int lastCopyIndex = 0;
            var builder = new System.Text.StringBuilder(input.Length);

            while (searchIndex < input.Length)
            {
                int openIndex = input.IndexOf(openTag, searchIndex, System.StringComparison.OrdinalIgnoreCase);
                if (openIndex < 0) break;

                int contentStart = openIndex + openTag.Length;
                int closeIndex = input.IndexOf(closeTag, contentStart, System.StringComparison.OrdinalIgnoreCase);
                if (closeIndex < 0) break;

                // Copy text before tag
                builder.Append(input, lastCopyIndex, openIndex - lastCopyIndex);

                // Capture the content and insert placeholder
                string content = input.Substring(contentStart, closeIndex - contentStart);
                int placeholderIndex = contents.Count;
                contents.Add(content);

                // Private-use Unicode placeholder, unlikely to appear in normal text
                // Format: \uE000{index}\uE001
                builder.Append('\uE000');
                builder.Append(placeholderIndex.ToString());
                builder.Append('\uE001');

                int afterClose = closeIndex + closeTag.Length;
                lastCopyIndex = afterClose;
                searchIndex = afterClose;
            }

            // Tail
            if (lastCopyIndex < input.Length)
                builder.Append(input, lastCopyIndex, input.Length - lastCopyIndex);

            return builder.ToString();
        }

        private static string ReinsertPlaceholders(
            string input,
            List<string> contents,
            Func<string, string> formatContent)
        {
            if (string.IsNullOrEmpty(input) || contents == null || contents.Count == 0)
                return input;

            var builder = new System.Text.StringBuilder(input.Length + 32);

            int i = 0;
            while (i < input.Length)
            {
                if (input[i] == '\uE000')
                {
                    int j = i + 1;
                    int numberStart = j;

                    while (j < input.Length && char.IsDigit(input[j]))
                        j++;

                    if (j < input.Length && input[j] == '\uE001')
                    {
                        string numberText = input.Substring(numberStart, j - numberStart);
                        if (int.TryParse(numberText, out int idx) && idx >= 0 && idx < contents.Count)
                        {
                            builder.Append(formatContent != null ? formatContent(contents[idx]) : contents[idx]);
                            i = j + 1;
                            continue;
                        }
                    }
                }

                builder.Append(input[i]);
                i++;
            }

            return builder.ToString();
        }

        private static string ReplaceToneTag(string input, string tagName, Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGB(color);
            return ReplaceSimpleTag(input, tagName, $"<color=#{hex}>", "</color>");
        }

        private static string ReplaceSimpleTag(string input, string tagName, string openReplacement, string closeReplacement)
        {
            if (string.IsNullOrEmpty(input)) return input;

            input = ReplaceInsensitive(input, "<" + tagName + ">", openReplacement);
            input = ReplaceInsensitive(input, "</" + tagName + ">", closeReplacement);
            return input;
        }

        private static string ReplaceInsensitive(string source, string find, string replace)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(find))
                return source;

            int index = 0;
            while (true)
            {
                int found = source.IndexOf(find, index, System.StringComparison.OrdinalIgnoreCase);
                if (found < 0) break;

                source = source.Substring(0, found) + replace + source.Substring(found + find.Length);
                index = found + replace.Length;
            }
            return source;
        }

        private static string EscapeTMP(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return input.Replace("<", "&lt;").Replace(">", "&gt;");
        }

        // If you want to keep the learnword visible but remove the tag wrapper:
        // private static string UnwrapSimpleTag(string input, string tagName)
        // {
        //     if (string.IsNullOrEmpty(input)) return input;
        //     input = ReplaceInsensitive(input, "<" + tagName + ">", "");
        //     input = ReplaceInsensitive(input, "</" + tagName + ">", "");
        //     return input;
        // }


        private static readonly Color LearnedColor = new Color(1.0f, 0.84f, 0.0f); // gold-ish

        public static string ProcessForDogDisplay(
            string originalText,
            bool deleteLearnwordFromSource,
            Func<string, bool> teachWordCallback,
            Func<string, string> translateHumanToDog,
            Func<string, string> combineRepeatedSubstitutionsOrNull,
            out List<string> learnedWords)
        {
            learnedWords = new List<string>();
            if (string.IsNullOrEmpty(originalText))
                return string.Empty;

            // 1) Extract learnword(s)
            string afterLearn;
            List<string> learned = GrabMarkupContent(
                originalText,
                "learnword",
                deleteLearnwordFromSource,
                out afterLearn);

            foreach (string raw in learned)
            {
                string w = (raw ?? "").Trim();
                if (w.Length == 0) continue;

                //teachWordCallback?.Invoke(w);

                bool wasNew = (teachWordCallback != null) && teachWordCallback(w);
                if (wasNew)
                    learnedWords.Add(w);
            }

            // 2) Extract untranslated blocks -> placeholders (same as before)
            var untranslatedBlocks = new List<string>();
            string protectedText = ReplaceTagBlocksWithPlaceholders(afterLearn, "untranslated", untranslatedBlocks);

            // 3) Translate
            string translated = translateHumanToDog != null ? translateHumanToDog(protectedText) : protectedText;

            if (combineRepeatedSubstitutionsOrNull != null)
                translated = combineRepeatedSubstitutionsOrNull(translated);

            // 4) Reinsert untranslated (same as before)
            string withUntranslated = ReinsertPlaceholders(
                translated,
                untranslatedBlocks,
                content => $"{UntranslatedOpen}{EscapeTMP(content)}{UntranslatedClose}");

            // 5) Highlight newly learned words in gold (TMP tags)
            if (learnedWords.Count > 0)
                withUntranslated = HighlightWords(withUntranslated, learnedWords, LearnedColor);

            // 6) Tone tags -> TMP (if you’re using them)
            withUntranslated = ReplaceToneTag(withUntranslated, "positive", PositiveColor);
            withUntranslated = ReplaceToneTag(withUntranslated, "neutral", NeutralColor);
            withUntranslated = ReplaceToneTag(withUntranslated, "negative", NegativeColor);

            return withUntranslated;
        }

        private static string HighlightWords(string input, List<string> wordsToHighlight, Color color)
        {
            if (string.IsNullOrEmpty(input) || wordsToHighlight == null || wordsToHighlight.Count == 0)
                return input;

            string hex = ColorUtility.ToHtmlStringRGB(color);

            // We'll scan through the string and only replace whole words that are NOT inside TMP tags.
            // This avoids coloring pieces of "<color=...>" or other tags.
            var sb = new System.Text.StringBuilder(input.Length + 16);

            bool insideTag = false;
            int i = 0;

            while (i < input.Length)
            {
                char c = input[i];

                if (c == '<') insideTag = true;
                if (!insideTag && char.IsLetterOrDigit(c))
                {
                    int start = i;
                    int end = i + 1;
                    while (end < input.Length && char.IsLetterOrDigit(input[end]))
                        end++;

                    string token = input.Substring(start, end - start);

                    // Case-insensitive match against learned words
                    bool match = false;
                    for (int w = 0; w < wordsToHighlight.Count; w++)
                    {
                        if (string.Equals(token, wordsToHighlight[w], StringComparison.OrdinalIgnoreCase))
                        {
                            match = true;
                            break;
                        }
                    }

                    if (match)
                        sb.Append($"<color=#{hex}><b>{EscapeTMP(token)}</b></color>");
                    else
                        sb.Append(token);

                    i = end;
                    continue;
                }

                sb.Append(c);

                if (c == '>') insideTag = false;

                i++;
            }

            return sb.ToString();
        }

    }
}