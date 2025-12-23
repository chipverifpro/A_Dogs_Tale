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

        public void SetVisible(bool visible)
        {
            // disable/enable all object renderers
            foreach (var rend in worldObject.GetComponentsInChildren<Renderer>())
                rend.enabled = visible;
        }

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

    }

    
}