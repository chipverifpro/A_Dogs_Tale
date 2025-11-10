using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// High-level category of an environment element.
/// Used for organization and for choosing how elements are manufactured/rendered.
/// </summary>
public enum ElementLayerKind
{
    Floor,
    TriangleFloor,
    Wall,
    DiagonalWall,
    Ramp,
    Scenery,
    Portable,
    Light,
    Trigger,
    DoorFrame,
    DoorLeaf,
    Fog,
    Custom
}

/// <summary>
/// Flags describing how this element is intended to be rendered/used.
/// This is purely configuration; Manufacturing/Rendering systems interpret it.
/// </summary>
[Flags]
public enum ElementRenderFlags
{
    None            = 0,
    StaticBatch     = 1 << 0, // ideal for static batching
    GpuInstanced    = 1 << 1, // eligible for GPU instancing / indirect
    DynamicObject   = 1 << 2, // moves/animates; avoid static batching
    ReceivesShadows = 1 << 3,
    CastsShadows    = 1 << 4,
    NavWalkable     = 1 << 5  // floor / walkable surface
}

[Flags]
public enum ElementUpdateFlags
{
    None  = 0,
    All   = 1 << 0,
    Color = 1 << 1,
    // Future: Transform = 1 << 1, Material = 1 << 2, etc.
}
/// <summary>
/// Describes the "recipe" or archetype for a family of elements.
/// Think of this as prefab-like data BEFORE it becomes an actual GameObject.
/// ManufactureGO will use this to decide which prefab/mesh/material to use,
/// or how to feed data into GPU instancing.
/// </summary>
[Serializable]
public class ElementArchetype
{
    [Tooltip("Unique id for this archetype. Used by instances to refer back to this definition.")]
    public string id = "default-archetype";

    [Tooltip("Optional human-readable label for debugging / tools.")]
    public string displayName = "Default Archetype";

    [Tooltip("Logical category for this archetype (floor, wall, prop, etc).")]
    public ElementLayerKind kind = ElementLayerKind.Custom;

    [Header("Prefab-based (optional)")]
    [Tooltip("Optional prefab to instantiate for this archetype. " +
             "If assigned, ManufactureGO will use this instead of mesh/material.")]
    public GameObject prefab;
    [Header("Geometry / Appearance (fallback if no prefab)")]

    [Tooltip("Optional mesh to use when manufacturing GameObjects or GPU instances.")]
    public Mesh mesh;

    [Tooltip("Optional material to use with the mesh.")]
    public Material material;

    [Tooltip("Default scale if instances do not override.")]
    public Vector3 defaultScale = Vector3.one;

    [Tooltip("Default color tint if instances do not override.")]
    public Color defaultColor = Color.white;

    [Header("Behavior / Rendering Hints")]
    [Tooltip("Rendering hints used by ManufactureGO / WarehouseGO for batching/instancing.")]
    public ElementRenderFlags renderFlags = ElementRenderFlags.GpuInstanced | ElementRenderFlags.NavWalkable;

    [Tooltip("Layer to assign manufactured GameObjects to (if applicable).")]
    public int unityLayer = 0;

    [Tooltip("Optional tag to assign to manufactured GameObjects.")]
    public string unityTag = "";
}

/// <summary>
/// A single placed element in the logical environment.
/// This is the raw data BEFORE it becomes a GameObject or GPU instance.
/// </summary>
[Serializable]
public struct ElementInstanceData
{
    [Tooltip("Archetype id this instance refers to.")]
    public string archetypeId;

    [Tooltip("Logical layer this element belongs to (floor, wall, ramp, door, etc.).")]
    public ElementLayerKind layerKind;

    [Tooltip("Index of the room this element belongs to.")]
    public int roomIndex;

    [Tooltip("Cell coordinates of this element within the room.")]
    public Vector2Int cellCoord;

    [Tooltip("Height steps of this element.")]
    public int heightSteps;

    [Tooltip("World position of this element.")]
    public Vector3 position;

    [Tooltip("World rotation of this element.")]
    public Quaternion rotation;

    [Tooltip("Local scale override for this element. If zero or Vector3.one, archetype defaultScale may be used.")]
    public Vector3 scale;

    [Tooltip("Color override. If alpha < 0, ManufactureGO can choose to use archetype defaultColor.")]
    public Color color;

    [Tooltip("Optional custom value: could be height, variant index, etc.")]
    public float customValue;

    [Tooltip("Application-specific flags (bitfield). Use as needed by game logic.")]
    public int customFlags;

    [NonSerialized]
    public ElementUpdateFlags dirtyFlags;

