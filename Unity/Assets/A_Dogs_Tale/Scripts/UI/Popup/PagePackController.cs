using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

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

        var stay   = root.Q<Button>("Order-Stay");
        var patrol = root.Q<Button>("Order-Patrol");
        var guard  = root.Q<Button>("Order-Guard");
        var track  = root.Q<Button>("Order-Track");

        if (stay != null)   stay.clicked   += () => { AudioPlayer.PlayUiButtonClick(); Debug.Log("Order: Stay"); };
        if (patrol != null) patrol.clicked += () => { AudioPlayer.PlayUiButtonClick(); Debug.Log("Order: Patrol"); };
        if (guard != null)  guard.clicked  += () => { AudioPlayer.PlayUiButtonClick(); Debug.Log("Order: Guard"); };
        if (track != null)  track.clicked  += () => { AudioPlayer.PlayUiButtonClick(); Debug.Log("Order: Track"); };
    }
}
