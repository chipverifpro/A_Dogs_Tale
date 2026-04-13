using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CityPeople
{
    public class CityPeople : MonoBehaviour
    {
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

        [Header("Current clip and speed")]
        public AnimationClip cl;
        public float currentSpeed;

        [Header("Thresholds for walk/run detection")]
        [Tooltip("Walk threshold set to < 2")]
        public float walkSpeed = 0.25f;    // standard walk is 2
        [Tooltip("Run threshold set to < 4.5")]
        public float jogSpeed = 3.25f;     // standard run is 4.5

        private Vector3 prev_pos3_world;

        private Animator animator;
        public const string people_pal_prefix = "people_pal";
        private List<Renderer> _paletteMeshes;

        private void Awake()
        {
            
            var AllRenderers = gameObject.GetComponentsInChildren<Renderer>();
            _paletteMeshes = new List<Renderer>();
            foreach (Renderer r in AllRenderers)
            {
                var matName = r.sharedMaterial.name;
                var len = Math.Min( people_pal_prefix.Length, matName.Length);
                if (matName[0..len] == CityPeople.people_pal_prefix)
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
                if (AutoPlayAnimations)
                {
                    //PlayAnyClip();
                    //StartCoroutine(ShuffleClips());
                }
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
                if (mat.name[0..people_pal_prefix.Length] == CityPeople.people_pal_prefix)
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

        public void PlayAnyClip()
        {
            if (myClips.Length > 0)
            {
                cl = myClips[Random.Range(0, myClips.Length)];
                animator.CrossFadeInFixedTime(cl.name, 1.0f, -1, Random.value * cl.length);
            }
            else Debug.LogWarning("Missing animations clips.");
        }

        IEnumerator ShuffleClips()
        {
            while (true)
            {
                yield return new WaitForSeconds(5.0f + Random.value * 2.0f);
                PlayAnyClip();
            }
        }

        public void Update()
        {
            float delta = Time.deltaTime;
            float moveSqr = ((transform.position - prev_pos3_world)/delta).sqrMagnitude;
            currentSpeed = Mathf.Sqrt(moveSqr);   // debug only, remove when not needed anymore
            float jogSqr = jogSpeed * jogSpeed;
            float walkSqr = walkSpeed * walkSpeed;
            //Debug.Log((transform.position - prev_pos3_world).sqrMagnitude);
            if (moveSqr > jogSqr)
            {
                if (cl!=myClips[jogClip])
                {
                    cl = myClips[jogClip];  // running
                    animator.CrossFadeInFixedTime(cl.name, 0.25f, -1, Random.value * cl.length);
                }
            }
            else if (moveSqr > walkSqr)
            {
                if (cl!=myClips[walkClip])
                {
                    cl = myClips[walkClip];  // walking
                    animator.CrossFadeInFixedTime(cl.name, 0.25f, -1, Random.value * cl.length);
                }
            }
            else
            {
                if (cl!=myClips[idleClip])
                {
                    cl = myClips[idleClip];  // standing still
                    animator.CrossFadeInFixedTime(cl.name, 1.0f, -1, Random.value * cl.length);
                }            
            }
            prev_pos3_world = transform.position;
        }

    }
}