    public ElementInstanceData(
        string archetypeId,
        ElementLayerKind layerKind,
        int roomIndex,
        Vector2Int cellCoord,
        int heightSteps,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        Color color,
        int customFlags = 0,
        float customValue = 0f)
    {
        this.archetypeId = archetypeId;
        this.layerKind   = layerKind;
        this.roomIndex   = roomIndex;
        this.cellCoord   = cellCoord;
        this.heightSteps = heightSteps;
        this.position    = position;
        this.rotation    = rotation;
        this.scale       = scale;
        this.color       = color;
        this.customFlags = customFlags;
        this.customValue = customValue;
        this.dirtyFlags  = ElementUpdateFlags.All;
    }
}

/// <summary>
/// A logical grouping of element instances, e.g. "Floor", "Walls", "Props".
/// ManufactureGO can decide to process each layer differently (batching, instancing, etc).
/// </summary>
[Serializable]
public class ElementLayer
{
    [Tooltip("Kind of this layer (Floor, Wall, Ramp, Scenery, etc.).")]
    public ElementLayerKind kind = ElementLayerKind.Custom;

    [Tooltip("Name of this layer (e.g. 'Floor', 'Walls', 'Props').")]
    public string name = "DefaultLayer";

    [Tooltip("Instances belonging to this layer.")]
    public List<ElementInstanceData> instances = new List<ElementInstanceData>();
}

/// <summary>
/// Central store of all environment element definitions and logical instances.
/// This is purely data - no GameObjects here.
/// ManufactureGO will read from this to create actual GameObjects / GPU instances,
/// and WarehouseGO will own the manufactured results.
/// </summary>
[CreateAssetMenu(menuName = "A_Dogs_Tale/Element Store", fileName = "ElementStore")]
public class ElementStore : ScriptableObject
{
    [Header("Archetypes (definitions)")]
    [Tooltip("All known element archetypes (recipes). Instances refer to these by id.")]
    public List<ElementArchetype> archetypes = new List<ElementArchetype>();

    [Header("Layers (grouped instances)")]
    [Tooltip("Logical layers grouping instances (e.g. Floor, Walls, Props).")]
    public List<ElementLayer> layers = new List<ElementLayer>();

    /// <summary>
    /// Optional flat list of all instances across all layers.
    /// This can be used as a convenience if ManufactureGO wants a single list.
    /// </summary>
    [Header("Optional Flat View (runtime only, not required)")]
    [NonSerialized] public List<ElementInstanceData> runtimeAllInstances;

    /// <summary>
    /// Lookup cache from archetype id to index for fast access at runtime.
    /// </summary>
    [NonSerialized] private Dictionary<string, int> archetypeLookup;

    /// <summary>
    /// Build or rebuild the archetype lookup dictionary.
    /// Call this in ManufactureGO before heavy processing.
    /// </summary>
    public void BuildArchetypeLookup()
    {
        if (archetypeLookup == null)
            archetypeLookup = new Dictionary<string, int>(StringComparer.Ordinal);

        archetypeLookup.Clear();
        for (int i = 0; i < archetypes.Count; i++)
        {
            var a = archetypes[i];
            if (string.IsNullOrEmpty(a.id))
                continue;

            if (!archetypeLookup.ContainsKey(a.id))
                archetypeLookup.Add(a.id, i);
            else
                Debug.LogWarning($"ElementStore: Duplicate archetype id '{a.id}' at index {i}");
        }
    }

    /// <summary>
    /// Retrieve an archetype by id. Returns null if not found.
    /// ManufactureGO can use this to find mesh/material/render hints for an instance.
    /// </summary>
    public ElementArchetype GetArchetype(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (archetypeLookup == null || (archetypeLookup.Count != archetypes.Count))
            BuildArchetypeLookup();

        if (archetypeLookup.TryGetValue(id, out int index))
            return archetypes[index];

        return null;
    }

    /// <summary>
    /// Add a new instance to a named layer, creating the layer if it does not exist.
    /// </summary>
    public void AddInstance(string layerName, ElementInstanceData instance)
    {
        if (layers == null)
            layers = new List<ElementLayer>();

        var layer = layers.Find(l => l.name == layerName);
        if (layer == null)
        {
            layer = new ElementLayer { name = layerName, kind = instance.layerKind };
            layers.Add(layer);
        } else if (layer.kind != instance.layerKind) {
            Debug.LogWarning($"ElementStore: Layer '{layerName}' kind mismatch. Existing: {layer.kind}, New Instance: {instance.layerKind}");
            layer.kind = instance.layerKind; // keep runtime data sane
        }

        instance.dirtyFlags = ElementUpdateFlags.All;
        layer.instances.Add(instance);
    }

