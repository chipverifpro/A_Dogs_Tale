using System.Collections.Generic;
using DogGame;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

internal sealed class InteractionDialogPreviewSlot
{
    public Image CircleImage;
    public RawImage Image;
    public RenderTexture Texture;
    public GameObject WorldRoot;
    public GameObject Clone;
    public Camera Camera;
    public Light Light;
    public Vector3 AnchorPosition;
    public float FramingRadius = 1f;
    public float OrthographicPadding = 2.15f;
    public Vector2 CircleSize;
    public Vector2 CircleWithArrowsSize;
    public WorldObject DisplayedObject;
}

internal static class InteractionDialogPreviewRenderer
{
    private const int PreviewRenderLayer = 31;
    private const string EmoteIconVisualInstanceName = "EmoteIconVisual";
    private const string QuestIconVisualInstanceName = "QuestRequestIconVisual";

    public static void BuildPreviewClone(InteractionDialogPreviewSlot slot, WorldObject worldObject, string label, float previewViewAngleDegrees)
    {
        if (slot == null)
            return;

        DestroyPreviewClone(slot);
        PurgePreviewWorldRenderables(slot);
        slot.DisplayedObject = worldObject;

        if (worldObject == null)
        {
            if (slot.Image != null)
            {
                slot.Image.texture = null;
                slot.Image.enabled = false;
            }

            ClearPreviewTexture(slot);
            return;
        }

        EnsurePreviewWorld(slot);
        if (slot.Image != null)
        {
            slot.Image.texture = slot.Texture;
            slot.Image.enabled = true;
        }

        slot.Clone = CreateVisualClone(worldObject.gameObject);
        slot.Clone.name = $"{worldObject.name}_Interaction{label}Preview";
        slot.Clone.hideFlags = HideFlags.HideAndDontSave;
        slot.Clone.transform.SetParent(slot.WorldRoot.transform, false);
        slot.Clone.transform.position = slot.AnchorPosition;
        SetLayerRecursive(slot.Clone, PreviewRenderLayer);

        CenterPreviewClone(slot);
        RenderPreview(slot, previewViewAngleDegrees);
    }

    public static void SpinPreview(InteractionDialogPreviewSlot slot, float previewSpinDegreesPerSecond, float previewViewAngleDegrees)
    {
        if (slot == null || slot.Clone == null)
            return;

        slot.Clone.transform.RotateAround(
            slot.AnchorPosition,
            Vector3.up,
            previewSpinDegreesPerSecond * Time.unscaledDeltaTime);

        RenderPreview(slot, previewViewAngleDegrees);
    }

    public static void ReleasePreviewSlot(InteractionDialogPreviewSlot slot)
    {
        if (slot == null)
            return;

        DestroyPreviewClone(slot);

        if (slot.Camera != null)
            slot.Camera.targetTexture = null;

        if (slot.Image != null)
            slot.Image.texture = null;

        if (slot.Texture != null)
        {
            slot.Texture.Release();
            if (Application.isPlaying)
                Object.Destroy(slot.Texture);
            else
                Object.DestroyImmediate(slot.Texture);
        }

        if (slot.WorldRoot != null)
        {
            if (Application.isPlaying)
                Object.Destroy(slot.WorldRoot);
            else
                Object.DestroyImmediate(slot.WorldRoot);
        }

        slot.Texture = null;
        slot.WorldRoot = null;
        slot.Camera = null;
        slot.Light = null;
        slot.DisplayedObject = null;
    }

