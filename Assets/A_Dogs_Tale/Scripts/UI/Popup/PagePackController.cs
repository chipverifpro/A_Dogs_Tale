using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using UnityEditor.UIElements;

public class PagePackController : MonoBehaviour
{
    public struct Dog { public string Name; public string Role; }
    List<Dog> sample = new()
    { new Dog{ Name="Shep", Role="Leader"}, new Dog{ Name="Corgi", Role="Scout"}, new Dog{ Name="Chihuahua", Role="Mascot"} };

    public void Bind(VisualElement root)
    {
        var list = root.Q<ListView>("PackList");
        if (list != null)
        {
            list.itemsSource = sample;
            list.makeItem = () => new Label();
            list.bindItem = (e, i) => (e as Label).text = sample[i].Name + " — " + sample[i].Role;
            list.reorderable = true;
        }

        var formation = root.Q<DropdownField>("Formation");
        if (formation != null)
        {
            formation.choices = new() { "Line", "Diamond", "Wedge", "Column" };
            formation.value = "Line";
        }

        var stay   = root.Q<ToolbarButton>("Order-Stay");
        var patrol = root.Q<ToolbarButton>("Order-Patrol");
        var guard  = root.Q<ToolbarButton>("Order-Guard");
        var track  = root.Q<ToolbarButton>("Order-Track");

        if (stay != null)   stay.clicked   += () => Debug.Log("Order: Stay");
        if (patrol != null) patrol.clicked += () => Debug.Log("Order: Patrol");
        if (guard != null)  guard.clicked  += () => Debug.Log("Order: Guard");
        if (track != null)  track.clicked  += () => Debug.Log("Order: Track");
    }
}