    /// <summary>
    /// Clear all instances (but keep archetypes intact).
    /// Useful when regenerating a dungeon/map at runtime.
    /// </summary>
    public void ClearInstances()
    {
        if (layers == null) return;

        foreach (var layer in layers)
        {
            if (layer.instances != null)
                layer.instances.Clear();
        }

        runtimeAllInstances?.Clear();
    }

    /// <summary>
    /// Build a flat runtime list of all instances across all layers.
    /// This does not modify stored data; it's just a convenience view.
    /// </summary>
    public List<ElementInstanceData> BuildRuntimeInstanceList()
    {
        if (runtimeAllInstances == null)
            runtimeAllInstances = new List<ElementInstanceData>();
        else
            runtimeAllInstances.Clear();

        if (layers != null)
        {
            foreach (var layer in layers)
            {
                if (layer.instances == null) continue;
                runtimeAllInstances.AddRange(layer.instances);
            }
        }

        return runtimeAllInstances;
    }

    public void AddDoor(
        string frameArchetypeId,
        string leafArchetypeId,
        int roomIndex,
        Vector2Int cellCoord,
        int heightSteps,
        Vector3 baseWorldPos,
        Quaternion facing,
        Vector3 frameScale,
        Vector3 leafScale,
        Color frameColor,
        Color leafColor,
        int doorId      // used to link frame & leaf logically
    )
    {
        // Frame instance (static)
        var frameInst = new ElementInstanceData(
            archetypeId: frameArchetypeId,
            layerKind: ElementLayerKind.DoorFrame,
            roomIndex: roomIndex,
            cellCoord: cellCoord,
            heightSteps: heightSteps,
            position: baseWorldPos,          // usually centered in the doorway
            rotation: facing,
            scale: frameScale,
            color: frameColor,
            customFlags: doorId,             // use this to link to its door leaf
            customValue: 0f
        );
        AddInstance("DoorFrame", frameInst);

        // Leaf instance (dynamic open/close)
        var leafInst = new ElementInstanceData(
            archetypeId: leafArchetypeId,
            layerKind: ElementLayerKind.DoorLeaf,
            roomIndex: roomIndex,
            cellCoord: cellCoord,
            heightSteps: heightSteps,
            position: baseWorldPos,          // same base pos; door controller can offset pivot
            rotation: facing,
            scale: leafScale,
            color: leafColor,
            customFlags: doorId,             // same id → ManufactureGO can pair them
            customValue: 0f
        );
        AddInstance("DoorLeaf", leafInst);
    }
    
    #region Convenience helpers for map generation
    /// <summary>
    /// Adds a floor tile instance to the store.
    /// </summary>
    public void AddFloorTile(
        string archetypeId,
        bool isTriangle,
        int roomIndex,
        Vector2Int cellCoord,
        int heightSteps,
        Vector3 worldPos,
        Quaternion rotation,
        Vector3 scale,
        Color color)
    {
        var kind = isTriangle ? ElementLayerKind.TriangleFloor : ElementLayerKind.Floor;

        var inst = new ElementInstanceData(
            archetypeId: archetypeId,
            layerKind: kind,
            roomIndex: roomIndex,
            cellCoord: cellCoord,
            heightSteps: heightSteps,
            position: worldPos,
            rotation: rotation,
            scale: scale,
            color: color,
            customFlags: 0,
            customValue: 0f
        );

        AddInstance("FloorTile", inst);
    }

    /// <summary>
    /// Adds a wall or diagonal wall instance.
    /// customFlags can mark door segments or other metadata.
    /// </summary>
    public void AddWall(
        string archetypeId,
        bool isDiagonal,
        int roomIndex,
        Vector2Int cellCoord,
        int heightSteps,
        Vector3 worldPos,
        Quaternion rotation,
        Vector3 scale,
        Color color,
        int customFlags = 0)
    {
        var kind = isDiagonal ? ElementLayerKind.DiagonalWall : ElementLayerKind.Wall;

        var inst = new ElementInstanceData(
            archetypeId: archetypeId,
            layerKind: kind,
            roomIndex: roomIndex,
            cellCoord: cellCoord,
            heightSteps: heightSteps,
            position: worldPos,
            rotation: rotation,
            scale: scale,
            color: color,
            customFlags: customFlags,
            customValue: 0f
        );

        AddInstance("Wall", inst);
    }

    public void AddFog(
        string archetypeId,
        int roomIndex,
        Vector2Int cellCoord,
        int heightSteps,
        Vector3 worldPos,
        Quaternion rotation,
        Vector3 scale,
        Color color,
        int customFlags = 0)
    {
        var inst = new ElementInstanceData(
            archetypeId: archetypeId,
            layerKind: ElementLayerKind.Fog,
            roomIndex: roomIndex,
            cellCoord: cellCoord,
            heightSteps: heightSteps,
            position: worldPos,
            rotation: rotation,
            scale: scale,
            color: color,
            customFlags: customFlags,
            customValue: 0f
        );

        AddInstance("Fog", inst);
    }

