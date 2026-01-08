using System.Linq;
using UnityEngine;

namespace DogGame.Modules
{
    [DisallowMultipleComponent]
    public class AppearanceModule : WorldModule
    {
        [Header("Size")]
        float head_height = 0.5f;   // maybe someday we make this a Vector3 to add more control.
        float eyesForward_distance = 1f;

        [Header("Camera Follow Settings")]
        // hint that we need to update the follow camera since we moved.
        // probably change this to a function call instead of a status bit.
        public bool cameraFollowingMe = false;      // CameraModeSwitcher.cs will set/clear this as camera target is changed.
        public GameObject head = null;
        public GameObject eyesForward = null;

        public bool camera_refresh_needed = true;
    
        public void SetCameraFollow()
        {
            // create two points in space relative to the prefab for camera tracking.
            if (head == null)
            {
                string headName = $"{worldObject.DisplayName}'s Head";

                Transform existing = transform.Find(headName);
                if (existing != null)
                {
                    head = existing.gameObject;
                }
                else
                {
                    head = new GameObject(headName);
                    head.transform.SetParent(transform, false);
                    head.transform.localPosition = new Vector3(0f, head_height, 0f);
                }
            }

            if (eyesForward == null)
            {
                string eyesForwardName = $"{worldObject.DisplayName}'s EyesForward";

                Transform existing = transform.Find(eyesForwardName);
                if (existing != null)
                {
                    eyesForward = existing.gameObject;
                }
                else
                {
                    eyesForward = new GameObject(eyesForwardName);
                    eyesForward.transform.SetParent(transform, false);
                    eyesForward.transform.localPosition = new Vector3(0f, head_height, eyesForward_distance);
                }
            }

            // change the cameras to follow new target
            if (dir.cameraModeSwitcher.target != worldObject)
            {
                dir.cameraModeSwitcher.SetViewTarget(worldObject);
            }   
        }

        public void SetEnable(bool enable)
        {
            worldObject.enabled = enable;
        }

        public bool IsEnabled()
        {
            return worldObject.enabled;
        }

        //public void SetVisible(bool visible)
        //{
        //    // disable/enable all object renderers
        //    foreach (var rend in worldObject.GetComponentsInChildren<Renderer>())
        //        rend.enabled = visible;
        //}

        public bool IsVisible()
        {
            var all_renderers = worldObject.GetComponentsInChildren<Renderer>();
            if (all_renderers.Count() == 0) return false; // no renderers, no visibility
            var rend = all_renderers[0];    // just grab the first one
            return rend.enabled;
        }

        // TODO:
        //   add animation controls
        // ` add camera controls (set view to closeup, set camera follow me)


        [Header("Vision Module")]
        public bool debugMode = false;
        [Tooltip("Primary renderer for this object. If left empty, will try GetComponentInChildren<Renderer>().")]
        public Renderer mainRenderer;

        [Tooltip("Optional extra renderers (LOD children, sub-meshes, etc.).")]
        public Renderer[] extraRenderers;

        private Color[] _originalColors;
        private bool _initializedColors;

        protected override void Awake()
        {
            base.Awake();

            // Auto-assign main renderer if not wired in Inspector
            if (mainRenderer == null)
            {
                mainRenderer = GetComponentInChildren<Renderer>();
                if (mainRenderer == null)
                {
                    Debug.LogWarning($"{name}: VisualModule could not find a Renderer.", this);
                    return;
                }
            }

            CacheOriginalColors();
        }

        private int debugDoubleTick = -1;
        public override void Tick(float deltaTime)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            if (debugMode) Debug.Log($"Vision Module {worldObject.DisplayName}: Tick {deltaTime}");
        }

        private void CacheOriginalColors()
        {
            if (mainRenderer == null) return;

            // We only snapshot the mainRenderer's material color for now;
            // you can extend to per-material or per-extraRenderer if you need.
            _originalColors = new Color[1];
            _originalColors[0] = GetCurrentColor();
            _initializedColors = true;
        }

        private Color GetCurrentColor()
        {
            if (mainRenderer == null) return Color.white;

            // Use material.color (instance) so we don't clobber sharedMaterial
            if (mainRenderer.material.HasProperty("_Color"))
                return mainRenderer.material.color;

            return Color.white;
        }

        /// <summary>
        /// Show/hide this object's renderers.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (mainRenderer != null)
                mainRenderer.enabled = visible;

            if (extraRenderers != null)
            {
                for (int i = 0; i < extraRenderers.Length; i++)
                {
                    if (extraRenderers[i] != null)
                        extraRenderers[i].enabled = visible;
                }
            }
        }

        /// <summary>
        /// Apply a tint color (multiplies onto the base color).
        /// Good for highlighting, sniff mode effects, etc.
        /// </summary>
        public void SetTint(Color tint)
        {
            if (mainRenderer == null) return;

            var mat = mainRenderer.material; // instance
            if (mat.HasProperty("_Color"))
            {
                Color baseColor = _initializedColors ? _originalColors[0] : mat.color;
                mat.color = baseColor * tint;
            }

            // Optional: apply to extra renderers if present
            if (extraRenderers != null)
            {
                for (int i = 0; i < extraRenderers.Length; i++)
                {
                    var r = extraRenderers[i];
                    if (r == null) continue;

                    var m = r.material;
                    if (m.HasProperty("_Color"))
                    {
                        Color baseColor = m.color;
                        m.color = baseColor * tint;
                    }
                }
            }
        }

        /// <summary>
        /// Restore original color.
        /// </summary>
        public void ResetColor()
        {
            if (mainRenderer == null || !_initializedColors) return;

            var mat = mainRenderer.material;
            if (mat.HasProperty("_Color"))
                mat.color = _originalColors[0];
        }

        /// <summary>
        /// Convenience: set unity layer for all renderers on this object.
        /// (Separate from Rendering Layer Mask.)
        /// </summary>
        public void SetUnityLayer(int layer)
        {
            gameObject.layer = layer;

            if (mainRenderer != null)
                mainRenderer.gameObject.layer = layer;

            if (extraRenderers != null)
            {
                for (int i = 0; i < extraRenderers.Length; i++)
                {
                    if (extraRenderers[i] != null)
                        extraRenderers[i].gameObject.layer = layer;
                }
            }
        }
    }
}