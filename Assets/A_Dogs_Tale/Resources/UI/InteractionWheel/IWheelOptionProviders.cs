#nullable enable
using System.Collections.Generic;

namespace DogGame.UI.InteractionWheel
{
    /// <summary>
    /// Implement on a WorldModule that can contribute wheel options
    /// for a given actor/target context.
    /// </summary>
    public interface IWheelOptionProvider
    {
        /// <summary>
        /// Add any wheel options this module contributes to 'results'.
        /// Keep it fast and side-effect free.
        /// </summary>
        void BuildWheelOptions(WheelContext context, List<WheelOption> results);
    }
}