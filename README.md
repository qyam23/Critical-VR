# Critical-VR

Critical-VR is now organized as a Unity-first Meta Quest passthrough project package with a desktop simulator kept only as a support tool.

## Primary Product Path

The primary target is `Meta Quest passthrough VR` with:

- Unity project structure at the repository root
- Quest-oriented runtime motion analysis in `Assets/CriticalVR/Scripts/Runtime`
- URP full-screen effect support in `Assets/CriticalVR/Scripts/Rendering`
- deployment and setup documentation in `Docs/Quest`
- editor bootstrap tooling to create a starting scene and material

Open the repository itself as a Unity project.

## Repository Layout

- `Assets/`
  Unity runtime scripts, rendering scripts, shaders, and generated bootstrap scene/materials
- `Packages/`
  Unity package manifest centered on URP, Input System, XR Management, and OpenXR
- `ProjectSettings/`
  Unity project settings for Quest-oriented Android setup
- `Docs/Quest/`
  Quest deployment guide, shader guide, build/install notes, and readiness checklists
- `Docs/Architecture/`
  repository structure notes, reference-research summary, and capability gap analysis
- `Tools/`
  helper scripts including ADB install and the desktop web simulator
- `Tools/WebSimulator/`
  reference-only React/Vite simulator for motion tuning before headset testing

## Start Here

1. Read [`Docs/Quest/SetupGuide.md`](./Docs/Quest/SetupGuide.md)
2. Run `CriticalVR > Bootstrap > Run Full Quest Bootstrap`
3. Import the Meta XR / Oculus packages in Unity
4. Replace the preview camera setup with `OVRCameraRig`
5. Run `CriticalVR > Validation > Run Quest Validation`
6. Build from Unity or use [`Tools/build_and_install_quest.bat`](./Tools/build_and_install_quest.bat)

## What Is Ready In Repo

- Quest-oriented motion runtime with subtle activation and release timing
- material-driven layer outputs for distortion, patterning, color atmosphere, breathing, overdrive, and intro fade
- a URP full-screen renderer feature script
- a production-targeted effect shader
- a Unity editor bootstrap tool for scene/material/project setup
- a Unity validation tool with report output
- local batch build automation
- local PC-to-Quest build and install automation
- deployment checklists and install helpers

## Fast Commands

- Validate from PC:
  `Tools\validate_quest_project.bat`
- Build development APK:
  `Tools\build_quest.bat`
- Build release APK:
  `Tools\build_quest.bat --release`
- Build and install to Quest:
  `Tools\build_and_install_quest.bat`
- Pull, build, and install to Quest:
  `Tools\build_and_install_quest.bat --pull`

## What Is Still Manual In Unity

- importing Meta XR / Oculus Integration packages
- switching the placeholder preview rig to `OVRCameraRig`
- enabling Passthrough on `OVRManager` and adding `OVRPassthroughLayer`
- replacing the preview CenterEyeAnchor reference with the real Quest eye anchor
- verifying Quest-specific OpenXR / Meta XR plugin toggles in the Editor

## Desktop Simulator

The browser app under [`Tools/WebSimulator`](./Tools/WebSimulator) is a support simulator only. It mirrors the motion logic and transition feel, but it is not the shipping product path.

## Research References

External patterns reviewed and adapted are summarized in [`Docs/Architecture/ReferenceResearch.md`](./Docs/Architecture/ReferenceResearch.md).

## November 2026 preview and readiness patch

See [research, local preview and installation steps](Docs/Quest/ResearchAndReadiness-2026-11-06.md) and [actual verification results](Docs/Quest/Verification-2026-11-06.md). The synthetic preview is available from `CriticalVR > Preview > Create And Play Synthetic Room`. Raw camera effects and headset validation remain pending; the compositor passthrough underlay is not the Unity camera color texture.
