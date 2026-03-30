# APK Ready Status

## Repository Status

The repository is now `more than APK-ready in structure`. It now includes repeatable validation and build automation in addition to the Unity-first project layout.

This means:

- it opens as a Unity project
- the Quest build path is the main documented path
- Android/URP/XR dependencies are declared
- runtime scripts and effect shader are in-repo
- a bootstrap scene/material workflow exists
- Quest deployment checklists and ADB helpers exist
- local validation exists
- local Unity batch build exists
- local PC-to-Quest install flow exists
- CI workflow scaffolding exists

## What Is Ready

- Unity root layout
- Quest-oriented motion runtime
- URP full-screen renderer feature source
- effect shader source
- bootstrap menu tools
- validation menu tools
- build automation menu tools
- Android build/install docs
- ADB install helper
- batch build/install scripts
- CI workflow file

## What Still Requires Manual Editor Work

- import Meta XR / Oculus Integration assets
- assign `OVRCameraRig` and passthrough components
- verify headset-specific OpenXR and Meta settings

## Why This Is Not Fully One-Click Quest Deployment Yet

The remaining blockers are now mostly external to repository editing:

1. Meta XR packages are not fully vendored in this repository
2. passthrough-specific Meta scene wiring still depends on imported package components
3. final comfort verification still requires real headset testing

Once those manual Unity steps are done, the repository has a practical repeatable path for PC-to-Quest updates.
