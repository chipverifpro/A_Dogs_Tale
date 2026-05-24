using System.Collections.Generic;
using DogGame;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

public partial class TopPulldown
{
    private void RefreshTargetButtonPreview(bool force = false)
    {
        if (targetPreviewImage == null)
            return;

        WorldObject previewObject = GetCurrentTargetPreviewWorldObject();
        if (!force && previewObject == targetPreviewedAgent && targetPreviewClone != null)
            return;

        BuildTargetPreviewClone(previewObject);
    }

    private WorldObject GetCurrentTargetPreviewWorldObject()
    {
        ScentSource selectedSource = GetSelectedTargetScent();
        if (selectedSource == null)
            return GetCurrentControlledWorldObject();

        return ResolveScentSourceWorldObject(selectedSource);
    }

    private ScentSource GetSelectedTargetScent()
    {
        return EnsureDir() && dir.scentRegistry != null
            ? dir.scentRegistry.SelectedTargetScent
            : null;
    }

    private WorldObject ResolveScentSourceWorldObject(ScentSource scentSource)
    {
        if (scentSource == null)
            return null;

        if (scentSource.agent != null)
            return scentSource.agent;

        if (scentSource.agentId < 0)
            return null;

        if (EnsureDir() && dir.worldObjectRegistry != null && dir.worldObjectRegistry.TryGet(scentSource.agentId, out WorldObject dirObject))
            return dirObject;

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        return registry != null && registry.TryGet(scentSource.agentId, out WorldObject registryObject)
            ? registryObject
            : null;
    }

