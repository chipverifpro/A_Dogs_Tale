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
        Transform? parent = FindGameInputParent();
        wheelInstance = Instantiate(wheelPrefab,parent);
        Debug.Log ($"instantiated wheelInstance");
        //wheelInstance.CloseMenuWheel(); // ensure hidden
    }

    public void Open(WheelMenuModel model, float? timeScaleOverride = null)
    {
        if (wheelInstance == null)
        {
            wheelInstance = Instantiate(wheelPrefab);
        }
        wheelInstance?.OpenMenuWheel(model, timeScaleOverride);
    }

    public void Close()
    {
        wheelInstance?.CloseMenuWheel();
    }

    public bool IsOpen => wheelInstance != null && wheelInstance.IsOpen;

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
