# Setup Guide

This repository is now a Unity project root. Open the repository itself in Unity.

## Version Assumptions

- Unity target design: `Unity 2022.3 LTS or newer`
- Local bootstrap seed currently saved with: `Unity 6000.3.11f1`
- Render pipeline: `URP`
- XR path: `Meta Quest passthrough` with `OVRCameraRig`
- Build target: `Android`

If your team must stay strictly on `2022.3 LTS`, re-save the project in that editor version after importing the same packages. The runtime code and repository layout were written to stay compatible with the `2022.3+` API surface.

## Before Opening Unity

Make sure the local machine has:

- Unity with `Android Build Support`
- `SDK`, `NDK`, and `OpenJDK` installed through Unity Hub
- Meta Quest connected by USB for `Build And Run` or `adb install`
- Developer Mode enabled on the headset

## Package Expectations

Already included in `Packages/manifest.json`:

- `com.unity.render-pipelines.universal`
- `com.unity.inputsystem`
- `com.unity.xr.management`
- `com.unity.xr.openxr`
- `com.unity.mobile.android-logcat`

Still manual:

- import `Meta XR All-in-One SDK` or the equivalent Oculus Integration / Meta XR packages that provide:
  - `OVRCameraRig`
  - `OVRManager`
  - `OVRPassthroughLayer`
  - controller bindings and Quest helpers

## First Open In Unity

1. Open the repository folder in Unity.
2. Let Package Manager finish importing URP, XR Management, and OpenXR.
3. In the top menu run:
   `CriticalVR > Bootstrap > Run Full Quest Bootstrap`

That bootstrap does three things:

- switches the project to `Android`
- configures Quest-oriented Android player settings
- creates:
  - `Assets/CriticalVR/Scenes/CriticalVR_QuestBootstrap.unity`
  - `Assets/CriticalVR/Resources/Materials/CriticalVRPassthroughTrip.mat`

The bootstrap scene starts with a preview camera rig placeholder so the repository opens cleanly before Meta packages are imported.

## URP Setup

You must still create or confirm the URP assets inside the Unity Editor.

1. Create a `URP Pipeline Asset` and `Renderer Data` asset if they are not already present.
2. Assign the URP pipeline asset in:
   `Project Settings > Graphics`
3. Also assign it in:
   `Project Settings > Quality`
4. In the active URP renderer:
   - enable `Opaque Texture`
   - enable `Depth Texture`
   - keep the project `Linear` color space

## Vulkan-First Recommendation

The bootstrap script already sets Android graphics APIs to:

1. `Vulkan`
2. `OpenGLES3`

Keep `Vulkan` first for Quest unless your specific headset/plugin combination forces a fallback.

## Meta XR / Oculus Setup

After importing Meta XR packages:

1. Open `Assets/CriticalVR/Scenes/CriticalVR_QuestBootstrap.unity`
2. Delete or disable the placeholder object:
   `XROriginPreview`
3. Add `OVRCameraRig`
4. Keep it at world origin: position `(0,0,0)` and rotation `(0,0,0)`
5. Add or confirm `OVRManager` on the rig
6. Set passthrough mode for `OVRManager` and `OVRPassthroughLayer` to `Underlay`
7. Add `OVRPassthroughLayer` if it is not already present

## Attach Motion Runtime

The runtime host object is:

- `CriticalVRExperienceRoot`

On that object:

1. Keep `MotionToExperienceManager`
2. Assign `CenterEyeAnchor` to:
   `OVRCameraRig/TrackingSpace/CenterEyeAnchor`
3. Assign `Effect Material` to:
   `Assets/CriticalVR/Resources/Materials/CriticalVRPassthroughTrip.mat`

This script already includes:

- motion history
- hysteresis
- layered attack/release timing
- gradual 10-second black intro
- Recovery phase logic
- emergency reset
- material parameter output for the shader

## Renderer Feature Setup

Production path:

1. Open the active URP `Renderer Data` asset
2. Add Renderer Feature:
   `CriticalVRFullscreenRendererFeature`
3. Assign the same material:
   `CriticalVRPassthroughTrip.mat`
4. Use injection point:
   `After Rendering Transparents`

Why this is the main path:

- passthrough is an `Underlay`, so the world remains the background
- the full-screen pass adds altered-perception layers on top
- the center remains more stable while the periphery blooms first

Fallback preview path:

- the bootstrap scene includes `CriticalVRPreviewOverlay`, a camera-locked quad
- keep it only for editor preview or debugging
- disable or remove it once the renderer feature is active

## Passthrough Layer Setup

On `OVRPassthroughLayer`:

- Composition Placement: `Underlay`
- style opacity: start conservative
- no aggressive color mapping at the passthrough layer itself

The altered perception should come from the URP effect material, not from harsh passthrough toggling.

## Android Player Settings

Recommended checks in `Project Settings > Player > Android`:

- `IL2CPP`
- `ARM64`
- `Minimum API Level`: `29` or higher
- `Target API Level`: `Automatic (highest installed)`
- `Color Space`: `Linear`
- `Graphics APIs`: Vulkan first, OpenGLES3 second
- `Multithreaded Rendering`: enabled
- package name:
  `com.qyam23.criticalvr`

## Build And Run

Preferred path:

1. Connect the Quest by USB
2. Accept the headset RSA prompt
3. In Unity choose:
   `File > Build Settings > Android > Build And Run`

Expected result:

- Unity builds an APK
- installs it to the connected headset
- launches it on device

## APK Generation Path

If you want the APK without immediate install:

1. In `Build Settings`, choose `Build`
2. Save into a local folder such as:
   `Builds/Android/CriticalVR.apk`

## ADB Install Fallback

If Unity build succeeds but install fails, use:

- [`Tools/install_apk.bat`](../../Tools/install_apk.bat)

Example:

```bat
Tools\install_apk.bat Builds\Android\CriticalVR.apk
```

Manual fallback:

```bat
adb devices
adb install -r Builds\Android\CriticalVR.apk
```

## First-Launch Debugging Notes

Use these checks first:

- confirm headset appears in `adb devices`
- open `Window > Analysis > Android Logcat`
- watch for shader compile warnings, OpenXR loader issues, or missing Meta XR components
- verify the app launches to a black screen that reveals gradually over `10s`
- verify `B` and `Y` reset the experience
- verify the effect blooms first in the periphery, not the center

## Manual Unity Editor Actions Still Required

These cannot be fully solved by repository editing alone:

- import Meta XR / Oculus Integration package set
- create or confirm the URP renderer asset and add the renderer feature to it
- replace the placeholder preview rig with `OVRCameraRig`
- assign `CenterEyeAnchor` from the imported Meta rig
- enable passthrough and confirm headset-specific plugin settings
- build once on the target headset to validate the final comfort tuning
