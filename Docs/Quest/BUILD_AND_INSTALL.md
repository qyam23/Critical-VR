# Build And Install

## Unity Build Path

1. Open `File > Build Settings`
2. Select `Android`
3. Confirm the current scene list includes:
   `Assets/CriticalVR/Scenes/CriticalVR_QuestBootstrap.unity`
4. Click `Build` or `Build And Run`

Recommended output path:

`Builds/Android/CriticalVR.apk`

## Repeatable Local Automation

Validation only:

```bat
Tools\validate_quest_project.bat
```

The helper fails fast if:

- another Unity editor instance still has the project open
- Android Build Support is not available in the detected Unity editor

Development build:

```bat
Tools\build_quest.bat
```

Release build:

```bat
Tools\build_quest.bat --release
```

Build and install to headset:

```bat
Tools\build_and_install_quest.bat
```

Pull latest, build, and install:

```bat
Tools\build_and_install_quest.bat --pull
```

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

`Tools\build_and_install_quest.bat` goes further:

- optional `git pull --ff-only`
- Unity validation
- Unity batch build
- adb device detection
- adb reinstall
- app launch attempt

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
- `Docs/Quest/ValidationReport.latest.md` does not contain blocking Meta XR findings
