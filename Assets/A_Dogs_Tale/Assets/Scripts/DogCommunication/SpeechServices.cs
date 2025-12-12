using UnityEngine;
using System;
using DogGame.Language;
using UnityEngine.UIElements;

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

            // TODO later:
            // - Gather all audible listeners (player, nearby dogs, NPCs).
            // - Check distance + obstructions.
            // - Per-listener translation (if you ever do per-dog vocab).
            // For now: assume player hears it.

            string translatedForDog = DogSpeechDictionary.TranslateHumanToDog(message);

            string speakerName = (speaker != null && !string.IsNullOrWhiteSpace(speaker.DisplayName))
                ? speaker.DisplayName
                : "Human";

            // Include intended listener hint (optional, helps debugging)
            string listenerHint = (listener != null && !string.IsNullOrWhiteSpace(listener.DisplayName))
                ? $" → {listener.DisplayName}"
                : string.Empty;

            BottomBanner.Show($"{speakerName}{listenerHint}: {translatedForDog}");
            Debug.Log($"Speech {message} -> {translatedForDog}");
        }
    }
}