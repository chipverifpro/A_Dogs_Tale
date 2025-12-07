using UnityEngine;

namespace DogGame.Modules
{
    [DisallowMultipleComponent]
    public class ActivatorModule : WorldModule
    {
        public UnityEngine.Events.UnityEvent OnActivate;
    }
}