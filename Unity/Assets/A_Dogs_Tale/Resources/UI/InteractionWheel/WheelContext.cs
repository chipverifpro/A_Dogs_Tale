#nullable enable
using UnityEngine;

namespace DogGame.UI.InteractionWheel
{
    /// <summary>
    /// Context passed into option resolvers and callbacks.
    /// You can add fields later as needed (distance, hit point, etc).
    /// </summary>
    public sealed class WheelContext
    {
        public readonly WorldObject actor;   // the player-controlled dog (or current agent)
        public readonly WorldObject target;  // the clicked/pressed object

        // Optional: the world point you pressed on, or a hit (handy for "sniff here")
        public readonly Vector3? worldPoint;

        public WheelContext(WorldObject actor, WorldObject target, Vector3? worldPoint = null)
        {
            this.actor = actor;
            this.target = target;
            this.worldPoint = worldPoint;
        }
    }


}