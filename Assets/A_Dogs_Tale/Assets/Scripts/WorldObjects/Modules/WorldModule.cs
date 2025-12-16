using UnityEngine;

// ----- ABSTRACT BASE CLASS -----

namespace DogGame.Modules
{
    // WorldModules are components that can be attached to WorldObjects to give them specific functionalities.
    // Based on WorldModule, are a variety of specialized modules like LocationModule, MotionModule, VisualModule, etc.
    public abstract class WorldModule : MonoBehaviour
    {
        private WorldObject _worldObject;
        public WorldObject worldObject => _worldObject ??= GetComponent<WorldObject>();

        private Directory _dir;
        public Directory dir => _dir ??= Directory.Instance;


        protected virtual void Awake()
        {
            //if (worldObject == null)
            //    worldObject = GetComponent<WorldObject>();
            if (worldObject == null)
                Debug.LogError($"[WorldModule] Awake failed to initialize worldObject. {this}");
        }

        // Initialize called from WorldObject.Awake phase
        // each WorldModule belongs to a WorldObject
        public virtual void Initialize(WorldObject owner)
        {
            Debug.Log($"[{owner.DisplayName}] Initialize WorldModule {this}");
            //worldObject = owner;
        }

        protected virtual void Update()
        {

        }

        public virtual void Tick(float deltaTime)
        {
            Debug.Log($"WorldModule {worldObject.DisplayName}: Tick {deltaTime}");
        }

        // future hook: OnWorldObjectAttached(), OnWorldObjectDetached(), etc.
    }
}