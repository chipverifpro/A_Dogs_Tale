using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds the fully manufactured GameObjects for a built environment.
/// ManufactureGO populates this from ElementStore.
/// Later, some layers can move to GPU-only, but this class stays the "registry".
/// </summary>
public class WarehouseGO : MonoBehaviour
{
    [Serializable]
    public class LayerBucket
    {
        public ElementLayerKind kind;
        public Transform parent;
        public List<GameObject> objects = new List<GameObject>();
    }

    [Header("Optional: human-readable label or id for this warehouse.")]
    public string warehouseName = "DefaultWarehouse";

    [Header("Per-layer GameObject buckets (auto-filled at runtime).")]
    public List<LayerBucket> layers = new List<LayerBucket>();

    // Fast lookup at runtime (not serialized)
    private Dictionary<ElementLayerKind, LayerBucket> layerLookup;

    /// <summary>
    /// Ensure the dictionaries are built.
    /// </summary>
    void Awake()
    {
        BuildLookup();
    }

    void BuildLookup()
    {
        if (layerLookup == null)
            layerLookup = new Dictionary<ElementLayerKind, LayerBucket>();

        layerLookup.Clear();

        foreach (var lb in layers)
        {
            if (lb == null) continue;
            if (!layerLookup.ContainsKey(lb.kind))
                layerLookup.Add(lb.kind, lb);
        }
    }

    /// <summary>
    /// Get or create the bucket & parent Transform for a given layer kind.
    /// ManufactureGO will use this as the parent for instantiated GOs.
    /// </summary>
    public LayerBucket GetOrCreateLayer(ElementLayerKind kind)
    {
        if (layerLookup == null || layerLookup.Count != layers.Count)
            BuildLookup();

        if (layerLookup.TryGetValue(kind, out var existing))
        {
            if (existing.parent == null)
                existing.parent = CreateLayerParent(kind);
            return existing;
        }

        var bucket = new LayerBucket
        {
            kind = kind,
            parent = CreateLayerParent(kind),
            objects = new List<GameObject>()
        };

        layers.Add(bucket);
        layerLookup[kind] = bucket;
        return bucket;
    }

    Transform CreateLayerParent(ElementLayerKind kind)
    {
        var go = new GameObject(kind.ToString());
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    /// <summary>
    /// Register a new manufactured GameObject into the appropriate layer bucket.
    /// ManufactureGO should call this whenever it instantiates something.
    /// </summary>
    public void RegisterInstance(ElementLayerKind kind, GameObject instance)
    {
        if (instance == null) return;

        var bucket = GetOrCreateLayer(kind);
        bucket.objects.Add(instance);
    }

    /// <summary>
    /// Destroy all manufactured GameObjects and clear the layer buckets.
    /// Does not touch the WarehouseGO GameObject itself.
    /// </summary>
    public void ClearAll()
    {
        foreach (var layer in layers)
        {
            if (layer == null) continue;

            if (layer.objects != null)
            {
                foreach (var go in layer.objects)
                {
                    if (go != null)
                        Destroy(go);
                }
                layer.objects.Clear();
            }

            if (layer.parent != null)
            {
                // Destroy children under this parent transform
                for (int i = layer.parent.childCount - 1; i >= 0; i--)
                {
                    var child = layer.parent.GetChild(i);
                    if (child != null)
                        Destroy(child.gameObject);
                }
            }
        }
    }

    /// <summary>
    /// Example hook if you later want to find all GameObjects for a given layer.
    /// </summary>
    public IReadOnlyList<GameObject> GetObjects(ElementLayerKind kind)
    {
        if (layerLookup == null || layerLookup.Count != layers.Count)
            BuildLookup();

        if (layerLookup.TryGetValue(kind, out var bucket))
            return bucket.objects;

        return Array.Empty<GameObject>();
    }
}