    private static void EnsurePreviewWorld(InteractionDialogPreviewSlot slot)
    {
        if (slot == null || slot.WorldRoot != null)
            return;

        slot.WorldRoot = new GameObject($"{slot.Image.name}World");
        slot.WorldRoot.hideFlags = HideFlags.HideAndDontSave;
        slot.WorldRoot.transform.position = slot.AnchorPosition;

        GameObject cameraObject = new("PreviewCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.transform.SetParent(slot.WorldRoot.transform, false);
        slot.Camera = cameraObject.AddComponent<Camera>();
        slot.Camera.enabled = false;
        slot.Camera.clearFlags = CameraClearFlags.SolidColor;
        slot.Camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        slot.Camera.orthographic = true;
        slot.Camera.nearClipPlane = 0.01f;
        slot.Camera.farClipPlane = 100f;
        slot.Camera.cullingMask = 1 << PreviewRenderLayer;
        slot.Camera.allowHDR = false;
        slot.Camera.allowMSAA = false;

        GameObject lightObject = new("PreviewLight");
        lightObject.hideFlags = HideFlags.HideAndDontSave;
        lightObject.transform.SetParent(slot.WorldRoot.transform, false);
        slot.Light = lightObject.AddComponent<Light>();
        slot.Light.type = LightType.Directional;
        slot.Light.intensity = 1.25f;
        slot.Light.color = Color.white;
        slot.Light.shadows = LightShadows.None;
        slot.Light.cullingMask = 1 << PreviewRenderLayer;
        slot.Light.transform.rotation = Quaternion.Euler(35f, 135f, 0f);

        EnsurePreviewTexture(slot);
    }

    private static void EnsurePreviewTexture(InteractionDialogPreviewSlot slot)
    {
        if (slot == null || slot.Texture != null)
            return;

        slot.Texture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        slot.Texture.name = $"{slot.Image.name}RT";
        slot.Texture.useMipMap = false;
        slot.Texture.autoGenerateMips = false;
        slot.Texture.Create();

        slot.Image.texture = slot.Texture;
        slot.Camera.targetTexture = slot.Texture;
        ClearPreviewTexture(slot);
    }

    private static void RenderPreview(InteractionDialogPreviewSlot slot, float previewViewAngleDegrees)
    {
        if (slot == null || slot.Camera == null)
            return;

        float distance = Mathf.Max(2f, slot.FramingRadius * 4f);
        float cameraHeight = Mathf.Tan(previewViewAngleDegrees * Mathf.Deg2Rad) * distance;
        slot.Camera.transform.position = slot.AnchorPosition + new Vector3(0f, cameraHeight, -distance);
        slot.Camera.transform.LookAt(slot.AnchorPosition + new Vector3(0f, slot.FramingRadius * 0.1f, 0f));
        slot.Camera.orthographicSize = slot.FramingRadius * slot.OrthographicPadding;
        ClearPreviewTexture(slot);
        slot.Camera.Render();
    }

    private static void ClearPreviewTexture(InteractionDialogPreviewSlot slot)
    {
        if (slot == null || slot.Texture == null)
            return;

        RenderTexture previousActiveTexture = RenderTexture.active;
        RenderTexture.active = slot.Texture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = previousActiveTexture;
    }

    private static void PurgePreviewWorldRenderables(InteractionDialogPreviewSlot slot)
    {
        if (slot == null || slot.WorldRoot == null)
            return;

        for (int i = slot.WorldRoot.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = slot.WorldRoot.transform.GetChild(i);
            if (child == null ||
                child == (slot.Camera != null ? slot.Camera.transform : null) ||
                child == (slot.Light != null ? slot.Light.transform : null))
            {
                continue;
            }

            child.gameObject.SetActive(false);
            if (Application.isPlaying)
                Object.Destroy(child.gameObject);
            else
                Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void CenterPreviewClone(InteractionDialogPreviewSlot slot)
    {
        Renderer[] renderers = slot.Clone.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            slot.FramingRadius = 1f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        slot.Clone.transform.position += slot.AnchorPosition - bounds.center;

        Bounds centeredBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            centeredBounds.Encapsulate(renderers[i].bounds);

        slot.FramingRadius = Mathf.Max(centeredBounds.extents.y, Mathf.Max(centeredBounds.extents.x, centeredBounds.extents.z));
        if (slot.FramingRadius < 0.1f)
            slot.FramingRadius = 0.5f;
    }

    private static GameObject CreateVisualClone(GameObject sourceRoot)
    {
        Dictionary<Transform, Transform> transformMap = new();
        HashSet<Transform> skippedTransforms = new();
        WorldObject sourceRootWorldObject = sourceRoot.GetComponent<WorldObject>();

        GameObject cloneRoot = new(sourceRoot.name);
        CopyTransform(sourceRoot.transform, cloneRoot.transform);
        transformMap[sourceRoot.transform] = cloneRoot.transform;

        Transform[] sourceTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < sourceTransforms.Length; i++)
        {
            Transform source = sourceTransforms[i];
            if (ShouldSkipPreviewCloneTransform(source, sourceRoot.transform, sourceRootWorldObject) ||
                skippedTransforms.Contains(source.parent))
            {
                skippedTransforms.Add(source);
                continue;
            }

            GameObject child = new(source.name);
            Transform childTransform = child.transform;
            childTransform.SetParent(transformMap[source.parent], false);
            CopyTransform(source, childTransform);
            transformMap[source] = childTransform;
        }

        for (int i = 0; i < sourceTransforms.Length; i++)
        {
            Transform source = sourceTransforms[i];
            if (!transformMap.TryGetValue(source, out Transform destination))
                continue;

            MeshFilter sourceMeshFilter = source.GetComponent<MeshFilter>();
            MeshRenderer sourceMeshRenderer = source.GetComponent<MeshRenderer>();
            if (sourceMeshFilter != null && sourceMeshRenderer != null)
                CopyMeshRenderer(sourceMeshFilter, sourceMeshRenderer, destination.gameObject);

            SkinnedMeshRenderer sourceSkinnedRenderer = source.GetComponent<SkinnedMeshRenderer>();
            if (sourceSkinnedRenderer != null)
                CopySkinnedMeshRenderer(sourceSkinnedRenderer, destination.gameObject, transformMap);
        }

        Renderer[] renderers = cloneRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
            renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
        }

        return cloneRoot;
    }

    private static bool ShouldSkipPreviewCloneTransform(
        Transform source,
        Transform sourceRoot,
        WorldObject sourceRootWorldObject)
    {
        if (source == null || source == sourceRoot)
            return false;

        if (source.GetComponentInParent<EmoteIconSpinner>() != null)
            return true;

        for (Transform current = source; current != null && current != sourceRoot; current = current.parent)
        {
            string objectName = current.name;
            if (objectName == EmoteIconVisualInstanceName || objectName == QuestIconVisualInstanceName)
                return true;
        }

        WorldObject[] parentWorldObjects = source.GetComponentsInParent<WorldObject>(true);
        for (int i = 0; i < parentWorldObjects.Length; i++)
        {
            WorldObject parentWorldObject = parentWorldObjects[i];
            if (parentWorldObject != null && parentWorldObject != sourceRootWorldObject)
                return true;
        }

        return false;
    }

    private static void DestroyPreviewClone(InteractionDialogPreviewSlot slot)
    {
        if (slot == null || slot.Clone == null)
            return;

        slot.Clone.SetActive(false);

        if (Application.isPlaying)
            Object.Destroy(slot.Clone);
        else
            Object.DestroyImmediate(slot.Clone);

        slot.Clone = null;
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++)
            SetLayerRecursive(rootTransform.GetChild(i).gameObject, layer);
    }

