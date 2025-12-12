using System;
using System.Collections.Generic;
using System.Text;

namespace DogGame.Language
{
    /// <summary>
    /// Global pack dictionary (not per-dog).
    /// Fast lookup using HashSet.
    /// </summary>
    public static class DogSpeechDictionary
    {
        // Case-insensitive lookup, so "Come" and "come" match.
        private static readonly HashSet<string> knownWords =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "come",
                "good",
                "bad",
                "dog",
                // add starter dog name(s) if you want:
                // "fido",
            };

        public static string substituteUnknown = "..."; // "[blah]";
        public static bool combineSubstitutions = true;

        public static bool IsKnown(string word) => knownWords.Contains(word);

        public static void Teach(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return;
            knownWords.Add(word.Trim());
        }

        /// <summary>
        /// Translates human text into dog-perceived text by replacing unknown words with "blah".
        /// Preserves punctuation attached to words (e.g., "dog!" stays "dog!").
        /// </summary>
        public static string TranslateHumanToDog(string humanText)
        {
            if (string.IsNullOrWhiteSpace(humanText))
                return string.Empty;

            // Simple whitespace tokenization.
            // Later you can improve this to handle quotes, em-dashes, etc.
            string[] tokens = humanText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var builder = new StringBuilder(humanText.Length + 16);

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];

                // Separate leading/trailing punctuation from the core word.
                SplitToken(token, out string leadingPunct, out string coreWord, out string trailingPunct);

                if (coreWord.Length == 0)
                {
                    // Token was only punctuation.
                    builder.Append(token);
                }
                else
                {
                    string translatedCore = IsKnown(coreWord) ? coreWord : substituteUnknown;
                    builder.Append(leadingPunct);
                    builder.Append(translatedCore);
                    builder.Append(trailingPunct);
                }

                if (i < tokens.Length - 1)
                    builder.Append(' ');
            }

            string final = builder.ToString();
            final = CombineRepeatedSubstitutions(final);
            return final;
        }

        private static void SplitToken(string token, out string leading, out string core, out string trailing)
        {
            int start = 0;
            int end = token.Length - 1;

            while (start <= end && char.IsPunctuation(token[start]))
                start++;

            while (end >= start && char.IsPunctuation(token[end]))
                end--;

            leading = (start > 0) ? token.Substring(0, start) : string.Empty;
            trailing = (end < token.Length - 1) ? token.Substring(end + 1) : string.Empty;
            core = (start <= end) ? token.Substring(start, end - start + 1) : string.Empty;
        }


        private static string CombineRepeatedSubstitutions(string s_input)
        {
            if (string.IsNullOrEmpty(s_input))
                return s_input;

            if (!combineSubstitutions)
                return s_input;

            // NOTE: defined elsewhere per your comment:
            // public static string substituteUnknown = "...";
            if (string.IsNullOrEmpty(substituteUnknown))
                return s_input;

            string token = substituteUnknown;

            // Fast single-pass collapse:
            // If we see token, we output it once and then skip any immediate repeats
            // (allowing whitespace between repeats).
            var builder = new StringBuilder(s_input.Length);

            int i = 0;
            bool lastOutputWasToken = false;

            while (i < s_input.Length)
            {
                // If token matches at position i
                if (MatchesAt(s_input, i, token))
                {
                    if (!lastOutputWasToken)
                    {
                        builder.Append(token);
                        lastOutputWasToken = true;
                    }

                    // Advance past this token
                    i += token.Length;

                    // Skip any whitespace + repeated tokens:  " ...   ...   ..."
                    while (true)
                    {
                        // Skip whitespace
                        while (i < s_input.Length && char.IsWhiteSpace(s_input[i]))
                            i++;

                        // If another token follows, skip it (don't output)
                        if (i < s_input.Length && MatchesAt(s_input, i, token))
                        {
                            i += token.Length;
                            continue;
                        }

                        break;
                    }

                    // Insert a single space if the next char exists and isn't whitespace/punctuation,
                    // to avoid smashing words together (optional but usually nicer).
                    if (i < s_input.Length && builder.Length > 0)
                    {
                        char next = s_input[i];
                        char prev = builder[builder.Length - 1];

                        bool prevIsSpace = char.IsWhiteSpace(prev);
                        bool nextIsSpace = char.IsWhiteSpace(next);

                        if (!prevIsSpace && !nextIsSpace)
                            builder.Append(' ');
                    }

                    continue;
                }

                // Normal char copy
                char c = s_input[i];
                builder.Append(c);

                // If we output something other than the token, clear the flag
                // (but keep it true if we're still “on” the token we just output).
                lastOutputWasToken = false;

                i++;
            }

            return builder.ToString();

            static bool MatchesAt(string text, int index, string pattern)
            {
                if (index + pattern.Length > text.Length)
                    return false;

                for (int j = 0; j < pattern.Length; j++)
                {
                    if (text[index + j] != pattern[j])
                        return false;
                }

                return true;
            }
        }
    }
}