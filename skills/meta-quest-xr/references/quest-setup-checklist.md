# Quest Setup Checklist

## Unity Project

- Use Unity `2022.3+ LTS`
- Use `URP`
- Enable Android build support
- Install Meta XR packages used by the project

## Scene Setup

- Add `OVRCameraRig`
- Use its `CenterEyeAnchor` for head-motion sampling
- Configure Passthrough so the real world is shown as `Underlay`
- Keep the effect driver script on a stable manager object
- Assign the material or volume profile references in the Inspector

## URP

- Confirm the active renderer matches the scene camera
- Enable opaque texture if the shader path expects `_CameraOpaqueTexture`
- Add a renderer feature or composition object if required by the effect approach

## Runtime Safety

- Map `B` / `Y` to instant reset
- Mirror reset with an Editor keyboard binding
- Clamp distortion and chromatic settings
- Keep all fades smooth and slower than shock-inducing transitions
