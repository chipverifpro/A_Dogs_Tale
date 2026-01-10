using UnityEngine;
using DogGame.Tasks;

namespace DogGame.Modules
{
    [DisallowMultipleComponent]
    public class BlackboardModule : WorldModule
    {
        public IBlackboard Blackboard { get; private set; } = null!;

        protected override void Awake()
        {
            ForceInitialize();
        }

        /// <summary>
        /// Ensure Blackboard exists even if this component was just added at runtime.
        /// </summary>
        public void ForceInitialize()
        {
            Blackboard ??= new SimpleBlackboard();
        }
    }
}