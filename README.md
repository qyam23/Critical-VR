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

1. Open the repository in Unity **6000.3.11f1** and allow package import.
2. Run `CriticalVR > Bootstrap > Run Full Quest Bootstrap` to create the production QuestCamera scene, official Meta rig, underlay, camera bridge and Android manifest automatically.
3. Run `CriticalVR > Validation > Run Quest Validation`.
4. Run `CriticalVR > Build > Build Quest Development APK`.
5. Connect and authorize the headset, install, then grant the application's camera permission.

For a rendered synthetic-room test use `CriticalVR > Preview > Run Automated Room Check`. With the editor closed, `Tools\run_quest_checks.ps1` runs the room check, Quest validation and APK build without bypassing validation.

**Left-hand pinch stops effects. Hold a right-hand pinch for one second to resume.** Enable hand tracking in the headset settings. With controllers, **B/Y stops** and **A/X resumes**. Camera denial, stale frames or lost focus returns to native passthrough.

Read [SetupGuide.md](Docs/Quest/SetupGuide.md) and [CameraIntegration-2026-11-07.md](Docs/Quest/CameraIntegration-2026-11-07.md). The independent per-eye camera overlay uses calibrated reference-distance reprojection; it is experimental and is not depth-correct reconstructed passthrough. Device alignment, near objects, latency and performance need headset verification.

## What Is Ready In Repo

- Quest-oriented motion runtime with subtle activation and release timing
- independent left/right camera bridge with permission handling and fail-closed native passthrough fallback
- material-driven layer outputs for distortion, patterning, color atmosphere, breathing, overdrive, and intro fade
- a URP full-screen renderer feature script
- a production-targeted effect shader
- a Unity editor bootstrap tool for scene/material/project setup
- a Unity validation tool with report output
- local batch build automation
- local PC-to-Quest build and install automation
- `Tools/install_quest_apk.ps1` for installing an existing APK without rebuilding
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

## Device Verification Still Required

- Approve USB debugging and the application's headset-camera permission on the physical device.
- Check each eye, image alignment, near-object parallax, gradual activation and decay, B/Y reset and A/X resume.
- Test camera/focus interruption and sustained device CPU/GPU timing.
- Do not infer headset readiness from desktop tests or an APK build alone.

## Desktop Simulator

The browser app under [`Tools/WebSimulator`](./Tools/WebSimulator) is a support simulator only. It mirrors the motion logic and transition feel, but it is not the shipping product path.

## Research References

External patterns reviewed and adapted are summarized in [`Docs/Architecture/ReferenceResearch.md`](./Docs/Architecture/ReferenceResearch.md).

## November 2026 preview and readiness patch

See [research, local preview and installation steps](Docs/Quest/ResearchAndReadiness-2026-11-06.md) and [actual verification results](Docs/Quest/Verification-2026-11-06.md). The synthetic preview is available from `CriticalVR > Preview > Create And Play Synthetic Room`. Raw camera effects and headset validation remain pending; the compositor passthrough underlay is not the Unity camera color texture.
