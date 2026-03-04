#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.UI.InteractionWheel
{
    public static class WheelOptionCollector
    {
        /// <summary>
        /// Collect options from all IWheelOptionProvider modules on the TARGET object.
        /// This stays simple: no sorting/dedup here yet (we can add in step 2/3).
        /// </summary>
        public static List<WheelOption> CollectFromTarget(WorldObject actor, WorldObject target, Vector3? worldPoint = null)
        {
            var context = new WheelContext(actor, target, worldPoint);

            // Results list
            var options = new List<WheelOption>(capacity: 12);

            // Find providers on the target
            // (If your WorldObject has its own module list system, swap this to use it.
            // For now, simple and robust: GetComponents.)
            var providers = target.GetComponents<MonoBehaviour>();

            for (int i = 0; i < providers.Length; i++)
            {
                if (providers[i] is IWheelOptionProvider provider)
                {
                    provider.BuildWheelOptions(context, options);
                }
            }

            return options;
        }
    }
}