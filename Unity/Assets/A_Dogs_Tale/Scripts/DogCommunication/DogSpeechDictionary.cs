using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Text.RegularExpressions;

namespace DogGame.Language
{
    /// <summary>
    /// Global pack dictionary (not per-dog).
    /// Fast lookup using HashSet.
    /// </summary>
    public class DogSpeechDictionary : MonoBehaviour
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

        // Parameters...

        public Dir dir;

        private BottomBanner bottomBanner;

        void Awake()
        {
            if (dir==null)
            {
                Debug.LogError("Dog Speech Dictionary does not have dir define.");
                return;
            }
            if (bottomBanner == null)
            {
                bottomBanner = dir.bottomBanner;
            }  
        }

        public string substituteUnknown = "..."; // "[blah]";
        public bool combineSubstitutions = false;

        // Configure colors
        // How to style untranslated stage directions
        private const string TAG_UntranslatedOpen   = "<i><color=#A0A0A0>";
        private const string TAG_UntranslatedClose  = "</color></i>";
        private const string TAG_Neutral            = "<color=white>";
        private const string TAG_Positive           = "<color=green>";
        private const string TAG_Negative           = "<color=red>";
        private const string TAG_Gold               = "<color=#FFD700>";
        private const string TAG_ColorClose         = "</color>";

        List<string> justLearnedWords;

        public bool IsKnown(string word) => knownWords.Contains(word);


        public bool Teach(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return false;
            return knownWords.Add(word.Trim());
        }

        /// <summary>
        /// speaker: who said it (can be null for narration/system)
        /// listener: intended target (can be null). Not necessarily the only one who hears it.
        /// message: human text
        /// </summary>
        public void Speak(WorldObject speaker, WorldObject listener, string message)
        {
            string richText = TranslateHumanToDogSimple(message);
            BottomBanner.LogAgentRichMessage(speaker, BannerSense.None, BannerLevel.None, richText);
            Debug.Log($"Speech {message} -> {richText}");
            if (justLearnedWords != null && justLearnedWords.Count > 0)
            {
                // Later: play a sound and show a floating cloud UI.
                // For now, at least log:
                Debug.Log("Just learned words: " + string.Join(", ", justLearnedWords));
            }
        }