    private void EnsureTargetPreviewWorld()
    {
        if (targetPreviewWorldRoot != null)
            return;

        targetPreviewWorldRoot = new GameObject("TopPulldownTargetPreviewWorld");
        targetPreviewWorldRoot.hideFlags = HideFlags.HideAndDontSave;
        targetPreviewWorldRoot.transform.position = TargetPreviewAnchorPosition;

        GameObject cameraObject = new("TargetPreviewCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.transform.SetParent(targetPreviewWorldRoot.transform, false);
        targetPreviewCamera = cameraObject.AddComponent<Camera>();
        targetPreviewCamera.enabled = false;
        targetPreviewCamera.clearFlags = CameraClearFlags.SolidColor;
        targetPreviewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        targetPreviewCamera.orthographic = true;
        targetPreviewCamera.nearClipPlane = 0.01f;
        targetPreviewCamera.farClipPlane = 100f;

        GameObject lightObject = new("TargetPreviewLight");
        lightObject.hideFlags = HideFlags.HideAndDontSave;
        lightObject.transform.SetParent(targetPreviewWorldRoot.transform, false);
        targetPreviewLight = lightObject.AddComponent<Light>();
        targetPreviewLight.type = LightType.Directional;
        targetPreviewLight.intensity = 1.25f;
        targetPreviewLight.color = Color.white;
        targetPreviewLight.shadows = LightShadows.None;
        targetPreviewLight.transform.rotation = Quaternion.Euler(35f, 135f, 0f);

        EnsureTargetPreviewTexture();
    }

    private void EnsureTargetPreviewTexture()
    {
        if (targetPreviewTexture != null)
            return;

        targetPreviewTexture = new RenderTexture(384, 384, 16, RenderTextureFormat.ARGB32);
        targetPreviewTexture.name = "TopPulldownTargetPreviewRT";
        targetPreviewTexture.Create();

        if (targetPreviewImage != null)
            targetPreviewImage.texture = targetPreviewTexture;
        if (targetPreviewCamera != null)
            targetPreviewCamera.targetTexture = targetPreviewTexture;
    }

    private void BuildTargetPreviewClone(WorldObject agent)
    {
        DestroyTargetPreviewClone();
        targetPreviewedAgent = agent;

        if (agent == null)
        {
            ClearTargetPreviewTexture();
            return;
        }

        EnsureTargetPreviewWorld();
        targetPreviewClone = CreateTargetVisualClone(agent.gameObject);
        targetPreviewClone.name = $"{agent.name}_TargetButtonPreview";
        targetPreviewClone.hideFlags = HideFlags.HideAndDontSave;
        targetPreviewClone.transform.SetParent(targetPreviewWorldRoot.transform, false);
        targetPreviewClone.transform.position = TargetPreviewAnchorPosition;

        CenterTargetPreviewClone(targetPreviewClone);
        RenderTargetPreview();
    }

    private void CenterTargetPreviewClone(GameObject clone)
    {
        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            targetPreviewFramingRadius = 1f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        clone.transform.position += TargetPreviewAnchorPosition - bounds.center;

        Bounds centeredBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            centeredBounds.Encapsulate(renderers[i].bounds);

        targetPreviewFramingRadius = Mathf.Max(centeredBounds.extents.y, Mathf.Max(centeredBounds.extents.x, centeredBounds.extents.z));
        if (targetPreviewFramingRadius < 0.1f)
            targetPreviewFramingRadius = 0.5f;
    }

    private void SpinTargetButtonPreview()
    {
        if (targetPreviewClone == null)
            return;

        targetPreviewClone.transform.RotateAround(
            TargetPreviewAnchorPosition,
            Vector3.up,
            targetPreviewSpinDegreesPerSecond * Time.unscaledDeltaTime);

        RenderTargetPreview();
    }

    private void RenderTargetPreview()
    {
        if (targetPreviewCamera == null)
            return;

        float distance = Mathf.Max(2f, targetPreviewFramingRadius * 4f);
        float cameraHeight = Mathf.Tan(targetPreviewViewAngleDegrees * Mathf.Deg2Rad) * distance;
        targetPreviewCamera.transform.position = TargetPreviewAnchorPosition + new Vector3(0f, cameraHeight, -distance);
        targetPreviewCamera.transform.LookAt(TargetPreviewAnchorPosition + new Vector3(0f, targetPreviewFramingRadius * 0.1f, 0f));
        float figureScale = Mathf.Max(0.01f, targetPreviewFigureScale);
        targetPreviewCamera.orthographicSize = (targetPreviewFramingRadius * 1.45f) / figureScale;
        targetPreviewCamera.Render();
    }

    private void ClearTargetPreviewTexture()
    {
        if (targetPreviewTexture == null)
            return;

        RenderTexture previousActiveTexture = RenderTexture.active;
        RenderTexture.active = targetPreviewTexture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = previousActiveTexture;
    }

    private static GameObject CreateTargetVisualClone(GameObject sourceRoot)
    {
        Dictionary<Transform, Transform> transformMap = new();

        GameObject cloneRoot = new(sourceRoot.name);
        CopyTargetTransform(sourceRoot.transform, cloneRoot.transform);
        transformMap[sourceRoot.transform] = cloneRoot.transform;

        Transform[] sourceTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < sourceTransforms.Length; i++)
        {
            Transform source = sourceTransforms[i];
            GameObject child = new(source.name);
            Transform childTransform = child.transform;
            childTransform.SetParent(transformMap[source.parent], false);
            CopyTargetTransform(source, childTransform);
            transformMap[source] = childTransform;
        }

        for (int i = 0; i < sourceTransforms.Length; i++)
        {
            Transform source = sourceTransforms[i];
            Transform destination = transformMap[source];

            MeshFilter sourceMeshFilter = source.GetComponent<MeshFilter>();
            MeshRenderer sourceMeshRenderer = source.GetComponent<MeshRenderer>();
            if (sourceMeshFilter != null && sourceMeshRenderer != null)
                CopyTargetMeshRenderer(sourceMeshFilter, sourceMeshRenderer, destination.gameObject);

            SkinnedMeshRenderer sourceSkinnedRenderer = source.GetComponent<SkinnedMeshRenderer>();
            if (sourceSkinnedRenderer != null)
                CopyTargetSkinnedMeshRenderer(sourceSkinnedRenderer, destination.gameObject, transformMap);
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

    private static void CopyTargetTransform(Transform source, Transform destination)
    {
        destination.localPosition = source.localPosition;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    private static void CopyTargetMeshRenderer(MeshFilter sourceFilter, MeshRenderer sourceRenderer, GameObject destination)
    {
        MeshFilter destinationFilter = destination.AddComponent<MeshFilter>();
        destinationFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer destinationRenderer = destination.AddComponent<MeshRenderer>();
        destinationRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        destinationRenderer.enabled = true;
    }

    private static void CopyTargetSkinnedMeshRenderer(
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

    private void DestroyTargetPreviewClone()
    {
        if (targetPreviewClone == null)
            return;

        if (Application.isPlaying)
            Destroy(targetPreviewClone);
        else
            DestroyImmediate(targetPreviewClone);

        targetPreviewClone = null;
        targetPreviewedAgent = null;
    }

    private void DestroyTargetPreviewWorld()
    {
        if (targetPreviewWorldRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(targetPreviewWorldRoot);
        else
            DestroyImmediate(targetPreviewWorldRoot);

        targetPreviewWorldRoot = null;
        targetPreviewCamera = null;
        targetPreviewLight = null;
    }

    private void ReleaseTargetPreviewTexture()
    {
        if (targetPreviewCamera != null)
            targetPreviewCamera.targetTexture = null;

        if (targetPreviewImage != null)
            targetPreviewImage.texture = null;

        if (targetPreviewTexture != null)
        {
            targetPreviewTexture.Release();
            if (Application.isPlaying)
                Destroy(targetPreviewTexture);
            else
                DestroyImmediate(targetPreviewTexture);
        }

        targetPreviewTexture = null;
    }
}
