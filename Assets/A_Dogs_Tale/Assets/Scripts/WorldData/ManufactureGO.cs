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

        for (int i = 0; i < instances.Count; i++)
        {
            var inst = instances[i];
            ManufactureInstance(inst, baseParent);

            if (yieldEveryNInstances > 0 && (i % yieldEveryNInstances) == 0)
                yield return null;
        }

        Debug.Log("ManufactureGO: Build completed.");
    }

    /// <summary>
    /// Creates a single GameObject for the given instance and registers it with the WarehouseGO.
    /// </summary>
    void ManufactureInstance_old(ElementInstanceData inst, Transform baseParent)
    {
        var archetype = elementStore.GetArchetype(inst.archetypeId);
        if (archetype == null)
        {
            Debug.LogWarning($"ManufactureGO: No archetype found for id '{inst.archetypeId}'. Skipping instance.");
            return;
        }

        // Create or get the per-layer parent under the WarehouseGO
        var bucket = warehouse.GetOrCreateLayer(inst.layerKind);
        Transform parent = bucket.parent != null ? bucket.parent : baseParent;

        // Create a new GameObject
        string goName = $"{inst.layerKind}_{archetype.displayName}";
        var go = new GameObject(goName);

        // Parent first, then set transform to world-space values
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.position = inst.position;
        go.transform.rotation = inst.rotation;
        go.transform.localScale = inst.scale;

        // Basic rendering setup from archetype
        if (archetype.mesh != null)
        {
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = archetype.mesh;
        }

        if (archetype.material != null)
        {
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = archetype.material;

            // Resolve color: use instance color if alpha >= 0, otherwise archetype default
            Color finalColor = inst.color;
            if (finalColor.a < 0f)
                finalColor = archetype.defaultColor;

            // For now, use a unique material per object like your existing code did.
            // Later you can switch to MaterialPropertyBlock for instancing.
            mr.material.color = finalColor;

            // Shadow flags (optional, based on archetype render flags)
            var flags = archetype.renderFlags;
            bool casts = (flags & ElementRenderFlags.CastsShadows) != 0;
            bool receives = (flags & ElementRenderFlags.ReceivesShadows) != 0;

            mr.shadowCastingMode = casts ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = receives;
        }

        // Apply Unity layer/tag hints from archetype
        if (archetype.unityLayer >= 0 && archetype.unityLayer < 32)
            go.layer = archetype.unityLayer;

        if (!string.IsNullOrEmpty(archetype.unityTag))
        {
            try
            {
                go.tag = archetype.unityTag;
            }
            catch
            {
                // Tag may not exist; ignore instead of spamming errors.
            }
        }

        // Static batching hint
        if ((archetype.renderFlags & ElementRenderFlags.StaticBatch) != 0)
        {
            go.isStatic = true;
        }

        // TODO: if you later want special components for certain layer kinds
        // (e.g. DoorController on DoorLeaf, Light for Light elements),
        // you can add them here by checking inst.layerKind or archetype.kind.

        warehouse.RegisterInstance(inst.layerKind, go);
    }

    void ManufactureInstance_old2(ElementInstanceData inst, Transform baseParent)
    {
        var archetype = elementStore.GetArchetype(inst.archetypeId);
        if (archetype == null)
        {
            Debug.LogWarning($"ManufactureGO: No archetype found for id '{inst.archetypeId}'. Skipping instance.");
            return;
        }

        // Get per-layer parent under WarehouseGO
        var bucket = warehouse.GetOrCreateLayer(inst.layerKind);
        Transform parent = bucket.parent != null ? bucket.parent : baseParent;

        GameObject go = null;

        // 1) Prefer prefab if assigned
        if (archetype.prefab != null)
        {
            go = Instantiate(archetype.prefab, parent);
            go.name = $"{inst.layerKind}_{archetype.displayName}";
            // Per-instance color override on prefabs:
            var rend = go.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                Color finalColor = inst.color.a < 0f ? archetype.defaultColor : inst.color;
                var mpb = new MaterialPropertyBlock();
                rend.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", finalColor);
                rend.SetPropertyBlock(mpb);
            }
        }
        else
        {
            // 2) Fallback: procedural GO using mesh + material
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

                // Resolve color: instance override if alpha >= 0, else archetype default
                Color finalColor = inst.color;
                if (finalColor.a < 0f)
                    finalColor = archetype.defaultColor;

                mr.material.color = finalColor;

                var flags = archetype.renderFlags;
                bool casts = (flags & ElementRenderFlags.CastsShadows) != 0;
                bool receives = (flags & ElementRenderFlags.ReceivesShadows) != 0;

                mr.shadowCastingMode = casts ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.receiveShadows = receives;
            }
        }

        if (go == null)
            return;

        // Apply transform AFTER parenting so position is world-space like your generator
        go.transform.position = inst.position;
        go.transform.rotation = inst.rotation;
        go.transform.localScale = inst.scale;

        // Layer / tag from archetype
        if (archetype.unityLayer >= 0 && archetype.unityLayer < 32)
            go.layer = archetype.unityLayer;

        if (!string.IsNullOrEmpty(archetype.unityTag))
        {
            try { go.tag = archetype.unityTag; }
            catch { /* tag might not exist; ignore */ }
        }

        // Static batching hint
        if ((archetype.renderFlags & ElementRenderFlags.StaticBatch) != 0)
            go.isStatic = true;

        warehouse.RegisterInstance(inst.layerKind, go);
    }

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
        go.transform.position    = inst.position;
        go.transform.rotation    = inst.rotation;
        go.transform.localScale  = inst.scale;

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
    /// Applies the instance color override to any Renderer(s) on this GO.
    /// Uses archetype.defaultColor if inst.color.a < 0.
    /// </summary>
    void ApplyInstanceColor(GameObject go, ElementArchetype archetype, ElementInstanceData inst)
    {
        // Decide final color
        Color finalColor = inst.color;
        if (finalColor.a < 0f)
            finalColor = archetype.defaultColor;

        // If no color data, bail early
        // (You can skip this if you always want to force at least defaultColor.)
        // if (finalColor.a < 0f) return;

        // Apply to all renderers in children so prefabs with child meshes work too
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return;

        // Safest: MaterialPropertyBlock so we don't break instancing later
        var mpb = new MaterialPropertyBlock();

        foreach (var r in renderers)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", finalColor);   // assumes shader uses _Color
            mpb.SetColor("_BaseColor", finalColor); // assumes _BaseColor (HDRP/Lit)
            r.SetPropertyBlock(mpb);

            // Also apply shadow flags here if you want, based on archetype.renderFlags
            var flags = archetype.renderFlags;
            bool casts    = (flags & ElementRenderFlags.CastsShadows)    != 0;
            bool receives = (flags & ElementRenderFlags.ReceivesShadows) != 0;

            r.shadowCastingMode = casts ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows    = receives;
        }
    }
}