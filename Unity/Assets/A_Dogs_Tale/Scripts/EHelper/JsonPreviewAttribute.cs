using UnityEngine;

namespace DogGame.Attributes
{
    /// <summary>
    /// Marks a string field to be shown as syntax-colored JSON in the Inspector.
    /// Rendering is handled by an Editor-only PropertyDrawer.
    /// </summary>
    public sealed class JsonPreviewAttribute : PropertyAttribute
    {
        public readonly float Height;

        public JsonPreviewAttribute(float height = 220f)
        {
            Height = height;
        }
    }
}