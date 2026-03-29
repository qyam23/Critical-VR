# Repository Structure

## Production Path

- `Assets/CriticalVR/Scripts/Runtime`
  motion analysis, state machine, safety reset, material output
- `Assets/CriticalVR/Scripts/Rendering`
  URP full-screen renderer feature
- `Assets/CriticalVR/Scripts/Editor`
  project bootstrap and scene/material creation
- `Assets/CriticalVR/Shaders`
  production effect shader
- `Assets/CriticalVR/Scenes`
  bootstrap Quest scene

## Build And Config

- `Packages/manifest.json`
  Unity package dependencies for URP, Input System, XR, and Android logcat
- `ProjectSettings/`
  Unity project settings saved in project form

## Documentation

- `Docs/Quest/`
  setup, shader implementation, build/install, checklist, readiness
- `Docs/Architecture/`
  structure and external reference research

## Support Only

- `Tools/WebSimulator/`
  desktop tuning simulator and browser reference
- `skills/`
  local Codex skill references used during development

The simulator is retained for pre-headset tuning, but it is no longer the primary product path.
