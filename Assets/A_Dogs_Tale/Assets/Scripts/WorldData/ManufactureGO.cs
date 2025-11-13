using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Converts logical data in ElementStore into manufactured GameObjects
/// under a WarehouseGO. For now this is a straightforward Instantiate-like
/// builder using ElementArchetype.mesh/material and ElementInstanceData.
/// Later, some layers can be switched to GPU-only (instancing/indirect).
/// </summary>
public class ManufactureGO : MonoBehaviour
{
    [Header("Input data (logical elements)")]
    [SerializeField] private ElementStore elementStore;

    [Header("Output (manufactured GameObjects)")]
    [SerializeField] private WarehouseGO warehouse;

    [Header("Build options")]
    [Tooltip("How many instances to build before yielding to keep the frame responsive.")]
    [SerializeField] private int yieldEveryNInstances = 500;

    [Tooltip("Optional root transform for all manufactured objects. If null, WarehouseGO.transform is used.")]
    [SerializeField] private Transform rootParentOverride;

    /// <summary>
    /// Public entry point: build everything in the ElementStore into the WarehouseGO.
    /// </summary>
    public void BuildAll()
    {
        StartCoroutine(BuildAllCoroutine());
    }

    /// <summary>
    /// Coroutine that walks through the ElementStore and makes GameObjects.
    /// </summary>
    public IEnumerator BuildAllCoroutine()
    {
        if (elementStore == null)
        {
            Debug.LogError("ManufactureGO: ElementStore is not assigned.");
            yield break;
        }

        if (warehouse == null)
        {
            Debug.LogError("ManufactureGO: WarehouseGO is not assigned.");
            yield break;
        }

        // Clear old manufactured objects
        warehouse.ClearAll();

        // Prepare lookup tables in the store
        elementStore.BuildArchetypeLookup();

        // Pull a flat list of all instances (across layers)
        var instances = elementStore.BuildRuntimeInstanceList();
        if (instances == null || instances.Count == 0)
        {
            Debug.Log("ManufactureGO: No instances found in ElementStore.");
            yield break;
        }

        Debug.Log($"ManufactureGO: Building {instances.Count} instances.");

        var baseParent = rootParentOverride != null ? rootParentOverride : warehouse.transform;

        for (int dlIdx = elementStore.layers.Count-1; dlIdx >= 0; dlIdx--)
        {
            var dataLayer = elementStore.layers[dlIdx];
            if (dataLayer == null || dataLayer.instances == null) continue;

            for (int iIdx = 0; iIdx < dataLayer.instances.Count; iIdx++)
            {
                var inst = dataLayer.instances[iIdx];
                ManufactureInstance(inst, baseParent);
                // ManufactureInstance internally calls warehouse.RegisterInstance(inst.layerKind, go)
                // in that same order, so indices will align.

                if (iIdx % (yieldEveryNInstances+1) == 0)
                    yield return null;
            }
        }

        Debug.Log("ManufactureGO: Build completed.");
    }

    /// <summary>
    /// Build GameObjects for any instances in the given layer kind that don't
    /// yet have GOs in WarehouseGO. Assumes instances are appended at the end.
    /// </summary>
    public void BuildNewInstancesForLayer(ElementLayerKind kind)
    {
        Debug.LogWarning("BuildNewInstanceForLayer begins");
        if (elementStore == null || warehouse == null)
            return;

        if (elementStore.layers == null)
            return;

        // Find the data layer for this kind
        var dataLayer = elementStore.layers.Find(l => l != null && l.kind == kind);
        if (dataLayer == null || dataLayer.instances == null)
            return;

        // Get or create the corresponding bucket in the warehouse
        var bucket = warehouse.GetOrCreateLayer(kind);
        if (bucket.objects == null)
            bucket.objects = new System.Collections.Generic.List<GameObject>();

        int existingCount = bucket.objects.Count;
        int total = dataLayer.instances.Count;

        if (existingCount >= total)
            return; // nothing new to build

        var baseParent = rootParentOverride != null ? rootParentOverride : warehouse.transform;

        for (int i = existingCount; i < total; i++)
        {
            var inst = dataLayer.instances[i];
            ManufactureInstance(inst, baseParent); // this will RegisterInstance internally
            Debug.LogWarning("Manufactured instance");
        }
    }

