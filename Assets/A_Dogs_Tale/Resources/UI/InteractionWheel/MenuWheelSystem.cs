#nullable enable
using UnityEngine;
using DogGame.UI.InteractionWheel;

public sealed class MenuWheelSystem : MonoBehaviour
{
    [SerializeField] private MenuWheelUIController wheelPrefab;

    private MenuWheelUIController? wheelInstance;

    public void Awake()
    {
        Initialize();  
    }
    
    public void Initialize()
    {
        wheelInstance = Instantiate(wheelPrefab);
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
}