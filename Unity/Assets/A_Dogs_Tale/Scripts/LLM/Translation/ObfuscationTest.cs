using System;
using DogGame.LLM;
using UnityEngine;

namespace DogGame.Test
{
    /// <summary>
    /// Deterministic word-level obfuscation for LLM experiments.
    /// Encoded words are four uppercase alphabetic characters.
    /// </summary>
    public sealed class ObfuscationTest : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("ObfuscationTest begins");

            var obfuscationDictionary = new DogGame.LLM.ObfuscationDictionary();

            if (!obfuscationDictionary.TryRegister("GET", "UPIO"))
                Debug.LogError("[ObfuscationTest] " + "Could not register GET.");

            if (!obfuscationDictionary.TryRegister("RETURN", "LPLK"))
                Debug.LogError("[ObfuscationTest] " + "Could not register RETURN.");

            string encoded = obfuscationDictionary.Encode("Fetch Ball, then Return Ball.");
            string decoded = obfuscationDictionary.Decode(encoded);

            Debug.Log($"encoded {encoded} -> decoded {decoded}");
            if (decoded != "FETCH BALL, THEN RETURN BALL.")
                Debug.LogError("[ObfuscationTest] " + $"Decode mismatch: {decoded}");

            string command = obfuscationDictionary.Encode("GET Ball\nRETURN Ball");
            string restored = obfuscationDictionary.Decode(command);

            if (!command.StartsWith("UPIO ", StringComparison.Ordinal) ||
                !command.Contains("\nLPLK ", StringComparison.Ordinal))
            {
                Debug.LogError("[ObfuscationTest] " + $"Command mapping mismatch: {command}");
            }

            obfuscationDictionary.Register("GET", "UPIO");
            obfuscationDictionary.RegisterPlaintext("GET");

            obfuscationDictionary.EncodeWord("GET");  // GET
            string decodeA = obfuscationDictionary.DecodeWord("UPIO"); // GET
            string decodeB = obfuscationDictionary.DecodeWord("GET");  // GET

            Debug.Log("[ObfuscationTest] " + encoded);
            Debug.Log("[ObfuscationTest] " + decoded);
            Debug.Log("[ObfuscationTest] " + command);
            Debug.Log("[ObfuscationTest] " + restored);
            Debug.Log("[ObfuscationTest] " + obfuscationDictionary.PrintTranslationTable());
            Debug.Log("[ObfuscationText] " + decodeA);
            Debug.Log("[ObfuscationText] " + decodeB);
        }
    }
}