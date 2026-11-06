# Critical-VR: reuse research and implementation status — 2026-11-06

## Recommended direction

Keep the Unity/Quest path for the real-room mixed-reality experience. Use the official Meta Passthrough Camera API sample as the reference for camera pixels. The repository's compositor underlay is not present in Unity camera color; a postprocess on that color cannot distort real-world pixels. The current patch fixes Unity-rendered content only. A stereoscopically aligned camera-texture integration remains required for the intended real-room effect. Do not present the desktop simulator as device verification.

## Reusable sources reviewed

| Source | Value | Decision / license |
| --- | --- | --- |
| https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples | Ready Unity project, CameraViewer, CameraToWorld and ShaderSample scenes; camera permissions and GPU texture effects | Best technical reference. Oculus SDK license applies unless marked otherwise. Do not copy whole project into this repo or replace existing Unity packages blindly. |
| https://developers.meta.com/vr/documentation/unity/unity-sample-camera-shader/ | Exact camera-to-shader flow, stereo and texture effects | Requires Quest 3/3S or supported Meta Horizon Link. XR Simulator does not support this camera API. |
| https://github.com/oculus-samples/Unity-Discover | Complete MR scenes, scene understanding, passthrough, anchors | Useful reference, too many unrelated networking/avatar dependencies for this small app. Mostly MIT; bundled dependencies retain own licenses. |
| https://github.com/meta-quest/agentic-tools | Official development skills and headset debugging integration | Appropriate for a locally connected development environment; not installed here. Apache-2.0 repository. |
| https://huggingface.co/datasets/adadai3132/PanoHK360 | 8K 360-degree RGB-D panoramas | Optional prerecorded environment data, not a Unity app or live passthrough replacement. CC BY 4.0, gated access requires accepting dataset conditions. Not downloaded. |
| https://github.com/keijiro/Kino | Visual-effect ideas | Current project targets HDRP; this app uses URP on mobile. Not a drop-in dependency. |

No ML training dataset is necessary for motion-driven visual shaders. A synthetic room provides reproducible visual targets without a large dataset download. Hugging Face connector was unavailable; dataset card was inspected through web retrieval.

## Code changed

- Added a Unity 6 Render Graph path, preserving the compatibility path.
- Switched the fullscreen shader to URP Blit.hlsl, XR eye setup, the actual blit source texture, correct texel size and replacement blending.
- Preserved source alpha for compositor underlay; this still does not process camera pixels.
- Removed mesh-quad usage of a fullscreen-triangle shader.
- Fixed serialized render-pass enum assignment and quality pipeline override.
- Replaced reflective controller input lookup with XR secondary-button input. Added Input System keyboard support.
- Rebased motion during intro, after reset and when focus returns to avoid artificial acceleration.
- Prevented held intensity renewing its own timer. Reset disables the debug driver in Unity and web simulator.
- Added `CriticalVR > Preview > Create And Play Synthetic Room`, using independent generated assets and a saved synthetic room. This replaces the current scene only after asking to save modified scenes. Restore Quest bootstrap before Android build.
- Added `Tools/quest_preflight.ps1` to locate the expected editor and Android tools and query ADB. Optional `-OpenUnity` opens the project locally.

## Local setup and device install

1. Preserve any uncommitted local work before changing branches. Use a separate clone of the review branch if unsure.
2. In Unity Hub install the version from ProjectSettings/ProjectVersion.txt (6000.3.11f1), including Android Build Support, SDK, NDK and OpenJDK.
3. From a PowerShell terminal inside the project run `powershell -NoProfile -File Tools\quest_preflight.ps1 -OpenUnity`.
4. For synthetic room preview use the new Preview menu. Wait 10 seconds, select CriticalVRExperienceRoot, change Debug Intensity in Inspector. Space/B/Y resets; re-enable Use Debug Slider deliberately to resume it.
5. For Quest restore `CriticalVR > Bootstrap > Run Full Quest Bootstrap`. Import compatible official Meta XR packages, configure OVRCameraRig/OVRManager/OVRPassthroughLayer and eye reference as described in SetupGuide.md. This remains local work and has not been automated or verified by this patch.
6. Run `CriticalVR > Validation > Run Quest Validation`. Resolve errors before building; do not use skip-validation to declare success.
7. Connect Quest by USB data cable, enable Developer Mode if needed, and accept USB debugging inside the headset. Having a Meta developer account does not prove this particular device is authorized.
8. Build via `CriticalVR > Build > Build Quest Development APK`. The expected output is `Builds/Quest/Development/CriticalVR-Quest-Development.apk`.
9. Close Unity before using batch build scripts. Make ADB available on PATH (preflight prints its SDK folder), then run `Tools\build_and_install_quest.bat --skip-build` for an APK already built in the Editor.
10. Open Critical-VR in the headset's Unknown Sources app list. Exact menu placement depends on Horizon OS. Confirm image in both eyes, reset buttons, tracking recovery and sustained performance. This is an unverified development build, not a completed camera-effects application.

## Permissions

No Meta account or device is connected to this environment. No Meta settings were opened or changed. Raw camera processing needs both build-time camera capability and a runtime device permission request; existing dashboard access alone is not sufficient. Follow the official CameraViewer permission flow: https://developers.meta.com/vr/documentation/unity/unity-sample-camera-viewer/ . Use Meta Project Setup Tool for the installed SDK, then approve the OS camera prompt in the headset. Do not grant unrelated permissions.

## Validation boundary

Unity, Android SDK/ADB and a headset are absent here. C# compilation, shader compilation, Unity Play Mode, camera authorization, stereo correctness, device FPS and Android APK generation are NOT verified. See Verification-2026-11-06.md for checks actually run. No APK is delivered by this patch.
