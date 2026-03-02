#nullable enable
using UnityEngine;

namespace DogGame.UI.InteractionWheel
{
    public static class MenuWheelUIFactory
    {
        // Path inside any Resources folder, without extension
        private const string PrefabPath = "Prefabs/UI/MenuWheel/MenuWheelUI";

        private static MenuWheelUIController? instance;

        public static MenuWheelUIController GetOrCreate()
        {
            if (instance != null)
                return instance;

            // If someone already placed one in the scene, use it.
            instance = Object.FindFirstObjectByType<MenuWheelUIController>();
            if (instance != null)
                return instance;

            // Load prefab from Resources and instantiate
            MenuWheelUIController prefab = Resources.Load<MenuWheelUIController>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[MenuWheelUIFactory] Could not load MenuWheelUIController prefab at Resources/{PrefabPath}.prefab");
                return null!;
            }

            instance = Object.Instantiate(prefab);
            Object.DontDestroyOnLoad(instance.gameObject);

            // Ensure starts hidden
            instance.CloseMenuWheel();

            return instance;
        }
    }
}