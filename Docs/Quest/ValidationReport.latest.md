# Quest Validation Report

- Generated: `2026-03-30 10:49:44 UTC`
- Branch: `codex/meta-quest-xr-upgrade`
- Errors: `3`
- Warnings: `1`
- Info: `6`

## [Info] branch.ok
Repository is opened on the expected working branch.

Fix: No action required.

## [Info] urp.assigned
GraphicsSettings uses `CriticalVR_URP`.

Fix: No action required.

## [Info] rendererFeature.ok
Renderer feature is attached with material `CriticalVRPassthroughTrip`.

Fix: No action required.

## [Warning] scene.previewAnchor
Scene is still wired to the preview center eye anchor.

Fix: Replace the preview rig with OVRCameraRig and assign the real CenterEyeAnchor.

## [Info] scene.loaded
Validated scene `Assets/CriticalVR/Scenes/CriticalVR_QuestBootstrap.unity`.

Fix: No action required.

## [Error] android.supportMissing
This Unity installation does not report Android build support for the current editor.

Fix: Install Android Build Support in Unity Hub or use a Unity editor that includes Android support.

## [Error] android.target
Active build target is not Android.

Fix: Switch the project to Android or rerun bootstrap.

## [Info] xr.openxrLoaderPresent
OpenXR loader is assigned for Android.

Fix: No action required.

## [Info] xr.openxrSettingsPresent
OpenXR package settings for Android exist.

Fix: No action required.

## [Error] meta.dependenciesMissing
Meta XR / Oculus Integration types were not found in the project.

Fix: Import the Meta XR package set so OVRCameraRig, OVRManager, and OVRPassthroughLayer are available.

