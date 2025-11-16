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
    ScentGround,
    ScentAir,
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
public class ElementInstanceData
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
        this.layerKind = layerKind;
        this.roomIndex = roomIndex;
        this.cellCoord = cellCoord;
        this.heightSteps = heightSteps;
        this.position = position;
        this.rotation = rotation;
        this.scale = scale;
        this.color = color;
        this.customFlags = customFlags;
        this.customValue = customValue;
        this.dirtyFlags = ElementUpdateFlags.All | ElementUpdateFlags.Color;
    }
    
    public void PrintElementInstanceData()
    {
        Debug.Log($"ElementInstanceData entry: {archetypeId}, {layerKind}, {cellCoord}, {color.a}, {dirtyFlags}");
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
    [System.NonSerialized]
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

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            // Start fresh at runtime
            layers = new List<ElementLayer>(); // clear legacy junk
        }
    }
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
/*
    public int AddInstance_old(string layerName, ElementInstanceData instance)
    {
        Debug.Log($"AddInstance({layerName}) at {instance.position}");
        
        if (layers == null)
            layers = new List<ElementLayer>();

        var layer = layers.Find(l => l.name == layerName);
        if (layer == null)
        {
            layer = new ElementLayer { name = layerName, kind = instance.layerKind };
            layer.instances = new();
            layers.Add(layer);
        } else if (layer.kind != instance.layerKind) {
            Debug.LogWarning($"ElementStore: Layer '{layerName}' kind mismatch. Existing: {layer.kind}, New Instance: {instance.layerKind}");
            layer.kind = instance.layerKind; // keep runtime data sane
        }

        instance.dirtyFlags = (ElementUpdateFlags.Color || ElementUpdateFlags.All);
        layer.instances.Add(instance);
        return layer.instances.Count - 1;   // array index of added GO
    }
*/
    public int AddInstance(string layerName, ElementInstanceData instance)
    {
        if (string.IsNullOrEmpty(layerName))
        {
            Debug.LogError("ElementStore.AddInstance: layerName is null/empty.");
            return -1;
        }

        // NEW: brand-new instances should ask the factory to build everything, not just color.
        instance.dirtyFlags = ElementUpdateFlags.All | ElementUpdateFlags.Color;

        // Ensure layers list
        layers ??= new List<ElementLayer>();

        // Find (prefer index-based so it works with class or struct)
        int layerIdx = layers.FindIndex(l => l != null && l.name == layerName);
        ElementLayer layer;

        if (layerIdx < 0)
        {
            layer = new ElementLayer { name = layerName, kind = instance.layerKind, instances = new List<ElementInstanceData>() };
            layers.Add(layer);
            layerIdx = layers.Count - 1;
        }
        else
        {
            layer = layers[layerIdx];

            if (layer.kind != instance.layerKind)
            {
                Debug.LogWarning($"ElementStore: Layer '{layerName}' kind mismatch. Existing={layer.kind}, New={instance.layerKind}. Changing layer.kind to match");
                layer.kind = instance.layerKind; // keep runtime data sane (fix bug somewhere else?)
                /*
                // Safer handling of kind mismatch
                // Create a sibling layer name to keep data sane (or you can abort)
                string altName = $"{layerName}_{instance.layerKind}";
                int altIdx = layers.FindIndex(l => l != null && l.name == altName);
                if (altIdx < 0)
                {
                    var newLayer = new ElementLayer { name = altName, kind = instance.layerKind, instances = new List<ElementInstanceData>(64) };
                    layers.Add(newLayer);
                    layer = newLayer;
                    layerIdx = layers.Count - 1;
                }
                else
                {
                    layer = layers[altIdx];
                    layerIdx = altIdx;
                }*/
            }
        }

        // Append; ElementInstanceData is a class, no need to copy back data
        layer.instances.Add(instance);

        // DEBUG: prove it got in
        //Debug.Log($"[AddInstance] layer='{layer.name}', kind={layer.kind}, newCount={layer.instances.Count}");

        // Return stable index within this layer's instance list
        return layer.instances.Count - 1;
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

    public int AddScentAir(Cell cell, Color color)
    {
        Quaternion quadRotate = Quaternion.Euler(-90f, 0f, 0f);
        Vector3 airFogOffset = new(0f, 1.1f, 0f);
        Vector3 overlapFuzzyScale = new(1.95f, 2f, 1.95f);
        var inst = new ElementInstanceData(
            archetypeId: "ScentAir",
            layerKind: ElementLayerKind.ScentAir,
            roomIndex: cell.room_number,
            cellCoord: cell.pos,
            heightSteps: cell.height,
            position: cell.pos3d_world + airFogOffset,
            rotation: quadRotate,
            scale: overlapFuzzyScale,
            color: color,
            customFlags: 0,
            customValue: 0f
        );

        // Optional: validate before trying to build
        //if (!elementStore.TryGetArchetype(inst.archetypeId, out _)) {
        //    Debug.LogError("AddScentGround: archetype {inst.architypeId} not registered. Register it before calling AddScentGround.");
        //    return -1;
        //}

        int GOindex = AddInstance("ScentAir", inst);
        //Debug.Log($"AddScentAir(@{cell.pos}, alpha={color.a}) -> GOindex={GOindex}");
        //inst.PrintElementInstanceData();    // more debug
        return GOindex;

    }

    public int AddScentGround(Cell cell, Color color)
    {
        Quaternion quadRotate = Quaternion.Euler(-90f, 0f, 0f);
        Vector3 groundFogOffset = new(0f, 0.7f, 0f);
        Vector3 overlapFuzzyScale = new(1.95f, 2f, 1.95f);
        
        var inst = new ElementInstanceData(
            archetypeId: "ScentGround",
            layerKind: ElementLayerKind.ScentGround,
            roomIndex: cell.room_number,
            cellCoord: cell.pos,
            heightSteps: cell.height,
            position: cell.pos3d_world + groundFogOffset,
            rotation: quadRotate,
            scale: overlapFuzzyScale,
            color: color,
            customFlags: 0,
            customValue: 0f
        );

        int GOindex = AddInstance("ScentGround", inst);
        //Debug.Log($"AddScentGround(@{cell.pos}, alpha={color.a}) -> GOindex={GOindex}");
        //inst.PrintElementInstanceData();
        return GOindex;
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
    // If we have cell and scent, no searching for them needed in ChangeColor()
    public bool ChangeColor(
        ElementLayerKind kind,
        int GOindex,
        Cell cell,
        Color newColor)
    {
        if ((cell == null) || (layers == null))
        {
            Debug.LogError("ChangeColor aborting due to null pointer.");
            return false;
        }
        //Debug.Log($"ChangeColor({kind}, GOindex {GOindex}, @{cell.pos}, alpha={newColor.a})");

        // Find the layer
        var layer = layers.Find(l => l != null && (l.kind == kind));
        //Error conditions...
        if (layer == null)
        {
            Debug.LogWarning($"  {kind} layer not found. creating one");
            //DumpLayers();
            // TODO: call layer creation routine in Manufacture.go instead.
            layer = new();
            layer.kind = kind;
            layer.instances = new();
            layer.name = kind.ToString();
            layers.Add(layer);
        }
        else if (layer.instances == null)
        {
            Debug.Log($"  {kind} layer.instances is null");
            layer.instances = new();
        }
        else
        {
            //Debug.Log($"  FOUND: layer.instances count = {layer.instances.Count}");
        }

        // DEBUG:
        int count = layer.instances?.Count ?? 0;
        if (GOindex < 0 || GOindex >= count)
        {
            Debug.LogError(
                $"ChangeColor: kind={kind}, layerName='{layer.name}', GOindex={GOindex}, instanceCount={count}. Cell={cell.pos}");
            DumpLayers();
            return false;
        }

        if (GOindex >= layer.instances.Count)
            Debug.LogError($"ChangeColor GOindex {GOindex} >= layer.instances {layer.instances.Count}");
        if (GOindex >= 0)
        {
            var inst = layer.instances[GOindex];
            inst.color = newColor;
            inst.dirtyFlags |= ElementUpdateFlags.Color;
            layer.instances[GOindex] = inst; // copy back
            //Debug.Log($"  {kind} Color changed");
            return true;
        }
        Debug.Log($"  No matching {kind} instance found for GOindex {GOindex}");
        return false;
    }

    private void DumpLayers()
    {
        if (layers == null)
        {
            Debug.Log("ElementStore: layers is null");
            return;
        }

        Debug.Log($"ElementStore: DumpLayers (count={layers.Count})");
        for (int i = 0; i < layers.Count; i++)
        {
            var l = layers[i];
            if (l == null)
            {
                Debug.Log($"  [{i}] null");
                continue;
            }

            int instCount = l.instances?.Count ?? 0;
            Debug.Log($"  [{i}] name='{l.name}', kind={l.kind}, instances={instCount}");
        }
    }

    public ElementLayer GetLayer(ElementLayerKind kind)
    {
        if (layers == null) return null;
        return layers.Find(l => l != null && l.kind == kind);
    }

    #endregion
}