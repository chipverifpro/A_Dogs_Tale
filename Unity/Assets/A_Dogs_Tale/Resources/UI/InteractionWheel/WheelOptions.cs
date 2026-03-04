#nullable enable
using System;
using UnityEngine;

namespace DogGame.UI.InteractionWheel
{
    /// <summary>
    /// One selectable item shown in the wheel.
    /// This is UI-agnostic; the UI system will render these.
    /// </summary>
    [Serializable]
    public sealed class WheelOption
    {
        [Header("Identity")]
        public string id = "";                 // Stable key: "sniff", "get", "quest_get", etc.
        public int sortPriority = 0;           // Higher first (or lower first; just be consistent)

        [Header("Text + Hints")]
        public string label = "";
        [TextArea] public string hint = "";
        [TextArea] public string disabledHint = "";

        [Header("State")]
        public bool isVisible = true;
        public bool isEnabled = true;

        [Header("Optional Visuals")]
        public Sprite? icon;

        /// <summary>
        /// Called when selected (if enabled).
        /// Not serialized; set at runtime by resolvers / registry.
        /// </summary>
        [NonSerialized] public Action<WheelContext>? callback;

        public override string ToString()
        {
            return $"WheelOption(id={id}, label={label}, enabled={isEnabled}, visible={isVisible}, priority={sortPriority})";
        }
    }
}