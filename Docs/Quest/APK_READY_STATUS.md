# APK Ready Status

## Repository Status

The repository is now `APK-ready in structure`, which means:

- it opens as a Unity project
- the Quest build path is the main documented path
- Android/URP/XR dependencies are declared
- runtime scripts and effect shader are in-repo
- a bootstrap scene/material workflow exists
- Quest deployment checklists and ADB helpers exist

## What Is Ready

- Unity root layout
- Quest-oriented motion runtime
- URP full-screen renderer feature source
- effect shader source
- bootstrap menu tools
- Android build/install docs
- ADB install helper

## What Still Requires Manual Editor Work

- import Meta XR / Oculus Integration assets
- assign `OVRCameraRig` and passthrough components
- attach the renderer feature to the active URP renderer data asset
- verify headset-specific OpenXR and Meta settings

## Why This Is Not Fully One-Click APK Build Yet

Two blockers remain outside repository-only editing:

1. Meta XR packages are not fully vendored in this repository
2. Unity serializes the active URP renderer asset and Meta rig setup through Editor-created assets that still need final manual wiring

Once those manual Unity steps are done, the repository is structured to build and transfer to Quest.