    private static void CopyTransform(Transform source, Transform destination)
    {
        destination.localPosition = source.localPosition;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    private static void CopyMeshRenderer(MeshFilter sourceFilter, MeshRenderer sourceRenderer, GameObject destination)
    {
        MeshFilter destinationFilter = destination.AddComponent<MeshFilter>();
        destinationFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer destinationRenderer = destination.AddComponent<MeshRenderer>();
        destinationRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        destinationRenderer.enabled = true;
    }

    private static void CopySkinnedMeshRenderer(
        SkinnedMeshRenderer sourceRenderer,
        GameObject destination,
        Dictionary<Transform, Transform> transformMap)
    {
        SkinnedMeshRenderer destinationRenderer = destination.AddComponent<SkinnedMeshRenderer>();
        destinationRenderer.sharedMesh = sourceRenderer.sharedMesh;
        destinationRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        destinationRenderer.enabled = true;
        destinationRenderer.localBounds = sourceRenderer.localBounds;
        destinationRenderer.updateWhenOffscreen = sourceRenderer.updateWhenOffscreen;
        destinationRenderer.rootBone = sourceRenderer.rootBone != null && transformMap.TryGetValue(sourceRenderer.rootBone, out Transform mappedRootBone)
            ? mappedRootBone
            : null;

        Transform[] sourceBones = sourceRenderer.bones;
        Transform[] destinationBones = new Transform[sourceBones.Length];
        for (int i = 0; i < sourceBones.Length; i++)
        {
            Transform bone = sourceBones[i];
            if (bone != null && transformMap.TryGetValue(bone, out Transform mappedBone))
                destinationBones[i] = mappedBone;
        }

        destinationRenderer.bones = destinationBones;
    }
}
