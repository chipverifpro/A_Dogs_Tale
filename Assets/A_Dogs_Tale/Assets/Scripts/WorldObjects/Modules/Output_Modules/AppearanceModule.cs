using System.Linq;
using UnityEngine;

namespace DogGame.Modules
{
    [DisallowMultipleComponent]
    public class AppearanceModule : WorldModule
    {
        // hint that we need to update the follow camera since we moved.
        // probably change this to a function call instead of a status bit.
        public bool camera_refresh_needed = true;
    
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
    }

    
}