using System;
using UnityEngine;

namespace InspectorTools
{
    public enum InspectorNoteMessageType
    {
        Info,
        Warning,
        Error
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class InspectorNoteAttribute : PropertyAttribute
    {
        public string Title { get; }
        public string Message { get; }
        public InspectorNoteMessageType MessageType { get; }

        public InspectorNoteAttribute(string title, string message, InspectorNoteMessageType messageType = InspectorNoteMessageType.Info)
        {
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            MessageType = messageType;
        }
    }
}
