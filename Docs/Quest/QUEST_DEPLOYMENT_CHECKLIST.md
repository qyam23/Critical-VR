# Quest Deployment Checklist

## Project

- Open the repository root as a Unity project
- Unity packages resolved without errors
- `CriticalVR > Bootstrap > Run Full Quest Bootstrap` completed
- `CriticalVR > Validation > Run Quest Validation` completed
- `Assets/CriticalVR/Scenes/CriticalVR_QuestBootstrap.unity` exists
- `Assets/CriticalVR/Resources/Materials/CriticalVRPassthroughTrip.mat` exists
- `Assets/CriticalVR/Settings/Rendering/CriticalVR_URP.asset` exists
- `Assets/CriticalVR/Settings/Rendering/CriticalVR_Renderer.asset` exists
- `Docs/Quest/ValidationReport.latest.md` reviewed

## Unity Rendering

- URP pipeline asset assigned in `Graphics` and `Quality`
- `Opaque Texture` enabled
- `Depth Texture` enabled
- `CriticalVRFullscreenRendererFeature` added to the active renderer data
- renderer feature uses `CriticalVRPassthroughTrip.mat`

## Meta Quest

- Meta XR / Oculus packages imported
- `OVRCameraRig` present in the scene
- `CenterEyeAnchor` assigned to `MotionToExperienceManager`
- `OVRManager` enabled
- `OVRPassthroughLayer` present and set to `Underlay`

## Android

- platform switched to `Android`
- scripting backend `IL2CPP`
- target architecture `ARM64`
- graphics APIs: `Vulkan` first, `OpenGLES3` second
- Quest device visible in `adb devices`

## Comfort And Runtime

- startup begins from full black and reveals over `10s`
- no visible hard “effect on” moment
- peripheral response is stronger than central response
- `B` and `Y` trigger reset on device
- reset fades over at least `1.5s`
- distortion remains capped

## Build

- `Build` produces an `.apk`
- `Tools\build_quest.bat` produces an `.apk`
- `Tools\build_and_install_quest.bat` works end to end from PC to headset
- `Build And Run` or `adb install -r` succeeds
- app launches on headset
- first headset test confirms the scene is comfortable at low, medium, and high intensity
