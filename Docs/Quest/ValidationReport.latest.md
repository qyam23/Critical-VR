# Quest Validation Report

- Generated: `2026-11-07 18:09:26 UTC`
- Branch: `codex/vr-preview-readiness-2026-11-06`
- Errors: `0`
- Warnings: `2`
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

## [Info] scene.loaded
Validated scene `Assets/CriticalVR/Scenes/CriticalVR_QuestCamera.unity`.

Fix: No action required.

## [Info] xr.openxrLoaderPresent
OpenXR loader is assigned for Android.

Fix: No action required.

## [Info] xr.openxrSettingsPresent
OpenXR package settings for Android exist.

Fix: No action required.

## [Warning] camera.hardwareUnverified
Static validation cannot verify device permission, frame pairing, per-eye alignment, near-object parallax or sustained GPU time. Reference-distance reprojection is approximate.

Fix: Run the device checklist on a Quest 3/3S. Never treat a green static report as headset verification.

## [Warning] meta.mrukSceneRecommendation
MRUK 85's generic rule remains unsatisfied: MR Utility Kit recommends Scene Support to be set to "Required". This camera-only scene contains no MRUK scene manager, OVRSceneManager or spatial anchors. PCA itself checks HEADSET_CAMERA only. Scene/anchor support and permissions are deliberately absent. The SDK task was evaluated and was not ignored or marked fixed.

Fix: If room reconstruction or spatial anchors are introduced, enable their support/permissions and satisfy the rule. See CameraIntegration-2026-11-07.md for this scoped applicability decision.
