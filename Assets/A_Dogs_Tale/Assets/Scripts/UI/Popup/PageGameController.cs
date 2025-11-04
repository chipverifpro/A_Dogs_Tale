using UnityEngine;
using UnityEngine.UIElements;

public class PageGameController : MonoBehaviour
{
    public void Bind(VisualElement root)
    {
        var pause = root.Q<Button>("Btn-Pause");
        var save  = root.Q<Button>("Btn-Save");
        var load  = root.Q<Button>("Btn-Load");
        var exit  = root.Q<Button>("Btn-Exit");

        if (pause != null) pause.clicked += () => Debug.Log("Pause/Resume clicked");
        if (save != null)  save.clicked  += () => Debug.Log("Save clicked");
        if (load != null)  load.clicked  += () => Debug.Log("Load clicked");
        if (exit != null)  exit.clicked  += () => Debug.Log("Exit clicked");
    }
}