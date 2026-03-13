using UnityEngine;

// ----- ABSTRACT BASE CLASS -----

// Note on function naming within modules:
// ---------------------------------------
// No function that is NOT named Tick, Update, Advance, or Simulate
// is allowed to take deltaTime as a parameter, nor is it allowed to
// simulate the passing of time.  It sets things up for lower modules.



namespace DogGame.Modules
{
    // WorldModules are components that can be attached to WorldObjects to give them specific functionalities.
    // Based on WorldModule, are a variety of specialized modules like LocationModule, MotionModule, VisualModule, etc.
    [RequireComponent(typeof(WorldObject))]
    public abstract class WorldModule : MonoBehaviour
    {
        private WorldObject _worldObject;
        public WorldObject worldObject => _worldObject ??= GetComponent<WorldObject>();

        private Dir _dir;
        public Dir dir => _dir ??= Dir.Instance;
        
        protected virtual void Awake()
        {
            if (worldObject == null)
            {
                Debug.Log($"[WorldModule] Awake failed to find worldObject.  Creating it. {this}");
                _worldObject = gameObject.AddComponent<WorldObject>();
                if (_worldObject == null)
                    Debug.LogError($"[WorldModule] Awake failed to create missing worldObject. {this}");
            }
        }

        // OBSOLETE: Initialize called from WorldObject.Awake phase
        // each WorldModule belongs to a WorldObject
        public virtual void Initialize(WorldObject owner)
        {
            //Debug.Log($"[{owner.DisplayName}] Initialize WorldModule {this}");
            //worldObject = owner;
        }

        protected virtual void Update()
        {

        }

        public virtual void Tick(float deltaTime)
        {
            //Debug.Log($"WorldModule {worldObject.DisplayName}: Tick {deltaTime}");
        }

        // future hook: OnWorldObjectAttached(), OnWorldObjectDetached(), etc.
    }
}