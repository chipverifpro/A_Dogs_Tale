#nullable enable
using UnityEngine;
using DogGame.UI.InteractionWheel;

public sealed class MenuWheelSystem : MonoBehaviour
{
    [SerializeField] private MenuWheelUIController? wheelPrefab;

    private MenuWheelUIController? wheelInstance;

    public void Awake()
    {
        Initialize();  
    }
    
    public void Initialize()
    {
        EnsureWheelInstance();
    }

    public void Open(WheelMenuModel model, float? timeScaleOverride = null)
    {
        EnsureWheelInstance();
        wheelInstance?.OpenMenuWheel(model, timeScaleOverride);
    }

    public void Close()
    {
        wheelInstance?.CloseMenuWheel();
    }

    public bool IsOpen => wheelInstance != null && wheelInstance.IsOpen;

    private void EnsureWheelInstance()
    {
        if (wheelInstance != null)
            return;

        wheelInstance = FindFirstObjectByType<MenuWheelUIController>(FindObjectsInactive.Include);
        if (wheelInstance != null)
        {
            Debug.Log("Reusing existing MenuWheelUIController instance.");
            return;
        }

        if (wheelPrefab == null)
        {
            Debug.LogError("MenuWheelSystem has no wheelPrefab assigned.", this);
            return;
        }

        Transform? parent = FindGameInputParent();
        wheelInstance = Instantiate(wheelPrefab, parent);
        Debug.Log("Instantiated MenuWheelUIController from prefab.");
        wheelInstance.CloseMenuWheel();
    }

    private Transform? FindGameInputParent()
    {
        GameObject inputGO = GameObject.Find("Input");
        if (inputGO == null)
        {
            Debug.LogWarning("Could not find Input in scene.");
            return null;
        }

        return inputGO.transform;
    }
}
