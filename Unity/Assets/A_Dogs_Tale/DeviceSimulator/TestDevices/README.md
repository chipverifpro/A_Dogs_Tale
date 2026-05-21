# Unity 6.3 Device Simulator Test Devices

These are generic Device Simulator definitions for testing responsive UI layouts in Unity 6.3.
They are not exact replicas of commercial devices. They are meant to exercise common aspect ratios,
safe-area behavior, Android navigation bar behavior, and tablet/foldable layouts.

## Install

Copy this folder into your Unity project so the files live somewhere under `Assets`, for example:

`Assets/DeviceSimulator/TestDevices/`

Unity 6.3 custom device definitions use the `.device` extension. After Unity imports the files,
open the Simulator view and look for devices whose names start with `ADT Test`.

If they do not show immediately, try one of these:

1. Select a `.device` file in the Project window and check the Inspector for JSON/schema errors.
2. Reopen the Simulator view.
3. Right-click the folder and choose Reimport.
4. Restart the Unity Editor.

## Included devices

- ADT Test Small Phone 720x1280: older/small Android phone shape, includes bottom navigation bar.
- ADT Test Tall Phone 1080x2400: modern tall phone, includes top cutout and bottom navigation bar.
- ADT Test Tablet 4x3 2048x1536: iPad-like 4:3 tablet layout, no cutout.
- ADT Test Tablet Wide 2560x1600: wide Android tablet / Chromebook-ish layout, no cutout.
- ADT Test Foldable Wide 2208x1768: large foldable/tablet-like layout with a small top safe-area inset.

## Notes

The Tall Phone cutout and all safe-area values are intentionally generic, not manufacturer-accurate.
Use real hardware before shipping if you care about exact device behavior, performance, input quirks,
permissions, GPU differences, and store compliance.
