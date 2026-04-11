using System;
using UnityEngine;
using UnityEditor;

namespace InspectorTools
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class InspectorNoteAttribute : PropertyAttribute
    {
        public string Title { get; }
        public string Message { get; }
        public MessageType MessageType { get; }

        public InspectorNoteAttribute(string title, string message, MessageType messageType = MessageType.Info)
        {
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            MessageType = messageType;
        }
    }
}