using UnityEngine;
using DogGame.Tasks;
using InspectorTools;

namespace DogGame.Modules
{
    [DisallowMultipleComponent]
    [InspectorNote("Data_Modules/Blackboard Module", "Creates an SimpleBlackboard which allows any module to store/retrieve any Key/Value pair of state data.")]
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