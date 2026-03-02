#nullable enable
using System.Collections.Generic;

namespace DogGame.UI.InteractionWheel
{
    /// <summary>
    /// Output of the resolver: 1+ pages of options.
    /// The UI can render page 0 initially and switch pages when "More..." / "Back" are selected.
    /// Cancel is intentionally not included here (UI always provides it).
    /// </summary>
    public sealed class WheelMenuModel
    {
        public WheelContext context;
        public List<List<WheelOption>> pages;

        public WheelMenuModel(WheelContext context, List<List<WheelOption>> pages)
        {
            this.context = context;
            this.pages = pages;
        }
    }
}