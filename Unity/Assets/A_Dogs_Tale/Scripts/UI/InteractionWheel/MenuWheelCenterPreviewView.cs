#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DogGame.UI.InteractionWheel
{
    public sealed class MenuWheelCenterPreviewView : MonoBehaviour
    {
        private static readonly Vector3 PreviewAnchorPosition = new(50000f, 50000f, 50000f);

        private RectTransform rectTransform = null!;
        private RawImage rawImage = null!;
        private RenderTexture? renderTexture;
        private GameObject? previewWorldRoot;
        private GameObject? nurseryRoot;
        private Camera? previewCamera;
        private Light? previewLight;
        private GameObject? previewClone;
        private float framingRadius = 1f;

        private void Awake()
        {
            EnsureInitialized();
            Hide();
        }

        private void OnDestroy()
        {
            DestroyPreviewClone();
            DestroyPreviewWorldRoot();
            ReleaseRenderTexture();
        }

        public void Show(WorldObject actor)
        {
            EnsureInitialized();
            BuildPreviewClone(actor);
            rawImage.gameObject.SetActive(previewClone != null);
            SetFacingDirection(Vector2.up);
        }

        public void Hide()
        {
            if (rawImage != null)
                rawImage.gameObject.SetActive(false);

            DestroyPreviewClone();
        }

        public void ApplyLayout(Vector2 wheelCenterOffset, MenuWheelResolvedLayout layout)
        {
            EnsureInitialized();

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = wheelCenterOffset;
            rectTransform.sizeDelta = Vector2.one * layout.PreviewSize;

            EnsureRenderTexture(layout.PreviewSize);
            UpdatePreviewCamera();
        }

        public void SetFacingDirection(Vector2 wheelDirection)
        {
            if (previewClone == null || wheelDirection.sqrMagnitude < 0.001f)
                return;

            Vector3 facing = new Vector3(wheelDirection.x, 0f, -wheelDirection.y);
            if (facing.sqrMagnitude < 0.001f)
                return;

            previewClone.transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            UpdatePreviewCamera();
        }

        private void EnsureInitialized()
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            if (rawImage == null)
            {
                rawImage = GetComponent<RawImage>();
                if (rawImage == null)
                    rawImage = gameObject.AddComponent<RawImage>();

                rawImage.raycastTarget = false;
                rawImage.color = Color.white;
            }

            if (previewWorldRoot == null)
            {
                previewWorldRoot = new GameObject("MenuWheelCenterPreviewWorld");
                previewWorldRoot.hideFlags = HideFlags.HideAndDontSave;
                previewWorldRoot.transform.position = PreviewAnchorPosition;

                nurseryRoot = new GameObject("Nursery");
                nurseryRoot.hideFlags = HideFlags.HideAndDontSave;
                nurseryRoot.transform.SetParent(previewWorldRoot.transform, false);
                nurseryRoot.SetActive(false);

                GameObject cameraObject = new("PreviewCamera");
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                cameraObject.transform.SetParent(previewWorldRoot.transform, false);
                previewCamera = cameraObject.AddComponent<Camera>();
                previewCamera.enabled = false;
                previewCamera.clearFlags = CameraClearFlags.SolidColor;
                previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                previewCamera.orthographic = true;
                previewCamera.nearClipPlane = 0.01f;
                previewCamera.farClipPlane = 50f;

                GameObject lightObject = new("PreviewLight");
                lightObject.hideFlags = HideFlags.HideAndDontSave;
                lightObject.transform.SetParent(previewWorldRoot.transform, false);
                previewLight = lightObject.AddComponent<Light>();
                previewLight.type = LightType.Directional;
                previewLight.intensity = 1.2f;
                previewLight.color = Color.white;
                previewLight.shadows = LightShadows.None;
                lightObject.transform.rotation = Quaternion.Euler(35f, 135f, 0f);
            }
        }

        private void EnsureRenderTexture(float previewSize)
        {
            int dimension = Mathf.Clamp(Mathf.CeilToInt(previewSize), 128, 1024);

            if (renderTexture != null && renderTexture.width == dimension && renderTexture.height == dimension)
                return;

            ReleaseRenderTexture();

            renderTexture = new RenderTexture(dimension, dimension, 16, RenderTextureFormat.ARGB32);
            renderTexture.name = "MenuWheelCenterPreviewRT";
            renderTexture.Create();

            rawImage.texture = renderTexture;

            if (previewCamera != null)
                previewCamera.targetTexture = renderTexture;
        }

        private void BuildPreviewClone(WorldObject actor)
        {
            DestroyPreviewClone();

            EnsureInitialized();

            if (nurseryRoot == null)
                return;

            GameObject donor = Instantiate(actor.gameObject, nurseryRoot.transform, false);
            donor.name = $"{actor.name}_WheelDonor";
            donor.hideFlags = HideFlags.HideAndDontSave;

            previewClone = CreateVisualClone(donor);
            previewClone.name = $"{actor.name}_WheelPreview";
            previewClone.hideFlags = HideFlags.HideAndDontSave;
            previewClone.transform.SetParent(previewWorldRoot!.transform, false);
            previewClone.transform.position = PreviewAnchorPosition;

            CenterPreviewClone(previewClone);
            DestroyImmediate(donor);
            UpdatePreviewCamera();
        }

        private void CenterPreviewClone(GameObject clone)
        {
            Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                framingRadius = 1f;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Vector3 offset = PreviewAnchorPosition - bounds.center;
            clone.transform.position += offset;

            Bounds centeredBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                centeredBounds.Encapsulate(renderers[i].bounds);

            framingRadius = Mathf.Max(
                centeredBounds.extents.y,
                Mathf.Max(centeredBounds.extents.x, centeredBounds.extents.z));

            if (framingRadius < 0.1f)
                framingRadius = 0.5f;
        }

        private void UpdatePreviewCamera()
        {
            if (previewCamera == null)
                return;

            float distance = Mathf.Max(2f, framingRadius * 4f);
            previewCamera.transform.position = PreviewAnchorPosition + new Vector3(0f, framingRadius * 0.3f, -distance);
            previewCamera.transform.LookAt(PreviewAnchorPosition + new Vector3(0f, framingRadius * 0.25f, 0f));
            previewCamera.orthographicSize = framingRadius * 1.35f;
            previewCamera.Render();
        }

        private static GameObject CreateVisualClone(GameObject donorRoot)
        {
            var transformMap = new Dictionary<Transform, Transform>();

            GameObject cloneRoot = new(donorRoot.name);
            CopyTransform(donorRoot.transform, cloneRoot.transform);
            transformMap[donorRoot.transform] = cloneRoot.transform;

            Transform[] sourceTransforms = donorRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 1; i < sourceTransforms.Length; i++)
            {
                Transform source = sourceTransforms[i];
                GameObject child = new(source.name);
                Transform childTransform = child.transform;
                childTransform.SetParent(transformMap[source.parent], false);
                CopyTransform(source, childTransform);
                transformMap[source] = childTransform;
            }

            for (int i = 0; i < sourceTransforms.Length; i++)
            {
                Transform source = sourceTransforms[i];
                Transform destination = transformMap[source];

                MeshFilter? sourceMeshFilter = source.GetComponent<MeshFilter>();
                MeshRenderer? sourceMeshRenderer = source.GetComponent<MeshRenderer>();
                if (sourceMeshFilter != null && sourceMeshRenderer != null)
                    CopyMeshRenderer(sourceMeshFilter, sourceMeshRenderer, destination.gameObject);

                SkinnedMeshRenderer? sourceSkinnedRenderer = source.GetComponent<SkinnedMeshRenderer>();
                if (sourceSkinnedRenderer != null)
                    CopySkinnedMeshRenderer(sourceSkinnedRenderer, destination.gameObject, transformMap);
            }

            Renderer[] renderers = cloneRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = ShadowCastingMode.Off;
                renderers[i].receiveShadows = false;
            }

            return cloneRoot;
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
            destinationRenderer.enabled = sourceRenderer.enabled;
        }

        private static void CopySkinnedMeshRenderer(
            SkinnedMeshRenderer sourceRenderer,
            GameObject destination,
            Dictionary<Transform, Transform> transformMap)
        {
            SkinnedMeshRenderer destinationRenderer = destination.AddComponent<SkinnedMeshRenderer>();
            destinationRenderer.sharedMesh = sourceRenderer.sharedMesh;
            destinationRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            destinationRenderer.enabled = sourceRenderer.enabled;
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

        private void DestroyPreviewClone()
        {
            if (previewClone != null)
                DestroyImmediate(previewClone);

            previewClone = null;
        }

        private void DestroyPreviewWorldRoot()
        {
            if (previewWorldRoot != null)
                DestroyImmediate(previewWorldRoot);

            previewWorldRoot = null;
            nurseryRoot = null;
            previewCamera = null;
            previewLight = null;
        }

        private void ReleaseRenderTexture()
        {
            if (previewCamera != null)
                previewCamera.targetTexture = null;

            if (rawImage != null)
                rawImage.texture = null;

            if (renderTexture != null)
            {
                renderTexture.Release();
                DestroyImmediate(renderTexture);
            }

            renderTexture = null;
        }
    }
}
