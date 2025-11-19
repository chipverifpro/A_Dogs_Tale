using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DogGame/Scenery Library", fileName = "SceneryLibrary")]
public class SceneryLibrary : ScriptableObject
{
    [Tooltip("All environment prefabs (trees, rocks, etc.) that can be scattered.")]
    public List<GameObject> prefabs = new List<GameObject>();
}