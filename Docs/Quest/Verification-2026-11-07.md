# Quest verification — 2026-11-07

## Implemented

Meta XR Core/MRUK 85, OpenXR 1.15.1 and an automated production OVRCameraRig/underlay scene are imported. The camera bridge uses independent calibrated left/right GPU textures. Effects fail closed to the native compositor view on camera/focus/permission loss. B/Y resets and suspends; A/X explicitly resumes. The original bootstrap scene is preserved.

The overlay projects camera imagery at a 2 m reference distance with bounded opacity. It does not reconstruct room depth. Near-object parallax, latency, eye alignment and comfort remain unverified until observed on hardware.

## Actual checks

- Unity 6000.3.11f1 imported the project and compiled C#. Input System/XR type ambiguities were fixed using explicit XR names.
- Final rendered Unity Play Mode check passed at `2026-11-07T18:08:05Z`, using game-loop motion input paced near 72 FPS: debug activation, actual tracked-transform motion, 56 seconds of stationary decay, reset, disabling the debug driver, subsequent movement while suspended, and deliberate resume. See [report](Evidence-2026-11-07/synthetic-check.md), [active image](Evidence-2026-11-07/synthetic-active.png) and [reset image](Evidence-2026-11-07/synthetic-reset.png).
- WebSimulator lint, Vite build and logic tests passed. These are separate from Unity and do not establish headset operation.
- Production Android validation passed with zero errors and two warnings. The real Meta setup tasks were evaluated; the generic MRUK Scene Support recommendation remains unsatisfied because this camera-only application has no scene reconstruction or spatial anchor component. It is reported explicitly, not marked fixed or ignored in the SDK. The other warning requires headset testing. See [current report](ValidationReport.latest.md).
- Quest preflight found the existing editor, working license, Android build module, SDK, NDK r27c and OpenJDK 17. No second editor was installed. Android tools are configured through the local ignored UserSettings file.
- Build scenes are explicitly restricted to `CriticalVR_QuestCamera.unity`. Synthetic preview restores the prior scene, pipelines, XR initialization and background setting. The original bootstrap scene has no diff.

## Hardware checks still required

The user will power on the headset after desktop work. No authorized headset was connected at preflight. Device model/Horizon OS, application camera grant/denial, actual paired feeds, each eye's alignment, camera interruptions, sustained GPU/CPU frame timing and visual comfort have not been tested. XR Simulator cannot replace them.

On connection: accept USB debugging in the headset, install the development APK, launch Critical-VR from Unknown Sources and respond to its camera permission prompt. Use B/Y to return to the native room and A/X to resume. Follow the [device checklist](CameraIntegration-2026-11-07.md#headset-checklist-human-observation-required).

## APK verification

- Development APK build succeeded at `2026-11-07 18:15:36 UTC`: zero build errors, eight warnings. Remaining warnings concern the compatibility rendering API/deprecated SDK interfaces and the documented MRUK/OpenXR recommendations. They are not presented as a warning-free SDK configuration.
- The incorrect `UniversalRenderPipeline` Shader tag was corrected to URP's `UniversalPipeline`; the earlier zero-variant stripping problem is resolved. Actual post-strip [variant records](Evidence-2026-11-07/shader-variants.txt) contain `STEREO_MULTIVIEW_ON` for both GLES3 and Vulkan. The final Shader source compiled without Shader warnings. Retention does not establish correct headset imagery.
- Active Input Handling is **Input System only**. The Android warning about unsupported Both handling is absent from the final build; validation now rejects that configuration.
- APK: `Builds/Quest/Development/CriticalVR-Quest-Development.apk`, **199,054,028 bytes**.
- SHA-256: `062432fcba36ec383fc0f8ef1bf38a7ae7bf5d5ddbf74a5d6415eb9ae76fb3c6`.
- `apksigner verify --verbose` passed with APK Signature Scheme v2 and one signer. This is a development/debug-signed build.
- `aapt dump badging` verified package `com.qyam23.criticalvr`, version `0.1.0` / code `1`, ARM64 only, minimum Android API `32`, target API `36`, and launch activity `com.unity3d.player.UnityPlayerGameActivity`.
- `aapt dump permissions` found HEADSET_CAMERA, INTERNET (development networking) and AndroidX's package-specific DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION. No scene, anchor, microphone or location permission is declared.
- The initial Gradle CMake failure was resolved by restoring the missing CMake 3.22.1 Modules directory and source.properties in the existing external Android toolchain. CMake's version/system-information checks and the complete subsequent Android build passed.

Local APK path: `C:\Users\user\Documents\Playground\Critical-VR\Builds\Quest\Development\CriticalVR-Quest-Development.apk`.

Install the existing APK without rebuilding:

```powershell
powershell -NoProfile -File Tools\install_quest_apk.ps1
```

The script's syntax and no-authorized-device refusal were checked. Installation, launch and device-log collection remain untested until a supported headset is authorized; the script does not silently uninstall an existing app or grant camera permission.
