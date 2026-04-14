using System.Linq;
using UnityEngine;
using InspectorTools;
using System;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace DogGame.Modules
{
    [DisallowMultipleComponent]
    [InspectorNote("Output_Modules/Appearance Module", "Camera parameter head_height, as well as function for color tint (convert a white dog to a brown one).")]
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
    
        #region Camera
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
        #endregion

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

        #region Color
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
            AnimationAwake();
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
        #endregion
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

// =============== Animation Controller ================
        #region Animation

        // select the type of animation controller based on what library asset came from
        public enum AnimationVersion {
            none = 0,
            dog,
            human,
            furniture
        }

        public enum AnimationCategory
        {
            idle = 0,
            walk,
            run,
            sit
        } 

        [Header("Animation")]
        public AnimationVersion animationVersion = AnimationVersion.none;
        
        [SerializeField]
        [Tooltip("Autoplay random animation clips")] 
        private bool AutoPlayAnimations = true;
        [SerializeField]
        [Tooltip("Overrides palette materials, skips other objects")]
        private Material PaletteOverride;
        public string CurrentPaletteName { get; private set; }

        [Header("All animations capable")]
        public AnimationClip[] myClips;
        [Header("Index of desired clip")]   // Adjust based on order of clips in Animator
        public int idleClip = 0;
        public int walkClip = 1;
        public int jogClip = 2;

        [Header("Current clip")]
        public AnimationCategory currentAnimation = AnimationCategory.idle;
        public AnimationClip cl;
        //public float currentSpeed;

        [Header("Thresholds for walk/run detection")]
        [Tooltip("Walk threshold set to < 2")]
        public float walkSpeed = 0.25f;    // standard walk is 2
        [Tooltip("Run threshold set to < 4.5")]
        public float jogSpeed = 3.25f;     // standard run is 4.5

        private Vector3 prev_pos3_world;

        private Animator animator;
        public const string people_pal_prefix = "people_pal";
        private List<Renderer> _paletteMeshes;

        private void AnimationAwake()
        {
            
            var AllRenderers = gameObject.GetComponentsInChildren<Renderer>();
            _paletteMeshes = new List<Renderer>();
            foreach (Renderer r in AllRenderers)
            {
                var matName = r.sharedMaterial.name;
                var len = Math.Min( people_pal_prefix.Length, matName.Length);
                if (matName[0..len] == people_pal_prefix)
                {
                    _paletteMeshes.Add(r);
                }
            }
            if (_paletteMeshes.Count > 0)
            {
                CurrentPaletteName = _paletteMeshes[0].sharedMaterial.name;
            }

            if (PaletteOverride != null)
            {
                SetPalette(PaletteOverride);
            }
        }

        void Start()
        {
            animator = GetComponent<Animator>();
            if (animator != null)
            {
                myClips = animator.runtimeAnimatorController.animationClips;
            }

            if (AutoPlayAnimations)
            {
                //collider for detect clicks near the character
                CapsuleCollider collider =  gameObject.AddComponent<CapsuleCollider>();
                //average character dimentions
                collider.center = new Vector3(0f, 0.8f, 0f);
                collider.radius = 0.3f;
                collider.height = 1.77f;
                collider.direction = 1;
            }

            prev_pos3_world = transform.position;
        }

        public void SetPalette(Material mat)
        {
            if (mat != null)
            {
                if (mat.name[0..people_pal_prefix.Length] == people_pal_prefix)
                {
                    CurrentPaletteName = mat.name;
                    foreach (Renderer r in _paletteMeshes)
                    {
                        r.material = mat;
                    }
                } else
                {
                    Debug.Log("Material name should start with 'palete_pal...' by convention.");
                } 
            }
        }

        protected override void Update()
        {
            base.Update();

            if (myClips.Count()==0) return; // this has no defined animations.

            float delta = Time.deltaTime;
            float moveSqr = ((transform.position - prev_pos3_world)/delta).sqrMagnitude;
            //currentSpeed = Mathf.Sqrt(moveSqr);   // debug only, remove when not needed anymore
            float jogSqr = jogSpeed * jogSpeed;
            float walkSqr = walkSpeed * walkSpeed;
            //Debug.Log((transform.position - prev_pos3_world).sqrMagnitude);
            if (moveSqr > jogSqr)
            {
                if (currentAnimation != AnimationCategory.run)
                {
                    currentAnimation = AnimationCategory.run;
                    cl = myClips[jogClip];  // running
                    Debug.Log($"{name} Jog:");
                    if (animationVersion == AnimationVersion.dog)
                        animator.SetInteger("AnimationID",jogClip);
                    else if (animationVersion == AnimationVersion.human)
                        animator.CrossFadeInFixedTime(cl.name, 0.25f, 0, Random.value * cl.length);
                }
            }
            else if (moveSqr > walkSqr)
            {
                if (currentAnimation != AnimationCategory.walk)
                {
                    currentAnimation = AnimationCategory.walk;
                    cl = myClips[walkClip];  // walking
                    Debug.Log($"{name} Walk:");
                    if (animationVersion == AnimationVersion.dog)
                        animator.SetInteger("AnimationID",walkClip);
                    else if (animationVersion == AnimationVersion.human)
                        animator.CrossFadeInFixedTime(cl.name, 0.25f, 0, Random.value * cl.length);
                }
            }
            else
            {
                if (currentAnimation != AnimationCategory.idle)
                {
                    currentAnimation = AnimationCategory.idle;
                    cl = myClips[idleClip];  // standing still
                    Debug.Log($"{name} Idle:");
                    if (animationVersion == AnimationVersion.dog)
                        animator.SetInteger("AnimationID",idleClip);
                    else if (animationVersion == AnimationVersion.human)
                        animator.CrossFadeInFixedTime(cl.name, 1.0f, 0, Random.value * cl.length);
                }            
            }
            prev_pos3_world = transform.position;
        }
        #endregion
    }
}

