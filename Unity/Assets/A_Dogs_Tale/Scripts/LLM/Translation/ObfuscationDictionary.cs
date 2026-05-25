#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DogGame.LLM
{
    /// <summary>
    /// Deterministic word-level obfuscation for LLM experiments.
    /// Encoded words are four uppercase alphabetic characters.
    /// </summary>
    public sealed class ObfuscationDictionary
    {
        private const int CodeLength = 4;
        private const int AlphabetLength = 26;
        private static readonly Regex WordRegex = new Regex("[A-Za-z0-9_]+", RegexOptions.Compiled);

        private readonly Dictionary<string, List<EncodeEntry>> plainToCode;
        private readonly Dictionary<string, string> codeToPlain;
        private readonly string salt;

        public ObfuscationDictionary(string salt = "A_Dogs_Tale.ObfuscationDictionary")
        {
            this.salt = salt ?? "";
            plainToCode = new Dictionary<string, List<EncodeEntry>>(StringComparer.OrdinalIgnoreCase);
            codeToPlain = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public int Count => plainToCode.Count;

        public IReadOnlyDictionary<string, string> PlainToCode
        {
            get
            {
                var activeMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, List<EncodeEntry>> pair in plainToCode)
                {
                    for (int i = pair.Value.Count - 1; i >= 0; i--)
                    {
                        if (!pair.Value[i].IsValid)
                            continue;

                        activeMappings[pair.Key] = pair.Value[i].Code;
                        break;
                    }
                }

                return activeMappings;
            }
        }

        public IReadOnlyDictionary<string, string> CodeToPlain => codeToPlain;

        public string Encode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? "";

            return WordRegex.Replace(text, match => EncodeWord(match.Value));
        }

        public string Decode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? "";

            return WordRegex.Replace(text, match => DecodeWord(match.Value));
        }

        public string EncodeWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return word ?? "";

            string normalized = NormalizePlainWord(word);
            if (plainToCode.TryGetValue(normalized, out List<EncodeEntry>? existingEntries))
            {
                for (int i = existingEntries.Count - 1; i >= 0; i--)
                {
                    if (existingEntries[i].IsValid)
                        return existingEntries[i].Code;
                }
            }

            string code = GenerateUnusedCode(normalized);
            AddEncodeEntry(normalized, code, true);
            codeToPlain[code] = normalized;
            return code;
        }

        public string DecodeWord(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return code ?? "";

            return codeToPlain.TryGetValue(code, out string? plain)
                ? plain
                : code;
        }

        public void Register(string plainWord, string code)
        {
            if (!TryRegister(plainWord, code))
                throw new ArgumentException($"Could not register obfuscation mapping '{plainWord}' -> '{code}'.");
        }

        public void RegisterPlaintext(string word)
        {
            if (!TryRegisterPlaintext(word))
                throw new ArgumentException($"Could not register plaintext obfuscation mapping '{word}'.");
        }

        public bool TryRegisterPlaintext(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return false;

            string normalized = NormalizePlainWord(word);
            return TryRegister(normalized, normalized, replaceExistingPlain: true, allowPlaintextCode: true);
        }

        public bool TryRegister(string plainWord, string code)
        {
            return TryRegister(plainWord, code, replaceExistingPlain: false, allowPlaintextCode: false);
        }

        private bool TryRegister(string plainWord, string code, bool replaceExistingPlain, bool allowPlaintextCode)
        {
            if (string.IsNullOrWhiteSpace(plainWord) || string.IsNullOrWhiteSpace(code))
                return false;

            string normalizedPlain = NormalizePlainWord(plainWord);
            string normalizedCode = NormalizeCode(code);

            if (!allowPlaintextCode && (normalizedCode.Length != CodeLength || !IsAlphabeticCode(normalizedCode)))
                return false;

            if (allowPlaintextCode && !IsWordToken(normalizedCode))
                return false;

            if (plainToCode.TryGetValue(normalizedPlain, out List<EncodeEntry>? existingEntries))
            {
                EncodeEntry? matchingEntry = FindEntry(existingEntries, normalizedCode);
                if (matchingEntry != null)
                {
                    if (replaceExistingPlain)
                        InvalidateEntries(existingEntries);

                    matchingEntry.IsValid = true;
                    return true;
                }

                if (!replaceExistingPlain)
                    return false;
            }

            if (codeToPlain.TryGetValue(normalizedCode, out string? existingPlain))
                return string.Equals(existingPlain, normalizedPlain, StringComparison.OrdinalIgnoreCase);

            if (existingEntries != null)
                InvalidateEntries(existingEntries);

            AddEncodeEntry(normalizedPlain, normalizedCode, true);
            codeToPlain[normalizedCode] = normalizedPlain;
            return true;
        }

        public string PrintTranslationTable()
        {
            var rows = new List<TranslationRow>();
            foreach (KeyValuePair<string, List<EncodeEntry>> pair in plainToCode)
            {
                for (int i = 0; i < pair.Value.Count; i++)
                    rows.Add(new TranslationRow(pair.Key, pair.Value[i]));
            }

            rows.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));

            var builder = new StringBuilder();
            builder.AppendLine("Plain\tCode\tEncodeStatus");
            for (int i = 0; i < rows.Count; i++)
                builder.AppendLine($"{rows[i].Key}\t{rows[i].Entry.Code}\t{(rows[i].Entry.IsValid ? "Valid" : "Invalid")}");

            return builder.ToString().TrimEnd();
        }

        public void Clear()
        {
            plainToCode.Clear();
            codeToPlain.Clear();
        }

        private string GenerateUnusedCode(string normalizedWord)
        {
            int maxCodes = 1;
            for (int i = 0; i < CodeLength; i++)
                maxCodes *= AlphabetLength;

            for (int attempt = 0; attempt < maxCodes; attempt++)
            {
                string code = HashToCode(normalizedWord, attempt);
                if (!codeToPlain.TryGetValue(code, out string? existingWord) ||
                    string.Equals(existingWord, normalizedWord, StringComparison.OrdinalIgnoreCase))
                {
                    return code;
                }
            }

            throw new InvalidOperationException("Obfuscation dictionary exhausted all four-letter codewords.");
        }

        private string HashToCode(string normalizedWord, int attempt)
        {
            string input = $"{salt}:{attempt}:{normalizedWord}";
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            int value = BitConverter.ToInt32(hash, 0) & int.MaxValue;

            char[] chars = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++)
            {
                chars[i] = (char)('A' + (value % AlphabetLength));
                value /= AlphabetLength;
            }

            return new string(chars);
        }

        private static string NormalizePlainWord(string word)
        {
            return word.Trim().ToUpperInvariant();
        }

        private static string NormalizeCode(string code)
        {
            return code.Trim().ToUpperInvariant();
        }

        private static bool IsAlphabeticCode(string code)
        {
            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                if (c < 'A' || c > 'Z')
                    return false;
            }

            return true;
        }

        private void AddEncodeEntry(string normalizedPlain, string normalizedCode, bool isValid)
        {
            if (!plainToCode.TryGetValue(normalizedPlain, out List<EncodeEntry>? entries))
            {
                entries = new List<EncodeEntry>();
                plainToCode[normalizedPlain] = entries;
            }

            entries.Add(new EncodeEntry(normalizedCode, isValid));
        }

        private static EncodeEntry? FindEntry(List<EncodeEntry> entries, string normalizedCode)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].Code, normalizedCode, StringComparison.OrdinalIgnoreCase))
                    return entries[i];
            }

            return null;
        }

        private static void InvalidateEntries(List<EncodeEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
                entries[i].IsValid = false;
        }

        private static bool IsWordToken(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }

            return value.Length > 0;
        }

        private sealed class EncodeEntry
        {
            public EncodeEntry(string code, bool isValid)
            {
                Code = code;
                IsValid = isValid;
            }

            public string Code { get; }
            public bool IsValid { get; set; }
        }

        private readonly struct TranslationRow
        {
            public TranslationRow(string key, EncodeEntry entry)
            {
                Key = key;
                Entry = entry;
            }

            public string Key { get; }
            public EncodeEntry Entry { get; }
        }
    }
}
