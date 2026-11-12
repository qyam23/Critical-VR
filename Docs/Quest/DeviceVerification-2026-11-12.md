# Quest 3S device verification — 2026-11-12

The camera development build from commit `506acc5` installed through authorized ADB and launched on Quest 3S. The device reported Android 14, OS build `UP1A.231005.007.A1`, and Horizon OS SDK property `160`. The app entered a focused OpenXR session and the headset camera permission was granted.

The first permission dialog paused the experience. After relaunch with the granted permission, the app reported live textures from the left and right cameras at 1280×960 each. A short VrApi sample showed rendering around 72 FPS. Per-eye image alignment, comfort, and sustained performance still require observation during use.

## Build with hand controls

- Left thumb–index pinch for 0.25 seconds stops the effects. Right thumb–index pinch for one second resumes or retries after release. Gestures require valid, confident hand tracking and app input focus. The stop input takes precedence.
- The hardware power button uses the headset's normal sleep behavior. App pause stops the camera effect. A resume input is required after waking. See [Meta application lifecycle handling](https://developers.meta.com/vr/documentation/unity/unity-lifecycle/).
- Unity built the Android development APK with zero errors and eight build warnings. Project validation reported zero errors and two documented warnings.
- APK signature verification passed. The merged APK declares `HEADSET_CAMERA` and `com.oculus.permission.HAND_TRACKING`.
- Output `Builds/Quest/Development/CriticalVR-Quest-Development.apk`, 199,055,687 bytes, SHA-256 `594deea5816148ab6981de8e38936bc84868fa378068732623c0d0607d398e3f`.

The updated build installed through ADB and launched on Quest 3S after USB debugging was authorized again. The SHA-256 hash of the installed base APK matched the build artifact. The app entered a focused OpenXR session, retained camera permission, and reported both 1280×960 camera textures. Hand-tracking permissions were granted. A short VrApi sample reported 72–73 FPS against a 72 Hz target.

The physical gesture test recorded `Left pinch stopped effects` at 17:42:14 and `Right pinch resumed effects` at 17:42:54 local time. Both 1280×960 camera textures became ready again about 70 ms after the resume event. The next 12 sampled VrApi reports showed 72–73 FPS against the 72 Hz target. Image alignment in each eye, comfort, and sustained performance require further observation in the headset.