    /// <summary>
    /// Adds a ramp instance between two tiles.
    /// customValue stores the height delta (difference between cells).
    /// </summary>
    public void AddRamp(
        string archetypeId,
        int roomIndex,
        Vector2Int cellCoord,
        int heightSteps,
        Vector3 worldPos,
        Quaternion rotation,
        Vector3 scale,
        Color color,
        float heightDelta)
    {
        var inst = new ElementInstanceData(
            archetypeId: archetypeId,
            layerKind: ElementLayerKind.Ramp,
            roomIndex: roomIndex,
            cellCoord: cellCoord,
            heightSteps: heightSteps,
            position: worldPos,
            rotation: rotation,
            scale: scale,
            color: color,
            customFlags: 0,
            customValue: heightDelta
        );

        AddInstance("Ramp", inst);
    }

    /// <summary>
    /// Change the color of a specific instance and mark it dirty so
    /// ManufactureGO.ApplyPendingUpdates can push it into the live GameObject.
    /// Returns true if an instance was found and changed.
    /// </summary>
/*    public bool ChangeColor(
        ElementLayerKind kind,
        int roomIndex,
        Vector2Int cellCoord,
        Color newColor)
    {
        if (layers == null) return false;

        // Find the layer
        var layer = layers.Find(l => l != null && l.kind == kind);
        if (layer == null || layer.instances == null) return false;

        for (int i = 0; i < layer.instances.Count; i++)
        {
            var inst = layer.instances[i];
            if (inst.roomIndex == roomIndex && inst.cellCoord == cellCoord)
            {
                inst.color = newColor;
                inst.dirtyFlags |= ElementUpdateFlags.Color;
                layer.instances[i] = inst; // struct copy back
                return true;
            }
        }

        return false;
    }
*/

    // variant that takes cell
    // If we have cell_scent_number, no searching needed in ChangeColor()
    public bool ChangeColor(
        ElementLayerKind kind,
        String layerName,
        Cell cell,
        int cell_scent_number,  // -1 if unknown
        Color newColor)
    {
        int fogIndex = -1;
        if (cell_scent_number >= 0)
            fogIndex = cell.scents[cell_scent_number].fogIndex;
            
        //Debug.Log($"XX ChangeColor: kind={kind}, room={cell.room_number}, pos={cell.pos}");
        if (layers == null)
        {
            Debug.Log("  layers is null");
            return false;
        }

        // Find the layer
        // TODO loop over all layers and debug print them
        //foreach (var l in layers)
        //{
            //Debug.Log($"  layer: {l} = kind {l.kind}, name {l.name} : == {l.kind == kind} / == {l.name == layerName}");
        //}
        var layer = layers.Find(l => l != null && (l.kind == kind));
        if (layer == null)
        {
            // backup: try finding by name
            layer = layers.Find(l => l != null && (l.name == layerName));
        }
        if (layer == null)
        {
            Debug.Log($"  layer = {layer} not found. kind={kind}");
            return false;
        } else if (layer.instances == null) {
            Debug.Log("  layer.instances is null");
            return false;
        } else {
            //Debug.Log($"  FOUND: layer.instances count = {layer.instances.Count}");
        }

        int i_start = fogIndex >= 0 ? fogIndex : 0; // TODO: start search at fogIndex if known.  See assertion below first.
        for (int i = 0; i < layer.instances.Count; i++)
        {
            var inst = layer.instances[i];
            //Debug.Log($"  Checking inst[{i}] room={inst.roomIndex}, coord={inst.cellCoord}");
            if (inst.roomIndex == cell.room_number && inst.cellCoord == cell.pos)
            {
                if (cell_scent_number >= 0) {
                    if (fogIndex < 0)
                    {
                        Debug.Log($"found cell.scents[{cell_scent_number}].fogIndex = {fogIndex}, save it for next time.");
                        cell.scents[cell_scent_number].fogIndex = i;    // found fogIndex, save it for next time.
                    } else if (i != fogIndex) {
                        Debug.LogError($"Another Assumption Wrong: fogIndex = {fogIndex} not correct, turned out to be {i}.  If this never fires, use i_start above.");
                    }
                }
                inst.color = newColor;
                inst.dirtyFlags |= ElementUpdateFlags.Color;
                layer.instances[i] = inst; // struct copy back
                Debug.Log("  Color changed");
                return true;
            }
        }
        Debug.Log("  No matching instance found");
        return false;
    }
    #endregion
}