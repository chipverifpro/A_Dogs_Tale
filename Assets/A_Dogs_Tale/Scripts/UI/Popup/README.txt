Unity UITK Popup (Tabbed) — Quick Start

1) Copy the 'Assets' folder from this package into your project.
2) Move 'Assets/UI/Popup' to 'Assets/Resources/UI/Popup' or update the paths in PopupBootstrap.cs.
3) Create a PanelSettings asset:
   - Right-click in Project: Create > UI Toolkit > Panel Settings
   - Name it `RuntimePanelSettings` and place it at 'Assets/Resources/UI/PanelSettings/'
4) In a scene, add an empty GameObject 'PopupUI' and add the component 'PopupBootstrap'.
5) (Optional) Add 'PageBinder' and the page controllers to the same GameObject to enable demo bindings.
6) Press Play. Toggle the popup with the Escape key. Tabs can also be switched with 1..4.