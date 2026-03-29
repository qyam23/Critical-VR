# Build And Install

## Unity Build Path

1. Open `File > Build Settings`
2. Select `Android`
3. Confirm the current scene list includes:
   `Assets/CriticalVR/Scenes/CriticalVR_QuestBootstrap.unity`
4. Click `Build` or `Build And Run`

Recommended output path:

`Builds/Android/CriticalVR.apk`

## ADB Install

Use the included helper:

```bat
Tools\install_apk.bat Builds\Android\CriticalVR.apk
```

What it does:

- checks that the APK exists
- checks that `adb` is available
- waits for a device
- runs `adb install -r`

## Quick Debug Commands

```bat
adb devices
adb logcat -s Unity ActivityManager AndroidRuntime OVRManager OpenXR
adb shell am force-stop com.qyam23.criticalvr
adb shell monkey -p com.qyam23.criticalvr 1
```

## If Build Succeeds But Quest Shows Nothing

Check these first:

- passthrough layer is still `Underlay`
- renderer feature is actually attached to the active URP renderer asset
- effect material is assigned to both the renderer feature and the runtime script
- `CenterEyeAnchor` points to the imported `OVRCameraRig` eye anchor
- Meta XR plugin settings are enabled for Android
