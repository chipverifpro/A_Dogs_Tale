using UnityEngine;
using DogGame.Modules;

public partial class FurniturePlacer
{
    /// <summary>
    /// Ensure the spawned object is a WorldObject with LocationModule, VisualModule, etc.,
    /// and register it. Adapt as needed to match your existing WorldObject API.
    /// </summary>
    private void InitializeWorldObject(GameObject instance, Cell cell)
    {
        if (instance == null || cell == null)
            return;

        WorldObject wo = instance.GetComponent<WorldObject>();
        if (wo == null)
            wo = instance.AddComponent<WorldObject>();

        LocationModule loc = instance.GetComponent<LocationModule>();
        if (loc == null)
            loc = instance.AddComponent<LocationModule>();

        VisionPerceptionModule visual = instance.GetComponent<VisionPerceptionModule>();
        if (visual == null)
            visual = instance.AddComponent<VisionPerceptionModule>();

        instance.transform.position = cell.pos3d_world + new Vector3(0f, baseYOffset, 0f);
        wo.RegisterIfNeeded();
    }
}
