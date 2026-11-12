# Quest camera integration

## Scope and limits

The native Meta passthrough underlay remains the baseline view. Unity camera color does not contain that underlay. `QuestPassthroughCameraBridge` supplies independent left/right GPU camera textures to the fullscreen effect instead. No camera images are read back to CPU, saved, or uploaded by this integration.

This is an **experimental stereo camera overlay**, not a verified replacement for Meta's reconstructed passthrough. Each eye uses the corresponding physical camera, capture pose, cropped sensor calibration and XR inverse view-projection. Projection currently assumes a two-metre reference distance. It does **not** reconstruct per-pixel scene depth: near-object parallax, occlusions, capture latency and image alignment still require device testing. Outside the sensor field of view the normal underlay remains visible; image edges fade rather than stretch. Overlay opacity defaults to 0.45 and is capped at 0.6.

## Packages and source references

- Unity 6000.3.11f1, URP 17.3, OpenXR 1.15.1.
- Meta XR Core and MRUK pinned to 85.0.0, matching the official sample's package manifest reviewed for this work. Unity resolves compatible transitive versions in `Packages/packages-lock.json`.
- [Meta camera samples](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples), reviewed commit `3baac1a1a53aa00af3f639425914f14238a97f84`: CameraViewer, CameraToWorld and ShaderSample. Their camera API use informed this integration; the whole sample project and its optional ML/networking dependencies were not copied.
- [Camera API guide](https://developers.meta.com/vr/documentation/unity/unity-pca-documentation), checked using official Meta VR CLI documentation retrieval.
- [Official Meta agentic tools](https://github.com/meta-quest/agentic-tools), reviewed commit `3a8553d10a5a1bd1bf9beaaa950243fbbed3b9ea` (Apache-2.0). `metavr` was invoked via npm; Meta Quest Developer Hub's existing ADB was reused.
- Meta packages and sample material retain the Oculus SDK license described in their packages/repository. New application code stays in this repository; package references preserve upstream notices and licenses. No sample artwork, ML models or datasets were imported.
- Unity Pipeline 0.10.0-exp.1 provides the official local editor command/capture interface. It is tooling, not proof of headset operation.

## Setup automation

`CriticalVR > Bootstrap > Run Full Quest Bootstrap` creates/repairs **CriticalVR_QuestCamera.unity**, copying the existing bootstrap scene on first use. The original **CriticalVR_QuestBootstrap.unity** is preserved. The production scene receives the official OVRCameraRig prefab, OVRManager, transparent eye-camera background, passthrough underlay, camera bridge, material references and a small in-headset status label. Existing preview cameras are disabled in the copy.

The bootstrap enables the Meta XR OpenXR feature, single-pass instanced rendering and Touch input. It sets ARM64/IL2CPP, Android minimum API 32, Vulkan first, the production URP pipeline, and Quest 3/3S targets. OVRProjectConfig enables passthrough and camera access; **the official Meta manifest generator** then generates/updates AndroidManifest.xml and the result is checked for `horizonos.permission.HEADSET_CAMERA`.

The build uses an explicit one-scene allowlist, preventing SyntheticRoom from entering the APK. Validation checks camera references, transparent camera clear, shader compiler state, Meta XR feature, permission declaration, build scenes and the real required Meta Project Setup tasks. Applicable required synchronous fixes are attempted, then tasks are evaluated again without accepting ignored/marked-fixed tasks as evidence of runtime operation. Hardware checks remain separate.

**MRUK 85 applicability warning:** its `OVRProjectSetupMRUK.cs` labels a generic “MR Utility Kit recommends Scene Support…” recommendation as Required whenever a root OVRCameraRig exists, even if no MRUK scene component exists. The camera API's own implementation requests HEADSET_CAMERA only. The application validator evaluates and reports this unsatisfied SDK rule as a warning only when there is a camera bridge and no MRUK, OVRSceneManager or OVRSpatialAnchor component (including inactive objects). It neither ignores nor marks the SDK task fixed, and does not change the SDK build validator or its error settings. Scene/anchor support and their permissions remain disabled. This is not a claim that every SDK rule is green. Adding scene reconstruction or anchors makes the normal requirement applicable again.

If Android tools are outside the editor installation, put their common root in **UserSettings/CriticalVRAndroidToolchain.txt** (local and ignored by Git), containing SDK/, NDK/ and OpenJDK/. Alternatively set **CRITICAL_VR_ANDROID_TOOLS**. The editor configures these paths automatically. A custom SDK needs `cmdline-tools/latest/bin/sdkmanager.bat`; a junction to the installed 16.0 command-line tools works on the development PC.

## Runtime controls and failures

- **B / Y**: remove camera effects immediately, smoothly release virtual effect layers, disable debug intensity and remain suspended.
- **A / X**: explicitly resume, or retry camera access. Subsequent head motion alone cannot undo a reset.
- Synthetic preview: **Space / B / Y** reset, **R** resumes; use the Inspector debug slider to tune intensity.
- The camera permission is requested by the bridge alone; OVRManager's duplicate startup permission request is disabled. Only HEADSET_CAMERA is requested, not microphone, location or unrelated permissions.
- Both cameras must provide fresh frames. Missing permission, unsupported model/OS, focus loss, missing texture, stale frames (0.25 s), initialization timeout or left/right timestamp difference over 50 ms removes the camera overlay. The compositor view remains available. Recovery requires an explicit A/X press.
- Logs use **[CriticalVR.Camera]**. Model, OS, permission handling, per-eye output and thermal performance cannot be inferred from editor tests.

## Repeatable checks

Close the project's editor after saving, then run:

```powershell
powershell -NoProfile -File Tools\run_quest_checks.ps1
```

This runs rendered Unity Play Mode checks, restores prior scene/pipeline/XR/background settings, validates the production Quest configuration, and builds a development APK. Every stage must succeed; it does not skip validation. Add `-Install` only when the headset is connected and USB debugging authorized; use `-Serial` when necessary. The APK path is **Builds/Quest/Development/CriticalVR-Quest-Development.apk**. To install an already built APK without rebuilding or closing Unity, run `powershell -NoProfile -File Tools\install_quest_apk.ps1`. It requires an authorized Quest 3/3S, reports its model/OS build, and leaves the camera permission prompt to the user.

The automated room check drives its tracked transform in the game PlayerLoop before MonoBehaviour.Update, paced at approximately 72 FPS. Unbounded batch mode was measured above 4,000 FPS, where Quaternion.Angle quantizes the tiny per-frame changes to zero; the test thresholds are unchanged. It exercises the actual MonoBehaviour: debug activation, tracked-transform head rotation, motion-free decay, reset, disabling the debug driver, motion while suspended, and explicit resume. Its report and captures are under **Builds/Quest/Logs/**. A passing report requires advancing player frames; editor window state alone is insufficient.

## Headset checklist (human observation required)

1. Read actual headset model and Horizon OS using ADB; confirm camera API support before evaluating imagery. Grant the application camera prompt in the headset. XR Simulator cannot stand in for this check.
2. Cover each eye in turn: verify the corresponding camera feed, orientation, alignment and fading sensor boundary. Compare distant and near objects; near-object mismatch is a known limit of reference-distance projection.
3. Turn the head gently, then hold still. Check gradual activation and full decay. Verify B and Y return to the normal room immediately and stay there during further movement. Verify A and X deliberately resume.
4. Test denied permission, background/resume, headset removal and camera interruption. No frozen or single-eye image may remain.
5. Capture Unity/AndroidRuntime errors and sustained CPU/GPU frame timing on device. Desktop FPS, API readiness flags and a successful APK build do not establish headset performance or visual comfort.