        /// <summary>
        /// Translates human text into dog-perceived text by replacing unknown words with "blah".
        /// Preserves punctuation attached to words (e.g., "dog!" stays "dog!").
        /// </summary>
        public string TranslateHumanToDogSimple(string humanText)
        {
            bool is_tag=false;          // this token is part of a tag
            bool end_tag=false;         // has >
            bool mid_untranslated=false; // between <untranslated> and </untranslated>
            bool mid_learn=false;        // between <learn> and </learn>
            string change_tag="";       // replacement tag, replaces built_tag at end_tag
            bool strip_tag=false;       // don't keep current tag in output
            string built_tag="";        // built copy of the current tag.  Used at end_tag

            // -----------------------
            // Before parsing the whole string, extract all the known words first so we can colorize them inline.
            justLearnedWords = new();
            
            string pattern = @"<learn>\s*(.*?)\s*</learn>";
            MatchCollection matches = Regex.Matches(humanText, pattern, RegexOptions.Singleline);
            foreach (Match match in matches)
            {
                string learnedText = match.Groups[1].Value;
                if (Teach(learnedText))
                    justLearnedWords.Add(learnedText);
            }
            // ----------------------
            if (string.IsNullOrWhiteSpace(humanText))
                return string.Empty;

            // first, make sure there is whitespace around tags so they don't merge with text
            string temp1 = humanText.Replace("<", " <", StringComparison.OrdinalIgnoreCase);
            humanText = temp1.Replace(">", "> ", StringComparison.OrdinalIgnoreCase);
            
            // Simple whitespace tokenization.
            // Later you can improve this to handle quotes, em-dashes, etc.
            string[] tokens = humanText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            // start processing humanText, one word at a time
            string token = "";
            var builder = new StringBuilder(humanText.Length + 16);

            for (int i = 0; i < tokens.Length; i++)
            {
                token = tokens[i];

                // ----------------
                // Separate leading/trailing punctuation from the core word.
                SplitToken(token, out string leadingPunct, out string coreWord, out string trailingPunct);

                // ----------------
                // is this a complete tag, or even a start or end of tag?
                if (token[0]=='<') 
                    { 
                        is_tag=true; strip_tag=false; built_tag="";
                        //if (token[1]=='/') close_tag = true;    // close_tag is unused
                    }
                
                if (token[token.Length-1]=='>') 
                    { end_tag = true; }


                // ----------------
                // Parse special tag contents
                if (is_tag)
                {
                    if (token.Equals("<untranslated>",StringComparison.OrdinalIgnoreCase))
                        { mid_untranslated=true;        change_tag=TAG_UntranslatedOpen; }
                    if (token.Equals("</untranslated>",StringComparison.OrdinalIgnoreCase))
                        { mid_untranslated=false;       change_tag=TAG_UntranslatedClose; }
                    if (token.Equals("<+>")) {          change_tag=TAG_Positive; }
                    if (token.Equals("<->")) {          change_tag=TAG_Negative; }
                    if (token.Equals("<.>")) {          change_tag=TAG_Neutral; }
                    if (token.Equals("</+>")) {         change_tag=TAG_ColorClose; }
                    if (token.Equals("</->")) {         change_tag=TAG_ColorClose; }
                    if (token.Equals("</.>")) {         change_tag=TAG_ColorClose; }
                    if (token.Equals("<learn>")) { mid_learn=true; strip_tag=true; }
                    if (token.Equals("</learn>")) { mid_learn=false; strip_tag=true; }

                    built_tag += token;
                    
                    // ----------------
                    // Act on the tag if we have seen the end of it.
                    // process the tag: things to do when current tag contains >
                    if (end_tag)
                    {
                        if (!string.IsNullOrEmpty(change_tag)) 
                            { built_tag=change_tag; change_tag=""; strip_tag=false; }
                        if (strip_tag) { built_tag=""; strip_tag=false; }
                        if (!string.IsNullOrEmpty(built_tag))
                            { 
                                builder.Append(built_tag);
                                if (i < tokens.Length - 1)
                                    builder.Append(' ');
                            }
                        end_tag=false;
                        built_tag="";
                        is_tag=false; 
                        continue;
                    } // end end_tag
                    continue;
                } // end is_tag

                // ----------------
                // process learn word
                if (mid_learn)
                {
                    Debug.Log($":: Learn word :: {token}");
                    //Teach(token); // now already being done in PreLearnWords
                    continue;
                }
                
                // ----------------
                // keep untranslated word
                if (mid_untranslated)  // don't translate this word
                {
                    // don't translate, just pass it through.
                    builder.Append(token);
                    if (i < tokens.Length - 1)
                        builder.Append(' ');
                    continue;
                } // end untranslated

                // ----------------
                // translate this word
                if (coreWord.Length == 0)
                {
                    // Token was only punctuation.
                    builder.Append(token);
                }
                else
                {
                    if (justLearnedWords.Contains(token))
                    {
                        builder.Append(TAG_Gold);
                    }
                    string translatedCore = IsKnown(coreWord) ? coreWord : substituteUnknown;
                    builder.Append(leadingPunct);
                    builder.Append(translatedCore);
                    builder.Append(trailingPunct);
                    if (justLearnedWords.Contains(token))
                    {
                        builder.Append(TAG_ColorClose);
                    }
                }
                if (i < tokens.Length - 1)
                    builder.Append(' ');
                continue;
                // end translated token
            }
            // TODO close any open tags (color)

            string final = builder.ToString();
            final = CombineRepeatedSubstitutions(final);
            return final;
        }

        // Splits a "puctuation/word/puctuation" into three parts.
        // This allows the core word to be translated
        // while being able to add back on the punctuations when done.
        private void SplitToken(string token, out string leading, out string core, out string trailing)
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

        // merges sequential fillers into one: "I am a dog" > "... ... ... dog" > "... dog"
        private string CombineRepeatedSubstitutions(string s_input)
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
        }

        // used in CombineRepeatedSubstitutions()
        private bool MatchesAt(string text, int index, string pattern)
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