    /// <summary>
    /// Creates a single GameObject for the given instance and registers it with the WarehouseGO.
    /// </summary>
    void ManufactureInstance(ElementInstanceData inst, Transform baseParent)
    {
        var archetype = elementStore.GetArchetype(inst.archetypeId);
        if (archetype == null)
        {
            Debug.LogWarning($"ManufactureGO: No archetype found for id '{inst.archetypeId}'. Skipping instance.");
            return;
        }

        var bucket = warehouse.GetOrCreateLayer(inst.layerKind);
        Transform parent = bucket.parent != null ? bucket.parent : baseParent;

        GameObject go = null;

        // 1) Prefer prefab if assigned
        if (archetype.prefab != null)
        {
            go = Instantiate(archetype.prefab, parent);
            go.name = $"{inst.layerKind}_{archetype.displayName}";
        }
        else
        {
            // 2) Fallback: build GO from mesh + material
            go = new GameObject($"{inst.layerKind}_{archetype.displayName}");
            go.transform.SetParent(parent, worldPositionStays: false);

            if (archetype.mesh != null)
            {
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = archetype.mesh;
            }

            if (archetype.material != null)
            {
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = archetype.material;
            }
        }

        if (go == null)
            return;

        // Transform
        go.transform.position = inst.position;
        go.transform.rotation = inst.rotation;
        go.transform.localScale = inst.scale;

        inst.dirtyFlags = ElementUpdateFlags.All; // mark dirty to apply changes

        // Layer / tag from archetype
        if (archetype.unityLayer >= 0 && archetype.unityLayer < 32)
            go.layer = archetype.unityLayer;

        if (!string.IsNullOrEmpty(archetype.unityTag))
        {
            try { go.tag = archetype.unityTag; }
            catch { /* tag might not exist; ignore */ }
        }

        // Apply per-instance color (works for prefab and non-prefab)
        ApplyInstanceColor(go, archetype, inst);

        // Static batching hint
        if ((archetype.renderFlags & ElementRenderFlags.StaticBatch) != 0)
            go.isStatic = true;

        warehouse.RegisterInstance(inst.layerKind, go);
    }

    /// <summary>
    /// Apply any pending changes in ElementStore (based on dirtyFlags)
    /// to the already manufactured GameObjects in WarehouseGO.
    /// Currently supports Color; can be extended for transforms, etc.
    /// </summary>
    public void ApplyPendingUpdates()
    {
        //BuildAll(); // DEBUG: rebuild everything
        //return;
        Debug.Log("ManufactureGO: Applying pending updates to manufactured GameObjects.");
        if (elementStore == null || warehouse == null)
        {
            Debug.LogWarning("ManufactureGO: elementStore or warehouse is null. Aborting pending updates.");
            return;
        }

        if (elementStore.layers == null || elementStore.layers.Count == 0)
        {
            Debug.LogWarning("ManufactureGO: No layers found in elementStore. Aborting pending updates.");
            return;
        }

        Debug.Log($"ManufactureGO: elementStore has {elementStore.layers.Count} layers.");
        // For each layer in the data store
        foreach (var dataLayer in elementStore.layers)
        {
            if (dataLayer == null || dataLayer.instances == null)
            {
                Debug.LogWarning($"ManufactureGO: Skipping null dataLayer or instances for layer {dataLayer?.kind}.");
                continue;
            }

            // Matching GO bucket in Warehouse
            var bucket = warehouse.GetLayerBucket(dataLayer.kind);
            if (bucket == null)
            {
                Debug.LogWarning($"ManufactureGO: No warehouse bucket found for layer {dataLayer.kind}. Skipping.");
                continue;
            }

            if (bucket.objects == null)
            {
                Debug.LogWarning($"ManufactureGO: No objects found in warehouse bucket for layer {dataLayer.kind}. Skipping.");
                continue;
            }

            // Instances and GOs should be in the same order they were built.
            int count = Mathf.Min(dataLayer.instances.Count, bucket.objects.Count);

            Debug.Log($"ManufactureGO: Applying {count} updates for layer {dataLayer.kind}, {dataLayer.instances.Count} dataLayer.instances, {bucket.objects.Count} GameObjects.");
            for (int i = 0; i < count; i++)
            {
                var inst = dataLayer.instances[i];
                if (inst.dirtyFlags == ElementUpdateFlags.None)
                    continue;

                GameObject go = bucket.objects[i];
                if (go == null)
                {
                    Debug.LogWarning($"ManufactureGO: Missing GameObject for instance {i} of layer {dataLayer.kind}.");
                    continue;
                }

                var archetype = elementStore.GetArchetype(inst.archetypeId);
                if (archetype == null)
                {
                    Debug.LogWarning($"ManufactureGO: Missing archetype for instance {i} of layer {dataLayer.kind}.");
                    continue;
                }

                // Apply color if it changed
                if ((inst.dirtyFlags & (ElementUpdateFlags.Color | ElementUpdateFlags.All)) != 0)
                {
                    Debug.Log($"ManufactureGO: Applying color update to instance {i} of layer {dataLayer.kind}. GO.name={go.name}, alpha={inst.color.a}");
                    ApplyInstanceColor(go, archetype, inst);
                }

                // Clear flags after applying
                inst.dirtyFlags = ElementUpdateFlags.None;
                dataLayer.instances[i] = inst; // write back
            }
        }
    }

    void ApplyInstanceColor(GameObject go, ElementArchetype archetype, ElementInstanceData inst)
    {
        if (go == null) return;

        // Decide final color
        Color finalColor = inst.color;
        if (finalColor.a < 0f)
            finalColor = archetype.defaultColor;

        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return;

        var mpb = new MaterialPropertyBlock();

        foreach (var r in renderers)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_Color",     finalColor);
            mpb.SetColor("_BaseColor", finalColor); // URP/HDRP compatibility
            r.SetPropertyBlock(mpb);

            // Optional: shadow flags from archetype
            var flags = archetype.renderFlags;
            bool casts    = (flags & ElementRenderFlags.CastsShadows)    != 0;
            bool receives = (flags & ElementRenderFlags.ReceivesShadows) != 0;
            r.shadowCastingMode = casts ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows    = receives;
        }
    }

}