#nullable enable
using System;
using UnityEngine;

namespace DogGame.Routines
{
    /// <summary>
    /// A named routine is a TaskSpec tree + optional metadata.
    /// Stored in code (v1) or later as ScriptableObject / JSON.
    /// </summary>
    [Serializable]
    public sealed class RoutineDefinition
    {
        [SerializeField] private string id = "routine_id";
        [SerializeField] [TextArea] private string description = "";
        [SerializeField] private string[] tags = Array.Empty<string>();

        // Stored as JSON for inspector friendliness (since TaskSpec has object args).
        // v1: you can keep these definitions in code via helper methods and ignore jsonBody.
        [SerializeField] [TextArea(8, 30)] private string jsonBody = "";

        // Runtime body (preferred in code paths)
        [NonSerialized] public DogGame.Reactions.TaskSpec Body;

        public string Id => id;
        public string Description => description;
        public string[] Tags => tags;
        public string JsonBody => jsonBody;

        public RoutineDefinition(string id, DogGame.Reactions.TaskSpec body, string description = "", string[]? tags = null)
        {
            this.id = id;
            Body = body;
            this.description = description;
            this.tags = tags ?? Array.Empty<string>();
        }
    }
}

// Note: v1 uses Body built in code. Later you can load jsonBody into TaskSpecs.