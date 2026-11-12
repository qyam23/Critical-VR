# Setup Guide

Open this repository as a Unity project with **6000.3.11f1**. The package manifest installs URP, OpenXR, Meta XR Core 85.0.0 and MRUK 85.0.0; Unity Pipeline is included for local editor automation.

## Automatic Quest setup

1. Let package import and C# compilation complete.
2. Run **CriticalVR > Bootstrap > Run Full Quest Bootstrap**.
3. Run **CriticalVR > Validation > Run Quest Validation** and resolve every error.
4. Run **CriticalVR > Build > Build Quest Development APK**.

The bootstrap copies the original QuestBootstrap scene to **Assets/CriticalVR/Scenes/CriticalVR_QuestCamera.unity** on first use, preserving the original. It installs the official OVRCameraRig prefab in the copy, enables OVRManager passthrough and its underlay, wires the motion runtime and dual-camera bridge, configures OpenXR/Touch input and single-pass instanced rendering, generates the Android manifest with Meta's generator and selects only the production scene for builds. No manual rig dragging or reference assignment is intended.

Build settings: Android, ARM64, IL2CPP, minimum API 32, linear colour, Input System only, Vulkan first and OpenGLES3 fallback. The validator rejects unsupported Active Input Handling = Both. Package: **com.qyam23.criticalvr**. Output: **Builds/Quest/Development/CriticalVR-Quest-Development.apk**.

## Local Android tools

Reuse Unity Hub's Android Build Support, SDK, NDK r27c and OpenJDK 17. The project also supports an existing external toolchain. Set **CRITICAL_VR_ANDROID_TOOLS** to a directory containing SDK/, NDK/ and OpenJDK/, or write that directory to the Git-ignored **UserSettings/CriticalVRAndroidToolchain.txt**. Unity applies those paths automatically. External SDKs must expose **cmdline-tools/latest/bin/sdkmanager.bat**.

Run **Tools/quest_preflight.ps1** to inspect the expected editor, toolchain and ADB devices. It does not activate a license or grant device/application permissions.

## Synthetic room

**CriticalVR > Preview > Create And Play Synthetic Room** creates/opens a separate synthetic scene and preview pipeline. Wait for the seven-second intro, then use the Inspector's debug intensity. Space/B/Y stops and suspends effects; R resumes. The prior scene, pipelines, XR initialization and background setting are restored on leaving Play Mode.

**CriticalVR > Preview > Run Automated Room Check** tests actual Unity runtime updates, motion-driven activation, decay, reset and explicit resume, and captures active/reset images through URP. Results are under **Builds/Quest/Logs/**. This is not passthrough-camera or headset verification.

For a full batch run, first save and close this project's editor:

```powershell
powershell -NoProfile -File Tools\run_quest_checks.ps1
```

The script runs Play Mode checks, production Quest validation and the development APK build in order, without bypassing validation. The older validate/build/install batch helpers remain available and also require the editor to be closed.

## Headset

The [Windows console](../../Tools/QuestConsole/README.md) can install and start the development APK, control the effect, and collect motion samples over USB.

Connect the Quest with a USB data cable and approve USB debugging inside it. Use the actual device model and Horizon OS to confirm camera compatibility. A developer account does not grant runtime camera permission or USB authorization.

Install and launch the existing APK using **powershell -NoProfile -File Tools\install_quest_apk.ps1**. This locates the configured ADB, checks the authorized device/model, records its OS build and launches the app without rebuilding or closing Unity. Grant the application's camera prompt in the headset.

For hands, enable **Settings > Movement tracking > Hand and body tracking**. Touch thumb and index finger on the left hand for a short pinch to stop. Release, then hold the right-hand thumb and index finger together for one second to resume or retry. The app requires a tracked, released hand before accepting a new pinch. With controllers, **B/Y stops** and **A/X resumes**.

The headset power button can put the headset to sleep. When the app loses focus or pauses, the camera effect stops. After returning, use the resume gesture or controller button. Denied permission and missing or stale camera frames also return to native passthrough.

Read [CameraIntegration-2026-11-07.md](CameraIntegration-2026-11-07.md) for the source references, stereo projection limits, lifecycle handling and device checklist. The current per-eye camera projection uses a reference distance and is not depth-correct reconstructed passthrough. Device stereo alignment, near objects, visual comfort and sustained GPU/CPU timing require observation and measurements on the headset.
